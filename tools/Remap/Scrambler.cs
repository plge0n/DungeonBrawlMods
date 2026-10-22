using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Remap
{
    /// <summary>
    /// 自测用：模拟一次"游戏更新后重新混淆"。把看起来是混淆名的类型、成员、参数全部换成新的随机名
    /// （跨程序集的引用、虚方法重写链、属性访问器一起改），打乱类型和成员的顺序，并随机改动一小部分方法里的常量。
    /// 记下 旧名 → 新名 作为标准答案。
    /// </summary>
    internal sealed class Scrambler
    {
        private readonly Random rng;
        private readonly double mutate;
        public readonly Dictionary<string, string> Truth = new Dictionary<string, string>();
        public int Renamed, Mutated;

        public Scrambler(int seed, double mutate)
        {
            rng = new Random(seed);
            this.mutate = mutate;
        }

        public void Run(string oldDir, IEnumerable<string> fallbackDirs, IEnumerable<string> names, string outDir)
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(oldDir);
            foreach (var d in fallbackDirs) resolver.AddSearchDirectory(d);
            var asms = new List<AssemblyDefinition>();
            foreach (var name in names)
                if (File.Exists(Path.Combine(oldDir, name + ".dll")))
                    asms.Add(resolver.Resolve(AssemblyNameReference.Parse(name))); // 不带参数的重载才进缓存，见 Side
            var processed = new HashSet<string>(asms.Select(a => a.Name.Name));
            bool IsProcessed(TypeReference t)
            {
                while (t is TypeSpecification s) t = s.ElementType;
                if (t is GenericParameter) return false;
                return t.Scope switch
                {
                    ModuleDefinition md => processed.Contains(md.Assembly.Name.Name),
                    AssemblyNameReference anr => processed.Contains(anr.Name),
                    _ => false,
                };
            }

            var types = asms.SelectMany(a => a.MainModule.GetTypes()).ToList();
            var members = new List<IMemberDefinition>();
            foreach (var t in types)
            {
                members.Add(t);
                members.AddRange(t.Fields);
                members.AddRange(t.Methods);
                members.AddRange(t.Properties);
                members.AddRange(t.Events);
            }
            var before = members.ToDictionary(m => m, Side.Key);

            // 1. 改名前先解析所有引用
            var refs = new List<(MemberReference Ref, IMemberDefinition Def)>();
            foreach (var a in asms)
            {
                foreach (var tr in a.MainModule.GetTypeReferences())
                    if (IsProcessed(tr))
                    {
                        var d = tr.Resolve();
                        if (d != null) refs.Add((tr, d));
                    }
                foreach (var mr in a.MainModule.GetMemberReferences())
                    if (IsProcessed(mr.DeclaringType))
                    {
                        IMemberDefinition d = mr switch
                        {
                            MethodReference m => m.Resolve(),
                            FieldReference f => f.Resolve(),
                            _ => null,
                        };
                        if (d != null) refs.Add((mr, d));
                    }
            }

            // 2. 虚方法重写链、接口实现必须同名：并查集分组，组里有外部方法或真名就整组不改
            var parent = new Dictionary<MethodDefinition, MethodDefinition>();
            MethodDefinition Find(MethodDefinition m)
            {
                while (parent.TryGetValue(m, out var p) && p != m) m = p;
                return m;
            }
            void Union(MethodDefinition a, MethodDefinition b)
            {
                parent.TryAdd(a, a);
                parent.TryAdd(b, b);
                var ra = Find(a);
                var rb = Find(b);
                if (ra != rb) parent[ra] = rb;
            }
            var fixedRoots = new HashSet<MethodDefinition>();
            var fixedMethods = new HashSet<MethodDefinition>();
            foreach (var t in types)
                foreach (var m in t.Methods.Where(m => m.IsVirtual))
                {
                    // 重写基类方法
                    if (!m.IsNewSlot)
                    {
                        bool found = false;
                        for (var bt = t.BaseType; bt != null && !found;)
                        {
                            TypeDefinition bd;
                            try { bd = bt.Resolve(); } catch (AssemblyResolutionException) { bd = null; }
                            if (bd == null) break;
                            var bm = bd.Methods.FirstOrDefault(x => x.IsVirtual && x.Name == m.Name && SameParams(x, m));
                            if (bm != null)
                            {
                                found = true;
                                if (IsProcessed(bt)) Union(m, bm);
                                else fixedMethods.Add(m);
                            }
                            bt = bd.BaseType;
                        }
                    }
                    // 实现接口（隐式按名字，显式见 Overrides）
                    foreach (var o in m.Overrides)
                    {
                        var od = o.Resolve();
                        if (od == null || !IsProcessed(o.DeclaringType)) fixedMethods.Add(m);
                        else Union(m, od);
                    }
                    foreach (var it in AllInterfaces(t))
                    {
                        TypeDefinition id;
                        try { id = it.Resolve(); } catch (AssemblyResolutionException) { id = null; }
                        if (id == null) continue;
                        var im = id.Methods.FirstOrDefault(x => x.Name == m.Name && SameParams(x, m));
                        if (im == null) continue;
                        if (IsProcessed(it)) Union(m, im);
                        else fixedMethods.Add(m);
                    }
                }
            foreach (var m in fixedMethods) fixedRoots.Add(Find(m));
            foreach (var m in parent.Keys)
                if (!Names.LooksObfuscated(m.Name)) fixedRoots.Add(Find(m));

            // 3. 改名。同一继承族（经基类相连的类型）共用一个已用名字集合：
            //    否则派生类的方法可能和基类属性同名，C# 按名字查找时先找到派生类的，源码就编译不过（真实混淆器不会这样）
            var family = new Dictionary<TypeDefinition, TypeDefinition>();
            TypeDefinition Root(TypeDefinition t)
            {
                while (family.TryGetValue(t, out var p) && p != t) t = p;
                return t;
            }
            foreach (var t in types)
            {
                family.TryAdd(t, t);
                TypeDefinition bd = null;
                try { if (t.BaseType != null && IsProcessed(t.BaseType)) bd = t.BaseType.Resolve(); } catch (AssemblyResolutionException) { }
                if (bd == null) continue;
                family.TryAdd(bd, bd);
                var ra = Root(t);
                var rb = Root(bd);
                if (ra != rb) family[ra] = rb;
            }
            var familyNames = new Dictionary<TypeDefinition, HashSet<string>>();
            foreach (var t in types)
            {
                var r = Root(t);
                if (!familyNames.TryGetValue(r, out var set)) familyNames[r] = set = new HashSet<string>();
                set.UnionWith(t.Fields.Select(f => f.Name).Concat(t.Methods.Select(m => m.Name))
                    .Concat(t.Properties.Select(p => p.Name)).Concat(t.Events.Select(e => e.Name)));
            }
            var groupName = new Dictionary<MethodDefinition, string>();
            foreach (var t in types)
            {
                if (t.Name != "<Module>" && Names.LooksObfuscated(t.Name) && !t.Name.StartsWith("<"))
                {
                    var siblings = new HashSet<string>(t.DeclaringType != null ? t.DeclaringType.NestedTypes.Select(x => x.Name) : t.Module.Types.Select(x => x.Name));
                    t.Name = Fresh(t.Name, siblings);
                    Renamed++;
                }
                var used = familyNames[Root(t)];
                // 事件的后备字段和事件同名（C# 编译器生成），改名时保持一致，在事件那里一起改
                var eventNames = new HashSet<string>(t.Events.Select(e => e.Name));
                foreach (var f in t.Fields)
                    if (Names.LooksObfuscated(f.Name) && !f.Name.StartsWith("<") && !eventNames.Contains(f.Name))
                    {
                        f.Name = Fresh(f.Name, used);
                        Renamed++;
                    }
                foreach (var p in t.Properties)
                    if (Names.LooksObfuscated(p.Name) && !p.Name.StartsWith("<"))
                    {
                        string old = p.Name;
                        p.Name = Fresh(old, used);
                        // 访问器 get_旧名 也要跟着改，否则等于把旧名泄露给匹配器
                        foreach (var acc in new[] { p.GetMethod, p.SetMethod })
                            if (acc != null && (acc.Name == "get_" + old || acc.Name == "set_" + old))
                                acc.Name = acc.Name.Substring(0, 4) + p.Name;
                        Renamed++;
                    }
                foreach (var e in t.Events)
                    if (Names.LooksObfuscated(e.Name) && !e.Name.StartsWith("<"))
                    {
                        string old = e.Name;
                        e.Name = Fresh(old, used);
                        var backing = t.Fields.FirstOrDefault(f => f.Name == old);
                        if (backing != null) backing.Name = e.Name;
                        foreach (var acc in new[] { e.AddMethod, e.RemoveMethod })
                            if (acc != null && (acc.Name == "add_" + old || acc.Name == "remove_" + old))
                                acc.Name = acc.Name.Substring(0, acc.Name.IndexOf('_') + 1) + e.Name;
                        Renamed++;
                    }
                foreach (var m in t.Methods)
                {
                    foreach (var p in m.Parameters)
                        if (Names.LooksObfuscated(p.Name)) p.Name = Fresh(p.Name, new HashSet<string>(m.Parameters.Select(x => x.Name)));
                    if (!Names.LooksObfuscated(m.Name) || m.Name.StartsWith("<")) continue;
                    if (parent.ContainsKey(m))
                    {
                        var root = Find(m);
                        if (fixedRoots.Contains(root)) continue;
                        if (!groupName.TryGetValue(root, out var gn)) groupName[root] = gn = Fresh(m.Name, used);
                        m.Name = gn;
                    }
                    else if (fixedMethods.Contains(m)) continue;
                    else m.Name = Fresh(m.Name, used);
                    Renamed++;
                }
            }

            // 4. 引用跟着改
            foreach (var (r, d) in refs)
            {
                if (r is TypeReference tr && d is TypeDefinition td)
                {
                    tr.Name = td.Name;
                    tr.Namespace = td.Namespace;
                }
                else r.Name = d.Name;
            }

            // 5. 打乱顺序、改常量
            foreach (var a in asms)
            {
                var top = a.MainModule.Types.Where(t => t.Name != "<Module>").ToList();
                var module = a.MainModule.Types.FirstOrDefault(t => t.Name == "<Module>");
                a.MainModule.Types.Clear();
                if (module != null) a.MainModule.Types.Add(module);
                foreach (var t in Shuffle(top)) a.MainModule.Types.Add(t);
            }
            foreach (var t in types)
            {
                if (Names.IsStringHolder(t) || t.IsExplicitLayout || t.IsSequentialLayout && t.IsValueType) continue;
                var ms = Shuffle(t.Methods.ToList());
                t.Methods.Clear();
                foreach (var m in ms) t.Methods.Add(m);
                if (!t.IsValueType && !t.IsEnum)
                {
                    var fs = Shuffle(t.Fields.ToList());
                    t.Fields.Clear();
                    foreach (var f in fs) t.Fields.Add(f);
                }
                if (mutate <= 0) continue;
                foreach (var m in t.Methods.Where(m => m.HasBody))
                {
                    if (rng.NextDouble() >= mutate) continue;
                    var ldc = m.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldc_I4).ToList();
                    if (ldc.Count > 0)
                    {
                        var i = ldc[rng.Next(ldc.Count)];
                        i.Operand = unchecked((int)i.Operand + 1);
                    }
                    m.Body.GetILProcessor().InsertBefore(m.Body.Instructions[0], Instruction.Create(OpCodes.Nop));
                    Mutated++;
                }
            }

            foreach (var kv in before) Truth[kv.Value] = Side.Key(kv.Key);

            Directory.CreateDirectory(outDir);
            foreach (var a in asms) a.Write(Path.Combine(outDir, a.Name.Name + ".dll"));
        }

        private static IEnumerable<TypeReference> AllInterfaces(TypeDefinition t)
        {
            for (var x = t; x != null;)
            {
                foreach (var i in x.Interfaces) yield return i.InterfaceType;
                try { x = x.BaseType?.Resolve(); } catch (AssemblyResolutionException) { x = null; }
            }
        }

        private static bool SameParams(MethodDefinition a, MethodDefinition b)
            => a.Parameters.Count == b.Parameters.Count
               && a.Parameters.Select(p => p.ParameterType.Name).SequenceEqual(b.Parameters.Select(p => p.ParameterType.Name));

        private List<T> Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        // 和原来同风格的新名字：短名 → 2~4 个小写字母，哈希名 → k + 32 位十六进制
        private string Fresh(string old, HashSet<string> taken)
        {
            while (true)
            {
                string name;
                if (old.Length == 33 && old[0] == 'k')
                    name = "k" + string.Concat(Enumerable.Range(0, 32).Select(_ => "0123456789abcdef"[rng.Next(16)]));
                else
                    name = string.Concat(Enumerable.Range(0, 2 + rng.Next(3)).Select(_ => (char)('a' + rng.Next(26))));
                if (taken.Add(name)) return name;
            }
        }
    }
}
