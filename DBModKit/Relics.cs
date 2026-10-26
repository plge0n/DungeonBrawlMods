using System;
using System.Collections.Generic;
using System.Linq;

namespace DBModKit
{
    /// <summary>
    /// 自定义遗物的定义，用 <see cref="ModContext.RegisterRelic"/> 注册。
    /// 注册后和游戏自己的遗物一样：会出现在遗物三选一里（<see cref="InPool"/>），显示在遗物栏，可以升级。
    /// </summary>
    public sealed class RelicDef
    {
        /// <summary>在这个 Mod 里唯一的键（只用字母、数字、下划线），游戏里的编号由它算出来，见 <see cref="CustomRelicInfo.Id"/>。</summary>
        public string Key = "";
        /// <summary>名字（中文原文；翻译键 relic.&lt;Key&gt;.name）。</summary>
        public string Name = "";
        /// <summary>说明，{0} {1}… 由游戏换成 <see cref="Values"/>（加上等级成长、按 <see cref="ValueTypes"/> 显示）；翻译键 relic.&lt;Key&gt;.desc。</summary>
        public string Description = "";
        /// <summary>图标：游戏遗物图标的名字（Bundles/UI/Icon/Remains/ 下，例如 remain_str），或 <see cref="ModContext.LoadIcon"/> 返回的名字。</summary>
        public string Icon = "remain_str";
        /// <summary>品级（三选一按品级抽）。</summary>
        public PassiveQuality Quality = PassiveQuality.C;
        /// <summary>数值，行为里用 <see cref="RelicBehaviour.Value"/> 读。</summary>
        public float[] Values = new float[0];
        /// <summary>每升一级各数值增加多少（可以比 Values 短）。</summary>
        public float[] LevelUp = new float[0];
        /// <summary>说明里每个数值的显示方式：Normal（原值）、Percent（小数转百分比）、PercentConst（直接加 %）、InversePercent（(1-值)×100%）。可以比 Values 短。</summary>
        public string[] ValueTypes = new string[0];
        /// <summary>最多升到几级：-1 不限，0 不能升级。</summary>
        public int MaxLevel = -1;
        /// <summary>已经有了就不会再出现在三选一里。</summary>
        public bool Unique = true;
        /// <summary>放进遗物三选一的候选池。为 false 时只能由 Mod 用 <see cref="CustomRelics.Give"/> 发放。</summary>
        public bool InPool = true;
        /// <summary>每获得一次就创建一个新的行为对象。</summary>
        public Func<RelicBehaviour> Create;
    }

    /// <summary>已注册的自定义遗物。</summary>
    public sealed class CustomRelicInfo
    {
        /// <summary>游戏里的遗物编号（ItemType 的值；由 Mod 的 GUID 和 Key 算出，3000000 起，不和游戏的冲突）。</summary>
        public int Id { get; }
        public string Key { get; }
        /// <summary>注册它的 Mod 的 GUID。</summary>
        public string Owner { get; }
        public RelicDef Def { get; }
        internal ModText Text { get; }

        internal CustomRelicInfo(int id, string owner, RelicDef def, ModText text)
        {
            Id = id;
            Owner = owner;
            Key = def.Key;
            Def = def;
            Text = text;
        }

        // 进遗物三选一：定义里允许，而且联机握手允许改玩法
        internal bool InPool => Def.InPool && Multiplayer.GameplayAllowed;

        public string Name => Text != null ? Text[$"relic.{Key}.name", Def.Name] : Def.Name;
        /// <summary>当前语言的说明（还没填数值）。</summary>
        public string Description => Text != null ? Text[$"relic.{Key}.desc", Def.Description] : Def.Description;
    }

    /// <summary>
    /// 自定义遗物的行为。继承它，按需重写 On… 方法。每获得一次对应一个实例，只在本地玩家身上运行。
    /// </summary>
    /// <remarks>
    /// 加属性用 <see cref="Player.AddStats"/> 等游戏自己的方法，在 <see cref="OnRemoved"/> 里撤销；升级时在 <see cref="OnLevelChanged"/> 里补差值。
    /// "重试波次"会把玩家属性和遗物还原到波次开始时，但不会还原这个对象里的字段。
    /// </remarks>
    public abstract class RelicBehaviour
    {
        internal object Native;

        public CustomRelicInfo Info { get; internal set; }

        /// <summary>拥有者（本地玩家）。</summary>
        public Player Owner => Native == null ? null : Player.Wrap(GameRaw.RelicOwner(Native));

        /// <summary>等级（从 0 开始）。</summary>
        public int Level => Native == null ? 0 : GameRaw.RelicLevel(Native);

        /// <summary>第 i 个数值：Values[i] + LevelUp[i] × 等级；越界时为 0。</summary>
        public float Value(int i) => Native == null ? 0f : GameRaw.RelicValue(Native, i, Level);

        /// <summary>某个等级时第 i 个数值（升级时算差值用）。</summary>
        public float ValueAt(int i, int level) => Native == null ? 0f : GameRaw.RelicValue(Native, i, level);

        /// <summary>获得时。</summary>
        protected internal virtual void OnAdded() { }

        /// <summary>失去时（被移除、一局结束）。</summary>
        protected internal virtual void OnRemoved() { }

        /// <summary>等级变了（升级或降级）。</summary>
        protected internal virtual void OnLevelChanged(int oldLevel, int newLevel) { }

        /// <summary>每帧。</summary>
        protected internal virtual void OnUpdate() { }

        /// <summary>战斗事件（见 <see cref="GameEvents"/>）。</summary>
        protected internal virtual void OnGameEvent(GameEvent e) { }

        /// <summary>本机造成的命中（见 <see cref="Combat.Hit"/>）。</summary>
        protected internal virtual void OnHit(HitInfo hit) { }
    }

    /// <summary>
    /// 自定义遗物的注册表。游戏按遗物表的"效果名"创建遗物对象（bby.law），ModKit 在这里接上 Mod 的行为。
    /// </summary>
    /// <remarks>
    /// 联机：遗物只在自己的客户端生效，别人不用装；不是团队遗物，也没有获取次数上限，所以选的时候不会发消息给房主。
    /// 同步给队友、上传国王对决、镜像 Boss 的构筑数据会去掉自定义遗物（玩家信息面板、结算界面因此也看不到它）。
    /// 遗物算改玩法的内容：<see cref="Multiplayer.GameplayAllowed"/> 为 false 时不进三选一，<see cref="Give"/> 也不给。
    /// </remarks>
    public static class CustomRelics
    {
        internal const int FirstId = 3000000;

        private static readonly Dictionary<int, CustomRelicInfo> registered = new Dictionary<int, CustomRelicInfo>();
        // 遗物实例的编号 → 行为对象（用编号：重试波次会用快照里的副本替换实例）
        private static readonly Dictionary<int, RelicBehaviour> behaviours = new Dictionary<int, RelicBehaviour>();
        private static readonly List<object> scratch = new List<object>();
        private static int nextInstance = 1;
        private static bool hooked;
        private static float nextCheck;

        /// <summary>相关补丁都装上了。为 false 时注册不会生效。</summary>
        public static bool Available { get; internal set; }

        public static IReadOnlyCollection<CustomRelicInfo> All => registered.Values;

        public static CustomRelicInfo Get(int id) => registered.TryGetValue(id, out var info) ? info : null;

        internal static bool IsCustom(int id) => registered.ContainsKey(id);

        internal static bool IsCustom(string id) => int.TryParse(id, out int n) && registered.ContainsKey(n);

        internal static bool InPool(int id) => registered.TryGetValue(id, out var info) && info.InPool;

        internal static CustomRelicInfo Register(string owner, ModText text, RelicDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (string.IsNullOrEmpty(def.Key) || !def.Key.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new ArgumentException($"遗物的 Key 只能用字母、数字、下划线：\"{def.Key}\"");
            if (def.Create == null) throw new ArgumentException($"遗物 {def.Key} 没有设置 Create");
            if (registered.Values.Any(r => r.Owner == owner && r.Key == def.Key))
                throw new ArgumentException($"遗物 {def.Key} 已经注册过");
            var badType = (def.ValueTypes ?? new string[0]).FirstOrDefault(t => !GameRaw.IsRelicValueType(t));
            if (badType != null) throw new ArgumentException($"遗物 {def.Key} 的 ValueTypes 里有不认识的 \"{badType}\"（可以用 Normal、Percent、PercentConst、InversePercent）");

            int id = SkillIds.Allocate(owner, def.Key, FirstId, registered.ContainsKey);
            var info = new CustomRelicInfo(id, owner, def, text);
            registered[id] = info;
            EnsureHooked();
            if (GameData.Ready) Safe.Run($"注册遗物 {def.Key}", () => GameRaw.InjectRelic(info));
            RelicInfo.Invalidate();
            RunLog.WriteCore(ModKitPlugin.Name, "遗物", RunLogLevel.Info, "注册", new (string, object)[] { ("Mod", owner), ("键", def.Key), ("编号", id) }, mirror: false);
            return info;
        }

        internal static void Unregister(CustomRelicInfo info)
        {
            if (info == null || !registered.Remove(info.Id)) return;
            RelicInfo.Invalidate();
            if (!ModKit.Compatible) return;
            // 先从玩家身上拿掉（走游戏的移除，会调用 OnRemoved），再删表
            Safe.Run($"移除遗物 {info.Key}", () => GameRaw.RemoveLocalRelics(info.Id));
            Safe.Run($"注销遗物 {info.Key}", () => GameRaw.EjectRelic(info.Id));
        }

        /// <summary>直接给本地玩家这个遗物（等级从 0 开始）。联机时 <see cref="Multiplayer.GameplayAllowed"/> 为 false 就不给，返回 false。</summary>
        public static bool Give(CustomRelicInfo info, int level = 0)
        {
            var p = Game.LocalPlayer;
            if (info == null || !registered.ContainsKey(info.Id) || !Available || p == null || !Multiplayer.GameplayAllowed) return false;
            return GameRaw.GiveRelic(p.Raw, info.Id, level);
        }

        /// <summary>本地玩家身上这个遗物的行为对象（可能有多个：不是 Unique 的可以拿好几次）。</summary>
        public static List<RelicBehaviour> OnLocalPlayer(CustomRelicInfo info)
        {
            var list = new List<RelicBehaviour>();
            if (info == null || !ModKit.Compatible) return list;
            GameRaw.LocalModRelics(scratch);
            foreach (var native in scratch)
                if (behaviours.TryGetValue(GameRaw.ModRelicInstance(native), out var b) && b.Info == info) list.Add(b);
            return list;
        }

        private static void EnsureHooked()
        {
            if (hooked) return;
            hooked = true;
            ModEvents.Update += Tick;
            ModEvents.BattleEnded += Prune;
            GameEvents.Raised += e => Dispatch(b => b.OnGameEvent(e), nameof(RelicBehaviour.OnGameEvent));
            Combat.Hit += hit => Dispatch(b => b.OnHit(hit), nameof(RelicBehaviour.OnHit));
        }

        // 表加载晚于 Mod 注册；游戏重建表后也要补回来
        private static void Tick()
        {
            if (registered.Count == 0 || UnityEngine.Time.unscaledTime < nextCheck || !GameData.Ready) return;
            nextCheck = UnityEngine.Time.unscaledTime + 1f;
            foreach (var info in registered.Values)
                if (!GameRaw.RelicInjected(info.Id))
                {
                    Safe.Run($"注册遗物 {info.Key}", () => GameRaw.InjectRelic(info));
                    RelicInfo.Invalidate();
                }
        }

        // 只发给本地玩家身上的遗物
        private static void Dispatch(Action<RelicBehaviour> call, string name)
        {
            if (behaviours.Count == 0) return;
            GameRaw.LocalModRelics(scratch);
            foreach (var native in scratch)
                if (behaviours.TryGetValue(GameRaw.ModRelicInstance(native), out var b))
                    Safe.Run($"{b.Info.Key}.{name}", () => call(b));
        }

        // 一局结束后丢掉不在身上的行为对象
        private static void Prune()
        {
            if (behaviours.Count == 0) return;
            GameRaw.LocalModRelics(scratch);
            var alive = new HashSet<int>(scratch.Select(GameRaw.ModRelicInstance));
            foreach (var key in behaviours.Keys.Where(k => !alive.Contains(k)).ToList()) behaviours.Remove(key);
        }

        // ---- 由游戏侧的 ModRelic 调用 ----

        internal static int Created(int id)
        {
            int instance = nextInstance++;
            if (registered.TryGetValue(id, out var info))
            {
                RelicBehaviour b = null;
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
            Safe.Run($"{b.Info.Key}.OnAdded", b.OnAdded);
        }

        internal static void Removed(int instance)
        {
            if (behaviours.TryGetValue(instance, out var b))
                Safe.Run($"{b.Info.Key}.OnRemoved", b.OnRemoved);
        }

        internal static void LevelChanged(int instance, object native, int oldLevel, int newLevel)
        {
            if (!behaviours.TryGetValue(instance, out var b)) return;
            b.Native = native;
            Safe.Run($"{b.Info.Key}.OnLevelChanged", () => b.OnLevelChanged(oldLevel, newLevel));
        }

        internal static void Update(int instance, object native)
        {
            if (!behaviours.TryGetValue(instance, out var b)) return;
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
}
