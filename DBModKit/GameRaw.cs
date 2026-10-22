using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DBModKit
{
    // 游戏混淆名只出现在这个文件（以及 Compat 的检查清单和 Hooks 的补丁签名）。
    // 游戏更新后用 tools/Remap 自动改成新名字（见 README"游戏更新后"），Mod 无需改动。
    // 只在 ModKit.Compatible 时调用；每个方法单独 JIT，类型缺失时不会提前崩溃。
    internal static class GameRaw
    {
        // 本地玩家 nn.bpbu（bbe : yb）
        public static Component LocalPlayer() => nn.bpbu;

        // cj.boyi.dff：找不到 key 时返回默认值，不打警告
        public static string Localize(string key, string fallback) => cj.boyi.dff(key, fallback);
        // cl.boyu：游戏设置里的语言（LanguageType）。不在兼容检查清单里：读不到时 Localization 退回中文，不必停用整个 ModKit
        public static int LanguageIndex() => (int)cj.boyi.boyu;

        // ---- 角色 yb ----
        public static long Hp(Component r) => ((yb)r).hp;
        public static long MaxHp(Component r) => ((yb)r).maxHp;
        public static bool IsAlive(Component r) => r != null && ((yb)r).bphx != null && ((yb)r).iyn();
        public static Vector3 Position(Component r) => ((yb)r).bphx.position;
        // 水平距离
        public static float Distance(Component from, Vector3 to) => ((yb)from).ivj(to);
        // 体型补偿：大体型敌人判定距离更远
        public static float HitRadius(Component r) => ((yb)r).bpia != null ? ((yb)r).bpia.addRange : 0f;
        public static bool IsBoss(Component r) => r is xo e && e.isBoss;
        public static bool IsElite(Component r) => r is xo e && e.isElite;
        // 召唤物 roleType = Summon，authorityId = 召唤者的 netId（xo.hkr）
        public static bool IsSummon(Component r) => ((yb)r).roleType == RoleType.Summon;
        public static uint AuthorityId(Component r) => ((yb)r).authorityId;
        public static List<yb> Enemies(Component r) => ((yb)r).izk();
        // 护盾 bpgz；无敌 beqd（受到的伤害记为 0）
        public static long Shield(Component r) => ((yb)r).bpgz;
        public static bool IsInvulnerable(Component r) => ((yb)r).beqd;
        // 普攻距离 roleModeBase.ico()（判定时还要加上对方体型半径）
        public static float AttackRange(Component r) => ((yb)r).bpia != null ? ((yb)r).bpia.ico() : 0f;
        public static float MoveSpeed(Component r) => ((yb)r).ivk();
        public static string State(Component r) => ((yb)r).bphy.ToString();
        // 变身中 henShinSkillId > 0（默认 -1）
        public static bool IsTransformed(Component r) => ((yb)r).henShinSkillId > 0;

        // ---- 普攻节奏：yb.timer 在开始攻击时清零，> iyi() 时可以再攻击；攻击动作长 iyj()。
        // isCheckAttack 由近战/弹道模式在出手时置位；远程模式不置位，按动作进度 0.41 出手，这里用 0.5 兜底 ----
        public static float AttackTimer(Component r) => ((yb)r).timer;
        public static float AttackInterval(Component r) => ((yb)r).iyi();
        public static bool IsAttacking(Component r) => ((yb)r).bphy == RoleState.Attack;
        public static bool AttackHitDone(Component r)
        {
            var role = (yb)r;
            return role.isCheckAttack || role.timer / role.iyj() >= 0.5f;
        }

        // ---- 属性（yb，bbe 有部分重写）----
        public static long Attack(Component r) => ((yb)r).bphz;
        public static int Strength(Component r) => ((yb)r).bphq;
        public static int Agility(Component r) => ((yb)r).bphp;
        public static int Stamina(Component r) => ((yb)r).bpho;
        public static float AttackSpeed(Component r) => ((yb)r).bphb;
        // 暴击率是 0~1 的概率（Random.value < bphl）
        public static float CriticalRate(Component r) => ((yb)r).bphl;
        public static float CriticalDamage(Component r) => ((yb)r).bphm;
        public static int Armor(Component r) => ((yb)r).bphk;
        public static int Dodge(Component r) => ((yb)r).bphr;
        public static float LifeSteal(Component r) => ((yb)r).bphv;

        // ---- 玩家 bbe ----
        public static int Mana(Component p) => ((bbe)p).bfru;
        public static int MaxMana(Component p) => ((bbe)p).bfrv;
        public static int Gold(Component p) => ((bbe)p).k4a25de8c63fe4a7192613346ae51af4e;

        // 以下都是游戏自己加数值的方法：金币、力敏体等有影子副本校验（nn.fsx），不能直接写字段
        // yb.iwz(量, 来源)：回血，内部走 CmdUpdateHp 同步
        public static void Heal(Component r, double amount) => ((yb)r).iwz(amount, (yb)r);
        // bbe.ksd：加蓝（自动限制在 0~上限）
        public static void AddMana(Component p, int amount) => ((bbe)p).ksd(amount);
        // bbe.kfd63a19…(飘字位置, 数量, 吃金币加成, 吃关卡倍率, 允许负债, 来源)：返回实际到账数量
        public static int AddGold(Component p, int amount)
            => ((bbe)p).kfd63a19d353a42878846361e6af4f637(((bbe)p).iwh(), amount, c: false, d: false, e: false, GoldIncomeSource.Gameplay);

        // bbe.kkl(力, 敏, 体, 攻击)：加到加成字段 bfpj/bfpk/bfpl/bfpi（不是被防篡改校验的基础值），负数撤销；
        // 体力变化会自动同步生命上限。游戏的卡牌、装备也用它
        public static void AddStats(Component p, int str, int agi, int sta, long attack) => ((bbe)p).kkl(str, agi, sta, attack);

        // 当前波次，从 0 开始（游戏显示 nn.bbsr + 1）
        public static int Wave() => nn.bbsr;

        // ---- 本地玩家的战斗回调（多播委托字段，只在拥有者客户端触发）----
        // 只挂没有返回值、不带 ref 的回调：damageEvent 之类的返回值会替换伤害，多挂一个就会改掉游戏结果
        private sealed class PlayerBinding
        {
            public bbe Player;
            public yb.EnemyDefeated Defeated;
            public yb.ActiveSkillCastEvent SkillCast;
            public yb.Critical Critical;
            public yb.DogeEvent Dodge;
            public Action LevelUp;
            public Action Relife;
            public bbe.AddBagItem BagItem;
            public yb.LocalActiveSkillCastEvent LocalCast;
        }

        public static object BindPlayerEvents(Component player)
        {
            var p = (bbe)player;
            var b = new PlayerBinding
            {
                Player = p,
                Defeated = (killer, dead, ctx) =>
                {
                    if (dead is xo) GameEvents.Raise(new GameEvent(GameEventKind.EnemyKilled, Role.Wrap(dead), isBoss: ctx.IsBoss, isElite: ctx.IsElite));
                },
                SkillCast = (caster, skill) => GameEvents.Raise(new GameEvent(GameEventKind.SkillCast, skill: SkillInfo.Get((int)skill))),
                Critical = (hurt, damage, type) => GameEvents.Raise(new GameEvent(GameEventKind.Critical, Role.Wrap(hurt), amount: damage)),
                Dodge = () => GameEvents.Raise(new GameEvent(GameEventKind.Dodged)),
                LevelUp = () => GameEvents.Raise(new GameEvent(GameEventKind.LevelUp)),
                Relife = () => GameEvents.Raise(new GameEvent(GameEventKind.Revived)),
                BagItem = item => GameEvents.Raise(new GameEvent(GameEventKind.ItemPicked, itemId: (int)item)),
                // localActiveSkillCastEvent(玩家, 技能, 目标点, 朝向, 目标 netId, 技能书编号, 是否免费)：只在 ez.dyf 末尾触发（真正放出技能时），
                // 免费释放（dyf 的 h = true）也会触发
                LocalCast = (_, skill, pos, rot, target, book, free) => Combat.RaiseSkillCast(new SkillCast((int)skill, pos, rot, target, book, free)),
            };
            p.enemyDefeatedEvent += b.Defeated;
            p.activeSkillCastEvent += b.SkillCast;
            p.criticalEvent += b.Critical;
            p.dogeEvent += b.Dodge;
            p.onPlayerLevelUp += b.LevelUp;
            p.onPlayerRelife += b.Relife;
            p.addBagItemEvent += b.BagItem;
            p.localActiveSkillCastEvent += b.LocalCast;
            return b;
        }

        public static void UnbindPlayerEvents(object binding)
        {
            var b = (PlayerBinding)binding;
            var p = b.Player;
            if (ReferenceEquals(p, null)) return;
            p.enemyDefeatedEvent -= b.Defeated;
            p.activeSkillCastEvent -= b.SkillCast;
            p.criticalEvent -= b.Critical;
            p.dogeEvent -= b.Dodge;
            p.onPlayerLevelUp -= b.LevelUp;
            p.onPlayerRelife -= b.Relife;
            p.addBagItemEvent -= b.BagItem;
            p.localActiveSkillCastEvent -= b.LocalCast;
        }

        // ---- 遗物：bcry（ItemType → RemainsData），名字/图标在原始 remains 表和本地化 "pickitem_" + 编号 ----
        public static List<RelicInfo> ReadRelics()
        {
            var list = new List<RelicInfo>();
            foreach (var kv in cj.boyl.bcry.OrderBy(kv => (int)kv.Key))
            {
                int id = (int)kv.Key;
                string icon = null;
                if (cx.zyc.TryGetValue("remains", out var t) && t is Dictionary<string, object> rows
                    && rows.TryGetValue(id.ToString(), out var r) && r is Dictionary<string, object> row)
                    icon = Cell(row, "icon");
                list.Add(new RelicInfo(id, cj.boyi.dff("pickitem_" + id, kv.Key.ToString()), kv.Value.grade, IconPath("Remains/", icon)));
            }
            return list;
        }

        // bbe.kqk(编号, 等级)：游戏的遗物三选一（ez.dzv）就调它；超过本局获取上限、或王者模式禁用时返回 null
        public static bool GiveRelic(Component p, int id) => cj.boyl.bcry.ContainsKey((ItemType)id) && ((bbe)p).kqk(id, 0) != null;

        // ---- 掉落物：cj.boyq（ot）.bctu，ou 的 bcty = 编号、bctz = 所属玩家 netId（0 = 公共）、bcua = ItemType、bcuc = 位置 ----
        public static void ReadLoot(List<LootItem> output)
        {
            output.Clear();
            var items = cj.boyq?.bctu;
            if (items == null) return;
            foreach (var kv in items)
            {
                var o = kv.Value;
                if (o == null) continue;
                // ot.gdb：公共物品、自己的物品，或物主允许共享时能捡（服务器 bbe.lbf 用同样的判断）
                output.Add(new LootItem(o.bcty, (int)o.bcua, o.bcuc, ot.gdb(o), ez.eaz(o.bcua), ez.ebc(o.bcua)));
            }
        }

        public static string ItemName(int itemType) => cj.boyi.dff("pickitem_" + itemType, ((ItemType)itemType).ToString());

        // bio.Pick（点击单个物品）的判断：死了不能捡（nn.bbth 时除外）
        public static bool CanPickNow(Component p) => nn.bbth || (((yb)p).hp > 0 && ((yb)p).bphy != RoleState.Dead);

        // 背包 bba.bfnb，上限 6 格；金币、宝石、阳光、力量/敏捷/耐力之书、护符不占格子（ez.eaz；全属性之书、金币袋占格子）
        public static int BagCount(Component p) => ((bbe)p).playerAttribute.kgw();

        // 护符需按键拾取（设置项 IsKeyPickTalisman）；关掉时护符靠碰到自动拾取，按 C 也不捡
        public static bool KeyPickTalisman() => nn.bbtz;

        // CmdPickItem 由服务器 bbe.lbf 处理：只检查物主，不检查距离，距离要自己限制
        public static void PickItem(Component p, uint id) => ((bbe)p).CmdPickItem(id);

        public static void PlayPickSound() => bz.ddr<AudioManager>()?.daw("Audio/Battle_Audio/UI/拾取物品", 0.9f);

        // ---- 三选一界面 bqa（ui_roguelike）----
        // 和游戏自己弹遗物前的判断一致（MyServerNetworkManager.qhz）：三选一界面开着，或技能栏的子面板（bpe.bprr）开着；
        // 另外开场界面开着时也算忙（它一关游戏就弹开局遗物，抢在前面打开会被游戏随机选掉）
        public static bool ChoiceBusy()
        {
            var ui = cj.boyb?.dqr<bqa>();
            if (ui != null && ui.bacf) return true;
            var bar = cj.boyb?.dqr<bpe>();
            return (bar != null && bar.bprr) || OpeningPending();
        }

        // bqa.osj(选项, 选中回调, 标题, 刷新(null = 不能刷新), 关闭回调, 延迟, 队伍数据, 来源)：ez.dzu 用的真方法。
        // 来源传 null：非空时 cs.dfx 会把选项计入游戏的统计
        public static void ShowChoice(string title, IList<ChoiceOption> options, Action<int> onPick)
        {
            var data = new RoguelikeUIData[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                data[i] = new RoguelikeUIData
                {
                    name = o.Name ?? "",
                    dec = o.Description ?? "",
                    icon = o.IconPath ?? "",
                    quality = o.Quality >= 0 && o.Quality <= 4 ? o.Quality : -1,
                    data = i.ToString(),
                };
            }
            var ui = (bqa)cj.boyb.dqk<bqa>();
            ui.osj(data, d => onPick(int.TryParse(d.data, out int i) ? i : 0), title, null, null, 0f, null, null);
        }

        // ---- 联机（Mirror：ciy = NetworkServer，cik = NetworkClient）----
        public static bool ServerActive() => ciy.bmyj;
        public static bool ClientActive() => cik.bqcb;
        // 主机上的连接数，包含主机自己
        public static int ServerConnections() => ciy.bmyc.Count;

        // ---- 输入、UI ----
        // os.gba()：持有期间屏蔽移动、技能、Tab 商店、ESC 菜单（os.day 在计数非 0 时提前返回）
        public static IDisposable BlockInput() => os.gba();
        // 游戏 UI 的 Canvas（UIRoot 第 0 个子节点）；主菜单之前可能还没创建
        public static Canvas UiCanvas() => cj.boyb?.bact != null ? cj.boyb.bact.GetComponent<Canvas>() : null;

        // 普攻、技能共用的增伤，以及对 Boss / 精英 / 普通敌人的额外增伤（见 yb.iwk）
        public static float DamageBonus(Component p, Component target)
        {
            var player = (bbe)p;
            float bonus = player.addDamagePercent;
            if (target is xo e)
                bonus += e.isBoss ? player.addBossEnemy : e.isElite ? player.addEliteEnemy : player.addNormalEnemy;
            return bonus;
        }

        // ---- 全局开关 ----
        public static bool AutoBattleOn() => nn.bbtv;
        // nn.bbtm：角色被锁定（过场等），kqi 里直接站着不动
        public static bool ControlLocked() => nn.bbtm;
        public static bool IsKing(Component p) => ((yb)p).roleType == RoleType.King;
        // 场地边界（圆形/多边形/矩形）内且离边缘至少 margin
        public static bool InsideArena(Vector3 pos, float margin) => nn.fqr(pos, margin);
        // 玩家的 CharacterController（bbe.bfrl，公开访问器 bpmb），移动时被场景碰撞体挡住
        public static CharacterController Controller(Component p) => ((bbe)p).bpmb;
        // 碰撞体属于某个角色（玩家、敌人、召唤物），不算障碍物
        public static bool IsRoleCollider(Collider c) => c.GetComponentInParent<yb>() != null;

        // ---- 技能伤害估算（未计暴击、减伤、各种事件），各技能的实际公式见 NOTES.md ----
        public static long? BaseSkillDamage(int id, Component p, Component target)
        {
            var skill = (ActiveSkillEnum)id;
            if (!cj.boyl.bcrw.TryGetValue(skill, out var d)) return null;
            var player = (bbe)p;
            switch (skill)
            {
                case ActiveSkillEnum.D_Execution:
                case ActiveSkillEnum.C_Execution:
                case ActiveSkillEnum.B_Execution:
                case ActiveSkillEnum.A_Execution:
                case ActiveSkillEnum.S_Execution:
                    // bgh.mmn：表内公式 + 目标最大血量 20%
                    return ez.dyk(d, player) + (long)nn.frk((yb)target, ((yb)target).maxHp * 0.2);
                case ActiveSkillEnum.D_Alchemy:
                case ActiveSkillEnum.C_Alchemy:
                case ActiveSkillEnum.B_Alchemy:
                case ActiveSkillEnum.A_Alchemy:
                case ActiveSkillEnum.S_Alchemy:
                    return ez.dyk(d, player);
                case ActiveSkillEnum.Bite:
                    // bfl.mna：自身最大血量 × exV[0]% + bphq × damageValue + damageBase
                    return (long)bfl.mna(player.maxHp, player.bphq, d);
                default:
                    return null;
            }
        }

        // ---- 正在生效的技能实例 cj.boyn.bcxi（bfe：bgwf 施法者、bgwi 技能、bgwg 剩余时间） ----
        public static float SkillEffectRemaining(Component p, int id)
        {
            var effects = cj.boyn?.bcxi;
            if (effects == null) return 0f;
            float best = 0f;
            foreach (var e in effects.Values)
                if (e != null && e.bgwf == (yb)p && (int)e.bgwi == id && e.bgwg > best)
                    best = e.bgwg;
            return best;
        }

        // ---- 地面预警 cj.boyj.bcqp（ok）：bcqt 变换、bcqw 已过时间、bcqx 总时长、bcqy 0=圆/扇形 1/2=矩形 ----
        private static readonly AccessTools.FieldRef<oj, List<ok>> warnings = AccessTools.FieldRefAccess<oj, List<ok>>("bcqp");

        public static void ReadTelegraphs(List<Telegraph> output)
        {
            output.Clear();
            var mgr = cj.boyj;
            if (mgr == null) return;
            foreach (var w in warnings(mgr))
            {
                if (w?.bcqt == null || w.bcqs == null || !w.bcqs.activeInHierarchy) continue;
                var t = w.bcqt;
                float left = w.bcqx - w.bcqw;
                if (w.bcqy == 0)
                {
                    // fvj：缩放 = 直径，_Range = 扇形全角，旋转 (90, 朝向+180, 0)，所以 -up 指向扇形朝向
                    Vector3 dir = -t.up;
                    dir.y = 0f;
                    float angle = w.bcqv != null ? w.bcqv.GetFloat(nw.bcmf) : 360f;
                    output.Add(Telegraph.Sector(t.position, t.localScale.x / 2f, dir, angle, left));
                }
                else
                {
                    // fvl / fvr：从 position 沿朝向延伸 scale.z，宽 scale.x
                    output.Add(Telegraph.Rect(t.position, t.forward, t.localScale.x, t.localScale.z, left));
                }
            }
        }

        // ---- 命中事件 bdc.bgfu：攻击方客户端上每次造成伤害时触发 ----
        // 伤害类型、来源、技能实例取自外层的 yb.iwk / yb.iwr（DamageContextHooks）：bgfu 在 iwk → iwl → bdc.lvz 里触发，
        // fact 只有来源标成 ActiveSkill 时才带技能，很多技能结算伤害时没标
        public static void SubscribeHits(System.Action<RawHit> handler)
        {
            bdc.bgfu += hit =>
            {
                var raw = new RawHit
                {
                    Source = hit.source,
                    Target = hit.target,
                    Damage = hit.fact.Amount + System.Math.Max(0L, hit.fact.ShieldDamage),
                    Hp = hit.targetHp,
                    Shield = hit.targetShield,
                    SkillId = hit.fact.Active ? hit.fact.SkillId : (int?)null,
                    Critical = hit.fact.Critical,
                    Normal = hit.fact.Normal,
                    Type = HitType.Unknown,
                    Origin = HitOrigin.Unknown,
                };
                if (DamageContextHooks.TryGet(hit.target, hit.source, out var ctx))
                {
                    raw.Type = MapEnum(ctx.Type, HitType.Unknown);
                    raw.Origin = MapEnum(ctx.Source, HitOrigin.Unknown);
                    if (ctx.Skill != null && ctx.Skill.bgwi != ActiveSkillEnum.None) raw.SourceSkillId = (int)ctx.Skill.bgwi;
                    else if (ctx.Caller != null) ResolveEffect(ctx.Caller, ctx.Target, ref raw);
                }
                handler(raw);
            };
        }

        // ---- 伤害来自哪个被动 / 装备特效 / 遗物 / Buff ----
        // DamageContextHooks 在调用栈里找到的类型是下面这几类之一；到本地玩家身上（Buff 在目标身上）找这个类型的实例取名字
        // 只认这几个基类：qp（被动，bht 子类）、ke / lm（装备特效）、rn（遗物）、hm（Buff）
        public static bool IsEffectType(Type t)
            => typeof(qp).IsAssignableFrom(t) || typeof(ke).IsAssignableFrom(t) || typeof(lm).IsAssignableFrom(t)
               || typeof(rn).IsAssignableFrom(t) || typeof(hm).IsAssignableFrom(t);

        // 被动、装备、遗物的名字按类型缓存（Buff 每次按目标查）；进战斗时清空
        private static readonly Dictionary<Type, (HitEffectKind kind, string name)> effectNames = new Dictionary<Type, (HitEffectKind, string)>();
        private static Component effectNamesFor;

        private static void ResolveEffect(Type t, yb target, ref RawHit raw)
        {
            if (typeof(hm).IsAssignableFrom(t))
            {
                raw.EffectKind = HitEffectKind.Buff;
                // 同一个目标身上同类型的 Buff 只有一个（localRoleBuffDic 以 LocalBuffType 为键）
                foreach (var b in target.localRoleBuffDic.Values)
                    if (b != null && b.GetType() == t)
                    {
                        raw.EffectName = cj.boyi.dff("Buff_" + b.baqz, b.baqz.ToString());
                        return;
                    }
                return;
            }
            var me = nn.bpbu;
            if (me != effectNamesFor)
            {
                effectNames.Clear();
                effectNamesFor = me;
            }
            if (!effectNames.TryGetValue(t, out var found))
            {
                found = FindEffect(me, t);
                // 没找到（比如刚换了装备）不缓存，下次再找
                if (found.name != null) effectNames[t] = found;
            }
            raw.EffectKind = found.kind;
            raw.EffectName = found.name;
        }

        private static (HitEffectKind, string) FindEffect(bbe me, Type t)
        {
            if (me == null) return (HitEffectKind.None, null);
            // 被动：bht.bhnu 是本地化后的名字（"p_" + 编号）
            if (typeof(qp).IsAssignableFrom(t))
            {
                foreach (var s in me.roleSkillList)
                    if (s != null && s.GetType() == t) return (HitEffectKind.Passive, s.bhnu);
                return (HitEffectKind.Passive, null);
            }
            var attr = me.playerAttribute;
            if (attr == null) return (HitEffectKind.None, null);
            // 装备技能 lm：同类型全身共享一个，用技能名（"equipSkill_" + EquipSkillType）
            if (typeof(lm).IsAssignableFrom(t))
            {
                foreach (var kv in attr.bfnc)
                    if (kv.Value != null && kv.Value.GetType() == t)
                        return (HitEffectKind.Equipment, cj.boyi.dff("equipSkill_" + kv.Key, kv.Key.ToString()));
                return (HitEffectKind.Equipment, null);
            }
            // 装备特效 ke：属于某件装备（bbcl），用装备名 jy.baxw
            if (typeof(ke).IsAssignableFrom(t))
            {
                foreach (var equip in attr.bfmz)
                {
                    if (equip == null) continue;
                    foreach (var k in equip.etb())
                        if (k != null && k.GetType() == t) return (HitEffectKind.Equipment, equip.baxw);
                }
                return (HitEffectKind.Equipment, null);
            }
            // 遗物：名字是 "pickitem_" + 编号
            if (typeof(rn).IsAssignableFrom(t))
            {
                foreach (var r in attr.bfna)
                    if (r != null && r.GetType() == t) return (HitEffectKind.Relic, cj.boyi.dff("pickitem_" + r.bdgp, r.bdgp));
                return (HitEffectKind.Relic, null);
            }
            return (HitEffectKind.None, null);
        }

        // 按名字对应游戏的枚举：游戏更新时就算调整了顺序也不会对错
        private static TOut MapEnum<TIn, TOut>(TIn value, TOut fallback) where TOut : struct
            => Enum.TryParse(value.ToString(), out TOut result) ? result : fallback;

        // ---- 技能数据 ActiveSkillData，表在 cj.boyl.bcrw ----
        public static SkillInfo ReadSkillInfo(int id)
        {
            var skill = (ActiveSkillEnum)id;
            if (!cj.boyl.bcrw.TryGetValue(skill, out var d)) return null;
            var tags = new List<string>();
            if (d.tags != null)
                foreach (var t in d.tags) tags.Add(t.ToString());
            return new SkillInfo(id, skill.ToString(), d.indicator, d.castingRange, d.range, d.cd, d.canAuto, tags);
        }

        // ---- 可改数值的配置表：技能 bcrw、英雄/怪物 bcru（都是 struct，改完要写回字典）----
        // 第一次改某一行时记下原版，之后每次都从原版重新计算，恢复时写回原版
        private static readonly Dictionary<(DataTable, string), object> originals = new Dictionary<(DataTable, string), object>();
        private static readonly Dictionary<(DataTable, string), FieldInfo> fieldInfos = new Dictionary<(DataTable, string), FieldInfo>();

        // 配置表在 cj.Init 里创建（bz.ddr 不会懒创建，之前是 null）
        public static bool TablesReady() => cj.boyl != null && cx.zyc.Count > 0;

        public static List<DataEntry> ReadEntries(DataTable table)
        {
            var list = new List<DataEntry>();
            switch (table)
            {
                case DataTable.Skill:
                    foreach (var kv in cj.boyl.bcrw.OrderBy(kv => (int)kv.Key))
                    {
                        if (kv.Key == ActiveSkillEnum.None) continue;
                        string name = cj.boyi.dff("a_" + (int)kv.Key, kv.Key.ToString());
                        list.Add(new DataEntry(table, kv.Key.ToString(), name, IconPath("Skill/", kv.Value.icon)));
                    }
                    break;
                case DataTable.Hero:
                    // 英雄的键是 HeroType 的数字，头像是 Player_<HeroType 名字最后一段>
                    foreach (var row in RawRows("hero"))
                    {
                        string id = Cell(row, "id");
                        if (id == null || !cj.boyl.bcru.ContainsKey(id) || !int.TryParse(id, out int n)) continue;
                        string head = ((HeroType)n).ToString().Split('_').Last();
                        // 英雄名的本地化 key 是 "hero_" + 编号（见 ez.dvm；不直接调 dvm，它会返回工坊模型的名字）
                        list.Add(new DataEntry(table, id, cj.boyi.dff("hero_" + id, id), IconPath("HeadIcon/Player_", head)));
                    }
                    break;
                case DataTable.Enemy:
                    // 怪物在每局开始时才加入 bcru，这里从原始表列出
                    foreach (var row in RawRows("enemy"))
                    {
                        string id = Cell(row, "id");
                        if (id == null) continue;
                        // 怪物名的本地化 key 就是 id（EnemyType 名字，见 EnemyDragonBoss 的 Boss 提示）；没有翻译的（多半是没用到的怪）显示 id
                        list.Add(new DataEntry(table, id, cj.boyi.dff(id, id), IconPath("HeadIcon/Enemy_", Cell(row, "model"))));
                    }
                    break;
            }
            return list;
        }

        public static double ReadOriginal(DataTable table, string key, string field)
        {
            if (!originals.TryGetValue((table, key), out var row) && !TryGetRow(table, key, out row)) return double.NaN;
            var fi = Field(table, field);
            return fi == null ? double.NaN : Convert.ToDouble(fi.GetValue(row));
        }

        // fields 为空表示恢复原版；怪物的值是倍率
        public static void WriteRow(DataTable table, string key, Dictionary<string, double> fields)
        {
            if (!TryGetRow(table, key, out var current)) return; // 怪物表还没加载，等 gad 之后再写
            bool restore = fields == null || fields.Count == 0;
            if (!originals.TryGetValue((table, key), out var original))
            {
                if (restore) return;
                originals[(table, key)] = original = current;
            }
            var row = CopyRow(original);
            if (!restore)
            {
                foreach (var f in fields)
                {
                    var fi = Field(table, f.Key);
                    if (fi == null) continue;
                    double v = table == DataTable.Enemy ? Convert.ToDouble(fi.GetValue(row)) * f.Value : f.Value;
                    fi.SetValue(row, ToFieldType(v, fi.FieldType));
                }
            }
            if (table == DataTable.Skill) cj.boyl.bcrw[(ActiveSkillEnum)Enum.Parse(typeof(ActiveSkillEnum), key)] = (ActiveSkillData)row;
            else cj.boyl.bcru[key] = (RoleAttribute)row;
            if (restore) originals.Remove((table, key));
        }

        private static bool TryGetRow(DataTable table, string key, out object row)
        {
            row = null;
            if (table == DataTable.Skill)
            {
                if (!Enum.TryParse(key, out ActiveSkillEnum skill) || !cj.boyl.bcrw.TryGetValue(skill, out var d)) return false;
                row = d;
                return true;
            }
            if (!cj.boyl.bcru.TryGetValue(key, out var r)) return false;
            row = r;
            return true;
        }

        public static int? SkillIdOf(string key) => Enum.TryParse(key, out ActiveSkillEnum skill) ? (int)skill : (int?)null;

        // 拆箱再装箱得到独立的副本，改它不会影响记下的原版
        private static object CopyRow(object row) => row is ActiveSkillData a ? (object)a : (RoleAttribute)row;

        private static FieldInfo Field(DataTable table, string name)
        {
            if (!fieldInfos.TryGetValue((table, name), out var fi))
            {
                var type = table == DataTable.Skill ? typeof(ActiveSkillData) : typeof(RoleAttribute);
                fi = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                fieldInfos[(table, name)] = fi;
            }
            return fi;
        }

        private static object ToFieldType(double v, Type type)
        {
            if (type == typeof(int)) return (int)Math.Round(Math.Max(int.MinValue, Math.Min(int.MaxValue, v)));
            if (type == typeof(long)) return (long)Math.Round(Math.Max(long.MinValue, Math.Min(long.MaxValue, v)));
            if (type == typeof(float)) return (float)v;
            return Convert.ChangeType(v, type);
        }

        // 原始表 cx.zyc[表名]：id → 行（列名 → 值）
        private static IEnumerable<Dictionary<string, object>> RawRows(string table)
        {
            if (!cx.zyc.TryGetValue(table, out var t) || !(t is Dictionary<string, object> rows)) yield break;
            foreach (var row in rows.Values)
                if (row is Dictionary<string, object> r) yield return r;
        }

        private static string Cell(Dictionary<string, object> row, string column)
            => row.TryGetValue(column, out var v) && v != null && v.ToString() != "" ? v.ToString() : null;

        private static string IconPath(string folder, string name) => string.IsNullOrEmpty(name) ? null : "Bundles/UI/Icon/" + folder + name;

        // ---- 创意工坊英雄模型（buj 换模型，bup 读 manifest.json，buu 工坊服务）----
        // 启动参数 -disable-mods 时为 true，游戏不加载任何工坊内容
        public static bool ModsDisabled() => buo.bkkn;

        // 同 buj.pwv：manifest 的英雄编号对得上，模型包文件存在；另外排除召唤物模型（bum.pyi，它的 manifest 也可能带 heroId）
        public static bool IsValidSkinFolder(string dir, int heroId)
            => ReadSkinManifest(dir, out var m) && m.heroId == heroId && !bum.pyi(m.contentType);

        private static bool ReadSkinManifest(string dir, out LocalWorkshopManifest m)
        {
            m = null;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            if (!bup.pyx(dir, out m) || m == null || m.heroId <= 0) return false;
            if (string.IsNullOrEmpty(m.bundleRelativePath) || string.IsNullOrEmpty(m.bundleAssetName)) return false;
            try
            {
                return File.Exists(Path.Combine(dir, m.bundleRelativePath));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        // 召唤物模型（bum.pyi）也带 manifest，但不是英雄模型
        public static HeroSkin ReadSkin(string id, string dir)
        {
            if (!ReadSkinManifest(dir, out var m) || bum.pyi(m.contentType)) return null;
            string title = !string.IsNullOrWhiteSpace(m.title) ? m.title.Trim()
                : !string.IsNullOrWhiteSpace(m.heroName) ? m.heroName.Trim()
                : Path.GetFileName(dir);
            return new HeroSkin(id, m.heroId, title, m.author?.Trim() ?? "", dir);
        }

        // buu.qaq()：已订阅的物品和状态（游戏自己每秒也调用一次）；bptc：Steam 可用
        public static List<HeroSkin> ReadWorkshopSkins()
        {
            var list = new List<HeroSkin>();
            var service = cj.boyt;
            if (service == null || !service.bptc) return list;
            foreach (var item in service.qaq())
            {
                if (item == null || !item.installed || item.heroId <= 0) continue;
                var skin = ReadSkin(HeroSkins.WorkshopId(item.publishedFileId), item.installFolder);
                if (skin != null) list.Add(skin);
            }
            return list;
        }

        // buu.qar：已安装物品的文件夹
        public static string WorkshopFolder(ulong id)
            => cj.boyt != null && cj.boyt.qar(id, out var item) && item != null ? item.installFolder : null;

        // buj.pwf(文件夹, manifest, out 图)：manifest.previewImage，游戏的工坊列表也用它。
        // 很多工坊物品没填 previewImage，但文件夹里有 preview.png / hero_icon.png（buj.pyb 按相对路径读图）
        public static Sprite SkinPreview(string dir)
        {
            if (!bup.pyx(dir, out var m)) return null;
            if (buj.pwf(dir, m, out var sprite)) return sprite;
            foreach (var name in new[] { "preview.png", m.heroIcon, "hero_icon.png" })
                if (!string.IsNullOrWhiteSpace(name) && File.Exists(Path.Combine(dir, name)) && buj.pyb(dir, name, out sprite))
                    return sprite;
            return null;
        }

        public static int LocalHeroId() => nn.bpbu is bbe p ? (int)p.heroType : 0;

        // buj.pwm：本地玩家正在用这个英雄时，撤掉当前模型（buj.pwn）再按 buj.pwu 的结果重新换（buj.pwh）
        // 血条上的英雄名 roleName 只在出生时按模型的 heroName 算一次（yb.iuh → ez.dvm），换完重新算；
        // 王者模式的 roleName 是 kingName，不动
        public static void ReapplyLocalHeroModel(int heroId)
        {
            buj.pwm(heroId);
            if (nn.bpbu is bbe p && (int)p.heroType == heroId && p.roleType == RoleType.Player)
                p.roleName = ez.dvm(p.heroType);
        }

        // 在 HeroSkinHooks 的角色上下文里撤掉模型（buj.pwn）再换（buj.pwh），名字（roleName）也按这个角色重新算
        public static void ReapplyHeroModel(Component p)
        {
            var player = (bbe)p;
            if (player.bpia == null) return;
            HeroSkinHooks.WithOwner(player, () =>
            {
                buj.pwn(player.bpia);
                buj.pwh(player);
                if (player.roleType == RoleType.Player) player.roleName = ez.dvm(player.heroType);
            });
        }

        // 游戏自己的工坊绑定（主菜单"工坊 Mod"里启用的物品），没有时为 0
        public static ulong GameSkinBinding(int heroId) => cj.boys != null && cj.boys.pzk(heroId, out ulong id) ? id : 0UL;

        // buu.qal：打开物品的创意工坊页面（游戏的工坊界面也用它）
        public static void OpenWorkshopPage(ulong id) => cj.boyt?.qal(id);

        // ---- 玩家 ----
        // yb.bphc：Mirror 的 isLocalPlayer
        public static bool IsLocalPlayer(Component r) => r is yb role && role.bphc;
        // Mirror 的 netId（NetworkBehaviour.bqbx），同一局里每个客户端看到的都一样
        public static uint NetId(Component r) => ((yb)r).bqbx;
        public static int HeroIdOf(Component p) => (int)((bbe)p).heroType;
        // bbe.bpnc：Steam 昵称（服务器生成角色时写入的 SyncVar）
        public static string PlayerName(Component p) => ((bbe)p).bpnc;
        public static bbe[] AllPlayers() => UnityEngine.Object.FindObjectsOfType<bbe>();

        // ---- Steam 大厅（cft = SteamMatchmaking，cgi = SteamUser）----
        // pq.bpcn：当前大厅，房主是 nn.bbwa、客户端是 nn.bbwb；进地牢后不离开大厅。本地（KCP）模式没有大厅，返回 0
        public static ulong LobbyId()
        {
            var lobby = bz.ddr<pq>();
            return lobby != null ? lobby.bpcn.m_SteamID : 0UL;
        }

        public static ulong MySteamId() => cgi.uep().m_SteamID;

        // 成员数据：每个成员只能写自己的，所有成员都能读。游戏不用它，pq.glv 收到成员数据的更新时直接返回
        public static void SetLobbyMemberData(ulong lobby, string key, string value)
            => cft.tqj(new Steamworks.CSteamID(lobby), key, value);

        public static string LobbyMemberData(ulong lobby, ulong member, string key)
            => cft.tqi(new Steamworks.CSteamID(lobby), new Steamworks.CSteamID(member), key);

        public static List<ulong> LobbyMembers(ulong lobby)
        {
            var id = new Steamworks.CSteamID(lobby);
            var list = new List<ulong>();
            int count = cft.tqb(id);
            for (int i = 0; i < count; i++) list.Add(cft.tqc(id, i).m_SteamID);
            return list;
        }

        // ---- 技能实例 bht ----
        public static int SkillId(object s) => (int)((bht)s).bhne;
        public static bool IsAutoCastOn(object s) => ((bht)s).bhny;
        public static float CooldownRemaining(object s) => ((bht)s).k896ff3548b31472b8b2cf8f649c25e65;
        public static int Cost(object s, Component p) => ((bht)s).kacb7d091b5904139a602f04ed9552242((bbe)p);
        // PayToWin 技能花的是金币不是蓝
        public static bool CostsGold(object s) => ((bht)s).bpqi;
        // 开关类技能（献祭等）当前开着
        public static bool IsToggledOn(object s) => ((bht)s).bhnv;
        public static int Charges(object s) => ((bht)s).k4b2ca10ddec34cdb90bf84a3f664f727;
        public static int MaxCharges(object s) => ((bht)s).kbb80bea445ac47778d36b05d19be7f37;
        // bht.bhng 技能按钮、bhnd 所属玩家
        public static object ButtonSkill(object s) => ((bht)s).bhng?.bjgk;
        public static Component SkillOwner(object s) => ((bht)s).bhnd;

        // ---- 英雄专属技能：hero 表 skill 列是编号，zhuDong 为真时是主动技能（ActiveSkillEnum），否则是被动（PasssiveSkillEnum）。
        // 出生时 bbe.kzz 按它发放（主动 yb.iuy、被动 yb.iuz） ----
        public static bool ReadHeroSkill(int heroId, out int skillId, out bool active)
        {
            skillId = 0;
            active = false;
            if (!cx.zyc.TryGetValue("hero", out var t) || !(t is Dictionary<string, object> rows)
                || !rows.TryGetValue(heroId.ToString(), out var r) || !(r is Dictionary<string, object> row))
                return false;
            if (!int.TryParse(Cell(row, "skill"), out skillId)) return false;
            active = row.TryGetValue("zhuDong", out var z) && z != null && (z is bool b ? b : bool.TryParse(z.ToString(), out b) && b);
            return true;
        }

        // 被动技能表 passsiveSkill：class（脚本类名）、icon、value（"|" 分隔）。名字的本地化 key 是 "p_" + 编号（yb.iuz）
        public static bool ReadPassive(int id, out string icon, out float[] values)
        {
            icon = null;
            values = new float[0];
            if (!cx.zyc.TryGetValue("passsiveSkill", out var t) || !(t is Dictionary<string, object> rows)
                || !rows.TryGetValue(id.ToString(), out var r) || !(r is Dictionary<string, object> row))
                return false;
            icon = IconPath("Skill/", Cell(row, "icon"));
            string raw = Cell(row, "value");
            if (raw != null)
                values = raw.Split('|').Select(s => float.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f).ToArray();
            return true;
        }

        // 主动技能的完整参数（SkillInfo 之外的数值）
        public static bool ReadSkillNumbers(int id, out float cost, out float duration, out int charges, out float damageBase, out float damageValue)
        {
            cost = duration = damageBase = damageValue = 0f;
            charges = 0;
            if (!cj.boyl.bcrw.TryGetValue((ActiveSkillEnum)id, out var d)) return false;
            cost = d.cost;
            duration = d.duration;
            charges = d.chargingNum;
            damageBase = d.damageBase;
            damageValue = d.damageValue;
            return true;
        }

        public static string SkillIcon(int id) => cj.boyl.bcrw.TryGetValue((ActiveSkillEnum)id, out var d) ? IconPath("Skill/", d.icon) : null;

        // 玩家身上的技能 bbe.roleSkillList：主动技能看 bhne，被动技能（qp）看 bhnj（= 被动表的 id，见 bht.msz）
        public static object FindActiveSkill(Component p, int id)
        {
            foreach (var s in ((bbe)p).roleSkillList)
                if (s != null && !(s is qp) && (int)s.bhne == id) return s;
            return null;
        }

        // 游戏后来替换掉的专属技能：拿到"鸡舞"卡牌后 iz.enj 把 ChickenDance 换成 RealChickenDance
        public static object FindReplacedSkill(Component p, int id)
            => id == (int)ActiveSkillEnum.ChickenDance ? FindActiveSkill(p, (int)ActiveSkillEnum.RealChickenDance) : null;

        public static object FindPassiveSkill(Component p, int id)
        {
            string key = id.ToString();
            foreach (var s in ((bbe)p).roleSkillList)
                if (s is qp && s.bhnj == key) return s;
            return null;
        }

        // ---- bht 上从表里拷来的数值（yb.iva / yb.iuy 获得技能时拷贝；yb.njw 是诱饵）。nn.fsx 不校验这些，游戏自己（ix.enp）也直接写 ----
        public static float CooldownTotal(object s) => ((bht)s).kb7f88a1b0a6e4124b9b27921f2778742;
        public static void SetCooldownTotal(object s, float v) => ((bht)s).kb7f88a1b0a6e4124b9b27921f2778742 = v;
        public static void SetCooldownRemaining(object s, float v) => ((bht)s).k896ff3548b31472b8b2cf8f649c25e65 = v;
        // 按键时判断蓝够不够用它；真正扣蓝时 ez.dyg 重新查表（所以 TuningHooks 还要临时替换表）
        public static int BaseCost(object s) => ((bht)s).k443b7b692a1648099b5e5890ad35ed6d;
        public static void SetBaseCost(object s, int v) => ((bht)s).k443b7b692a1648099b5e5890ad35ed6d = v;
        public static void SetMaxCharges(object s, int v)
        {
            var skill = (bht)s;
            skill.kbb80bea445ac47778d36b05d19be7f37 = v;
            if (skill.k4b2ca10ddec34cdb90bf84a3f664f727 > v) skill.k4b2ca10ddec34cdb90bf84a3f664f727 = v;
        }

        // 被动技能的数值：获得时 bht.msz 把表里的 value 解析进私有 bhnq，再由 mtm 算出 bhnp = bhnq + levelup × 等级；
        // 多数被动每次触发都读 bhnp。升级（grs）会用 bhnq 重算覆盖，所以两个一起改
        private static readonly AccessTools.FieldRef<bht, float[]> passiveBase = AccessTools.FieldRefAccess<bht, float[]>("bhnq");

        public static float[] PassiveValues(object s) => ((bht)s).bhnp;

        // 504 内部折扣在 gou 里把折扣加进 shopDiscount、gov 按值删除；511 电锯恶魔在 gou 里把数值缓存到私有字段。
        // 这两个要先 gov（按旧值撤销），改完再 gou（按新值生效）
        private static readonly HashSet<string> rebindPassives = new HashSet<string> { "504", "511" };

        public static void SetPassiveValues(object s, float[] values)
        {
            var skill = (qp)s;
            bool rebind = rebindPassives.Contains(skill.bhnj);
            if (rebind) skill.gov();
            var current = skill.bhnp;
            var baseValues = passiveBase(skill);
            for (int i = 0; i < values.Length; i++)
            {
                if (current != null && i < current.Length) current[i] = values[i];
                if (baseValues != null && i < baseValues.Length) baseValues[i] = values[i];
            }
            if (rebind) skill.gou();
        }

        // ---- 只改本地玩家的技能参数：施法者是本地玩家时，在 ez.dzc / dye / dyf 执行期间把 bcrw[技能] 换成调过的副本，结束后换回原版。
        // 表本身一直是原版，镜像 Boss（zq 复制英雄技能）和其他玩家的同名技能不受影响 ----
        private static readonly List<(ActiveSkillEnum key, ActiveSkillData original)> swaps = new List<(ActiveSkillEnum, ActiveSkillData)>();

        public static bool BeginSwap(Component caster, int id)
        {
            if (caster == null || caster != nn.bpbu) return false;
            var tune = SkillTuning.Get(id);
            if (tune.Cost == 1f && tune.Duration == 1f) return false;
            var key = (ActiveSkillEnum)id;
            // 嵌套调用（dyf 里会再走到 dzc）时表已经换过了
            foreach (var s in swaps)
                if (s.key == key) return false;
            if (!cj.boyl.bcrw.TryGetValue(key, out var original)) return false;
            var tuned = original;
            tuned.cost = Mathf.Max(0, Mathf.RoundToInt(original.cost * tune.Cost));
            tuned.duration = original.duration * tune.Duration;
            cj.boyl.bcrw[key] = tuned;
            swaps.Add((key, original));
            return true;
        }

        public static void EndSwap()
        {
            if (swaps.Count == 0) return;
            var (key, original) = swaps[swaps.Count - 1];
            swaps.RemoveAt(swaps.Count - 1);
            cj.boyl.bcrw[key] = original;
        }

        // 换过之后 dyf 的参数也要换成调过的那份
        public static ActiveSkillData CurrentRow(ActiveSkillEnum key) => cj.boyl.bcrw[key];

        // ez.dyf(玩家, 技能, 目标点, 技能数据, 朝向, 目标 netId, 技能书编号, h = true)：游戏的免费释放（被动"长老的多重施法"就这么用）。
        // 不查蓝、不扣蓝、不进冷却，技能自己的检查和状态切换照常。不要用 h = false：冷却会扣到最后按下的那一格
        public static bool FreeCast(Component p, int skill, Vector3 pos, float rotation, int target, int book)
            => cj.boyl.bcrw.TryGetValue((ActiveSkillEnum)skill, out var d)
               && ez.dyf((bbe)p, (ActiveSkillEnum)skill, pos, d, rotation, target, book, h: true);

        // ---- 属性加成（都是累加，负数撤销；不在 nn.fsx 的校验范围内）----
        // yb.iqz：攻速加成（比例，0.25 = +25%）；ez.ebt 的 "AttackSpeed" 就是它
        public static void AddAttackSpeed(Component p, float ratio) => ((yb)p).iqz(ratio);
        // yb.irv：护甲（会刷新属性面板）
        public static void AddArmor(Component p, int amount) => ((yb)p).irv(amount);
        // 游戏的无敌药水 buff（roleBuffManager.mim<T>(名字, 秒)，同名 buff 重复添加时刷新时长）
        public static void GiveInvulnerable(Component p, float seconds) => ((yb)p).roleBuffManager.mim<Buff无敌>("Buff无敌", seconds);

        // ---- 悬停提示框 bka：技能（wh）、遗物和 buff（iq）、三选一卡片（bqa.osp）都用它。njb(true, 信息) 显示、njb(false, default) 隐藏；
        // 还没创建时用 dqk 打开（同 bqa.osp）----
        public static void ShowTip(string name, string info, string icon, Vector3 position, int quality)
        {
            var tip = cj.boyb?.dqr<bka>() ?? cj.boyb?.dqk<bka>() as bka;
            tip?.njb(true, new bka.TipInfo
            {
                nameStr = name ?? "",
                info = info ?? "",
                iconPath = icon ?? "",
                showPos = position,
                quality = quality,
                isRelic = false,
            });
        }

        public static void HideTip() => cj.boyb?.dqr<bka>()?.njb(false, default(bka.TipInfo));

        // bht.bhof：技能悬停提示末尾的附加说明（wh.OnPointerEnter 拼在描述后面）。游戏自己也会写（五五开的外挂、土豆兄弟），所以只追加
        public static string SkillExtraInfo(object s) => ((bht)s).bhof;
        public static void SetSkillExtraInfo(object s, string v) => ((bht)s).bhof = v;

        // ---- 波次配置 nn.bbst（SOSpawnConfig）：enemySpawnData[i] 是第 i+1 波，eventList 带 Roguelike 时，
        // 进入这一波前的休息阶段弹遗物三选一（服务器先 bbsr++ 再发 OnStartRest）----
        public static int TotalWaves() => nn.bbst?.enemySpawnData?.Length ?? 0;

        public static bool RelicBeforeWave(int index)
        {
            var waves = nn.bbst?.enemySpawnData;
            if (waves == null || index < 0 || index >= waves.Length) return false;
            var events = waves[index].eventList;
            return events != null && Array.IndexOf(events, RoguelikeEventType.Roguelike) >= 0;
        }

        // 开场界面 bkh：关闭时（bift = MyServerNetworkManager.qib）游戏弹开局遗物
        public static bool OpeningPending()
        {
            var ui = cj.boyb?.dqr<bkh>();
            return ui != null && ui.bacf;
        }

        // 重试波次的快照有效（wb.bpcw）
        public static bool CheckpointValid() => wb.bpcw;

        // ---- 技能栏 bpe.bjes（bpf 按钮，bpf.bjgk 技能实例，bpf.bjgm 图标上的 bpa 组件） ----
        public static void ReadSkillBar(List<(object button, object skill, RectTransform icon)> output)
        {
            output.Clear();
            var bar = cj.boyb?.dqr<bpe>();
            if (bar?.bjes == null) return;
            foreach (var b in bar.bjes)
            {
                if (b == null || b.bjgm == null) continue;
                object skill = b.bjgk != null && b.bjgk.bhne != ActiveSkillEnum.None ? b.bjgk : null;
                output.Add((b, skill, (RectTransform)b.bjgm.transform));
            }
        }
    }
}
