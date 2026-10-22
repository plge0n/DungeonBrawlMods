using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Remap
{
    internal sealed class Pair
    {
        public IMemberDefinition Old, New;
        // 1 = 同名锚定或由属性/访问器推出；否则是打分结果
        public double Score;
        // 次优候选的分数（越接近 Score 越可疑）
        public double Runner;
        public string How;
        public int Round;
    }

    /// <summary>
    /// 把旧版程序集里的类型、方法、字段、属性、事件对应到新版。
    /// 1. 锚点：不像混淆名的名字（类名、序列化字段、Unity 消息方法）同名即对应。
    /// 2. 迭代打分：特征包括字符串常量、外部调用、数字常量、指令分布，以及"引用/被引用的已对应成员"。
    ///    每轮对应上的成员又成为下一轮的特征，逐步扩散。只接受双方互为最优、且领先次优足够多的配对。
    /// </summary>
    internal sealed class Matcher
    {
        private readonly Side o, n;
        public readonly Dictionary<IMemberDefinition, Pair> ByOld = new Dictionary<IMemberDefinition, Pair>();
        public readonly Dictionary<IMemberDefinition, Pair> ByNew = new Dictionary<IMemberDefinition, Pair>();
        public Action<string> Log = _ => { };

        // (阈值, 领先次优的最小差距)：先严后宽，一旦有新配对就回到最严
        private static readonly (double Min, double Margin)[] Levels =
        {
            (0.80, 0.20), (0.65, 0.15), (0.50, 0.12), (0.38, 0.10), (0.25, 0.10),
        };

        private int round;

        public Matcher(Side oldSide, Side newSide)
        {
            o = oldSide;
            n = newSide;
        }

        public void Run()
        {
            AnchorTypes();
            int level = 0;
            while (true)
            {
                round++;
                int before = ByOld.Count;
                MatchTypes(Levels[level]);
                foreach (var p in ByOld.Values.Where(p => p.Old is TypeDefinition).ToList())
                    MatchMembers((TypeDefinition)p.Old, (TypeDefinition)p.New, Levels[level]);
                Propagate(level);
                int added = ByOld.Count - before;
                Log($"  第 {round} 轮（级别 {level}）：+{added}，共 {ByOld.Count}");
                if (added > 0) level = 0;
                else if (level < Levels.Length - 1) level++;
                else if (MatchClones() > 0) level = 0;
                else break;
            }
        }

        public IMemberDefinition NewOf(IMemberDefinition old) => ByOld.TryGetValue(old, out var p) ? p.New : null;

        /// <summary>排查用：列出某个旧类型在新版里的候选和分数。</summary>
        public void Explain(string typeName, Action<string> log)
        {
            var t = o.Types.FirstOrDefault(x => x.FullName == typeName);
            if (t == null)
            {
                log($"旧版没有类型 {typeName}");
                return;
            }
            log($"{typeName}：{(NewOf(t) is TypeDefinition nt ? "已对应 " + nt.FullName : "未对应")}，基类 {t.BaseType?.FullName} → {(t.BaseType == null ? "" : Canon(t.BaseType, o) ?? "未对应")}");
            var feat = TypeFeatures(t, o);
            var rows = n.Types.Where(x => SameKind(t, x))
                .Select(x => (x, ok: TypeHardOk(t, x), sc: Bag.Dice(feat, TypeFeatures(x, n)), taken: ByNew.TryGetValue(x, out var p) ? p.Old.FullName : null))
                .OrderByDescending(r => r.sc).Take(8);
            foreach (var r in rows)
                log($"  {r.x.FullName,-30} 分数 {r.sc:F3} 硬条件 {(r.ok ? "过" : "不过")} {(r.taken != null ? "已被 " + r.taken + " 占用" : "")}，基类 {r.x.BaseType?.FullName}");
        }

        private bool Add(IMemberDefinition a, IMemberDefinition b, double score, double runner, string how)
        {
            if (ByOld.ContainsKey(a) || ByNew.ContainsKey(b)) return false;
            var p = new Pair { Old = a, New = b, Score = score, Runner = runner, How = how, Round = round };
            ByOld[a] = p;
            ByNew[b] = p;
            switch (a)
            {
                case TypeDefinition ta:
                    AnchorMembers(ta, (TypeDefinition)b);
                    break;
                case PropertyDefinition pa:
                    var pb = (PropertyDefinition)b;
                    string fromProp = how == "等价副本" ? how : "访问器";
                    if (pa.GetMethod != null && pb.GetMethod != null) Add(pa.GetMethod, pb.GetMethod, 1, 0, fromProp);
                    if (pa.SetMethod != null && pb.SetMethod != null) Add(pa.SetMethod, pb.SetMethod, 1, 0, fromProp);
                    break;
                case EventDefinition ea:
                    var eb = (EventDefinition)b;
                    // 后备字段和事件同名
                    var fa = ea.DeclaringType.Fields.FirstOrDefault(x => x.Name == ea.Name);
                    var fb = eb.DeclaringType.Fields.FirstOrDefault(x => x.Name == eb.Name);
                    if (fa != null && fb != null) Add(fa, fb, 1, 0, "事件字段");
                    if (ea.AddMethod != null && eb.AddMethod != null) Add(ea.AddMethod, eb.AddMethod, 1, 0, "访问器");
                    if (ea.RemoveMethod != null && eb.RemoveMethod != null) Add(ea.RemoveMethod, eb.RemoveMethod, 1, 0, "访问器");
                    break;
                case MethodDefinition ma:
                    // 访问器对上了，所属属性/事件也对上
                    var mb = (MethodDefinition)b;
                    var propA = ma.DeclaringType.Properties.FirstOrDefault(x => x.GetMethod == ma || x.SetMethod == ma);
                    var propB = mb.DeclaringType.Properties.FirstOrDefault(x => x.GetMethod == mb || x.SetMethod == mb);
                    // 由等价副本推出的仍标为等价副本（例如迭代器里两个返回同一字段的 Current）
                    string derived = how == "等价副本" ? how : "访问器";
                    if (propA != null && propB != null) Add(propA, propB, 1, 0, derived);
                    var evA = ma.DeclaringType.Events.FirstOrDefault(x => x.AddMethod == ma || x.RemoveMethod == ma);
                    var evB = mb.DeclaringType.Events.FirstOrDefault(x => x.AddMethod == mb || x.RemoveMethod == mb);
                    if (evA != null && evB != null) Add(evA, evB, 1, 0, derived);
                    break;
            }
            return true;
        }

        // ---------------- 锚点 ----------------

        private void AnchorTypes()
        {
            foreach (var t in o.Types)
            {
                if (t.DeclaringType != null || Names.IsStringHolder(t) || Names.LooksObfuscated(t.Name)) continue;
                if (n.TypesByName.TryGetValue(Side.Key(t), out var nt) && SameKind(t, nt)) Add(t, nt, 1, 0, "同名");
            }
        }

        private void AnchorMembers(TypeDefinition a, TypeDefinition b)
        {
            foreach (var nt in a.NestedTypes)
            {
                if (Names.LooksObfuscated(nt.Name)) continue;
                var cand = b.NestedTypes.FirstOrDefault(x => x.Name == nt.Name);
                if (cand != null && SameKind(nt, cand)) Add(nt, cand, 1, 0, "同名");
            }
            foreach (var f in a.Fields)
            {
                if (Names.LooksObfuscated(f.Name)) continue;
                var cand = b.Fields.FirstOrDefault(x => x.Name == f.Name);
                if (cand != null && cand.IsStatic == f.IsStatic) Add(f, cand, 1, 0, "同名");
            }
            foreach (var pr in a.Properties)
            {
                if (Names.LooksObfuscated(pr.Name)) continue;
                var cand = b.Properties.Where(x => x.Name == pr.Name && x.Parameters.Count == pr.Parameters.Count).ToList();
                if (cand.Count == 1) Add(pr, cand[0], 1, 0, "同名");
            }
            foreach (var ev in a.Events)
            {
                if (Names.LooksObfuscated(ev.Name)) continue;
                var cand = b.Events.FirstOrDefault(x => x.Name == ev.Name);
                if (cand != null) Add(ev, cand, 1, 0, "同名");
            }
            // P/Invoke 的入口名是给原生库用的，混淆器不能改
            foreach (var m in a.Methods.Where(m => m.HasPInvokeInfo && !ByOld.ContainsKey(m)))
            {
                var cand = b.Methods.Where(x => x.HasPInvokeInfo && x.PInvokeInfo.EntryPoint == m.PInvokeInfo.EntryPoint
                    && x.PInvokeInfo.Module.Name == m.PInvokeInfo.Module.Name && !ByNew.ContainsKey(x)).ToList();
                if (cand.Count == 1) Add(m, cand[0], 1, 0, "入口名");
            }
            foreach (var m in a.Methods)
            {
                if (Names.LooksObfuscated(m.Name) || ByOld.ContainsKey(m)) continue;
                var cand = b.Methods.Where(x => x.Name == m.Name && !ByNew.ContainsKey(x) && MethodShapeOk(m, x)).ToList();
                // 重载：签名能确定的先配
                if (cand.Count > 1) cand = cand.Where(x => SignatureEqual(m, x)).ToList();
                if (cand.Count == 1) Add(m, cand[0], 1, 0, "同名");
            }
        }

        // ---------------- 类型 ----------------

        private static string KindOf(TypeDefinition t)
        {
            if (t.IsEnum) return "enum";
            if (t.IsInterface) return "interface";
            if (t.IsValueType) return "struct";
            if (t.BaseType?.FullName == "System.MulticastDelegate") return "delegate";
            return t.IsAbstract && t.IsSealed ? "static" : "class";
        }

        private static bool SameKind(TypeDefinition a, TypeDefinition b)
            => KindOf(a) == KindOf(b) && a.GenericParameters.Count == b.GenericParameters.Count && Side.AssemblyOf(a) == Side.AssemblyOf(b);

        private bool TypeHardOk(TypeDefinition a, TypeDefinition b)
        {
            if (!SameKind(a, b) || (a.DeclaringType == null) != (b.DeclaringType == null)) return false;
            if (a.DeclaringType != null)
            {
                var d = NewOf(a.DeclaringType);
                if (d != null ? d != b.DeclaringType : ByNew.ContainsKey(b.DeclaringType)) return false;
            }
            if ((a.BaseType == null) != (b.BaseType == null)) return false;
            return a.BaseType == null || Compatible(a.BaseType, b.BaseType);
        }

        private void MatchTypes((double Min, double Margin) level)
        {
            var olds = o.Types.Where(t => !ByOld.ContainsKey(t) && !Names.IsStringHolder(t)).ToList();
            var news = n.Types.Where(t => !ByNew.ContainsKey(t) && !Names.IsStringHolder(t)).ToList();
            if (olds.Count == 0 || news.Count == 0) return;
            // 按硬条件分桶，减少两两比较
            string Bucket(TypeDefinition t) => KindOf(t) + "|" + t.GenericParameters.Count + "|" + Side.AssemblyOf(t) + "|" + (t.DeclaringType != null);
            var newBuckets = news.GroupBy(Bucket).ToDictionary(g => g.Key, g => g.ToList());
            var oldFeat = olds.ToDictionary(t => t, t => TypeFeatures(t, o));
            var newFeat = news.ToDictionary(t => t, t => TypeFeatures(t, n));
            var scores = new List<(IMemberDefinition, IMemberDefinition, double)>();
            foreach (var t in olds)
            {
                if (!newBuckets.TryGetValue(Bucket(t), out var cands)) continue;
                foreach (var c in cands)
                    if (TypeHardOk(t, c)) scores.Add((t, c, Bag.Dice(oldFeat[t], newFeat[c])));
            }
            Accept(scores, level, "打分");
        }

        private Bag TypeFeatures(TypeDefinition t, Side s)
        {
            var bag = new Bag();
            foreach (var f in t.Fields)
            {
                if (!Names.LooksObfuscated(f.Name)) bag.Add("mn:" + f.Name, 3);
                bag.Add("ft:" + (Canon(f.FieldType, s) ?? "?"), Canon(f.FieldType, s) == null ? 0.2 : 1);
                if (t.IsEnum && f.HasConstant) bag.Add("e:" + f.Constant, 2);
            }
            foreach (var m in t.Methods)
            {
                if (!Names.LooksObfuscated(m.Name)) bag.Add("mn:" + m.Name, 3);
                bag.Add("ms:" + (SigCanon(m, s) ?? m.Parameters.Count + "?"), 0.5);
                if (s.Methods.TryGetValue(m, out var info))
                {
                    foreach (var str in info.Strings) bag.Add("s:" + str, 1);
                    foreach (var x in info.External) bag.Add("x:" + x, 0.2);
                }
            }
            foreach (var p in t.Properties)
                if (!Names.LooksObfuscated(p.Name)) bag.Add("mn:" + p.Name, 3);
            foreach (var e in t.Events)
                if (!Names.LooksObfuscated(e.Name)) bag.Add("mn:" + e.Name, 3);
            foreach (var nt in t.NestedTypes)
                if (!Names.LooksObfuscated(nt.Name)) bag.Add("mn:" + nt.Name, 3);
            foreach (var a in t.CustomAttributes) bag.Add("a:" + a.AttributeType.FullName, 1);
            foreach (var i in t.Interfaces)
            {
                var c = Canon(i.InterfaceType, s);
                if (c != null) bag.Add("i:" + c, 2);
            }
            if (t.BaseType != null)
            {
                var c = Canon(t.BaseType, s);
                if (c != null) bag.Add("b:" + c, 3);
            }
            if (!Names.LooksObfuscated(t.Name)) bag.Add("tn:" + t.Name, 5);
            bag.Add("nf:" + t.Fields.Count, 1);
            bag.Add("nm:" + t.Methods.Count, 1);
            // 被哪些已对应的方法引用（只算一部分，避免大类型的特征爆炸）
            return bag;
        }

        // ---------------- 成员 ----------------

        private void MatchMembers(TypeDefinition a, TypeDefinition b, (double Min, double Margin) level)
        {
            // 字段
            var fo = a.Fields.Where(f => !ByOld.ContainsKey(f)).ToList();
            var fn = b.Fields.Where(f => !ByNew.ContainsKey(f)).ToList();
            if (fo.Count > 0 && fn.Count > 0)
            {
                var featO = fo.ToDictionary(f => f, f => FieldFeatures(f, o));
                var featN = fn.ToDictionary(f => f, f => FieldFeatures(f, n));
                var scores = new List<(IMemberDefinition, IMemberDefinition, double)>();
                foreach (var f in fo)
                    foreach (var g in fn)
                        if (f.IsStatic == g.IsStatic && f.HasConstant == g.HasConstant && f.IsLiteral == g.IsLiteral && Compatible(f.FieldType, g.FieldType))
                            scores.Add((f, g, Bag.Dice(featO[f], featN[g])));
                Accept(scores, level, "打分");
            }

            // 方法：按形状分桶
            var mo = a.Methods.Where(m => !ByOld.ContainsKey(m)).ToList();
            var mn = b.Methods.Where(m => !ByNew.ContainsKey(m)).ToList();
            if (mo.Count > 0 && mn.Count > 0)
            {
                string Shape(MethodDefinition m) => (m.IsStatic ? "s" : "i") + m.Parameters.Count + "|" + m.GenericParameters.Count + "|" + m.HasBody + "|" + m.IsConstructor;
                var buckets = mn.GroupBy(Shape).ToDictionary(g => g.Key, g => g.ToList());
                var featO = new Dictionary<MethodDefinition, Bag>();
                var featN = new Dictionary<MethodDefinition, Bag>();
                var scores = new List<(IMemberDefinition, IMemberDefinition, double)>();
                foreach (var m in mo)
                {
                    if (!buckets.TryGetValue(Shape(m), out var cands)) continue;
                    foreach (var c in cands)
                    {
                        if (!MethodShapeOk(m, c) || !SignatureCompatible(m, c)) continue;
                        if (!featO.TryGetValue(m, out var fa)) featO[m] = fa = MethodFeatures(m, o);
                        if (!featN.TryGetValue(c, out var fb)) featN[c] = fb = MethodFeatures(c, n);
                        scores.Add((m, c, Bag.Dice(fa, fb)));
                    }
                }
                Accept(scores, level, "打分");
            }

            // 属性、事件：访问器还没对上时（比如没有方法体的抽象属性）按类型和名字形状配
            var po = a.Properties.Where(p => !ByOld.ContainsKey(p)).ToList();
            var pn = b.Properties.Where(p => !ByNew.ContainsKey(p)).ToList();
            if (po.Count > 0 && pn.Count > 0)
            {
                var scores = new List<(IMemberDefinition, IMemberDefinition, double)>();
                foreach (var p in po)
                    foreach (var q in pn)
                        if (p.Parameters.Count == q.Parameters.Count && Compatible(p.PropertyType, q.PropertyType)
                            && (p.GetMethod == null) == (q.GetMethod == null) && (p.SetMethod == null) == (q.SetMethod == null))
                            scores.Add((p, q, AccessorScore(p.GetMethod, q.GetMethod) * 0.5 + AccessorScore(p.SetMethod, q.SetMethod) * 0.5));
                Accept(scores, level, "打分");
            }
            var eo = a.Events.Where(e => !ByOld.ContainsKey(e)).ToList();
            var en = b.Events.Where(e => !ByNew.ContainsKey(e)).ToList();
            if (eo.Count > 0 && en.Count > 0)
            {
                var scores = new List<(IMemberDefinition, IMemberDefinition, double)>();
                foreach (var e in eo)
                    foreach (var f in en)
                        if (Compatible(e.EventType, f.EventType))
                            scores.Add((e, f, AccessorScore(e.AddMethod, f.AddMethod)));
                Accept(scores, level, "打分");
            }
        }

        private double AccessorScore(MethodDefinition a, MethodDefinition b)
        {
            if (a == null || b == null) return a == b ? 1 : 0;
            var p = NewOf(a);
            return p == b ? 1 : p != null || ByNew.ContainsKey(b) ? 0 : 0.5;
        }

        private static bool MethodShapeOk(MethodDefinition a, MethodDefinition b)
            => a.IsStatic == b.IsStatic && a.Parameters.Count == b.Parameters.Count && a.GenericParameters.Count == b.GenericParameters.Count
               && a.IsConstructor == b.IsConstructor && a.HasBody == b.HasBody && a.IsVirtual == b.IsVirtual;

        private bool SignatureCompatible(MethodDefinition a, MethodDefinition b)
        {
            if (!Compatible(a.ReturnType, b.ReturnType)) return false;
            for (int i = 0; i < a.Parameters.Count; i++)
                if (!Compatible(a.Parameters[i].ParameterType, b.Parameters[i].ParameterType)) return false;
            return true;
        }

        private bool SignatureEqual(MethodDefinition a, MethodDefinition b)
        {
            if (Canon(a.ReturnType, o) == null || Canon(a.ReturnType, o) != Canon(b.ReturnType, n)) return false;
            for (int i = 0; i < a.Parameters.Count; i++)
            {
                var c = Canon(a.Parameters[i].ParameterType, o);
                if (c == null || c != Canon(b.Parameters[i].ParameterType, n)) return false;
            }
            return true;
        }

        private Bag MethodFeatures(MethodDefinition m, Side s)
        {
            var bag = new Bag();
            var sig = SigCanon(m, s);
            if (sig != null) bag.Add("sig:" + sig, 2);
            if (m.HasPInvokeInfo) bag.Add("pi:" + m.PInvokeInfo.EntryPoint, 10);
            var ov = BaseMethod(m, s);
            if (ov != null)
            {
                var c = MemberCanon(ov, s);
                if (c != null) bag.Add("ov:" + c, 8);
            }
            if (s.Methods.TryGetValue(m, out var info))
            {
                foreach (var str in info.Strings) bag.Add("s:" + str, 3);
                foreach (var c in info.Constants) bag.Add("c:" + c, 0.5);
                foreach (var x in info.External) bag.Add("x:" + x, 1.5);
                foreach (var kv in info.Opcodes) bag.Add("o:" + kv.Key, 0.05 * kv.Value);
                foreach (var r in info.Refs)
                {
                    var c = MemberCanon(r, s);
                    if (c != null) bag.Add("r:" + c, 2);
                    else if (!(r is TypeDefinition))
                    {
                        // 成员没对上，但它所在的类型对上了
                        var dt = MemberCanon(r.DeclaringType, s);
                        if (dt != null) bag.Add("rt:" + dt, 0.5);
                    }
                }
            }
            if (s.Users.TryGetValue(m, out var users))
                foreach (var u in users)
                {
                    var c = MemberCanon(u, s);
                    if (c != null) bag.Add("u:" + c, 2);
                }
            if (bag.Total == 0) bag.Add("empty", 0.1);
            return bag;
        }

        private Bag FieldFeatures(FieldDefinition f, Side s)
        {
            var bag = new Bag();
            var t = Canon(f.FieldType, s);
            if (t != null) bag.Add("ft:" + t, 1);
            if (f.HasConstant) bag.Add("c:" + f.Constant, 4);
            if (f.InitialValue != null && f.InitialValue.Length > 0) bag.Add("iv:" + Convert.ToBase64String(f.InitialValue), 4);
            foreach (var a in f.CustomAttributes) bag.Add("a:" + a.AttributeType.FullName, 1);
            if (s.Users.TryGetValue(f, out var users))
                foreach (var u in users)
                {
                    var c = MemberCanon(u, s);
                    if (c != null) bag.Add("u:" + c, 2);
                    else
                    {
                        // 使用者还没对上：用它的字符串作弱特征（同一个方法里读写的字段）
                        if (s.Methods.TryGetValue(u, out var info))
                            foreach (var str in info.Strings.Take(3)) bag.Add("us:" + str, 0.5);
                    }
                }
            if (bag.Total == 0) bag.Add("empty", 0.1);
            return bag;
        }

        // 重写的基类虚方法（被处理程序集内）
        private static MethodDefinition BaseMethod(MethodDefinition m, Side s)
        {
            if (!m.IsVirtual || m.IsNewSlot) return null;
            for (var t = m.DeclaringType.BaseType == null ? null : s.Resolve(m.DeclaringType.BaseType); t != null;
                 t = t.BaseType == null ? null : s.Resolve(t.BaseType))
            {
                var b = t.Methods.FirstOrDefault(x => x.Name == m.Name && x.IsVirtual && x.Parameters.Count == m.Parameters.Count);
                if (b != null) return b;
            }
            return null;
        }

        // ---------------- 传播：对齐已对应方法的引用序列 ----------------

        // 已对应的方法 A ↔ A'，按指令顺序比对它们引用的成员：同一位置上两边都还没对应的成员很可能是一对。
        // 对齐用加权最长公共子序列（外部引用、已对应的成员必须一致；未对应的只要种类和形状相同）。
        // 每个旧成员收集所有"投票"，只有始终指向同一个新成员（反过来也一样）时才接受
        private readonly HashSet<Pair> propagated = new HashSet<Pair>();

        private void Propagate(int level)
        {
            var votes = new Dictionary<IMemberDefinition, Dictionary<IMemberDefinition, int>>();
            var back = new Dictionary<IMemberDefinition, HashSet<IMemberDefinition>>();
            foreach (var p in ByOld.Values.ToList())
            {
                if (!(p.Old is MethodDefinition ma) || !ma.HasBody || propagated.Contains(p)) continue;
                if (!o.Methods.TryGetValue(ma, out var ia) || !n.Methods.TryGetValue((MethodDefinition)p.New, out var ib)) continue;
                var sa = ia.Sequence;
                var sb = ib.Sequence;
                // 没有未对应的引用就不用再算
                if (!sa.Any(x => x is IMemberDefinition d && !ByOld.ContainsKey(d)))
                {
                    propagated.Add(p);
                    continue;
                }
                if (sb.Count == 0 || (long)sa.Count * sb.Count > 4_000_000) continue;
                foreach (var (a, b) in Align(sa, sb))
                {
                    if (!votes.TryGetValue(a, out var vs)) votes[a] = vs = new Dictionary<IMemberDefinition, int>();
                    vs[b] = vs.TryGetValue(b, out int c) ? c + 1 : 1;
                    if (!back.TryGetValue(b, out var bs)) back[b] = bs = new HashSet<IMemberDefinition>();
                    bs.Add(a);
                }
            }
            int need = level == 0 ? 2 : 1;
            foreach (var kv in votes)
            {
                var a = kv.Key;
                if (kv.Value.Count != 1 || ByOld.ContainsKey(a)) continue;
                var (b, count) = kv.Value.First();
                if (count < need || back[b].Count != 1 || ByNew.ContainsKey(b)) continue;
                if (PairOk(a, b)) Add(a, b, 1, 0, "传播");
            }
        }

        // 种类、形状一致，所属类型能对上（都没对应时一起对应）
        private bool PairOk(IMemberDefinition a, IMemberDefinition b)
        {
            if (a.GetType() != b.GetType()) return false;
            if (a is TypeDefinition ta) return TypeHardOk(ta, (TypeDefinition)b);
            var da = a.DeclaringType;
            var db = b.DeclaringType;
            var mapped = NewOf(da);
            if (mapped != null && mapped != db) return false;
            if (mapped == null && (ByNew.ContainsKey(db) || !TypeHardOk(da, db))) return false;
            bool ok = a switch
            {
                MethodDefinition m => MethodShapeOk(m, (MethodDefinition)b) && SignatureCompatible(m, (MethodDefinition)b),
                FieldDefinition f => f.IsStatic == ((FieldDefinition)b).IsStatic && Compatible(f.FieldType, ((FieldDefinition)b).FieldType),
                _ => false,
            };
            if (ok && mapped == null) Add(da, db, 1, 0, "传播");
            return ok && NewOf(da) == db;
        }

        private List<(IMemberDefinition, IMemberDefinition)> Align(List<object> a, List<object> b)
        {
            int n1 = a.Count, n2 = b.Count;
            var dp = new int[n1 + 1, n2 + 1];
            for (int i = n1 - 1; i >= 0; i--)
                for (int j = n2 - 1; j >= 0; j--)
                {
                    int best = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                    int w = Weight(a[i], b[j]);
                    if (w > 0) best = Math.Max(best, dp[i + 1, j + 1] + w);
                    dp[i, j] = best;
                }
            var result = new List<(IMemberDefinition, IMemberDefinition)>();
            for (int i = 0, j = 0; i < n1 && j < n2;)
            {
                int w = Weight(a[i], b[j]);
                if (w > 0 && dp[i, j] == dp[i + 1, j + 1] + w)
                {
                    if (w == 1) result.Add(((IMemberDefinition)a[i], (IMemberDefinition)b[j]));
                    i++;
                    j++;
                }
                else if (dp[i, j] == dp[i + 1, j]) i++;
                else j++;
            }
            return result;
        }

        // 3 = 外部引用相同或已对应的成员一致；1 = 两边都未对应且种类形状相同；0 = 不能对齐
        private int Weight(object x, object y)
        {
            if (x is string sx) return y is string sy && sx == sy ? 3 : 0;
            if (!(y is IMemberDefinition dy)) return 0;
            var dx = (IMemberDefinition)x;
            var mapped = NewOf(dx);
            if (mapped != null) return mapped == dy ? 3 : 0;
            if (ByNew.ContainsKey(dy) || dx.GetType() != dy.GetType()) return 0;
            return dx switch
            {
                MethodDefinition m => MethodShapeOk(m, (MethodDefinition)dy) ? 1 : 0,
                FieldDefinition f => f.IsStatic == ((FieldDefinition)dy).IsStatic ? 1 : 0,
                TypeDefinition t => SameKind(t, (TypeDefinition)dy) ? 1 : 0,
                _ => 0,
            };
        }

        // ---------------- 等价副本 ----------------

        // 混淆器生成的诱饵副本里有逐字节相同的方法（比如 cft.tqi 和 cft.kcv），又都没人调用，无法区分。
        // 实在分不出来时，特征完全相同、两边数量相等的一组按顺序配对：代码一样，用哪一个效果都相同
        private int MatchClones()
        {
            int added = 0;
            foreach (var p in ByOld.Values.Where(p => p.Old is TypeDefinition).ToList())
            {
                var a = (TypeDefinition)p.Old;
                var b = (TypeDefinition)p.New;
                // 引用的成员必须全部已对应：否则两个读不同字段的 getter 特征也一样，会被误当成副本
                var olds = a.Methods.Where(m => !ByOld.ContainsKey(m) && Resolved(m, o, ByOld)).GroupBy(m => MethodFeatures(m, o).Signature()).ToList();
                if (olds.Count == 0) continue;
                var news = b.Methods.Where(m => !ByNew.ContainsKey(m) && Resolved(m, n, ByNew)).GroupBy(m => MethodFeatures(m, n).Signature()).ToDictionary(g => g.Key, g => g.ToList());
                foreach (var g in olds)
                {
                    var list = g.ToList();
                    if (list.Count < 2 || !news.TryGetValue(g.Key, out var cands) || cands.Count != list.Count) continue;
                    for (int i = 0; i < list.Count; i++)
                        if (MethodShapeOk(list[i], cands[i]) && SignatureCompatible(list[i], cands[i]) && Add(list[i], cands[i], 1, 1, "等价副本"))
                            added++;
                }
            }
            Log($"  等价副本：+{added}");
            return added;
        }

        // 只有"没人调用、不参与重写、引用的成员都已对应"的方法才可能是诱饵副本：
        // 有调用者或是虚方法时，代码相同也不等价（比如基类里两个空的虚方法，被不同的地方调用）
        private static bool Resolved(MethodDefinition m, Side s, Dictionary<IMemberDefinition, Pair> map)
            => !m.IsVirtual && !s.Users.ContainsKey(m)
               && (!s.Methods.TryGetValue(m, out var info) || info.Sequence.All(x => !(x is IMemberDefinition d) || map.ContainsKey(d)));

        // ---------------- 接受配对 ----------------

        // 双方互为最优、分数达到阈值、且领先各自的次优足够多
        private void Accept(List<(IMemberDefinition Old, IMemberDefinition New, double Score)> scores, (double Min, double Margin) level, string how)
        {
            if (scores.Count == 0) return;
            var bestO = new Dictionary<IMemberDefinition, (IMemberDefinition, double, double)>();
            var bestN = new Dictionary<IMemberDefinition, (IMemberDefinition, double, double)>();
            void Update(Dictionary<IMemberDefinition, (IMemberDefinition Who, double Best, double Second)> d, IMemberDefinition k, IMemberDefinition v, double sc)
            {
                if (!d.TryGetValue(k, out var cur)) d[k] = (v, sc, 0);
                else if (sc > cur.Best) d[k] = (v, sc, cur.Best);
                else if (sc > cur.Second) d[k] = (cur.Who, cur.Best, sc);
            }
            foreach (var (a, b, sc) in scores)
            {
                Update(bestO, a, b, sc);
                Update(bestN, b, a, sc);
            }
            foreach (var kv in bestO.OrderByDescending(kv => kv.Value.Item2))
            {
                var a = kv.Key;
                var (b, sc, second) = kv.Value;
                if (sc < level.Min || !bestN.TryGetValue(b, out var back) || back.Item1 != a) continue;
                double runner = Math.Max(second, back.Item3);
                if (sc - runner < level.Margin) continue;
                Add(a, b, sc, runner, how);
            }
        }

        // ---------------- 规范名：旧版名字作为两边共同的"身份" ----------------

        public string MemberCanon(IMemberDefinition d, Side s)
        {
            if (d == null) return null;
            if (s == o) return ByOld.ContainsKey(d) ? Side.Key(d) : null;
            return ByNew.TryGetValue(d, out var p) ? Side.Key(p.Old) : null;
        }

        // 类型的规范名；含有尚未对应的被处理类型时返回 null
        public string Canon(TypeReference t, Side s)
        {
            switch (t)
            {
                case GenericInstanceType g:
                {
                    var e = Canon(g.ElementType, s);
                    if (e == null) return null;
                    var args = new List<string>();
                    foreach (var a in g.GenericArguments)
                    {
                        var c = Canon(a, s);
                        if (c == null) return null;
                        args.Add(c);
                    }
                    return e + "<" + string.Join(",", args) + ">";
                }
                case ArrayType a:
                {
                    var e = Canon(a.ElementType, s);
                    return e == null ? null : e + "[" + a.Rank + "]";
                }
                case ByReferenceType r:
                {
                    var e = Canon(r.ElementType, s);
                    return e == null ? null : e + "&";
                }
                case PointerType p:
                {
                    var e = Canon(p.ElementType, s);
                    return e == null ? null : e + "*";
                }
                case GenericParameter gp:
                    return (gp.Type == GenericParameterType.Method ? "!!" : "!") + gp.Position;
                case IModifierType m:
                    return Canon(m.ElementType, s);
                default:
                    var def = s.Resolve(t);
                    if (def == null) return s.IsProcessed(t.Scope) ? null : t.FullName;
                    return MemberCanon(def, s);
            }
        }

        private string SigCanon(MethodDefinition m, Side s)
        {
            var parts = new List<string> { Canon(m.ReturnType, s) };
            foreach (var p in m.Parameters) parts.Add(Canon(p.ParameterType, s));
            return parts.Any(x => x == null) ? null : string.Join(",", parts);
        }

        // 结构兼容：已对应的必须一致，未对应的必须也未对应且来自同一程序集，外部类型按全名
        private bool Compatible(TypeReference a, TypeReference b)
        {
            if (a.GetType() != b.GetType()) return false;
            switch (a)
            {
                case GenericInstanceType ga:
                {
                    var gb = (GenericInstanceType)b;
                    if (ga.GenericArguments.Count != gb.GenericArguments.Count || !Compatible(ga.ElementType, gb.ElementType)) return false;
                    for (int i = 0; i < ga.GenericArguments.Count; i++)
                        if (!Compatible(ga.GenericArguments[i], gb.GenericArguments[i])) return false;
                    return true;
                }
                case ArrayType aa:
                    return aa.Rank == ((ArrayType)b).Rank && Compatible(aa.ElementType, ((ArrayType)b).ElementType);
                case TypeSpecification sa:
                    return Compatible(sa.ElementType, ((TypeSpecification)b).ElementType);
                case GenericParameter pa:
                    return pa.Position == ((GenericParameter)b).Position && pa.Type == ((GenericParameter)b).Type;
            }
            var da = o.Resolve(a);
            var db = n.Resolve(b);
            if ((da == null) != (db == null)) return false;
            if (da == null) return o.IsProcessed(a.Scope) == n.IsProcessed(b.Scope) && (o.IsProcessed(a.Scope) || a.FullName == b.FullName);
            var mapped = NewOf(da);
            if (mapped != null) return mapped == db;
            return !ByNew.ContainsKey(db) && SameKind(da, db);
        }
    }

    /// <summary>带权重的特征多重集，用加权 Dice 系数比较。</summary>
    internal sealed class Bag
    {
        private readonly Dictionary<string, double> items = new Dictionary<string, double>();
        public double Total { get; private set; }

        public void Add(string key, double weight)
        {
            if (weight <= 0) return;
            items[key] = items.TryGetValue(key, out var w) ? w + weight : weight;
            Total += weight;
        }

        public string Signature() => string.Join("\n", items.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value.ToString("R")));

        public static double Dice(Bag a, Bag b)
        {
            if (a.Total + b.Total <= 0) return 0;
            var (small, large) = a.items.Count <= b.items.Count ? (a, b) : (b, a);
            double common = 0;
            foreach (var kv in small.items)
                if (large.items.TryGetValue(kv.Key, out var w)) common += Math.Min(kv.Value, w);
            return 2 * common / (a.Total + b.Total);
        }
    }
}
