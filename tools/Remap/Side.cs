using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Remap
{
    internal static class Names
    {
        // GUPS 的短名（bbtv、kqi）和长哈希名（k896ff…）。hp、name、icon 这类真名也符合，所以只当作"可能被改名"，
        // 同名不能直接认定是同一个成员，要靠特征确认
        private static readonly Regex Short = new Regex("^[a-z]{1,4}$");
        private static readonly Regex Hash = new Regex("^k[0-9a-f]{32}$");

        public static bool LooksObfuscated(string name)
            => Short.IsMatch(name) || Hash.IsMatch(name) || name.StartsWith("<");

        public static bool IsStringHolder(TypeDefinition t)
        {
            for (var d = t; d != null; d = d.DeclaringType)
                if (d.Namespace.StartsWith("<PrivateImplementationDetails>")) return true;
            return false;
        }
    }

    /// <summary>
    /// 一个版本的游戏程序集（旧版或新版）。只处理会被混淆的程序集，其余（Unity、mscorlib）当作外部引用，按名字比较。
    /// 加载时还原加密字符串、建立"谁引用了谁"的索引，供匹配打分用。
    /// </summary>
    internal sealed class Side
    {
        public readonly string Label;
        public readonly string Dir;
        public readonly HashSet<string> Processed;
        public readonly List<AssemblyDefinition> Assemblies = new List<AssemblyDefinition>();
        public readonly List<TypeDefinition> Types = new List<TypeDefinition>();
        public readonly Dictionary<string, TypeDefinition> TypesByName = new Dictionary<string, TypeDefinition>();
        public readonly Dictionary<MethodDefinition, MethodInfo> Methods = new Dictionary<MethodDefinition, MethodInfo>();
        // 方法/字段 → 引用它的方法
        public readonly Dictionary<IMemberDefinition, List<MethodDefinition>> Users = new Dictionary<IMemberDefinition, List<MethodDefinition>>();
        public int DecryptedStrings;

        private readonly DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
        private readonly Dictionary<MemberReference, IMemberDefinition> resolved = new Dictionary<MemberReference, IMemberDefinition>(ReferenceEqualityComparer.Instance);

        public sealed class MethodInfo
        {
            public readonly List<string> Strings = new List<string>();
            public readonly List<string> Constants = new List<string>();
            public readonly Dictionary<string, int> Opcodes = new Dictionary<string, int>();
            // 引用的被处理程序集里的成员/类型（定义）
            public readonly List<IMemberDefinition> Refs = new List<IMemberDefinition>();
            // 引用的外部成员/类型（按全名）
            public readonly List<string> External = new List<string>();
            // 按指令顺序的引用序列：IMemberDefinition（被处理程序集）或 string（外部成员全名），用于对齐已对应的方法
            public readonly List<object> Sequence = new List<object>();
        }

        public Side(string label, string dir, IEnumerable<string> fallbackDirs, IEnumerable<string> assemblies)
        {
            Label = label;
            Dir = Path.GetFullPath(dir);
            resolver.AddSearchDirectory(Dir);
            foreach (var d in fallbackDirs)
                if (Directory.Exists(d)) resolver.AddSearchDirectory(d);

            Processed = new HashSet<string>();
            foreach (var name in assemblies)
            {
                string path = Path.Combine(Dir, name + ".dll");
                if (!File.Exists(path)) continue;
                // 通过 resolver 读，跨程序集的引用（Assembly-CSharp → Mirror）才会解析到同一份定义。
                // 只有不带 ReaderParameters 的重载会进缓存，带参数的每次都重新加载一份
                var asm = resolver.Resolve(AssemblyNameReference.Parse(name));
                if (!string.Equals(Path.GetFullPath(asm.MainModule.FileName), path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"{name} 解析到了 {asm.MainModule.FileName}，应为 {path}");
                Assemblies.Add(asm);
                Processed.Add(name);
            }
            if (Assemblies.Count == 0) throw new InvalidOperationException($"{Dir} 里没有要处理的程序集");

            foreach (var asm in Assemblies)
                foreach (var t in asm.MainModule.GetTypes())
                {
                    Types.Add(t);
                    TypesByName[Key(t)] = t;
                }
            foreach (var asm in Assemblies) Index(asm.MainModule);
        }

        public static string AssemblyOf(TypeDefinition t) => t.Module.Assembly.Name.Name;

        // 程序集名 + Cecil 全名（嵌套类型用 /）
        public static string Key(TypeDefinition t) => AssemblyOf(t) + "|" + t.FullName;

        public static string Key(IMemberDefinition m)
            => m is TypeDefinition t ? Key(t) : AssemblyOf(m.DeclaringType) + "|" + m.FullName;

        public bool IsProcessed(IMetadataScope scope)
        {
            switch (scope)
            {
                case ModuleDefinition md: return Processed.Contains(md.Assembly.Name.Name);
                case AssemblyNameReference anr: return Processed.Contains(anr.Name);
                case ModuleReference _: return false;
                default: return false;
            }
        }

        public TypeDefinition Resolve(TypeReference t)
        {
            while (t is TypeSpecification spec) t = spec.ElementType;
            if (t is GenericParameter || !IsProcessed(t.Scope)) return null;
            return (TypeDefinition)Resolve((MemberReference)t);
        }

        // 被处理程序集里的成员定义；外部成员返回 null
        public IMemberDefinition Resolve(MemberReference r)
        {
            if (resolved.TryGetValue(r, out var d)) return d;
            d = null;
            try
            {
                switch (r)
                {
                    case TypeReference t:
                        var scope = t;
                        while (scope is TypeSpecification spec) scope = spec.ElementType;
                        if (!(scope is GenericParameter) && IsProcessed(scope.Scope)) d = scope.Resolve();
                        break;
                    case MethodReference m:
                        if (IsProcessed(Root(m.DeclaringType).Scope)) d = m.Resolve();
                        break;
                    case FieldReference f:
                        if (IsProcessed(Root(f.DeclaringType).Scope)) d = f.Resolve();
                        break;
                }
            }
            catch (AssemblyResolutionException)
            {
            }
            resolved[r] = d;
            return d;
        }

        private static TypeReference Root(TypeReference t)
        {
            while (t is TypeSpecification spec) t = spec.ElementType;
            return t;
        }

        private void Index(ModuleDefinition module)
        {
            var accessors = DecryptStrings(module);
            foreach (var type in module.GetTypes())
            {
                if (Names.IsStringHolder(type)) continue;
                foreach (var method in type.Methods)
                {
                    var info = new MethodInfo();
                    Methods[method] = info;
                    if (!method.HasBody) continue;
                    foreach (var ins in method.Body.Instructions)
                    {
                        string op = ins.OpCode.Name;
                        info.Opcodes[op] = info.Opcodes.TryGetValue(op, out int c) ? c + 1 : 1;
                        switch (ins.Operand)
                        {
                            case string s:
                                info.Strings.Add(s);
                                break;
                            case int _:
                            case long _:
                            case float _:
                            case double _:
                            case sbyte _:
                                info.Constants.Add(Convert.ToString(ins.Operand, System.Globalization.CultureInfo.InvariantCulture));
                                break;
                            case MemberReference mr:
                                if (mr is MethodReference call && accessors.TryGetValue(Resolve(call) as MethodDefinition ?? NoMethod, out var text))
                                {
                                    info.Strings.Add(text);
                                    break;
                                }
                                var def = Resolve(mr);
                                if (def == null)
                                {
                                    string ext = mr is TypeReference ? mr.FullName : mr.DeclaringType?.FullName + "::" + mr.Name;
                                    info.External.Add(ext);
                                    info.Sequence.Add(ext);
                                    break;
                                }
                                info.Refs.Add(def);
                                info.Sequence.Add(def);
                                if (!(def is TypeDefinition))
                                {
                                    if (!Users.TryGetValue(def, out var list)) Users[def] = list = new List<MethodDefinition>();
                                    list.Add(method);
                                }
                                break;
                        }
                    }
                }
            }
        }

        private static readonly MethodDefinition NoMethod = new MethodDefinition("<none>", MethodAttributes.Static, new TypeReference("System", "Void", null, null));

        // GUPS 字符串加密：<PrivateImplementationDetails>{GUID}.a 的静态构造里把字节数组按 data[i] ^ i ^ K 还原，
        // 每个访问器是 a.xxx()，最后两个整数常量是偏移和长度。K 从静态构造里的 "ldc.i4 K; xor" 读，读不到用 0xAA
        private Dictionary<MethodDefinition, string> DecryptStrings(ModuleDefinition module)
        {
            var table = new Dictionary<MethodDefinition, string>();
            foreach (var holder in module.Types.Where(t => t.Namespace.StartsWith("<PrivateImplementationDetails>")))
            {
                var cctor = holder.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic && m.HasBody);
                if (cctor == null) continue;
                var ins = cctor.Body.Instructions;
                var rva = ins.Where(i => i.OpCode == OpCodes.Ldtoken && i.Operand is FieldReference)
                    .Select(i => ((FieldReference)i.Operand).Resolve())
                    .FirstOrDefault(f => f?.InitialValue != null && f.InitialValue.Length > 0);
                if (rva == null) continue;
                int key = 0xAA;
                for (int i = 1; i < ins.Count; i++)
                    if (ins[i].OpCode == OpCodes.Xor && TryInt(ins[i - 1], out int k) && k > 1 && k < 256)
                        key = k;
                var data = (byte[])rva.InitialValue.Clone();
                for (int i = 0; i < data.Length; i++) data[i] = (byte)(data[i] ^ i ^ key);

                foreach (var m in holder.Methods.Where(m => m.IsStatic && m.HasBody && m.Parameters.Count == 0 && m.ReturnType.FullName == "System.String"))
                {
                    var ints = new List<int>();
                    foreach (var i in m.Body.Instructions)
                        if (TryInt(i, out int v)) ints.Add(v);
                    if (ints.Count < 3) continue;
                    int off = ints[^2], len = ints[^1];
                    if (off < 0 || len < 0 || off + len > data.Length) continue;
                    table[m] = Encoding.UTF8.GetString(data, off, len);
                }
            }
            DecryptedStrings += table.Count;
            return table;
        }

        public static bool TryInt(Instruction i, out int v)
        {
            switch (i.OpCode.Code)
            {
                case Code.Ldc_I4_M1: v = -1; return true;
                case Code.Ldc_I4_0: v = 0; return true;
                case Code.Ldc_I4_1: v = 1; return true;
                case Code.Ldc_I4_2: v = 2; return true;
                case Code.Ldc_I4_3: v = 3; return true;
                case Code.Ldc_I4_4: v = 4; return true;
                case Code.Ldc_I4_5: v = 5; return true;
                case Code.Ldc_I4_6: v = 6; return true;
                case Code.Ldc_I4_7: v = 7; return true;
                case Code.Ldc_I4_8: v = 8; return true;
                case Code.Ldc_I4_S: v = (sbyte)i.Operand; return true;
                case Code.Ldc_I4: v = (int)i.Operand; return true;
                default: v = 0; return false;
            }
        }

        // 成员所在类型及其基类（被处理程序集内）里找名字
        public IEnumerable<IMemberDefinition> FindMembers(TypeDefinition type, string name)
        {
            for (var t = type; t != null; t = t.BaseType == null ? null : Resolve(t.BaseType))
            {
                foreach (var f in t.Fields) if (f.Name == name) yield return f;
                foreach (var m in t.Methods) if (m.Name == name) yield return m;
                foreach (var p in t.Properties) if (p.Name == name) yield return p;
                foreach (var e in t.Events) if (e.Name == name) yield return e;
                foreach (var n in t.NestedTypes) if (n.Name == name) yield return n;
            }
        }

        // 按简单名找类型（Compat 清单、AccessTools.TypeByName 用的都是简单名；嵌套类型不算）
        public List<TypeDefinition> FindTypesBySimpleName(string name)
            => Types.Where(t => t.DeclaringType == null && (t.Name == name || t.FullName == name)).ToList();
    }
}
