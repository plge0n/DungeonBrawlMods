using System;
using System.Collections.Generic;
using System.Linq;

namespace DBModKit
{
    /// <summary>
    /// 自定义装备特效的定义，用 <see cref="ModContext.RegisterEquipEffect"/> 注册。
    /// 可以挂到游戏已有的装备上（<see cref="AttachTo"/>），也可以给自定义装备用（<see cref="EquipmentDef.Effects"/>）。
    /// </summary>
    public sealed class EquipEffectDef
    {
        /// <summary>在这个 Mod 里唯一的键（只用字母、数字、下划线），游戏里的编号由它算出来，见 <see cref="CustomEquipEffectInfo.Id"/>。</summary>
        public string Key = "";
        /// <summary>名字（中文原文；翻译键 equipEffect.&lt;Key&gt;.name）。</summary>
        public string Name = "";
        /// <summary>说明，显示在装备说明里；{0} {1}… 由游戏换成 <see cref="Values"/>（加上等级成长）。翻译键 equipEffect.&lt;Key&gt;.desc。</summary>
        public string Description = "";
        /// <summary>数值，行为里用 <see cref="EquipEffectBehaviour.Value"/> 读。</summary>
        public float[] Values = new float[0];
        /// <summary>装备每强化一级各数值增加多少（可以比 Values 短）。</summary>
        public float[] LevelUp = new float[0];
        /// <summary>挂到哪些游戏装备上（装备表的编号，例如 2 = 狂战斧）。空表示只给自定义装备用。</summary>
        public int[] AttachTo = new int[0];
        /// <summary>每件装备穿上时创建一个新的行为对象。</summary>
        public Func<EquipEffectBehaviour> Create;
    }

    /// <summary>已注册的自定义装备特效。</summary>
    public sealed class CustomEquipEffectInfo
    {
        /// <summary>游戏里的特效编号（EquipSkillType 的值；由 Mod 的 GUID 和 Key 算出，4000000 起，不和游戏的冲突）。</summary>
        public int Id { get; }
        public string Key { get; }
        /// <summary>注册它的 Mod 的 GUID。</summary>
        public string Owner { get; }
        public EquipEffectDef Def { get; }
        internal ModText Text { get; }

        internal CustomEquipEffectInfo(int id, string owner, EquipEffectDef def, ModText text)
        {
            Id = id;
            Owner = owner;
            Key = def.Key;
            Def = def;
            Text = text;
        }

        public string Name => Text != null ? Text[$"equipEffect.{Key}.name", Def.Name] : Def.Name;
        /// <summary>当前语言的说明（还没填数值）。</summary>
        public string Description => Text != null ? Text[$"equipEffect.{Key}.desc", Def.Description] : Def.Description;
    }

    /// <summary>
    /// 自定义装备特效的行为。继承它，按需重写 On… 方法。每件装备穿上时对应一个实例，只在本地玩家身上运行。
    /// </summary>
    /// <remarks>
    /// 加属性用 <see cref="Player.AddStats"/> 等游戏自己的方法，在 <see cref="OnRemoved"/> 里撤销；强化时在 <see cref="OnLevelChanged"/> 里补差值。
    /// "重试波次"会把玩家属性和装备还原到波次开始时，但不会还原这个对象里的字段。
    /// </remarks>
    public abstract class EquipEffectBehaviour
    {
        internal object Native;
        internal bool Active;

        public CustomEquipEffectInfo Info { get; internal set; }

        /// <summary>拥有者（本地玩家）。</summary>
        public Player Owner => Native == null ? null : Player.Wrap(GameRaw.EquipEffectOwner(Native));

        /// <summary>挂在哪件装备上（装备表的编号；自定义装备是 <see cref="CustomEquipmentInfo.Id"/>）。</summary>
        public int Equipment => Native == null ? 0 : GameRaw.EquipEffectEquipment(Native);

        /// <summary>装备的强化等级（从 0 开始）。</summary>
        public int Level => Native == null ? 0 : GameRaw.EquipEffectLevel(Native);

        /// <summary>第 i 个数值：Values[i] + LevelUp[i] × 等级；越界时为 0。</summary>
        public float Value(int i) => Native == null ? 0f : GameRaw.EquipEffectValue(Native, i, Level);

        /// <summary>某个等级时第 i 个数值（强化时算差值用）。</summary>
        public float ValueAt(int i, int level) => Native == null ? 0f : GameRaw.EquipEffectValue(Native, i, level);

        /// <summary>穿上装备时。</summary>
        protected internal virtual void OnAdded() { }

        /// <summary>卸下装备时（卖掉、替换、一局结束）。</summary>
        protected internal virtual void OnRemoved() { }

        /// <summary>装备等级变了（强化或降级）。</summary>
        protected internal virtual void OnLevelChanged(int oldLevel, int newLevel) { }

        /// <summary>每帧。</summary>
        protected internal virtual void OnUpdate() { }

        /// <summary>战斗事件（见 <see cref="GameEvents"/>）。</summary>
        protected internal virtual void OnGameEvent(GameEvent e) { }

        /// <summary>本机造成的命中（见 <see cref="Combat.Hit"/>）。</summary>
        protected internal virtual void OnHit(HitInfo hit) { }
    }

    /// <summary>
    /// 自定义装备特效的注册表。游戏穿装备时按特效编号创建特效对象（bbm.kpw），ModKit 在这里接上 Mod 的行为。
    /// </summary>
    /// <remarks>
    /// 联机：特效只在自己的客户端生效，别人不用装；同步给队友的构筑数据只有装备编号和等级，不带追加的特效。
    /// 特效算改玩法的内容：<see cref="Multiplayer.GameplayAllowed"/> 为 false 时不再挂到游戏装备上（已经挂上的不收回）。
    /// </remarks>
    public static class CustomEquipEffects
    {
        internal const int FirstId = 4000000;

        private static readonly Dictionary<int, CustomEquipEffectInfo> registered = new Dictionary<int, CustomEquipEffectInfo>();
        // 特效实例的编号 → 行为对象（用编号：重试波次会把快照里的字段写回特效对象）
        private static readonly Dictionary<int, EquipEffectBehaviour> behaviours = new Dictionary<int, EquipEffectBehaviour>();
        private static readonly List<object> scratch = new List<object>();
        private static int nextInstance = 1;
        private static bool hooked;
        private static float nextCheck;

        /// <summary>相关补丁都装上了。为 false 时注册不会生效。</summary>
        public static bool Available { get; internal set; }

        public static IReadOnlyCollection<CustomEquipEffectInfo> All => registered.Values;

        public static CustomEquipEffectInfo Get(int id) => registered.TryGetValue(id, out var info) ? info : null;

        internal static bool IsCustom(int id) => registered.ContainsKey(id);

        // 挂到某件游戏装备上的特效
        internal static IEnumerable<CustomEquipEffectInfo> AttachedTo(int equipment)
        {
            foreach (var info in registered.Values)
                if (info.Def.AttachTo != null && Array.IndexOf(info.Def.AttachTo, equipment) >= 0)
                    yield return info;
        }

        internal static CustomEquipEffectInfo Register(string owner, ModText text, EquipEffectDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (string.IsNullOrEmpty(def.Key) || !def.Key.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new ArgumentException($"装备特效的 Key 只能用字母、数字、下划线：\"{def.Key}\"");
            if (def.Create == null) throw new ArgumentException($"装备特效 {def.Key} 没有设置 Create");
            if (registered.Values.Any(r => r.Owner == owner && r.Key == def.Key))
                throw new ArgumentException($"装备特效 {def.Key} 已经注册过");
            var badTarget = (def.AttachTo ?? new int[0]).Where(n => n >= CustomEquipment.FirstId || n <= 0).ToList();
            if (badTarget.Count > 0)
                throw new ArgumentException($"装备特效 {def.Key} 的 AttachTo 只能写游戏装备的编号：{string.Join(", ", badTarget)}（自定义装备用 EquipmentDef.Effects）");

            int id = SkillIds.Allocate(owner, def.Key, FirstId, registered.ContainsKey);
            var info = new CustomEquipEffectInfo(id, owner, def, text);
            registered[id] = info;
            EnsureHooked();
            if (GameData.Ready) Safe.Run($"注册装备特效 {def.Key}", () => GameRaw.InjectEquipEffect(info));
            RunLog.WriteCore(ModKitPlugin.Name, "装备", RunLogLevel.Info, "注册特效", new (string, object)[] { ("Mod", owner), ("键", def.Key), ("编号", id) }, mirror: false);
            return info;
        }

        internal static void Unregister(CustomEquipEffectInfo info)
        {
            if (info == null || !registered.Remove(info.Id)) return;
            // 身上装备里的特效对象留在游戏里，但不再转给 Mod：先让行为撤销自己的改动
            foreach (var b in behaviours.Values.Where(b => b.Info == info && b.Active).ToList())
            {
                b.Active = false;
                Safe.Run($"{info.Key}.OnRemoved", b.OnRemoved);
            }
            foreach (var key in behaviours.Where(kv => kv.Value.Info == info).Select(kv => kv.Key).ToList()) behaviours.Remove(key);
            if (!ModKit.Compatible) return;
            Safe.Run($"注销装备特效 {info.Key}", () => GameRaw.EjectEquipEffect(info.Id));
        }

        /// <summary>本地玩家装备上这个特效的行为对象（可能有多个：好几件装备都挂了它）。</summary>
        public static List<EquipEffectBehaviour> OnLocalPlayer(CustomEquipEffectInfo info)
        {
            var list = new List<EquipEffectBehaviour>();
            if (info == null || !ModKit.Compatible) return list;
            GameRaw.LocalModEquipEffects(scratch);
            foreach (var native in scratch)
                if (behaviours.TryGetValue(GameRaw.ModEquipEffectInstance(native), out var b) && b.Info == info && b.Active) list.Add(b);
            return list;
        }

        private static void EnsureHooked()
        {
            if (hooked) return;
            hooked = true;
            ModEvents.Update += Tick;
            ModEvents.BattleEnded += Prune;
            ModEvents.CheckpointRestored += Restored;
            GameEvents.Raised += e => Dispatch(b => b.OnGameEvent(e), nameof(EquipEffectBehaviour.OnGameEvent));
            Combat.Hit += hit => Dispatch(b => b.OnHit(hit), nameof(EquipEffectBehaviour.OnHit));
        }

        // 表加载晚于 Mod 注册；游戏重建表、重建本地化表后也要补回来
        private static void Tick()
        {
            if (registered.Count == 0 || UnityEngine.Time.unscaledTime < nextCheck || !GameData.Ready) return;
            nextCheck = UnityEngine.Time.unscaledTime + 1f;
            foreach (var info in registered.Values)
                if (!GameRaw.EquipEffectInjected(info.Id))
                    Safe.Run($"注册装备特效 {info.Key}", () => GameRaw.InjectEquipEffect(info));
        }

        // 只发给本地玩家穿着的装备上的特效
        private static void Dispatch(Action<EquipEffectBehaviour> call, string name)
        {
            if (behaviours.Count == 0) return;
            GameRaw.LocalModEquipEffects(scratch);
            foreach (var native in scratch)
                if (behaviours.TryGetValue(GameRaw.ModEquipEffectInstance(native), out var b) && b.Active)
                    Safe.Run($"{b.Info.Key}.{name}", () => call(b));
        }

        // 重试波次把装备还原到波次开始时：波次中卖掉的装备回到身上（特效对象不会再收到 Init），重新接上
        private static void Restored()
        {
            if (behaviours.Count == 0) return;
            GameRaw.LocalModEquipEffects(scratch);
            foreach (var native in scratch)
                if (behaviours.TryGetValue(GameRaw.ModEquipEffectInstance(native), out var b))
                {
                    b.Native = native;
                    b.Active = true;
                }
        }

        // 一局结束后丢掉不在身上的行为对象
        private static void Prune()
        {
            if (behaviours.Count == 0) return;
            GameRaw.LocalModEquipEffects(scratch);
            var alive = new HashSet<int>(scratch.Select(GameRaw.ModEquipEffectInstance));
            foreach (var key in behaviours.Keys.Where(k => !alive.Contains(k)).ToList()) behaviours.Remove(key);
        }

        // ---- 由游戏侧的 ModEquipEffect 调用（只在本地玩家的装备上）----

        internal static int Created(int id)
        {
            int instance = nextInstance++;
            if (registered.TryGetValue(id, out var info))
            {
                EquipEffectBehaviour b = null;
                Safe.Run($"{info.Key}.Create", () => b = info.Def.Create());
                if (b != null)
                {
                    b.Info = info;
                    behaviours[instance] = b;
                }
            }
            return instance;
        }

        internal static void Added(int instance, object native)
        {
            if (!behaviours.TryGetValue(instance, out var b)) return;
            b.Native = native;
            b.Active = true;
            Safe.Run($"{b.Info.Key}.OnAdded", b.OnAdded);
        }

        internal static void Removed(int instance)
        {
            if (!behaviours.TryGetValue(instance, out var b) || !b.Active) return;
            // 卸下后同一件装备再穿上会新建特效对象，旧对象可能还挂在装备上，不再转发
            b.Active = false;
            Safe.Run($"{b.Info.Key}.OnRemoved", b.OnRemoved);
        }

        internal static void LevelChanged(int instance, object native, int oldLevel, int newLevel)
        {
            if (!behaviours.TryGetValue(instance, out var b) || !b.Active || oldLevel == newLevel) return;
            b.Native = native;
            Safe.Run($"{b.Info.Key}.OnLevelChanged", () => b.OnLevelChanged(oldLevel, newLevel));
        }

        internal static void Update(int instance, object native)
        {
            if (!behaviours.TryGetValue(instance, out var b) || !b.Active) return;
            b.Native = native;
            // 每帧调用，不用 Safe.Run（避免每帧拼字符串）
            try
            {
                b.OnUpdate();
            }
            catch (Exception e)
            {
                Safe.Report(b.Info.Key + ".OnUpdate", e);
            }
        }

        internal static string Text(int id, bool description)
            => registered.TryGetValue(id, out var info) ? description ? info.Description : info.Name : null;
    }

    /// <summary>
    /// 全新装备的定义，用 <see cref="ModContext.RegisterEquipment"/> 注册。
    /// 不进商店；<see cref="Artifact"/> 为 true 时进神器三选一，也可以由 Mod 用 <see cref="CustomEquipment.Give"/> 发放。
    /// </summary>
    public sealed class EquipmentDef
    {
        /// <summary>在这个 Mod 里唯一的键（只用字母、数字、下划线），游戏里的编号由它算出来，见 <see cref="CustomEquipmentInfo.Id"/>。</summary>
        public string Key = "";
        /// <summary>名字（中文原文；翻译键 equip.&lt;Key&gt;.name）。</summary>
        public string Name = "";
        /// <summary>说明（装备说明的第一段，特效的说明由游戏接在后面）；翻译键 equip.&lt;Key&gt;.desc。</summary>
        public string Description = "";
        /// <summary>图标：游戏装备图标的名字（Bundles/UI/Icon/Shop/ 和 Equipment/ 下，例如 equip_2），或 <see cref="ModContext.LoadIcon"/> 返回的名字。</summary>
        public string Icon = "equip_2";
        /// <summary>
        /// 属性加成：键是装备表的属性名（hp、mp、attack、attackSpeed、Armor、moveSpeed、STR、STA、AGI、baojilv、baojiDamage、skillDamage、luck…，
        /// 完整列表见 <see cref="CustomEquipment.AttributeNames"/>），值和装备表一样（百分比类写小数，例如 0.1 = 10%）。
        /// </summary>
        public Dictionary<string, float> Attributes = new Dictionary<string, float>();
        /// <summary>自定义特效（先用 <see cref="ModContext.RegisterEquipEffect"/> 注册）。</summary>
        public List<CustomEquipEffectInfo> Effects = new List<CustomEquipEffectInfo>();
        /// <summary>游戏自己的特效（EquipSkillType 的名字，例如 Sputtering），数值用游戏里带这个特效的第一件装备的。</summary>
        public List<string> GameEffects = new List<string>();
        /// <summary>进神器三选一的候选池（联机握手不允许改玩法时自动去掉）。</summary>
        public bool Artifact;
    }

    /// <summary>已注册的自定义装备。</summary>
    public sealed class CustomEquipmentInfo
    {
        /// <summary>游戏里的装备编号（装备表的 id；由 Mod 的 GUID 和 Key 算出，5000000 起，不和游戏的冲突）。</summary>
        public int Id { get; }
        public string Key { get; }
        /// <summary>注册它的 Mod 的 GUID。</summary>
        public string Owner { get; }
        public EquipmentDef Def { get; }
        internal ModText Text { get; }
        // 注册时的特效列表（定义之后被改也不影响）
        internal readonly CustomEquipEffectInfo[] Effects;
        internal readonly string[] GameEffects;

        internal CustomEquipmentInfo(int id, string owner, EquipmentDef def, ModText text)
        {
            Id = id;
            Owner = owner;
            Key = def.Key;
            Def = def;
            Text = text;
            Effects = (def.Effects ?? new List<CustomEquipEffectInfo>()).ToArray();
            GameEffects = (def.GameEffects ?? new List<string>()).ToArray();
        }

        public string Name => Text != null ? Text[$"equip.{Key}.name", Def.Name] : Def.Name;
        /// <summary>当前语言的说明。</summary>
        public string Description => Text != null ? Text[$"equip.{Key}.desc", Def.Description] : Def.Description;
    }

    /// <summary>
    /// 自定义装备的注册表。装备写进游戏的装备表，穿上、属性、特效、说明都走游戏自己的流程。
    /// </summary>
    /// <remarks>
    /// 联机：装备只在自己的客户端生效，别人不用装；同步给队友、上传国王对决、镜像 Boss 的构筑数据会去掉自定义装备
    /// （队友看你的构筑面板、结算界面因此也看不到它）。不会掉在地上，也不进商店。
    /// 装备算改玩法的内容：<see cref="Multiplayer.GameplayAllowed"/> 为 false 时不进神器三选一，<see cref="Give"/> 也不给。
    /// 第一版不能强化（maxLevel = 0），卖出价按游戏的默认价格。
    /// </remarks>
    public static class CustomEquipment
    {
        internal const int FirstId = 5000000;

        private static readonly Dictionary<int, CustomEquipmentInfo> registered = new Dictionary<int, CustomEquipmentInfo>();
        private static bool hooked;
        private static float nextCheck;

        /// <summary>相关补丁都装上了。为 false 时注册不会生效。</summary>
        public static bool Available { get; internal set; }

        public static IReadOnlyCollection<CustomEquipmentInfo> All => registered.Values;

        public static CustomEquipmentInfo Get(int id) => registered.TryGetValue(id, out var info) ? info : null;

        /// <summary><see cref="EquipmentDef.Attributes"/> 能用的属性名（游戏表加载前也能读；ModKit 不兼容时为空）。</summary>
        public static IReadOnlyList<string> AttributeNames => ModKit.Compatible ? GameRaw.EquipAttributeNames() : new string[0];

        internal static bool IsCustom(int id) => registered.ContainsKey(id);

        internal static bool IsCustom(string id) => int.TryParse(id, out int n) && n >= FirstId && registered.ContainsKey(n);

        // 进神器三选一：定义里允许，而且联机握手允许改玩法
        internal static bool InPool(int id) => registered.TryGetValue(id, out var info) && info.Def.Artifact && Multiplayer.GameplayAllowed;

        internal static CustomEquipmentInfo Register(string owner, ModText text, EquipmentDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (string.IsNullOrEmpty(def.Key) || !def.Key.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new ArgumentException($"装备的 Key 只能用字母、数字、下划线：\"{def.Key}\"");
            if (registered.Values.Any(r => r.Owner == owner && r.Key == def.Key))
                throw new ArgumentException($"装备 {def.Key} 已经注册过");
            foreach (var e in def.Effects ?? new List<CustomEquipEffectInfo>())
                if (e == null || !CustomEquipEffects.IsCustom(e.Id))
                    throw new ArgumentException($"装备 {def.Key} 的 Effects 里有没注册（或已经注销）的特效");
            if (ModKit.Compatible)
            {
                var names = GameRaw.EquipAttributeNames();
                var badAttr = (def.Attributes ?? new Dictionary<string, float>()).Keys.FirstOrDefault(k => !names.Contains(GameRaw.NormalizeEquipAttribute(k)));
                if (badAttr != null) throw new ArgumentException($"装备 {def.Key} 的 Attributes 里有不认识的属性 \"{badAttr}\"（可以用的见 CustomEquipment.AttributeNames）");
                var badEffect = (def.GameEffects ?? new List<string>()).FirstOrDefault(n => !GameRaw.IsGameEquipEffect(n));
                if (badEffect != null) throw new ArgumentException($"装备 {def.Key} 的 GameEffects 里有不认识的特效 \"{badEffect}\"（写 EquipSkillType 的名字）");
            }

            int id = SkillIds.Allocate(owner, def.Key, FirstId, registered.ContainsKey);
            var info = new CustomEquipmentInfo(id, owner, def, text);
            registered[id] = info;
            EnsureHooked();
            if (GameData.Ready) Safe.Run($"注册装备 {def.Key}", () => GameRaw.InjectEquipment(info));
            RunLog.WriteCore(ModKitPlugin.Name, "装备", RunLogLevel.Info, "注册装备", new (string, object)[] { ("Mod", owner), ("键", def.Key), ("编号", id) }, mirror: false);
            return info;
        }

        internal static void Unregister(CustomEquipmentInfo info)
        {
            if (info == null || !registered.Remove(info.Id)) return;
            if (!ModKit.Compatible) return;
            // 先从玩家身上卸下（走游戏的卸下，特效会收到 OnRemoved），再删表：之后游戏查不到这件装备的行
            Safe.Run($"卸下装备 {info.Key}", () => GameRaw.RemoveLocalEquipment(info.Id));
            Safe.Run($"注销装备 {info.Key}", () => GameRaw.EjectEquipment(info.Id));
        }

        /// <summary>
        /// 直接给本地玩家穿上这件装备。装备栏满了（6 格）、联机时 <see cref="Multiplayer.GameplayAllowed"/> 为 false 就不给，返回 false。
        /// </summary>
        public static bool Give(CustomEquipmentInfo info)
        {
            var p = Game.LocalPlayer;
            if (info == null || !registered.ContainsKey(info.Id) || !Available || p == null || !Multiplayer.GameplayAllowed) return false;
            if (!GameRaw.EquipmentInjected(info.Id)) return false;
            return GameRaw.GiveEquipment(info.Id);
        }

        /// <summary>本地玩家身上有几件这个装备。</summary>
        public static int CountOnLocalPlayer(CustomEquipmentInfo info)
            => info == null || !ModKit.Compatible ? 0 : GameRaw.LocalEquipmentCount(info.Id);

        private static void EnsureHooked()
        {
            if (hooked) return;
            hooked = true;
            ModEvents.Update += Tick;
        }

        // 表加载晚于 Mod 注册；游戏重建表后也要补回来
        private static void Tick()
        {
            if (registered.Count == 0 || UnityEngine.Time.unscaledTime < nextCheck || !GameData.Ready) return;
            nextCheck = UnityEngine.Time.unscaledTime + 1f;
            foreach (var info in registered.Values)
                if (!GameRaw.EquipmentInjected(info.Id))
                    Safe.Run($"注册装备 {info.Key}", () => GameRaw.InjectEquipment(info));
        }

        internal static string Text(int id, bool description)
            => registered.TryGetValue(id, out var info) ? description ? info.Description : info.Name : null;
    }
}
