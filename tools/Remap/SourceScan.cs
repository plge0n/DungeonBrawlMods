using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Mono.Cecil;

namespace Remap
{
    internal enum UseKind
    {
        // 代码里的标识符（类型、成员、命名参数）
        Code,
        // 字符串里的名字（Compat 清单、AccessTools.Method(typeof(X), "名字")）
        Literal,
        // 注释、日志文字里的 "类型.成员"
        Text,
    }

    internal sealed class Use
    {
        public string File;
        public TextSpan Span;
        public UseKind Kind;
        public string OldText;
        // 对应的旧版定义；参数用 (方法, 序号) 表示
        public IMemberDefinition Def;
        public int ParameterIndex = -1;
        public int Line;
    }

    /// <summary>
    /// 用 Roslyn 按旧版程序集编译 Mod 源码，找出每一处引用游戏混淆名的地方（标识符、字符串、注释），
    /// 换成新版名字后再按新版程序集编译一遍检查。
    /// </summary>
    internal sealed class SourceScan
    {
        public readonly List<Use> Uses = new List<Use>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> OldErrors = new List<string>();
        public readonly HashSet<IMemberDefinition> Used = new HashSet<IMemberDefinition>();
        // 像混淆名、但找不到所属类型的字符串（反射用的字段名、表格列名等）：改写时只在同名成员真的改了名时提示
        private readonly List<(string Where, string Value, List<IMemberDefinition> Candidates)> orphans = new List<(string, string, List<IMemberDefinition>)>();

        private readonly Side side;
        private readonly List<(string Path, SyntaxTree Tree, Encoding Encoding)> files = new List<(string, SyntaxTree, Encoding)>();
        private readonly CSharpCompilation compilation;
        private readonly CSharpParseOptions parse = new CSharpParseOptions(LanguageVersion.Latest);

        public SourceScan(Side oldSide, IEnumerable<string> sourceDirs, IEnumerable<string> extraRefs)
        {
            side = oldSide;
            foreach (var dir in sourceDirs)
                foreach (var path in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(dir, path);
                    if (rel.StartsWith("obj" + Path.DirectorySeparatorChar) || rel.StartsWith("bin" + Path.DirectorySeparatorChar)) continue;
                    var bytes = File.ReadAllBytes(path);
                    var enc = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? new UTF8Encoding(true) : new UTF8Encoding(false);
                    var text = SourceText.From(bytes, bytes.Length, enc);
                    files.Add((path, CSharpSyntaxTree.ParseText(text, parse, path), enc));
                }
            compilation = Compile(files.Select(f => f.Tree), References(side, extraRefs));
            foreach (var d in compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Take(20))
                OldErrors.Add(d.ToString());
            foreach (var f in files) Scan(f.Path, f.Tree);
            foreach (var u in Uses)
                if (u.Def != null) Used.Add(u.Def);
        }

        public static List<MetadataReference> References(Side s, IEnumerable<string> extraRefs)
        {
            var refs = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asm in s.Assemblies)
            {
                refs.Add(MetadataReference.CreateFromFile(asm.MainModule.FileName));
                seen.Add(Path.GetFileName(asm.MainModule.FileName));
            }
            foreach (var path in extraRefs)
                if (File.Exists(path) && seen.Add(Path.GetFileName(path)))
                    refs.Add(MetadataReference.CreateFromFile(path));
            return refs;
        }

        private static CSharpCompilation Compile(IEnumerable<SyntaxTree> trees, IEnumerable<MetadataReference> refs)
            => CSharpCompilation.Create("DBModKit", trees, refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Disable));

        // ---------------- 扫描 ----------------

        private void Scan(string path, SyntaxTree tree)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var token in root.DescendantTokens())
            {
                if (token.IsKind(SyntaxKind.IdentifierToken)) ScanIdentifier(path, model, token);
                else if (token.IsKind(SyntaxKind.StringLiteralToken)) ScanLiteral(path, model, token);
                else if (token.IsKind(SyntaxKind.InterpolatedStringTextToken)) ScanText(path, token.Span, token.Text);
            }
            foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
                if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.XmlTextLiteralToken))
                    ScanText(path, trivia.Span, trivia.ToString());
            // XML 文档注释里的文字是 token 不是 trivia
            foreach (var t in root.DescendantTokens(descendIntoTrivia: true).Where(t => t.IsKind(SyntaxKind.XmlTextLiteralToken)))
                ScanText(path, t.Span, t.Text);
        }

        private void ScanIdentifier(string path, SemanticModel model, SyntaxToken token)
        {
            var node = token.Parent;
            if (node == null) return;
            ISymbol symbol;
            if (node is IdentifierNameSyntax || node is GenericNameSyntax)
            {
                var info = model.GetSymbolInfo(node);
                symbol = info.Symbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);
                if (symbol == null && info.CandidateSymbols.Length > 1)
                {
                    var defs = info.CandidateSymbols.Select(ToCecil).Where(d => d.Def != null).ToList();
                    if (defs.Count > 0) Warnings.Add($"{Where(path, token.Span, model.SyntaxTree)}：`{token.Text}` 有多个候选，未处理");
                    return;
                }
            }
            else if (node is MethodDeclarationSyntax || node is PropertyDeclarationSyntax || node is EventDeclarationSyntax)
            {
                // 重写游戏的虚方法（class ModPassive : qp { override void gou() }）：声明的名字跟着被重写的方法改
                var declaredSymbol = model.GetDeclaredSymbol(node);
                symbol = declaredSymbol switch
                {
                    IMethodSymbol m => m.OverriddenMethod,
                    IPropertySymbol p => p.OverriddenProperty,
                    IEventSymbol e => e.OverriddenEvent,
                    _ => null,
                };
            }
            else return;
            if (symbol == null) return;
            var (def, param) = ToCecil(symbol);
            if (def == null) return;
            // 只改字面上就是这个名字的地方：var、using 别名等绑定到游戏类型但写的不是它的名字
            string declared = param >= 0 ? ((MethodDefinition)def).Parameters[param].Name : def.Name;
            if (token.ValueText != declared) return;
            Uses.Add(new Use
            {
                File = path, Span = token.Span, Kind = UseKind.Code, OldText = token.Text, Def = def, ParameterIndex = param,
                Line = model.SyntaxTree.GetLineSpan(token.Span).StartLinePosition.Line + 1,
            });
        }

        // Roslyn 符号 → 旧版 Cecil 定义；不是被处理程序集里的符号时返回 null
        private (IMemberDefinition Def, int Param) ToCecil(ISymbol symbol)
        {
            if (symbol is IAliasSymbol alias) symbol = alias.Target;
            if (symbol is IMethodSymbol rm && rm.ReducedFrom != null) symbol = rm.ReducedFrom;
            symbol = symbol.OriginalDefinition;
            if (symbol.ContainingAssembly == null || !side.Processed.Contains(symbol.ContainingAssembly.Name)) return (null, -1);
            switch (symbol)
            {
                case INamedTypeSymbol t:
                    return (FindType(t), -1);
                case IParameterSymbol p when p.ContainingSymbol is IMethodSymbol owner:
                    var m = FindMethod(owner);
                    return m == null ? (null, -1) : (m, p.Ordinal);
                case IFieldSymbol f:
                    return (FindType(f.ContainingType)?.Fields.FirstOrDefault(x => x.Name == f.MetadataName), -1);
                case IPropertySymbol pr:
                    return (FindType(pr.ContainingType)?.Properties.FirstOrDefault(x => x.Name == pr.MetadataName), -1);
                case IEventSymbol e:
                    return (FindType(e.ContainingType)?.Events.FirstOrDefault(x => x.Name == e.MetadataName), -1);
                case IMethodSymbol me:
                    return (FindMethod(me), -1);
            }
            return (null, -1);
        }

        private TypeDefinition FindType(INamedTypeSymbol t)
        {
            t = t.OriginalDefinition;
            string name = t.ContainingType != null
                ? FindType(t.ContainingType)?.FullName + "/" + t.MetadataName
                : (t.ContainingNamespace.IsGlobalNamespace ? "" : t.ContainingNamespace.ToDisplayString() + ".") + t.MetadataName;
            return side.TypesByName.TryGetValue(t.ContainingAssembly.Name + "|" + name, out var def) ? def : null;
        }

        private MethodDefinition FindMethod(IMethodSymbol m)
        {
            var type = FindType(m.ContainingType);
            if (type == null) return null;
            var cands = type.Methods.Where(x => x.Name == m.MetadataName && x.Parameters.Count == m.Parameters.Length
                && x.GenericParameters.Count == m.TypeParameters.Length).ToList();
            if (cands.Count > 1)
                cands = cands.Where(x => x.Parameters.Select(p => SimpleName(p.ParameterType))
                    .SequenceEqual(m.Parameters.Select(p => SimpleName(p.Type)))).ToList();
            if (cands.Count > 1) Warnings.Add($"{type.FullName}::{m.MetadataName} 有多个同签名重载，取第一个");
            return cands.FirstOrDefault();
        }

        private static string SimpleName(TypeReference t)
        {
            if (t is ByReferenceType r) t = r.ElementType;
            if (t is ArrayType a) return SimpleName(a.ElementType) + "[]";
            if (t is GenericInstanceType g) t = g.ElementType;
            return t.Name;
        }

        private static string SimpleName(ITypeSymbol t)
        {
            if (t is IArrayTypeSymbol a) return SimpleName(a.ElementType) + "[]";
            return t.MetadataName;
        }

        // ---------------- 字符串里的名字 ----------------

        private void ScanLiteral(string path, SemanticModel model, SyntaxToken token)
        {
            string value = token.ValueText;
            if (string.IsNullOrEmpty(value) || value.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '`')))
            {
                ScanText(path, token.Span, token.Text);
                return;
            }
            var contentSpan = new TextSpan(token.SpanStart + (token.Text.StartsWith("@") ? 2 : 1), value.Length);
            if (token.Text.Length != value.Length + (token.Text.StartsWith("@") ? 3 : 2)) return; // 带转义的不处理

            // 1) 名字本身是一个被处理的类型
            var types = side.FindTypesBySimpleName(value);
            // 2) 上下文里出现的类型的成员
            var context = ContextTypes(model, token);
            var members = context.SelectMany(t => side.FindMembers(t, value)).Distinct().ToList();

            IMemberDefinition def = null;
            if (members.Count > 0)
            {
                def = members[0];
                if (members.Count > 1)
                {
                    // 重载：只有全部改成同一个新名字时才安全，交给应用阶段判断
                    Uses.AddRange(members.Skip(1).Select(m => new Use
                    {
                        File = path, Span = contentSpan, Kind = UseKind.Literal, OldText = value, Def = m,
                        Line = model.SyntaxTree.GetLineSpan(token.Span).StartLinePosition.Line + 1,
                    }));
                }
            }
            else if (types.Count == 1) def = types[0];
            else if (types.Count > 1)
            {
                Warnings.Add($"{Where(path, token.Span, model.SyntaxTree)}：字符串 \"{value}\" 对应多个类型，未处理");
                return;
            }
            if (def == null)
            {
                if (Names.LooksObfuscated(value) && value.Length >= 2)
                {
                    var cands = side.Types.Where(t => !Names.IsStringHolder(t)).SelectMany(t => side.FindMembers(t, value)).Distinct().ToList();
                    if (cands.Count > 0) orphans.Add((Where(path, token.Span, model.SyntaxTree), value, cands));
                }
                return;
            }
            Uses.Add(new Use
            {
                File = path, Span = contentSpan, Kind = UseKind.Literal, OldText = value, Def = def,
                Line = model.SyntaxTree.GetLineSpan(token.Span).StartLinePosition.Line + 1,
            });
        }

        // 字符串所在的调用 / 字典项里提到的类型：typeof(X)、泛型参数、"X" 形式的类型名、由 TypeByName("X") 得到的变量
        private List<TypeDefinition> ContextTypes(SemanticModel model, SyntaxToken token)
        {
            SyntaxNode scope = null;
            for (var n = token.Parent; n != null; n = n.Parent)
            {
                if (n is AssignmentExpressionSyntax a && a.Left is ImplicitElementAccessSyntax) { scope = n; break; }
                if (n is InvocationExpressionSyntax) { scope = n; break; }
                if (n is StatementSyntax || n is MemberDeclarationSyntax) break;
            }
            var list = new List<TypeDefinition>();
            if (scope == null) return list;
            void AddType(ITypeSymbol t)
            {
                if (t is INamedTypeSymbol nt && ToCecil(nt).Def is TypeDefinition d && !list.Contains(d)) list.Add(d);
            }
            foreach (var node in scope.DescendantNodesAndSelf())
            {
                switch (node)
                {
                    case TypeOfExpressionSyntax tof:
                        AddType(model.GetTypeInfo(tof.Type).Type);
                        break;
                    case GenericNameSyntax g:
                        foreach (var arg in g.TypeArgumentList.Arguments) AddType(model.GetTypeInfo(arg).Type);
                        break;
                    case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                        foreach (var t in side.FindTypesBySimpleName(lit.Token.ValueText))
                            if (!list.Contains(t)) list.Add(t);
                        break;
                    case IdentifierNameSyntax id:
                        // var ez = AccessTools.TypeByName("ez") 之类的局部变量
                        if (model.GetSymbolInfo(id).Symbol is ILocalSymbol local)
                            foreach (var r in local.DeclaringSyntaxReferences)
                                if (r.GetSyntax() is VariableDeclaratorSyntax v && v.Initializer?.Value is InvocationExpressionSyntax inv
                                    && inv.ArgumentList.Arguments.Count == 1 && inv.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax l)
                                    foreach (var t in side.FindTypesBySimpleName(l.Token.ValueText))
                                        if (!list.Contains(t)) list.Add(t);
                        break;
                }
            }
            return list;
        }

        // ---------------- 注释和文字里的 "类型.成员" ----------------

        private static readonly Regex Dotted = new Regex(@"(?<![\w.])([A-Za-z_]\w*)\.([A-Za-z_]\w*)(?![\w])");

        private void ScanText(string path, TextSpan span, string text)
        {
            foreach (Match m in Dotted.Matches(text))
            {
                var types = side.FindTypesBySimpleName(m.Groups[1].Value);
                if (types.Count != 1) continue;
                var type = types[0];
                if (!Names.LooksObfuscated(type.Name) && !Names.LooksObfuscated(m.Groups[2].Value)) continue;
                var members = side.FindMembers(type, m.Groups[2].Value).ToList();
                if (members.Count == 0) continue;
                var g1 = m.Groups[1];
                if (Names.LooksObfuscated(type.Name))
                    Uses.Add(new Use { File = path, Span = new TextSpan(span.Start + g1.Index, g1.Length), Kind = UseKind.Text, OldText = g1.Value, Def = type });
                var g2 = m.Groups[2];
                foreach (var member in members)
                    Uses.Add(new Use { File = path, Span = new TextSpan(span.Start + g2.Index, g2.Length), Kind = UseKind.Text, OldText = g2.Value, Def = member });
            }
        }

        private static string Where(string path, TextSpan span, SyntaxTree tree)
            => Path.GetFileName(path) + ":" + (tree.GetLineSpan(span).StartLinePosition.Line + 1);

        // ---------------- 改写 ----------------

        public sealed class Result
        {
            public readonly Dictionary<string, string> NewTexts = new Dictionary<string, string>();
            public readonly List<string> Unresolved = new List<string>();
            public readonly List<string> NewErrors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public int Changed;
        }

        /// <summary>
        /// 按对应表计算新源码。代码里的标识符缺对应时保留原样（编译会报错，提示人工处理）；
        /// 文字里的同一处有多个候选（重载）时，只有全部改成同一个名字才改。
        /// </summary>
        public Result Rewrite(Func<IMemberDefinition, int, string> newName, Side newSide, IEnumerable<string> extraRefs)
        {
            var result = new Result();
            foreach (var (where, value, cands) in orphans)
            {
                var renamed = cands.Where(c => { var nn = newName(c, -1); return nn != null && nn != value; }).ToList();
                if (renamed.Count > 0)
                    result.Warnings.Add($"{where}：字符串 \"{value}\" 没有改（找不到所属类型），但新版里 {string.Join("、", renamed.Take(3).Select(c => c.DeclaringType?.Name + "." + c.Name))} 改了名，确认它是不是游戏成员名");
            }
            var trees = new List<SyntaxTree>();
            foreach (var (path, tree, enc) in files)
            {
                var changes = new List<TextChange>();
                foreach (var group in Uses.Where(u => u.File == path).GroupBy(u => u.Span))
                {
                    var uses = group.ToList();
                    var names = uses.Select(u => newName(u.Def, u.ParameterIndex)).Distinct().ToList();
                    var first = uses[0];
                    if (names.Count != 1 || names[0] == null)
                    {
                        if (first.Kind != UseKind.Text)
                            result.Unresolved.Add($"{Path.GetFileName(path)}:{first.Line} `{first.OldText}`（{Describe(first)}）"
                                                  + (names.Count > 1 ? "：重载对应到不同的名字 " + string.Join("/", names) : "：没有对应"));
                        continue;
                    }
                    string name = names[0];
                    // 新名字恰好是 C# 关键字时要加 @
                    if (first.Kind == UseKind.Code && SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None) name = "@" + name;
                    if (name != first.OldText) changes.Add(new TextChange(group.Key, name));
                }
                var text = tree.GetText();
                var updated = text.WithChanges(changes);
                result.Changed += changes.Count;
                if (changes.Count > 0) result.NewTexts[path] = updated.ToString();
                trees.Add(CSharpSyntaxTree.ParseText(updated, parse, path));
            }
            var check = Compile(trees, References(newSide, extraRefs));
            foreach (var d in check.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Take(50))
                result.NewErrors.Add(d.ToString());
            return result;
        }

        public void Write(Result result)
        {
            foreach (var (path, _, enc) in files)
                if (result.NewTexts.TryGetValue(path, out var text))
                    File.WriteAllText(path, text, enc);
        }

        public static string Describe(Use u)
            => u.Def is TypeDefinition t ? "类型 " + t.FullName
                : u.ParameterIndex >= 0 ? $"{u.Def.DeclaringType.Name}.{u.Def.Name} 的参数 {u.ParameterIndex}"
                : u.Def.DeclaringType.Name + "." + u.Def.Name;
    }
}
