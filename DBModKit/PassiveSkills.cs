using System;
using System.Collections.Generic;
using System.Linq;

namespace DBModKit
{
    /// <summary>被动技能的品级（技能书三选一按品级分池）。</summary>
    public enum PassiveQuality
    {
        D,
        C,
        B,
        A,
        S,
    }

    /// <summary>
    /// 自定义被动技能的定义，用 <see cref="ModContext.RegisterPassive"/> 注册。
    /// 注册后和游戏自己的被动一样：会出现在技能书三选一里（<see cref="InSkillBook"/>），占一个技能格，图鉴里也能看到。
    /// </summary>
    public sealed class PassiveSkillDef
    {
        /// <summary>在这个 Mod 里唯一的键（只用字母、数字、下划线），游戏里的编号由它算出来，见 <see cref="PassiveSkillInfo.Id"/>。</summary>
        public string Key = "";
        /// <summary>名字（中文原文；翻译写进 Mod 的 lang 文件，键是 passive.&lt;Key&gt;.name）。</summary>
        public string Name = "";
        /// <summary>说明，{0} {1}… 会换成 <see cref="Values"/>（加上等级成长后）的值；翻译键是 passive.&lt;Key&gt;.desc。</summary>
        public string Description = "";
        /// <summary>图标：游戏技能图标的名字（Bundles/UI/Icon/Skill/ 下，例如 SpellBook01_66）。</summary>
        public string Icon = "SpellBook01_66";
        public PassiveQuality Quality = PassiveQuality.C;
        /// <summary>数值，行为里用 <see cref="PassiveSkill.Value"/> 读。</summary>
        public float[] Values = new float[0];
        /// <summary>每升一级各数值增加多少（可以比 Values 短）。</summary>
        public float[] LevelUp = new float[0];
        /// <summary>放进技能书三选一的候选池。为 false 时只能由 Mod 用 <see cref="PassiveSkills.Give"/> 发放。</summary>
        public bool InSkillBook = true;
        /// <summary>已经有这个被动时，技能书里还能再出现。</summary>
        public bool Repeatable;
        /// <summary>技能标签（游戏的 SkillTag 名字，例如 Buff、Heal），图鉴和流派用。</summary>
        public string[] Tags = { "Buff" };
        /// <summary>每获得一次就创建一个新的行为对象。</summary>
        public Func<PassiveSkill> Create;
    }

    /// <summary>已注册的自定义被动。</summary>
    public sealed class PassiveSkillInfo
    {
        /// <summary>游戏里的被动编号（由 Mod 的 GUID 和 Key 算出，每次启动都一样；1000000 起，不和游戏的冲突）。</summary>
        public int Id { get; }
        public string Key { get; }
        /// <summary>注册它的 Mod 的 GUID。</summary>
        public string Owner { get; }
        public PassiveSkillDef Def { get; }
        internal ModText Text { get; }

        internal PassiveSkillInfo(int id, string owner, PassiveSkillDef def, ModText text)
        {
            Id = id;
            Owner = owner;
            Key = def.Key;
            Def = def;
            Text = text;
        }

        /// <summary>当前语言的名字。</summary>
        public string Name => Text != null ? Text[$"passive.{Key}.name", Def.Name] : Def.Name;
        /// <summary>当前语言的说明（还没填数值）。</summary>
        public string Description => Text != null ? Text[$"passive.{Key}.desc", Def.Description] : Def.Description;
    }

    /// <summary>
    /// 自定义被动的行为。继承它，按需重写 On… 方法。每个技能格里的被动对应一个实例。
    /// 只在本地玩家身上运行（游戏的被动也是在拥有者的客户端上生效）。
    /// </summary>
    /// <remarks>
    /// 加属性请用 <see cref="Player.AddStats"/> 等游戏自己的方法，并在 <see cref="OnRemoved"/> 里撤销（和游戏的被动一样）。
    /// "重试波次"会把玩家属性恢复到波次开始时，但不会恢复这个对象里的字段。
    /// </remarks>
    public abstract class PassiveSkill
    {
        internal object Native;

        public PassiveSkillInfo Info { get; internal set; }

        /// <summary>拥有者（本地玩家）。</summary>
        public Player Owner => Native == null ? null : Player.Wrap(GameRaw.PassiveOwner(Native));

        /// <summary>等级（技能升级后提高，从 0 开始）。</summary>
        public int Level => Native == null ? 0 : GameRaw.PassiveLevel(Native);

        /// <summary>第 i 个数值（含等级成长），越界时为 0。</summary>
        public float Value(int i) => Native == null ? 0f : GameRaw.PassiveValue(Native, i);

        /// <summary>获得时（游戏调用被动的"获得"时）。</summary>
        protected internal virtual void OnAdded() { }

        /// <summary>失去时（被替换、遗忘、一局结束）。</summary>
        protected internal virtual void OnRemoved() { }

        /// <summary>每帧（在技能栏里时）。</summary>
        protected internal virtual void OnUpdate() { }

        /// <summary>战斗事件（击杀、暴击、受伤、波次等，见 <see cref="GameEvents"/>）。</summary>
        protected internal virtual void OnGameEvent(GameEvent e) { }

        /// <summary>本机造成的命中（见 <see cref="Combat.Hit"/>）。</summary>
        protected internal virtual void OnHit(HitInfo hit) { }
    }

    /// <summary>
    /// 自定义被动技能的注册表。游戏把被动按"脚本名"创建（被动表的 class 列），ModKit 在这里接上 Mod 的行为。
    /// </summary>
    /// <remarks>
    /// 联机：被动只在自己的客户端生效，别人不用装。上传给别人的构筑数据（玩家信息面板、国王对决、镜像 Boss）
    /// 会去掉自定义被动，没装 Mod 的人不会看到也不会出错；代价是这些地方也看不到它。
    /// </remarks>
    public static class PassiveSkills
    {
        internal const int FirstId = 1000000;

        private static readonly Dictionary<int, PassiveSkillInfo> registered = new Dictionary<int, PassiveSkillInfo>();
        // 被动实例的编号 → 行为对象。用编号而不是实例本身：重试波次可能用快照里的副本替换实例
        private static readonly Dictionary<int, PassiveSkill> behaviours = new Dictionary<int, PassiveSkill>();
        private static readonly List<object> scratch = new List<object>();
        private static int nextInstance = 1;
        private static bool hooked;
        private static float nextCheck;

        /// <summary>相关补丁都装上了。为 false 时注册不会生效。</summary>
        public static bool Available { get; internal set; }

        public static IReadOnlyCollection<PassiveSkillInfo> All => registered.Values;

        public static PassiveSkillInfo Get(int id) => registered.TryGetValue(id, out var info) ? info : null;

        internal static bool IsCustom(int id) => registered.ContainsKey(id);

        internal static bool IsCustom(string id) => int.TryParse(id, out int n) && registered.ContainsKey(n);

        internal static PassiveSkillInfo Register(string owner, ModText text, PassiveSkillDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (string.IsNullOrEmpty(def.Key) || !def.Key.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new ArgumentException($"被动的 Key 只能用字母、数字、下划线：\"{def.Key}\"");
            if (def.Create == null) throw new ArgumentException($"被动 {def.Key} 没有设置 Create");
            if (registered.Values.Any(r => r.Owner == owner && r.Key == def.Key))
                throw new ArgumentException($"被动 {def.Key} 已经注册过");

            int id = SkillIds.Allocate(owner, def.Key, FirstId, registered.ContainsKey);

            var info = new PassiveSkillInfo(id, owner, def, text);
            registered[id] = info;
            EnsureHooked();
            if (GameData.Ready) Safe.Run($"注册被动 {def.Key}", () => GameRaw.InjectPassive(info));
            RunLog.WriteCore(ModKitPlugin.Name, "被动", RunLogLevel.Info, "注册", new (string, object)[] { ("Mod", owner), ("键", def.Key), ("编号", id) }, mirror: false);
            return info;
        }

        internal static void Unregister(PassiveSkillInfo info)
        {
            if (info == null || !registered.Remove(info.Id)) return;
            if (!ModKit.Compatible) return;
            // 先从技能栏拿掉（走游戏的移除，会调用 OnRemoved），再删表
            Safe.Run($"移除被动 {info.Key}", () => GameRaw.RemoveLocalPassives(info.Id));
            Safe.Run($"注销被动 {info.Key}", () => GameRaw.EjectPassive(info.Id));
        }

        /// <summary>
        /// 直接给本地玩家一个被动。技能格满了时：replaceSlot 为技能格序号（0 起）就替换那一格，为 -1 时不给、返回 false。
        /// </summary>
        public static bool Give(PassiveSkillInfo info, int replaceSlot = -1)
        {
            if (info == null || !registered.ContainsKey(info.Id) || !Available || Game.LocalPlayer == null) return false;
            return GameRaw.GivePassive(info.Id, replaceSlot);
        }

        /// <summary>本地玩家技能栏里这个被动的行为对象（可能有多个，例如可重复学习的）。</summary>
        public static List<PassiveSkill> OnLocalPlayer(PassiveSkillInfo info)
        {
            var list = new List<PassiveSkill>();
            if (info == null || !ModKit.Compatible) return list;
            GameRaw.LocalModPassives(scratch);
            foreach (var native in scratch)
                if (behaviours.TryGetValue(GameRaw.ModPassiveInstance(native), out var b) && b.Info == info) list.Add(b);
            return list;
        }

        private static void EnsureHooked()
        {
            if (hooked) return;
            hooked = true;
            ModEvents.Update += Tick;
            ModEvents.BattleEnded += Prune;
            GameEvents.Raised += e => Dispatch(b => b.OnGameEvent(e), nameof(PassiveSkill.OnGameEvent));
            Combat.Hit += hit => Dispatch(b => b.OnHit(hit), nameof(PassiveSkill.OnHit));
        }

        // 表加载晚于 Mod 注册；游戏重建表（切语言、重新初始化）后也要补回来
        private static void Tick()
        {
            if (registered.Count == 0 || UnityEngine.Time.unscaledTime < nextCheck || !GameData.Ready) return;
            nextCheck = UnityEngine.Time.unscaledTime + 1f;
            foreach (var info in registered.Values)
                if (!GameRaw.PassiveInjected(info.Id))
                    Safe.Run($"注册被动 {info.Key}", () => GameRaw.InjectPassive(info));
        }

        // 只发给本地玩家技能栏里的被动：被替换掉的、被"重试波次"撤掉的都收不到
        private static void Dispatch(Action<PassiveSkill> call, string name)
        {
            if (behaviours.Count == 0) return;
            GameRaw.LocalModPassives(scratch);
            foreach (var native in scratch)
                if (behaviours.TryGetValue(GameRaw.ModPassiveInstance(native), out var b))
                    Safe.Run($"{b.Info.Key}.{name}", () => call(b));
        }

        // 一局结束后丢掉不在技能栏里的行为对象
        private static void Prune()
        {
            if (behaviours.Count == 0) return;
            GameRaw.LocalModPassives(scratch);
            var alive = new HashSet<int>(scratch.Select(GameRaw.ModPassiveInstance));
            foreach (var key in behaviours.Keys.Where(k => !alive.Contains(k)).ToList()) behaviours.Remove(key);
        }

        // ---- 由游戏侧的 ModPassive 调用 ----

        internal static int Created(int id)
        {
            int instance = nextInstance++;
            if (registered.TryGetValue(id, out var info))
            {
                PassiveSkill b = null;
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
