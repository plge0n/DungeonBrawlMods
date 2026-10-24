using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DBModKit
{
    /// <summary>游戏全局状态。</summary>
    public static class Game
    {
        /// <summary>本地玩家；不在战斗中时为 null。</summary>
        public static Player LocalPlayer => ModKit.Compatible ? Player.Wrap(GameRaw.LocalPlayer()) : null;

        public static bool InBattle => LocalPlayer != null;

        /// <summary>
        /// 房间里只有自己：没联机，或者自己当主机且没有别人连进来。连到别人的房间时为 false。
        /// 改数值、作弊类功能应该只在单人时生效（游戏不检查 Mod，伤害由各自客户端计算，改了会影响队友）。
        /// </summary>
        public static bool IsSolo
        {
            get
            {
                if (!ModKit.Compatible) return false;
                if (GameRaw.ServerActive()) return GameRaw.ServerConnections() <= 1;
                return !GameRaw.ClientActive();
            }
        }

        /// <summary>当前是第几波，从 1 开始，和游戏界面显示的一致（不在战斗中时是上一局的值）。</summary>
        public static int Wave => ModKit.Compatible ? GameRaw.Wave() + 1 : 0;

        /// <summary>本局的总波数（关卡配置里的波次数；不在战斗中时是上一局的值，读不到为 0）。</summary>
        public static int TotalWaves => ModKit.Compatible ? GameRaw.TotalWaves() : 0;

        /// <summary>
        /// 进入哪些波次（从 1 开始，和 <see cref="Wave"/> 一致）前的休息阶段会弹遗物三选一。
        /// 休息阶段触发 <see cref="GameEventKind.WaveEnded"/> 时，<see cref="Wave"/> 已经是下一波的编号。
        /// </summary>
        public static List<int> RelicWaves
        {
            get
            {
                var list = new List<int>();
                if (!ModKit.Compatible) return list;
                int total = GameRaw.TotalWaves();
                for (int i = 0; i < total; i++)
                    if (GameRaw.RelicBeforeWave(i)) list.Add(i + 1);
                return list;
            }
        }

        /// <summary>自己是房主（联机主机，或者单人）。</summary>
        public static bool IsHost => ModKit.Compatible && (GameRaw.ServerActive() || !GameRaw.ClientActive());

        /// <summary>房间里的玩家数（含自己）。只有主机知道准确人数；连到别人的房间时返回 -1。</summary>
        public static int PlayerCount
        {
            get
            {
                if (!ModKit.Compatible) return 1;
                if (GameRaw.ServerActive()) return Mathf.Max(1, GameRaw.ServerConnections());
                return GameRaw.ClientActive() ? -1 : 1;
            }
        }

        /// <summary>场上所有玩家（含自己）。会遍历场景，不要每帧调用。</summary>
        public static List<Player> Players()
        {
            var result = new List<Player>();
            if (!ModKit.Compatible) return result;
            foreach (var p in GameRaw.AllPlayers())
            {
                var player = Player.Wrap(p);
                if (player != null) result.Add(player);
            }
            return result;
        }

        /// <summary>场上除了自己以外的玩家（不含国王对决的国王）。会遍历场景，不要每帧调用。</summary>
        public static List<Player> Teammates()
        {
            var result = Players();
            result.RemoveAll(p => p.IsLocal || GameRaw.IsKing(p.Native));
            return result;
        }

        /// <summary>游戏设置里的"自动战斗"开着。</summary>
        public static bool AutoBattle => ModKit.Compatible && GameRaw.AutoBattleOn();

        /// <summary>取游戏本地化文本，找不到时返回 fallback。</summary>
        public static string Localize(string key, string fallback = null)
            => ModKit.Compatible ? GameRaw.Localize(key, fallback ?? key) : fallback ?? key;
    }

    /// <summary>场上的角色（玩家、敌人、召唤物）。同一个游戏对象总是对应同一个 Role 实例，可以直接比较。</summary>
    public class Role
    {
        private static readonly ConditionalWeakTable<Component, Role> cache = new ConditionalWeakTable<Component, Role>();

        internal readonly Component Native;

        internal Role(Component native) => Native = native;

        internal static Role Wrap(Component native)
        {
            if (native == null) return null;
            return cache.GetValue(native, n => n is bbe ? new Player(n) : new Role(n));
        }

        /// <summary>游戏原始对象（混淆类型），仅供调试；不要依赖它的成员名。</summary>
        public Component Raw => Native;

        public long Hp => GameRaw.Hp(Native);
        public long MaxHp => GameRaw.MaxHp(Native);
        public float HpPercent => (float)Hp / Mathf.Max(1L, MaxHp);
        /// <summary>存活且在场。对象被销毁后返回 false。</summary>
        public bool IsAlive => Native != null && GameRaw.IsAlive(Native);
        public Vector3 Position => GameRaw.Position(Native);
        /// <summary>体型半径：判断距离时游戏会把它加到范围上。</summary>
        public float HitRadius => GameRaw.HitRadius(Native);
        public bool IsBoss => GameRaw.IsBoss(Native);
        public bool IsElite => GameRaw.IsElite(Native);
        /// <summary>召唤物（玩家召唤出来的单位）。</summary>
        public bool IsSummon => GameRaw.IsSummon(Native);

        /// <summary>联机时属于队友的怪（每个玩家有自己的怪，打别人的怪收益归动手的人）。</summary>
        public bool IsTeammateMonster => GameRaw.LocalPlayer() is Component me && GameRaw.IsTeammateMonster(me, Native);

        /// <summary>是 owner 的召唤物。</summary>
        public bool IsSummonOf(Player owner)
            => owner != null && IsSummon && GameRaw.AuthorityId(Native) == GameRaw.NetId(owner.Native);
        public long Shield => GameRaw.Shield(Native);
        /// <summary>血量 + 护盾：伤害要达到这个值才能击杀。</summary>
        public long EffectiveHp => Hp + Shield;
        /// <summary>无敌中，受到的伤害记为 0。</summary>
        public bool IsInvulnerable => GameRaw.IsInvulnerable(Native);
        /// <summary>普攻距离；对方在 距离 ≤ AttackRange + 对方体型半径 内时可以被普攻打到。</summary>
        public float AttackRange => GameRaw.AttackRange(Native);
        public float MoveSpeed => GameRaw.MoveSpeed(Native);
        /// <summary>当前状态（RoleState 枚举名）：Idle、Run、Attack、Skill、Skill2…、Dead 等。</summary>
        public string State => GameRaw.State(Native);
        public bool IsAttacking => GameRaw.IsAttacking(Native);
        /// <summary>在对方的普攻范围内。</summary>
        public bool InAttackRangeOf(Role attacker, float extra = 0f)
            => attacker.DistanceTo(this) <= attacker.AttackRange + HitRadius + extra;

        // ---- 属性（含装备、遗物、卡牌等加成后的当前值）----
        public long Attack => GameRaw.Attack(Native);
        public int Strength => GameRaw.Strength(Native);
        public int Agility => GameRaw.Agility(Native);
        public int Stamina => GameRaw.Stamina(Native);
        /// <summary>每秒攻击次数。</summary>
        public float AttackSpeed => GameRaw.AttackSpeed(Native);
        /// <summary>暴击率，0~1。</summary>
        public float CriticalRate => GameRaw.CriticalRate(Native);
        public float CriticalDamage => GameRaw.CriticalDamage(Native);
        public int Armor => GameRaw.Armor(Native);
        public int Dodge => GameRaw.Dodge(Native);
        public float LifeSteal => GameRaw.LifeSteal(Native);

        /// <summary>水平距离（忽略高度）。</summary>
        public float DistanceTo(Role other) => GameRaw.Distance(Native, other.Position);

        /// <summary>到某个位置的水平距离（忽略高度）。</summary>
        public float DistanceTo(Vector3 point) => GameRaw.Distance(Native, point);

        /// <summary>与游戏判定一致：range ≤ 0 表示不限距离，否则 距离 ≤ range + 对方体型半径。</summary>
        public bool IsWithin(Role other, float range)
            => range <= 0f || DistanceTo(other) <= range + other.HitRadius;
    }

    /// <summary>玩家角色。</summary>
    public sealed class Player : Role
    {
        internal Player(Component native) : base(native) { }

        internal new static Player Wrap(Component native) => Role.Wrap(native) as Player;

        /// <summary>联机编号（Mirror netId）：同一局里每台电脑看到的都一样，可以用来对应别人发来的数据。</summary>
        public uint NetId => GameRaw.NetId(Native);
        /// <summary>玩家名（Steam 昵称）；局域网模式下可能为空。</summary>
        public string Name => GameRaw.PlayerName(Native);
        /// <summary>英雄编号（HeroType）。</summary>
        public int HeroId => GameRaw.HeroIdOf(Native);
        /// <summary>英雄名（本地化，不受工坊模型影响）。</summary>
        public string HeroName => Game.Localize("hero_" + HeroId, "英雄 " + HeroId);

        public int Mana => GameRaw.Mana(Native);
        public int MaxMana => GameRaw.MaxMana(Native);
        public int Gold => GameRaw.Gold(Native);

        /// <summary>是本地玩家（只有本地玩家能用下面的修改方法）。</summary>
        public bool IsLocal => Native != null && Native == GameRaw.LocalPlayer();

        // 修改都走游戏自己的方法：金币等数值有防篡改校验，直接写字段会被当成作弊上报
        /// <summary>回血（会同步给服务器）。</summary>
        public void Heal(double amount)
        {
            RequireLocal();
            if (amount <= 0 || Hp >= MaxHp) return;
            long before = Hp;
            GameRaw.Heal(Native, amount);
            // 单人（主机）时立刻生效；在别人的房间里要等服务器回传，"之后"可能还是旧值
            RunLog.Info(RunLog.CallerMod(), "玩家", "回血", ("请求", Math.Round(amount)), ("生命", $"{before}→{Hp}/{MaxHp}"));
        }

        /// <summary>加蓝（负数为扣蓝），结果限制在 0~上限。</summary>
        public void AddMana(int amount)
        {
            RequireLocal();
            int before = Mana;
            if (amount == 0 || amount > 0 && before >= MaxMana || amount < 0 && before <= 0) return;
            GameRaw.AddMana(Native, amount);
            RunLog.Info(RunLog.CallerMod(), "玩家", amount >= 0 ? "加蓝" : "扣蓝", ("请求", amount), ("蓝", $"{before}→{Mana}/{MaxMana}"));
        }

        /// <summary>加金币（不吃金币加成和关卡倍率），返回实际到账数量。</summary>
        public int AddGold(int amount)
        {
            RequireLocal();
            if (amount <= 0) return 0;
            int before = Gold;
            int got = GameRaw.AddGold(Native, amount);
            RunLog.Info(RunLog.CallerMod(), "玩家", "加金币", ("请求", amount), ("到账", got), ("金币", $"{before}→{Gold}"));
            return got;
        }

        /// <summary>获得一个遗物（ItemType 编号，见 RelicInfo）。超过本局获取上限等情况下返回 false。</summary>
        public bool GiveRelic(int relicId)
        {
            RequireLocal();
            bool ok = GameRaw.GiveRelic(Native, relicId);
            RunLog.Write(RunLog.CallerMod(), "玩家", ok ? RunLogLevel.Info : RunLogLevel.Warning, ok ? "获得遗物" : "遗物没有发出（可能超过本局获取上限）",
                ("遗物", RelicInfo.Get(relicId)?.Name ?? relicId.ToString()), ("编号", relicId));
            return ok;
        }

        /// <summary>
        /// 加力量、敏捷、体力、攻击（负数为减），加在加成上而不是基础值上，不会触发防篡改校验；体力会同步生命上限。
        /// 本局有效，下一局英雄重新生成时清零。要临时加成就到时间后用相反数调用一次。
        /// </summary>
        public void AddStats(int strength, int agility, int stamina, long attack)
        {
            RequireLocal();
            string before = $"{Strength}/{Agility}/{Stamina} 攻击 {Attack}";
            GameRaw.AddStats(Native, strength, agility, stamina, attack);
            RunLog.Info(RunLog.CallerMod(), "玩家", "改属性", ("力/敏/体/攻", $"{strength:+0;-0;0}/{agility:+0;-0;0}/{stamina:+0;-0;0}/{attack:+0;-0;0}"),
                ("之前", before), ("之后", $"{Strength}/{Agility}/{Stamina} 攻击 {Attack}"));
        }

        /// <summary>
        /// 按游戏的属性键加成（遗物、卡牌、锻体用的同一套，负数撤销）。本局有效，不会自动撤销，临时加成要自己到时间用相反数再调一次。
        /// 整数键：Str、Agi、Sta、Three（三围）、Attack、HP、MP、Armor、HPSec、MPSec、Luck、XiXue、CoolDown（技能急速，冷却 ×1/(1+急速/100)）、
        /// ReduceInjury、ExtraDamage、Doge、MoveSpeed、SkillHit；
        /// 百分比键（25 = 25%）：AttackSpeed、Critical、CriticalDamage、XiXueRate、NormalAdd、SkillAdd、AddDamage（总伤害）、HpPercent、HpSecRate、
        /// NormalBreak、SkillBreak、AllBreakShield、ExpAdd、SummonAdd、HenshinAdd、HaloAdd、ArmedAdd、GoldAdd。
        /// 加力量、敏捷、体力会改基础值（游戏的官方方法，会过防篡改校验）；只想要可撤销的三围加成用 <see cref="AddStats"/>。
        /// </summary>
        public void AddAttribute(string key, int amount)
        {
            RequireLocal();
            if (amount == 0 || string.IsNullOrEmpty(key)) return;
            GameRaw.AddAttribute(key, amount);
            RunLog.Info(RunLog.CallerMod(), "玩家", "加属性", ("键", key), ("数值", amount));
        }

        /// <summary>多重攻击数（1 = 只打一个目标）。</summary>
        public int AttackCount => GameRaw.AttackNum(Native);

        /// <summary>多重攻击 +n（负数撤销），和多重攻击被动一样会同步。本局有效，不会自动撤销。</summary>
        public void AddAttackCount(int n)
        {
            RequireLocal();
            if (n == 0) return;
            GameRaw.AddAttackNum(Native, n);
            RunLog.Info(RunLog.CallerMod(), "玩家", "多重攻击", ("加", n), ("现在", AttackCount));
        }

        /// <summary>在地上掉道具（游戏的 ItemType 编号，例如 1407 痊愈神符、1501 蟹黄堡），走游戏自己的生成，玩家照常拾取。</summary>
        public void DropItem(int itemType, Vector3 position, int count = 1)
        {
            RequireLocal();
            GameRaw.DropItem(Native, itemType, position, count);
            RunLog.Debug(RunLog.CallerMod(), "玩家", "掉道具", ("道具", itemType), ("数量", count));
        }

        /// <summary>
        /// 以 center 为圆心对范围内的敌人各造成一次技能伤害（暴击照常判定，算本地玩家打的），并播放特效（游戏的特效路径，例如 "Effect/DeathExplosion"）。
        /// effect 传 null 不播特效（频繁调用时用，否则每次都广播一个特效）。
        /// </summary>
        public void DamageArea(float damage, Vector3 center, float radius, string effect = "Effect/DeathExplosion", float effectScale = 1f)
        {
            RequireLocal();
            if (damage <= 0f || radius <= 0f) return;
            GameRaw.DamageArea(Native, damage, center, radius, effect, effectScale);
            RunLog.Debug(RunLog.CallerMod(), "玩家", "范围伤害", ("伤害", Mathf.Round(damage)), ("半径", radius));
        }

        /// <summary>
        /// 同 <see cref="DamageArea(float, Vector3, float, string, float)"/>，spareTeammates 为 true 时不打队友的怪。
        /// 联机时每个玩家有自己的怪，打别人的怪收益归动手的人；在队友身边放的范围伤害应该跳过队友的怪，免得抢他的收益。
        /// </summary>
        public void DamageArea(float damage, Vector3 center, float radius, string effect, float effectScale, bool spareTeammates)
        {
            RequireLocal();
            if (damage <= 0f || radius <= 0f) return;
            GameRaw.DamageArea(Native, damage, center, radius, effect, effectScale, spareTeammates);
            RunLog.Debug(RunLog.CallerMod(), "玩家", "范围伤害", ("伤害", Mathf.Round(damage)), ("半径", radius), ("跳过队友的怪", spareTeammates));
        }

        /// <summary>在某个位置播放游戏的特效（例如 "Effect/DeathExplosion"），所有玩家都看得到。不要每帧调用。</summary>
        public void PlayEffect(string effect, Vector3 position, float scale = 1f)
        {
            RequireLocal();
            if (string.IsNullOrEmpty(effect)) return;
            GameRaw.PlayEffect(Native, effect, position, scale);
        }

        /// <summary>
        /// 治疗另一个玩家（队友），算自己治疗的。走游戏原版的治疗同步，队友没装 Mod 也会回血。不能超过对方的生命上限。
        /// </summary>
        public void HealAlly(Player ally, double amount)
        {
            RequireLocal();
            if (ally == null || ally == this || amount <= 0 || !ally.IsAlive || ally.Hp >= ally.MaxHp) return;
            GameRaw.HealOther(Native, ally.Native, amount);
            RunLog.Debug(RunLog.CallerMod(), "队友", "治疗", ("队友", ally.Name), ("量", Math.Round(amount)));
        }

        /// <summary>
        /// 自己周围 radius 米内的所有玩家（含自己）各加一个护盾：各自生命上限 × ratio。游戏装备"人性"系列的原版效果，由服务器执行，
        /// 范围最多 50、比例最多 1。
        /// </summary>
        public void ShieldNearbyPlayers(float radius, float ratio)
        {
            RequireLocal();
            if (radius <= 0f || ratio <= 0f) return;
            GameRaw.ShieldNearbyPlayers(Native, Mathf.Min(radius, 50f), Mathf.Min(ratio, 1f));
            RunLog.Debug(RunLog.CallerMod(), "队友", "范围护盾", ("半径", radius), ("比例", ratio));
        }

        /// <summary>
        /// 给一个玩家（可以是队友，也可以是自己）上游戏原版的"圣焰祝福"：伤害 +damageBonus（0.2 = +20%）、护甲 +armor，持续 seconds 秒；
        /// 重复上时取较大的值并刷新时长。走原版 buff 同步，队友没装 Mod 也生效。
        /// </summary>
        public void BlessPlayer(Player target, float damageBonus, float seconds, int armor = 0)
        {
            RequireLocal();
            if (target == null || !target.IsAlive || seconds <= 0f) return;
            GameRaw.BlessPlayer(Native, target.Native, Mathf.Max(0f, damageBonus), seconds, Mathf.Max(0, armor));
            RunLog.Debug(RunLog.CallerMod(), "队友", "祝福", ("对象", target.Name), ("增伤", damageBonus), ("护甲", armor), ("秒", seconds));
        }

        /// <summary>身上所有的主动技能（专属技能、技能书学到的、自定义的），不含被动。</summary>
        public List<SkillSlot> ActiveSkills()
        {
            var result = new List<SkillSlot>();
            foreach (var s in GameRaw.ActiveSkillsOf(Native)) result.Add(SkillSlot.Wrap(s));
            return result;
        }

        /// <summary>这个英雄的专属技能。</summary>
        public HeroSkill HeroSkill => HeroSkill.Of(HeroId);

        /// <summary>
        /// 身上的专属主动技能（被动英雄、或者还没发放时为 null）。
        /// 篮球巨星拿到"鸡舞"卡牌后，游戏会把花式扣篮换成真·鸡舞，这里返回换过之后的那个。
        /// </summary>
        public SkillSlot HeroSkillSlot
        {
            get
            {
                var hs = HeroSkill;
                if (hs == null || !hs.IsActive) return null;
                return SkillSlot.Wrap(GameRaw.FindActiveSkill(Native, hs.SkillId) ?? GameRaw.FindReplacedSkill(Native, hs.SkillId));
            }
        }

        /// <summary>身上的某个主动技能（不在技能栏里的也算）。</summary>
        public SkillSlot FindSkill(int skillId) => SkillSlot.Wrap(GameRaw.FindActiveSkill(Native, skillId));

        /// <summary>
        /// 免费再放一次技能（游戏的"多重施法"同款：不查蓝、不扣蓝、不进冷却），参数照抄一次释放。
        /// 会再次触发 <see cref="Combat.Cast"/>（IsFree = true），处理时注意不要无限连锁。返回游戏是否放了出来。
        /// </summary>
        public bool FreeCast(SkillCast cast)
        {
            RequireLocal();
            bool ok = GameRaw.FreeCast(Native, cast.SkillId, cast.Position, cast.Rotation, cast.TargetId, cast.BookId);
            RunLog.Debug(RunLog.CallerMod(), "玩家", "免费释放", ("技能", SkillInfo.Get(cast.SkillId)?.Name ?? cast.SkillId.ToString()), ("成功", ok));
            return ok;
        }

        /// <summary>加攻速（比例，0.25 = +25%；负数撤销）。本局有效，临时加成要自己到时间撤销。</summary>
        public void AddAttackSpeed(float ratio)
        {
            RequireLocal();
            GameRaw.AddAttackSpeed(Native, ratio);
            RunLog.Info(RunLog.CallerMod(), "玩家", "改攻速", ("加成", $"{ratio:+0.##;-0.##}"), ("攻速", AttackSpeed));
        }

        /// <summary>加护甲（负数撤销）。本局有效，临时加成要自己到时间撤销。</summary>
        public void AddArmor(int amount)
        {
            RequireLocal();
            int before = Armor;
            GameRaw.AddArmor(Native, amount);
            RunLog.Info(RunLog.CallerMod(), "玩家", "改护甲", ("加成", amount), ("护甲", $"{before}→{Armor}"));
        }

        /// <summary>无敌若干秒（游戏的无敌药水效果，重复使用时刷新时长）。</summary>
        public void GiveInvulnerable(float seconds)
        {
            RequireLocal();
            GameRaw.GiveInvulnerable(Native, seconds);
            RunLog.Info(RunLog.CallerMod(), "玩家", "无敌", ("秒", seconds));
        }

        /// <summary>身上有这个被动技能（被动表编号）。</summary>
        public bool HasPassive(int passiveId) => GameRaw.FindPassiveSkill(Native, passiveId) != null;

        /// <summary>被动技能当前的数值（技能实例上的，可能已被改过）；没有这个被动时为 null。</summary>
        public float[] PassiveValues(int passiveId)
        {
            var s = GameRaw.FindPassiveSkill(Native, passiveId);
            return s == null ? null : (float[])GameRaw.PassiveValues(s)?.Clone();
        }

        /// <summary>
        /// 改被动技能的数值（只改本地玩家身上这个技能实例，不动配置表）。各项含义见 NOTES.md 的"专属技能强化"。
        /// 本局有效，下一局重新发放时恢复；要撤销就把 <see cref="HeroSkill.PassiveValues"/> 写回来。返回是否找到了这个被动。
        /// </summary>
        public bool SetPassiveValues(int passiveId, IReadOnlyList<float> values)
        {
            RequireLocal();
            var s = GameRaw.FindPassiveSkill(Native, passiveId);
            if (s == null) return false;
            var before = GameRaw.PassiveValues(s);
            var arr = new float[values.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = values[i];
            GameRaw.SetPassiveValues(s, arr);
            RunLog.Info(RunLog.CallerMod(), "玩家", "改被动数值", ("被动", passiveId),
                ("之前", before == null ? "" : string.Join("|", before)), ("之后", string.Join("|", arr)));
            return true;
        }

        private void RequireLocal()
        {
            if (!IsLocal) throw new InvalidOperationException("只能修改本地玩家");
        }

        /// <summary>变身中（变身类技能生效期间）。</summary>
        public bool IsTransformed => GameRaw.IsTransformed(Native);

        /// <summary>普攻冷却好了。</summary>
        public bool AttackReady => GameRaw.AttackTimer(Native) > GameRaw.AttackInterval(Native);
        /// <summary>距离下次能普攻还有多少秒。</summary>
        public float AttackCooldownLeft => Mathf.Max(0f, GameRaw.AttackInterval(Native) - GameRaw.AttackTimer(Native));
        public float AttackInterval => GameRaw.AttackInterval(Native);
        /// <summary>本次普攻已经出手（伤害或弹道已经发出）。之后移动不会丢掉这次攻击；出手前移动会取消攻击，但冷却会立刻转好。</summary>
        public bool AttackHitDone => GameRaw.AttackHitDone(Native);

        /// <summary>
        /// 自己放的某个技能还在生效的剩余秒数（持续伤害、增益、变身等），没有则为 0。
        /// 瞬发技能的实例也会短暂存在，判断"增益还在"时最好留一点阈值。
        /// </summary>
        public float SkillEffectRemaining(int skillId) => GameRaw.SkillEffectRemaining(Native, skillId);

        /// <summary>对该敌人的增伤比例（全局增伤 + 对 Boss/精英/普通敌人的增伤），0.3 表示 +30%。</summary>
        public float DamageBonusAgainst(Role target) => GameRaw.DamageBonus(Native, target.Native);

        /// <summary>当前所有存活的敌人。</summary>
        public List<Role> Enemies()
        {
            var result = new List<Role>();
            foreach (var e in GameRaw.Enemies(Native))
            {
                var role = Role.Wrap(e);
                if (role != null && role.IsAlive) result.Add(role);
            }
            return result;
        }

        /// <summary>范围内存活的敌人（range ≤ 0 表示不限距离）。</summary>
        public List<Role> EnemiesWithin(float range)
        {
            var result = Enemies();
            result.RemoveAll(e => !IsWithin(e, range));
            return result;
        }

        /// <summary>按评分选范围内的敌人，分数越低越优先；没有敌人时返回 null。</summary>
        public Role PickEnemy(float range, Func<Role, float> score)
        {
            Role best = null;
            float bestScore = float.MaxValue;
            foreach (var e in EnemiesWithin(range))
            {
                float s = score(e);
                if (s < bestScore)
                {
                    best = e;
                    bestScore = s;
                }
            }
            return best;
        }
    }
}
