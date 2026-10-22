using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DBModKit
{
    /// <summary>主动技能的瞄准方式（游戏的 indicator）。</summary>
    public enum SkillIndicator
    {
        /// <summary>不用瞄准，在自己脚下释放。</summary>
        None,
        /// <summary>选一个地面范围（圆）。</summary>
        Range,
        /// <summary>朝一个方向。</summary>
        Vector,
        /// <summary>锁定一个敌人。</summary>
        Target,
    }

    /// <summary>
    /// 自定义主动技能的定义，用 <see cref="ModContext.RegisterSkill"/> 注册。
    /// 注册后和游戏自己的主动技能一样：扣蓝、冷却、充能、瞄准、自动释放、施法动作都走游戏原来的流程，
    /// 技能真正生效时 ModKit 创建 <see cref="Create"/> 返回的行为对象。
    /// </summary>
    public sealed class ActiveSkillDef
    {
        /// <summary>在这个 Mod 里唯一的键（只用字母、数字、下划线）。</summary>
        public string Key = "";
        /// <summary>名字（中文原文；翻译键 skill.&lt;Key&gt;.name）。</summary>
        public string Name = "";
        /// <summary>
        /// 说明（翻译键 skill.&lt;Key&gt;.desc）。有 <see cref="Values"/> 时 {0} {1}… 换成这些数值；
        /// 否则 {0} 是游戏按伤害公式生成的文字（例如"100+攻击力×1.5"），{1} 是伤害间隔。
        /// </summary>
        public string Description = "";
        /// <summary>图标：游戏技能图标的名字（Bundles/UI/Icon/Skill/ 下）。</summary>
        public string Icon = "SpellBook01_66";
        public PassiveQuality Quality = PassiveQuality.C;
        public SkillIndicator Indicator = SkillIndicator.Range;
        /// <summary>蓝耗。</summary>
        public int Cost = 20;
        /// <summary>冷却（秒）。</summary>
        public float Cooldown = 8f;
        /// <summary>施法距离（瞄准范围；Target 类是能锁定多远的敌人）。</summary>
        public float CastingRange = 8f;
        /// <summary>作用半径（会乘上玩家的技能范围加成，行为里用 <see cref="ActiveSkill.Radius"/>）。</summary>
        public float Range = 3f;
        /// <summary>持续时间（秒），行为对象存活这么久；0 表示一次性（只调用 OnCast）。</summary>
        public float Duration;
        /// <summary>伤害间隔（只用于说明文字）。</summary>
        public float Interval;
        /// <summary>伤害公式：DamageBase + 属性 × DamageValue，见 <see cref="ActiveSkill.FormulaDamage"/>。</summary>
        public int DamageBase;
        public float DamageValue;
        /// <summary>DamageValue 乘的属性：0 攻击力、1 力量、2 敏捷、3 耐力、4 最大生命。</summary>
        public int DamageType;
        /// <summary>充能层数（0 = 没有充能）和每层充能时间。</summary>
        public int Charges;
        public float ChargeTime;
        /// <summary>说明里 {0} {1}… 的数值，行为里也能用 <see cref="ActiveSkill.Value"/> 读。</summary>
        public float[] Values = new float[0];
        /// <summary>允许右键开启自动释放。</summary>
        public bool CanAuto = true;
        /// <summary>放进技能书三选一的候选池。</summary>
        public bool InSkillBook = true;
        /// <summary>技能标签（SkillTag 名字），自动释放的判断（ModKit 的 AutoCast、SmartSkill）也会用到。</summary>
        public string[] Tags = { "Area" };
        /// <summary>每次释放都创建一个新的行为对象。</summary>
        public Func<ActiveSkill> Create;
    }

    /// <summary>已注册的自定义主动技能。</summary>
    public sealed class ActiveSkillInfo
    {
        /// <summary>游戏里的技能编号（ActiveSkillEnum 的值；由 GUID 和 Key 算出，2000000 起）。</summary>
        public int Id { get; }
        public string Key { get; }
        public string Owner { get; }
        public ActiveSkillDef Def { get; }
        internal ModText Text { get; }

        internal ActiveSkillInfo(int id, string owner, ActiveSkillDef def, ModText text)
        {
            Id = id;
            Owner = owner;
            Key = def.Key;
            Def = def;
            Text = text;
        }

        // 进技能书候选池：定义里允许，而且联机握手允许改玩法
        internal bool InSkillBook => Def.InSkillBook && Multiplayer.GameplayAllowed;

        public string Name => Text != null ? Text[$"skill.{Key}.name", Def.Name] : Def.Name;
        public string Description => Text != null ? Text[$"skill.{Key}.desc", Def.Description] : Def.Description;

        /// <summary>技能表里的数据（<see cref="SkillInfo"/>，和游戏技能一样能查标签、冷却等）。</summary>
        public SkillInfo Skill => SkillInfo.Get(Id);
    }

    /// <summary>
    /// 自定义主动技能的行为：一次释放对应一个实例。重写 <see cref="OnCast"/>（必要时加 <see cref="OnUpdate"/> / <see cref="OnEnd"/>）。
    /// </summary>
    /// <remarks>
    /// 联机时技能会在每个装了这个 Mod 的客户端上都创建一次（施法者的和别人的），所以特效写在哪里都能被看到；
    /// 伤害、回血等结算只在施法者的客户端（<see cref="IsMine"/>）上做，<see cref="DamageArea"/> 等方法在别的客户端上不起作用。
    /// 没装 Mod 的人看到施法动作，但没有效果。
    /// </remarks>
    public abstract class ActiveSkill
    {
        internal object Native;

        public ActiveSkillInfo Info { get; internal set; }

        /// <summary>施法者。</summary>
        public Role Caster => Native == null ? null : Role.Wrap(GameRaw.SkillCaster(Native));

        /// <summary>施法者是本机的角色：只有这里的结算会生效。</summary>
        public bool IsMine => Native != null && GameRaw.SkillIsMine(Native);

        /// <summary>释放位置（Range 类是选的地面点，其他是施法者脚下）。</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>朝向（角度，0 = +Z，顺时针）。</summary>
        public float Rotation { get; internal set; }

        /// <summary>朝向的单位向量（水平）。</summary>
        public Vector3 Direction => Quaternion.Euler(0f, Rotation, 0f) * Vector3.forward;

        /// <summary>Target 类锁定的敌人；没有时为 null。</summary>
        public Role Target { get; internal set; }

        /// <summary>作用半径（表里的 Range × 施法者的技能范围加成）。</summary>
        public float Radius { get; internal set; }

        /// <summary>剩余时间（秒）。小于 0 后技能结束（调用 OnEnd）；可以改，比如提前结束。</summary>
        public float Remaining
        {
            get => Native == null ? 0f : GameRaw.SkillRemaining(Native);
            set
            {
                if (Native != null) GameRaw.SetSkillRemaining(Native, value);
            }
        }

        /// <summary>表里的伤害公式按施法者当前属性算出的值（未计暴击、增伤、减伤，命中时游戏会再算）。</summary>
        public long FormulaDamage => Native == null ? 0 : GameRaw.SkillFormulaDamage(Native);

        public float Value(int i)
        {
            var v = Info?.Def.Values;
            return v != null && i >= 0 && i < v.Length ? v[i] : 0f;
        }

        /// <summary>技能生效时（每个客户端都会调用）。</summary>
        protected internal virtual void OnCast() { }

        /// <summary>持续期间每帧（每个客户端都会调用）。</summary>
        protected internal virtual void OnUpdate(float deltaTime) { }

        /// <summary>结束时（时间到、被游戏清除）。</summary>
        protected internal virtual void OnEnd() { }

        /// <summary>
        /// 对 center 半径 radius 内施法者的敌人造成伤害（默认 <see cref="FormulaDamage"/>），走游戏的伤害结算（暴击、增伤、吸血、飘字、命中事件）。
        /// 只在 <see cref="IsMine"/> 时生效，返回命中的数量。
        /// </summary>
        protected int DamageArea(Vector3 center, float radius, long? damage = null)
            => IsMine ? GameRaw.SkillDamageArea(Native, center, radius, damage) : 0;

        /// <summary>对一个目标造成伤害（同上），返回实际伤害。</summary>
        protected long Damage(Role target, long? damage = null)
            => IsMine && target != null ? GameRaw.SkillDamage(Native, target.Native, damage) : 0;

        /// <summary>
        /// 把施法者的动作换成 clip，直到技能结束（或调用 <see cref="StopAnimation"/>）。期间施法者停在施法状态，不能移动、普攻，
        /// 结束后回到待机。和游戏的旋风斩、鸡舞是同一种做法；每个装了 Mod 的客户端都会执行，所以别人也能看到。
        /// duration 为 null 时按片段长度 ÷ speed 延长技能的剩余时间（剩余时间本来更长时不变），传 0 表示不延长。
        /// clip 要是 Humanoid 片段（英雄模型都是 Humanoid，工坊模型也是），可以用 <see cref="ModBundle.Clip"/> 从 AssetBundle 读。
        /// 返回 false 表示没换成（施法者没有动画机等）。
        /// </summary>
        protected bool PlayAnimation(AnimationClip clip, float speed = 1f, float? duration = null)
        {
            if (Native == null || clip == null) return false;
            bool ok = false;
            Safe.Run($"{Info?.Key}.PlayAnimation({clip.name})", () => ok = GameRaw.PlaySkillAnimation(Native, clip, speed));
            if (!ok) return false;
            float time = duration ?? clip.length / Mathf.Max(0.01f, speed);
            if (time > Remaining) Remaining = time;
            return true;
        }

        /// <summary>
        /// 用游戏自带的动作片段（Resources/Bundles/Animator/ 下的名字），例如 Frank_RPG_2Hand_Skill01_WhirlWind（旋风斩）、
        /// GreatSword_Whirlwind_Loop_Root、core_main_riposte_01（处决）、ChickenDance、RasenganSkill、RachelSkill、yinduFlash、
        /// GreatSword_FocusEnergy_R_Attack03_Root、Frank_RPG_2Hand_Combo03_All、Frank_RPG_Dual_Skill04_InfinitySlash。
        /// </summary>
        protected bool PlayAnimation(string gameClip, float speed = 1f, float? duration = null)
            => PlayAnimation(GameRaw.GameAnimation(gameClip), speed, duration);

        /// <summary>提前把动作换回来（技能本身不结束）。</summary>
        protected void StopAnimation()
        {
            if (Native != null) Safe.Run($"{Info?.Key}.StopAnimation", () => GameRaw.StopSkillAnimation(Native));
        }

        /// <summary>
        /// 在 position 放一个游戏的特效（Resources 路径，不含 Bundles/，例如 Effect/SpellThunder、Effect/FireFlash），duration 秒后自动回收。
        /// 每个客户端各自生成，不需要同步。
        /// </summary>
        protected void Effect(string path, Vector3 position, float duration, float scale = 1f)
        {
            // Mod 的特效模板没了时不交给游戏（对象池会缓存空模板并在 Instantiate 时抛异常）
            if (ModAssets.IsOurs(path) && ModAssets.EffectFor(path) == null) return;
            Safe.Run($"{Info?.Key}.Effect({path})", () => GameRaw.SpawnEffect(path, position, duration, scale));
        }
    }

    /// <summary>自定义主动技能的注册表。</summary>
    /// <remarks>
    /// 联机：技能编号随游戏的释放消息同步，装了同一个 Mod 的人能看到效果；没装的人只看到施法动作（游戏会打一条"技能表缺少"的日志，不会出错）。
    /// 同步给队友、上传国王对决、镜像 Boss 的构筑数据会去掉自定义技能。
    /// </remarks>
    public static class ActiveSkills
    {
        internal const int FirstId = 2000000;

        private static readonly Dictionary<int, ActiveSkillInfo> registered = new Dictionary<int, ActiveSkillInfo>();
        // 技能实例 → 行为对象（实例里只存编号，见 PassiveSkills 的说明）
        private static readonly Dictionary<int, ActiveSkill> behaviours = new Dictionary<int, ActiveSkill>();
        private static int nextInstance = 1;
        private static bool hooked;
        private static float nextCheck;

        /// <summary>相关补丁都装上了。为 false 时注册不会生效。</summary>
        public static bool Available { get; internal set; }

        public static IReadOnlyCollection<ActiveSkillInfo> All => registered.Values;

        public static ActiveSkillInfo Get(int id) => registered.TryGetValue(id, out var info) ? info : null;

        internal static bool IsCustom(int id) => registered.ContainsKey(id);

        internal static bool IsCustom(string id) => int.TryParse(id, out int n) && registered.ContainsKey(n);

        internal static ActiveSkillInfo Register(string owner, ModText text, ActiveSkillDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (string.IsNullOrEmpty(def.Key) || !def.Key.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new ArgumentException($"技能的 Key 只能用字母、数字、下划线：\"{def.Key}\"");
            if (def.Create == null) throw new ArgumentException($"技能 {def.Key} 没有设置 Create");
            if (registered.Values.Any(r => r.Owner == owner && r.Key == def.Key))
                throw new ArgumentException($"技能 {def.Key} 已经注册过");

            int id = SkillIds.Allocate(owner, def.Key, FirstId, registered.ContainsKey);
            var info = new ActiveSkillInfo(id, owner, def, text);
            registered[id] = info;
            EnsureHooked();
            if (GameData.Ready) Safe.Run($"注册技能 {def.Key}", () => GameRaw.InjectSkill(info));
            RunLog.WriteCore(ModKitPlugin.Name, "技能", RunLogLevel.Info, "注册", new (string, object)[] { ("Mod", owner), ("键", def.Key), ("编号", id) }, mirror: false);
            return info;
        }

        internal static void Unregister(ActiveSkillInfo info)
        {
            if (info == null || !registered.Remove(info.Id)) return;
            if (!ModKit.Compatible) return;
            Safe.Run($"移除技能 {info.Key}", () => GameRaw.RemoveLocalSkills(info.Id));
            Safe.Run($"注销技能 {info.Key}", () => GameRaw.EjectSkill(info.Id));
        }

        /// <summary>直接给本地玩家这个技能（联机时 <see cref="Multiplayer.GameplayAllowed"/> 为 false 就不给）。技能栏满了时：replaceSlot 为技能格序号（0 起）就替换那一格，为 -1 时不给、返回 false。</summary>
        public static bool Give(ActiveSkillInfo info, int replaceSlot = -1)
        {
            if (info == null || !registered.ContainsKey(info.Id) || !Available || Game.LocalPlayer == null || !Multiplayer.GameplayAllowed) return false;
            return GameRaw.GiveSkill(info.Id, replaceSlot);
        }

        private static void EnsureHooked()
        {
            if (hooked) return;
            hooked = true;
            ModEvents.Update += Tick;
            Multiplayer.Changed += () =>
            {
                if (!GameData.Ready) return;
                foreach (var info in registered.Values)
                    Safe.Run($"注册技能 {info.Key}", () => GameRaw.InjectSkill(info));
            };
            ModEvents.Update += () =>
            {
                if (GameRaw.HasStrayAnimations) Safe.Run("施法动作（善后）", GameRaw.FixStrayAnimations);
            };
        }

        // 表加载晚于 Mod 注册；游戏重建配置表后要补回来
        private static void Tick()
        {
            if (registered.Count == 0 || Time.unscaledTime < nextCheck || !GameData.Ready) return;
            nextCheck = Time.unscaledTime + 1f;
            foreach (var info in registered.Values)
                if (!GameRaw.SkillInjected(info.Id))
                    Safe.Run($"注册技能 {info.Key}", () => GameRaw.InjectSkill(info));
        }

        // ---- 由游戏侧的 ModSkill 调用 ----

        internal static int Cast(int id, object native, Vector3 position, float rotation, Component target, float radius)
        {
            int instance = nextInstance++;
            if (!registered.TryGetValue(id, out var info)) return instance;
            ActiveSkill b = null;
            Safe.Run($"{info.Key}.Create", () => b = info.Def.Create());
            if (b == null) return instance;
            b.Info = info;
            b.Native = native;
            b.Position = position;
            b.Rotation = rotation;
            b.Target = Role.Wrap(target);
            b.Radius = radius;
            behaviours[instance] = b;
            Safe.Run($"{info.Key}.OnCast", b.OnCast);
            return instance;
        }

        internal static void Update(int instance, float deltaTime)
        {
            if (!behaviours.TryGetValue(instance, out var b)) return;
            // 每帧调用，不用 Safe.Run（避免每帧拼字符串）
            try
            {
                b.OnUpdate(deltaTime);
            }
            catch (Exception e)
            {
                Safe.Report(b.Info.Key + ".OnUpdate", e);
            }
        }

        internal static void End(int instance)
        {
            if (!behaviours.TryGetValue(instance, out var b)) return;
            behaviours.Remove(instance);
            Safe.Run($"{b.Info.Key}.OnEnd", b.OnEnd);
            b.Native = null;
        }

        internal static string Text(int id, bool description)
            => registered.TryGetValue(id, out var info) ? description ? info.Description : info.Name : null;
    }

    internal static class SkillIds
    {
        private const int Range = 1000000;

        // 由 GUID 和 Key 算出固定编号（FNV-1a），冲突时往后找
        public static int Allocate(string owner, string key, int first, Func<int, bool> taken)
        {
            uint hash = 2166136261;
            foreach (char c in owner + ":" + key)
                hash = (hash ^ c) * 16777619;
            int id = first + (int)(hash % Range);
            while (taken(id)) id = id + 1 >= first + Range ? first : id + 1;
            return id;
        }
    }
}
