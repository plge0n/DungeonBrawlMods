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
        // 本地玩家 nr.bqtm（bbe : yb）
        public static Component LocalPlayer() => nr.bqtm;

        // cj.bqps.dff：找不到 key 时返回默认值，不打警告
        public static string Localize(string key, string fallback) => cj.bqps.dhh(key, fallback);
        // cl.bqqe：游戏设置里的语言（LanguageType）。不在兼容检查清单里：读不到时 Localization 退回中文，不必停用整个 ModKit
        public static int LanguageIndex() => (int)cj.bqps.bqqe;

        // ---- 角色 yb ----
        // 7.06 起 ym 里有个混淆后也叫 hp 的方法，源码里写 .hp 会解析成那个方法，字段只能用访问器读
        private static readonly AccessTools.FieldRef<yl, long> roleHp = AccessTools.FieldRefAccess<yl, long>("hp");
        public static long Hp(Component r) => roleHp((yl)r);
        public static long MaxHp(Component r) => ((yl)r).maxHp;
        public static bool IsAlive(Component r) => r != null && ((yl)r).braf != null && ((yl)r).jgi();
        public static Vector3 Position(Component r) => ((yl)r).braf.position;
        // 水平距离
        public static float Distance(Component from, Vector3 to) => ((yl)from).jdc(to);
        // 体型补偿：大体型敌人判定距离更远
        public static float HitRadius(Component r) => ((yl)r).brai != null ? ((yl)r).brai.addRange : 0f;
        public static bool IsBoss(Component r) => r is xy e && e.isBoss;
        public static bool IsElite(Component r) => r is xy e && e.isElite;
        // 召唤物 roleType = Summon，authorityId = 召唤者的 netId（xy.hqf）
        public static bool IsSummon(Component r) => ((yl)r).roleType == RoleType.Summon;
        public static uint AuthorityId(Component r) => ((yl)r).authorityId;
        public static List<yl> Enemies(Component r) => ((yl)r).jhg();
        // 护盾 bpgz；无敌 beqd（受到的伤害记为 0）
        public static long Shield(Component r) => ((yl)r).bqzh;
        public static bool IsInvulnerable(Component r) => ((yl)r).bfse;
        // 普攻距离 roleModeBase.ico()（判定时还要加上对方体型半径）
        public static float AttackRange(Component r) => ((yl)r).brai != null ? ((yl)r).brai.ijk() : 0f;
        public static float MoveSpeed(Component r) => ((yl)r).jdd();
        public static string State(Component r) => ((yl)r).brag.ToString();
        // 变身中 henShinSkillId > 0（默认 -1）
        public static bool IsTransformed(Component r) => ((yl)r).henShinSkillId > 0;

        // ---- 普攻节奏：yl.timer 在开始攻击时清零，> iyi() 时可以再攻击；攻击动作长 iyj()。
        // isCheckAttack 由近战/弹道模式在出手时置位；远程模式不置位，按动作进度 0.41 出手，这里用 0.5 兜底 ----
        public static float AttackTimer(Component r) => ((yl)r).timer;
        public static float AttackInterval(Component r) => ((yl)r).jgd();
        public static bool IsAttacking(Component r) => ((yl)r).brag == RoleState.Attack;
        public static bool AttackHitDone(Component r)
        {
            var role = (yl)r;
            return role.isCheckAttack || role.timer / role.jge() >= 0.5f;
        }

        // ---- 属性（yb，bbe 有部分重写）----
        public static long Attack(Component r) => ((yl)r).brah;
        public static int Strength(Component r) => ((yl)r).bqzy;
        public static int Agility(Component r) => ((yl)r).bqzx;
        public static int Stamina(Component r) => ((yl)r).bqzw;
        public static float AttackSpeed(Component r) => ((yl)r).bqzj;
        // 暴击率是 0~1 的概率（Random.value < bphl）
        public static float CriticalRate(Component r) => ((yl)r).bqzt;
        public static float CriticalDamage(Component r) => ((yl)r).bqzu;
        public static int Armor(Component r) => ((yl)r).bqzs;
        public static int Dodge(Component r) => ((yl)r).bqzz;
        public static float LifeSteal(Component r) => ((yl)r).brad;

        // ---- 玩家 bbe ----
        public static int Mana(Component p) => ((bbx)p).bgxu;
        public static int MaxMana(Component p) => ((bbx)p).bgxv;
        public static int Gold(Component p) => ((bbx)p).k6f41850941404079a07256c13d65099e;

        // 以下都是游戏自己加数值的方法：金币、力敏体等有影子副本校验（nr.fxd），不能直接写字段
        // yl.jes(量, 来源)：回血，内部走 CmdUpdateHp 同步
        public static void Heal(Component r, double amount) => ((yl)r).jes(amount, (yl)r);
        // bbx.ldr：加蓝（自动限制在 0~上限）
        public static void AddMana(Component p, int amount) => ((bbx)p).ldr(amount);
        // bbe.kfd63a19…(飘字位置, 数量, 吃金币加成, 吃关卡倍率, 允许负债, 来源)：返回实际到账数量
        public static int AddGold(Component p, int amount)
            => ((bbx)p).kad2a3d5a59724b85bfc565c5f7d7246e(((bbx)p).jea(), amount, c: false, d: false, e: false, GoldIncomeSource.Gameplay);

        // bbx.kvv(力, 敏, 体, 攻击)：加到加成字段 bfpj/bfpk/bfpl/bfpi（不是被防篡改校验的基础值），负数撤销；
        // 体力变化会自动同步生命上限。游戏的卡牌、装备也用它
        public static void AddStats(Component p, int str, int agi, int sta, long attack) => ((bbx)p).kvv(str, agi, sta, attack);

        // 当前波次，从 0 开始（游戏显示 nr.bcqk + 1）
        public static int Wave() => nr.bcqk;

        // ---- 本地玩家的战斗回调（多播委托字段，只在拥有者客户端触发）----
        // 只挂没有返回值、不带 ref 的回调：damageEvent 之类的返回值会替换伤害，多挂一个就会改掉游戏结果
        private sealed class PlayerBinding
        {
            public bbx Player;
            public yl.EnemyDefeated Defeated;
            public yl.ActiveSkillCastEvent SkillCast;
            public yl.Critical Critical;
            public yl.DogeEvent Dodge;
            public Action LevelUp;
            public Action Relife;
            public Action<bbx> TeammateLevelUp;
            public yl.DieEvent Died;
            public bbx.AddBagItem BagItem;
            public yl.LocalActiveSkillCastEvent LocalCast;
        }

        public static object BindPlayerEvents(Component player)
        {
            var p = (bbx)player;
            var b = new PlayerBinding
            {
                Player = p,
                Defeated = (killer, dead, ctx) =>
                {
                    if (dead is xy) GameEvents.Raise(new GameEvent(GameEventKind.EnemyKilled, Role.Wrap(dead), isBoss: ctx.IsBoss, isElite: ctx.IsElite));
                },
                SkillCast = (caster, skill) => GameEvents.Raise(new GameEvent(GameEventKind.SkillCast, skill: SkillInfo.Get((int)skill))),
                Critical = (hurt, damage, type) => GameEvents.Raise(new GameEvent(GameEventKind.Critical, Role.Wrap(hurt), amount: damage)),
                Dodge = () => GameEvents.Raise(new GameEvent(GameEventKind.Dodged)),
                LevelUp = () => GameEvents.Raise(new GameEvent(GameEventKind.LevelUp)),
                Relife = () => GameEvents.Raise(new GameEvent(GameEventKind.Revived)),
                // onTeammateLevelUp：队友升级时本地玩家身上触发（bbe.cs 10844，伙伴之刃被动用它）
                TeammateLevelUp = mate => GameEvents.Raise(new GameEvent(GameEventKind.TeammateLevelUp, Role.Wrap(mate))),
                // dieEvent(参数名叫 attackRole，实际传的是死者自己：yl.hrk 里 dieEvent?.Invoke(this))
                Died = dead => GameEvents.Raise(new GameEvent(GameEventKind.Died, Role.Wrap(dead))),
                BagItem = item => GameEvents.Raise(new GameEvent(GameEventKind.ItemPicked, itemId: (int)item)),
                // localActiveSkillCastEvent(玩家, 技能, 目标点, 朝向, 目标 netId, 技能书编号, 是否免费)：只在 ey.eaq 末尾触发（真正放出技能时），
                // 免费释放（dyf 的 h = true）也会触发
                LocalCast = (_, skill, pos, rot, target, book, free) => Combat.RaiseSkillCast(new SkillCast((int)skill, pos, rot, target, book, free)),
            };
            p.enemyDefeatedEvent += b.Defeated;
            p.activeSkillCastEvent += b.SkillCast;
            p.criticalEvent += b.Critical;
            p.dogeEvent += b.Dodge;
            p.onPlayerLevelUp += b.LevelUp;
            p.onPlayerRelife += b.Relife;
            p.onTeammateLevelUp += b.TeammateLevelUp;
            p.dieEvent += b.Died;
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
            p.onTeammateLevelUp -= b.TeammateLevelUp;
            p.dieEvent -= b.Died;
            p.addBagItemEvent -= b.BagItem;
            p.localActiveSkillCastEvent -= b.LocalCast;
        }

        // ---- 遗物：bcry（ItemType → RemainsData），名字/图标在原始 remains 表和本地化 "pickitem_" + 编号 ----
        public static List<RelicInfo> ReadRelics()
        {
            var list = new List<RelicInfo>();
            foreach (var kv in cj.bqpv.bdsj.OrderBy(kv => (int)kv.Key))
            {
                int id = (int)kv.Key;
                string icon = null;
                if (cx.bauu.TryGetValue("remains", out var t) && t is Dictionary<string, object> rows
                    && rows.TryGetValue(id.ToString(), out var r) && r is Dictionary<string, object> row)
                    icon = Cell(row, "icon");
                list.Add(new RelicInfo(id, cj.bqps.dhh("pickitem_" + id, kv.Key.ToString()), kv.Value.grade, IconPath("Remains/", icon)));
            }
            return list;
        }

        // bbx.lbw(编号, 等级)：游戏的遗物三选一（ey.ecg）就调它；超过本局获取上限、或王者模式禁用时返回 null
        public static bool GiveRelic(Component p, int id) => cj.bqpv.bdsj.ContainsKey((ItemType)id) && ((bbx)p).lbw(id, 0) != null;

        // ---- 掉落物：cj.bqqa（ot）.bctu，ou 的 bcty = 编号、bctz = 所属玩家 netId（0 = 公共）、bcua = ItemType、bcuc = 位置 ----
        public static void ReadLoot(List<LootItem> output)
        {
            output.Clear();
            var items = cj.bqqa?.bdul;
            if (items == null) return;
            foreach (var kv in items)
            {
                var o = kv.Value;
                if (o == null) continue;
                // pa.git：公共物品、自己的物品，或物主允许共享时能捡（服务器 bbx.lnw 用同样的判断）
                output.Add(new LootItem(o.bduq, (int)o.bdus, o.bduu, pa.git(o), ey.edm(o.bdus), ey.edp(o.bdus)));
            }
        }

        public static string ItemName(int itemType) => cj.bqps.dhh("pickitem_" + itemType, ((ItemType)itemType).ToString());

        // bki.Pick（点击单个物品）的判断：死了不能捡（nr.bcra 时除外）
        public static bool CanPickNow(Component p) => nr.bcra || (Hp(p) > 0 && ((yl)p).brag != RoleState.Dead);

        // 背包 bbq.bgsk，上限 6 格；金币、宝石、阳光、力量/敏捷/耐力之书、护符不占格子（ey.edm；全属性之书、金币袋占格子）
        public static int BagCount(Component p) => ((bbx)p).playerAttribute.kry();

        // 护符需按键拾取（设置项 IsKeyPickTalisman）；关掉时护符靠碰到自动拾取，按 C 也不捡
        public static bool KeyPickTalisman() => nr.bcrs;

        // CmdPickItem 由服务器 bbx.lnw 处理：只检查物主，不检查距离，距离要自己限制
        public static void PickItem(Component p, uint id) => ((bbx)p).CmdPickItem(id);

        public static void PlayPickSound() => bz.dfv<AudioManager>()?.dcy("Audio/Battle_Audio/UI/拾取物品", 0.9f);

        // ---- 三选一界面 bqa（ui_roguelike）----
        // 和游戏自己弹遗物前的判断一致（MyServerNetworkManager.rcx）：三选一界面开着，或技能栏的子面板（bqz.brlk）开着；
        // 另外开场界面开着时也算忙（它一关游戏就弹开局遗物，抢在前面打开会被游戏随机选掉）
        public static bool ChoiceBusy()
        {
            var ui = cj.bqpl?.dst<brx>();
            if (ui != null && ui.bayx) return true;
            var bar = cj.bqpl?.dst<bqz>();
            return (bar != null && bar.brlk) || OpeningPending();
        }

        // brx.pmi(选项, 选中回调, 标题, 刷新(null = 不能刷新), 关闭回调, 延迟, 队伍数据, 来源)：ey.ecf 用的真方法。
        // 来源传 null：非空时 cs.dhz 会把选项计入游戏的统计
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
            var ui = (brx)cj.bqpl.dsm<brx>();
            ui.pmi(data, d => onPick(int.TryParse(d.data, out int i) ? i : 0), title, null, null, 0f, null, null);
        }

        // ---- 联机（Mirror：ciy = NetworkServer，cik = NetworkClient）----
        public static bool ServerActive() => cla.bopw;
        public static bool ClientActive() => ckm.brvy;
        // 主机上的连接数，包含主机自己
        public static int ServerConnections() => cla.bopp.Count;

        // ---- 输入、UI ----
        // oz.ggq()：持有期间屏蔽移动、技能、Tab 商店、ESC 菜单（oz.dda 在计数非 0 时提前返回）
        public static IDisposable BlockInput() => oz.ggq();
        // 游戏 UI 的 Canvas（UIRoot 第 0 个子节点）；主菜单之前可能还没创建
        public static Canvas UiCanvas() => cj.bqpl?.bazk != null ? cj.bqpl.bazk.GetComponent<Canvas>() : null;
        // 主菜单（ui_startGame/trans_bg/trans_main，竖排布局）里的"创意工坊"按钮；主菜单没显示时为 null。
        // 只用预制体里的对象名、不碰混淆代码，ModKit 不兼容时也能用
        public static GameObject MainMenuWorkshopButton()
        {
            var go = GameObject.Find("btn_workShop");
            return go != null && go.transform.parent != null && go.transform.parent.name == "trans_main" ? go : null;
        }

        // 普攻、技能共用的增伤，以及对 Boss / 精英 / 普通敌人的额外增伤（见 yl.jed）
        public static float DamageBonus(Component p, Component target)
        {
            var player = (bbx)p;
            float bonus = player.addDamagePercent;
            if (target is xy e)
                bonus += e.isBoss ? player.addBossEnemy : e.isElite ? player.addEliteEnemy : player.addNormalEnemy;
            return bonus;
        }

        // ---- 全局开关 ----
        public static bool AutoBattleOn() => nr.bcro;
        // nr.bcrf：角色被锁定（过场等），kqi 里直接站着不动
        public static bool ControlLocked() => nr.bcrf;
        public static bool IsKing(Component p) => ((yl)p).roleType == RoleType.King;
        // 场地边界（圆形/多边形/矩形）内且离边缘至少 margin
        public static bool InsideArena(Vector3 pos, float margin) => nr.fuy(pos, margin);
        // 玩家的 CharacterController（bbx.bgxl，公开访问器 bpmb），移动时被场景碰撞体挡住
        public static CharacterController Controller(Component p) => ((bbx)p).brew;
        // 碰撞体属于某个角色（玩家、敌人、召唤物），不算障碍物
        public static bool IsRoleCollider(Collider c) => c.GetComponentInParent<yl>() != null;

        // ---- 技能伤害估算（未计暴击、减伤、各种事件），各技能的实际公式见 NOTES.md ----
        public static long? BaseSkillDamage(int id, Component p, Component target)
        {
            var skill = (ActiveSkillEnum)id;
            if (!cj.bqpv.bdsh.TryGetValue(skill, out var d)) return null;
            var player = (bbx)p;
            switch (skill)
            {
                case ActiveSkillEnum.D_Execution:
                case ActiveSkillEnum.C_Execution:
                case ActiveSkillEnum.B_Execution:
                case ActiveSkillEnum.A_Execution:
                case ActiveSkillEnum.S_Execution:
                    // bhe.ncx：表内公式 + 目标最大血量 20%
                    return ey.eav(d, player) + (long)nr.fvq((yl)target, ((yl)target).maxHp * 0.2);
                case ActiveSkillEnum.D_Alchemy:
                case ActiveSkillEnum.C_Alchemy:
                case ActiveSkillEnum.B_Alchemy:
                case ActiveSkillEnum.A_Alchemy:
                case ActiveSkillEnum.S_Alchemy:
                    return ey.eav(d, player);
                case ActiveSkillEnum.Bite:
                    // bgg.ndk：自身最大血量 × exV[0]% + bphq × damageValue + damageBase
                    return (long)bgg.ndk(player.maxHp, player.bqzy, d);
                default:
                    return null;
            }
        }

        // ---- 正在生效的技能实例 cj.bqpx.bcxi（bfe：bgwf 施法者、bgwi 技能、bgwg 剩余时间） ----
        public static float SkillEffectRemaining(Component p, int id)
        {
            var effects = cj.bqpx?.bdya;
            if (effects == null) return 0f;
            float best = 0f;
            foreach (var e in effects.Values)
                if (e != null && e.bifz == (yl)p && (int)e.bigc == id && e.biga > best)
                    best = e.biga;
            return best;
        }

        // ---- 地面预警 cj.bqpt.bcqp（ok）：bcqt 变换、bcqw 已过时间、bcqx 总时长、bcqy 0=圆/扇形 1/2=矩形 ----
        private static readonly AccessTools.FieldRef<oq, List<or>> warnings = AccessTools.FieldRefAccess<oq, List<or>>("bdra");

        public static void ReadTelegraphs(List<Telegraph> output)
        {
            output.Clear();
            var mgr = cj.bqpt;
            if (mgr == null) return;
            foreach (var w in warnings(mgr))
            {
                if (w?.bdre == null || w.bdrd == null || !w.bdrd.activeInHierarchy) continue;
                var t = w.bdre;
                float left = w.bdri - w.bdrh;
                if (w.bdrj == 0)
                {
                    // fvj：缩放 = 直径，_Range = 扇形全角，旋转 (90, 朝向+180, 0)，所以 -up 指向扇形朝向
                    Vector3 dir = -t.up;
                    dir.y = 0f;
                    float angle = w.bdrg != null ? w.bdrg.GetFloat(oa.bdkb) : 360f;
                    output.Add(Telegraph.Sector(t.position, t.localScale.x / 2f, dir, angle, left));
                }
                else
                {
                    // fvl / fvr：从 position 沿朝向延伸 scale.z，宽 scale.x
                    output.Add(Telegraph.Rect(t.position, t.forward, t.localScale.x, t.localScale.z, left));
                }
            }
        }

        // ---- 命中事件 bdx.bhpo：攻击方客户端上每次造成伤害时触发 ----
        // 伤害类型、来源、技能实例取自外层的 yl.jed / yl.jek（DamageContextHooks）：bgfu 在 iwk → iwl → bdx.mml 里触发，
        // fact 只有来源标成 ActiveSkill 时才带技能，很多技能结算伤害时没标
        public static void SubscribeHits(System.Action<RawHit> handler)
        {
            bdx.bhpo += hit =>
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
                raw.RawDamage = RawDamageHooks.TryGet(hit.target, hit.source, out long rawDamage)
                    ? System.Math.Max(rawDamage, raw.Damage)
                    : raw.Damage;
                if (DamageContextHooks.TryGet(hit.target, hit.source, out var ctx))
                {
                    raw.Type = MapEnum(ctx.Type, HitType.Unknown);
                    raw.Origin = MapEnum(ctx.Source, HitOrigin.Unknown);
                    if (ctx.Skill != null && ctx.Skill.bigc != ActiveSkillEnum.None) raw.SourceSkillId = (int)ctx.Skill.bigc;
                    else if (ctx.Caller != null) ResolveEffect(ctx.Caller, ctx.Target, ref raw);
                }
                handler(raw);
            };
        }

        // ---- 伤害来自哪个被动 / 装备特效 / 遗物 / Buff ----
        // DamageContextHooks 在调用栈里找到的类型是下面这几类之一；到本地玩家身上（Buff 在目标身上）找这个类型的实例取名字
        // 只认这几个基类：qp（被动，bht 子类）、ke / lm（装备特效）、rn（遗物）、hm（Buff）
        public static bool IsEffectType(Type t)
            => typeof(qw).IsAssignableFrom(t) || typeof(kh).IsAssignableFrom(t) || typeof(lq).IsAssignableFrom(t)
               || typeof(rv).IsAssignableFrom(t) || typeof(hn).IsAssignableFrom(t);

        // 被动、装备、遗物的名字按类型缓存（Buff 每次按目标查）；进战斗时清空
        private static readonly Dictionary<Type, (HitEffectKind kind, string name)> effectNames = new Dictionary<Type, (HitEffectKind, string)>();
        private static Component effectNamesFor;

        private static void ResolveEffect(Type t, yl target, ref RawHit raw)
        {
            if (typeof(hn).IsAssignableFrom(t))
            {
                raw.EffectKind = HitEffectKind.Buff;
                // 同一个目标身上同类型的 Buff 只有一个（localRoleBuffDic 以 LocalBuffType 为键）
                foreach (var b in target.localRoleBuffDic.Values)
                    if (b != null && b.GetType() == t)
                    {
                        raw.EffectName = cj.bqps.dhh("Buff_" + b.bbop, b.bbop.ToString());
                        return;
                    }
                return;
            }
            var me = nr.bqtm;
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

        private static (HitEffectKind, string) FindEffect(bbx me, Type t)
        {
            if (me == null) return (HitEffectKind.None, null);
            // 被动：bir.bixu 是本地化后的名字（"p_" + 编号）
            if (typeof(qw).IsAssignableFrom(t))
            {
                foreach (var s in me.roleSkillList)
                    if (s != null && s.GetType() == t) return (HitEffectKind.Passive, s.bixu);
                return (HitEffectKind.Passive, null);
            }
            var attr = me.playerAttribute;
            if (attr == null) return (HitEffectKind.None, null);
            // 装备技能 lm：同类型全身共享一个，用技能名（"equipSkill_" + EquipSkillType）
            if (typeof(lq).IsAssignableFrom(t))
            {
                foreach (var kv in attr.bgsl)
                    if (kv.Value != null && kv.Value.GetType() == t)
                        return (HitEffectKind.Equipment, cj.bqps.dhh("equipSkill_" + kv.Key, kv.Key.ToString()));
                return (HitEffectKind.Equipment, null);
            }
            // 装备特效 ke：属于某件装备（bbcl），用装备名 kb.bbvo
            if (typeof(kh).IsAssignableFrom(t))
            {
                foreach (var equip in attr.bgsi)
                {
                    if (equip == null) continue;
                    foreach (var k in equip.exd())
                        if (k != null && k.GetType() == t) return (HitEffectKind.Equipment, equip.bbvo);
                }
                return (HitEffectKind.Equipment, null);
            }
            // 遗物：名字是 "pickitem_" + 编号
            if (typeof(rv).IsAssignableFrom(t))
            {
                foreach (var r in attr.bgsj)
                    if (r != null && r.GetType() == t) return (HitEffectKind.Relic, cj.bqps.dhh("pickitem_" + r.beho, r.beho));
                return (HitEffectKind.Relic, null);
            }
            return (HitEffectKind.None, null);
        }

        // 按名字对应游戏的枚举：游戏更新时就算调整了顺序也不会对错
        private static TOut MapEnum<TIn, TOut>(TIn value, TOut fallback) where TOut : struct
            => Enum.TryParse(value.ToString(), out TOut result) ? result : fallback;

        // ---- 技能数据 ActiveSkillData，表在 cj.bqpv.bcrw ----
        public static SkillInfo ReadSkillInfo(int id)
        {
            var skill = (ActiveSkillEnum)id;
            if (!cj.bqpv.bdsh.TryGetValue(skill, out var d)) return null;
            var tags = new List<string>();
            if (d.tags != null)
                foreach (var t in d.tags) tags.Add(t.ToString());
            return new SkillInfo(id, skill.ToString(), d.indicator, d.castingRange, d.range, d.cd, d.canAuto, tags);
        }

        // ---- 可改数值的配置表：技能 bcrw、英雄/怪物 bcru（都是 struct，改完要写回字典）----
        // 第一次改某一行时记下原版，之后每次都从原版重新计算，恢复时写回原版
        private static readonly Dictionary<(DataTable, string), object> originals = new Dictionary<(DataTable, string), object>();
        private static readonly Dictionary<(DataTable, string), FieldInfo> fieldInfos = new Dictionary<(DataTable, string), FieldInfo>();

        // 配置表在 cj.Init 里创建（bz.dfv 不会懒创建，之前是 null）
        public static bool TablesReady() => cj.bqpv != null && cx.bauu.Count > 0;

        public static List<DataEntry> ReadEntries(DataTable table)
        {
            var list = new List<DataEntry>();
            switch (table)
            {
                case DataTable.Skill:
                    foreach (var kv in cj.bqpv.bdsh.OrderBy(kv => (int)kv.Key))
                    {
                        if (kv.Key == ActiveSkillEnum.None) continue;
                        string name = cj.bqps.dhh("a_" + (int)kv.Key, kv.Key.ToString());
                        list.Add(new DataEntry(table, kv.Key.ToString(), name, IconPath("Skill/", kv.Value.icon)));
                    }
                    break;
                case DataTable.Hero:
                    // 英雄的键是 HeroType 的数字，头像是 Player_<HeroType 名字最后一段>
                    foreach (var row in RawRows("hero"))
                    {
                        string id = Cell(row, "id");
                        if (id == null || !cj.bqpv.bdsf.ContainsKey(id) || !int.TryParse(id, out int n)) continue;
                        string head = ((HeroType)n).ToString().Split('_').Last();
                        // 英雄名的本地化 key 是 "hero_" + 编号（见 ey.dxx；不直接调 dvm，它会返回工坊模型的名字）
                        list.Add(new DataEntry(table, id, cj.bqps.dhh("hero_" + id, id), IconPath("HeadIcon/Player_", head)));
                    }
                    break;
                case DataTable.Enemy:
                    // 怪物在每局开始时才加入 bcru，这里从原始表列出
                    foreach (var row in RawRows("enemy"))
                    {
                        string id = Cell(row, "id");
                        if (id == null) continue;
                        // 怪物名的本地化 key 就是 id（EnemyType 名字，见 EnemyDragonBoss 的 Boss 提示）；没有翻译的（多半是没用到的怪）显示 id
                        list.Add(new DataEntry(table, id, cj.bqps.dhh(id, id), IconPath("HeadIcon/Enemy_", Cell(row, "model"))));
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
            if (table == DataTable.Skill) cj.bqpv.bdsh[(ActiveSkillEnum)Enum.Parse(typeof(ActiveSkillEnum), key)] = (ActiveSkillData)row;
            else cj.bqpv.bdsf[key] = (RoleAttribute)row;
            if (restore) originals.Remove((table, key));
        }

        private static bool TryGetRow(DataTable table, string key, out object row)
        {
            row = null;
            if (table == DataTable.Skill)
            {
                if (!Enum.TryParse(key, out ActiveSkillEnum skill) || !cj.bqpv.bdsh.TryGetValue(skill, out var d)) return false;
                row = d;
                return true;
            }
            if (!cj.bqpv.bdsf.TryGetValue(key, out var r)) return false;
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

        // 原始表 cx.bauu[表名]：id → 行（列名 → 值）
        private static IEnumerable<Dictionary<string, object>> RawRows(string table)
        {
            if (!cx.bauu.TryGetValue(table, out var t) || !(t is Dictionary<string, object> rows)) yield break;
            foreach (var row in rows.Values)
                if (row is Dictionary<string, object> r) yield return r;
        }

        private static string Cell(Dictionary<string, object> row, string column)
            => row.TryGetValue(column, out var v) && v != null && v.ToString() != "" ? v.ToString() : null;

        private static string IconPath(string folder, string name) => string.IsNullOrEmpty(name) ? null : "Bundles/UI/Icon/" + folder + name;

        // ---- 创意工坊英雄模型（buj 换模型，bup 读 manifest.json，buu 工坊服务）----
        // 启动参数 -disable-mods 时为 true，游戏不加载任何工坊内容
        public static bool ModsDisabled() => bwl.blzt;

        // 同 bwg.qrq：manifest 的英雄编号对得上，模型包文件存在；另外排除召唤物模型（bwj.qtd，它的 manifest 也可能带 heroId）
        public static bool IsValidSkinFolder(string dir, int heroId)
            => ReadSkinManifest(dir, out var m) && m.heroId == heroId && !bwj.qtd(m.contentType);

        private static bool ReadSkinManifest(string dir, out LocalWorkshopManifest m)
        {
            m = null;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            if (!bwm.qts(dir, out m) || m == null || m.heroId <= 0) return false;
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

        // 召唤物模型（bwj.qtd）也带 manifest，但不是英雄模型
        public static HeroSkin ReadSkin(string id, string dir)
        {
            if (!ReadSkinManifest(dir, out var m) || bwj.qtd(m.contentType)) return null;
            string title = !string.IsNullOrWhiteSpace(m.title) ? m.title.Trim()
                : !string.IsNullOrWhiteSpace(m.heroName) ? m.heroName.Trim()
                : Path.GetFileName(dir);
            return new HeroSkin(id, m.heroId, title, m.author?.Trim() ?? "", dir);
        }

        // bwr.qvl()：已订阅的物品和状态（游戏自己每秒也调用一次）；bptc：Steam 可用
        public static List<HeroSkin> ReadWorkshopSkins()
        {
            var list = new List<HeroSkin>();
            var service = cj.bqqd;
            if (service == null || !service.brmy) return list;
            foreach (var item in service.qvl())
            {
                if (item == null || !item.installed || item.heroId <= 0) continue;
                var skin = ReadSkin(HeroSkins.WorkshopId(item.publishedFileId), item.installFolder);
                if (skin != null) list.Add(skin);
            }
            return list;
        }

        // bwr.qvm：已安装物品的文件夹
        public static string WorkshopFolder(ulong id)
            => cj.bqqd != null && cj.bqqd.qvm(id, out var item) && item != null ? item.installFolder : null;

        // bwg.qra(文件夹, manifest, out 图)：manifest.previewImage，游戏的工坊列表也用它。
        // 很多工坊物品没填 previewImage，但文件夹里有 preview.png / hero_icon.png（bwg.qsw 按相对路径读图）
        public static Sprite SkinPreview(string dir)
        {
            if (!bwm.qts(dir, out var m)) return null;
            if (bwg.qra(dir, m, out var sprite)) return sprite;
            foreach (var name in new[] { "preview.png", m.heroIcon, "hero_icon.png" })
                if (!string.IsNullOrWhiteSpace(name) && File.Exists(Path.Combine(dir, name)) && bwg.qsw(dir, name, out sprite))
                    return sprite;
            return null;
        }

        public static int LocalHeroId() => nr.bqtm is bbx p ? (int)p.heroType : 0;

        // bwg.qrh：本地玩家正在用这个英雄时，撤掉当前模型（bwg.qri）再按 bwg.qrp 的结果重新换（bwg.qrc）
        // 血条上的英雄名 roleName 只在出生时按模型的 heroName 算一次（yl.jca → ey.dxx），换完重新算；
        // 王者模式的 roleName 是 kingName，不动
        public static void ReapplyLocalHeroModel(int heroId)
        {
            bwg.qrh(heroId);
            if (nr.bqtm is bbx p && (int)p.heroType == heroId && p.roleType == RoleType.Player)
                p.roleName = HeroVariants.LocalName(heroId) ?? ey.dxx(p.heroType);
        }

        // 在 HeroSkinHooks 的角色上下文里撤掉模型（bwg.qri）再换（bwg.qrc），名字（roleName）也按这个角色重新算
        public static void ReapplyHeroModel(Component p)
        {
            var player = (bbx)p;
            if (player.brai == null) return;
            HeroSkinHooks.WithOwner(player, () =>
            {
                bwg.qri(player.brai);
                bwg.qrc(player);
                if (player.roleType == RoleType.Player)
                    player.roleName = (player.bqzk ? HeroVariants.LocalName((int)player.heroType) : null) ?? ey.dxx(player.heroType);
            });
        }

        // 游戏自己的工坊绑定（主菜单"工坊 Mod"里启用的物品），没有时为 0
        public static ulong GameSkinBinding(int heroId) => cj.bqqc != null && cj.bqqc.quf(heroId, out ulong id) ? id : 0UL;

        // bwr.qvg：打开物品的创意工坊页面（游戏的工坊界面也用它）
        public static void OpenWorkshopPage(ulong id) => cj.bqqd?.qvg(id);

        // ---- 玩家 ----
        // yl.bqzk：Mirror 的 isLocalPlayer
        public static bool IsLocalPlayer(Component r) => r is yl role && role.bqzk;
        // Mirror 的 netId（NetworkBehaviour.bqbx），同一局里每个客户端看到的都一样
        public static uint NetId(Component r) => ((yl)r).brvu;
        public static int HeroIdOf(Component p) => (int)((bbx)p).heroType;
        // bbx.brfz：Steam 昵称（服务器生成角色时写入的 SyncVar）
        public static string PlayerName(Component p) => ((bbx)p).brfz;
        public static bbx[] AllPlayers() => UnityEngine.Object.FindObjectsOfType<bbx>();

        // ---- Steam 大厅（cft = SteamMatchmaking，cgi = SteamUser）----
        // py.bqul：当前大厅，房主是 nr.bctt、客户端是 nr.bctu；进地牢后不离开大厅。本地（KCP）模式没有大厅，返回 0
        public static ulong LobbyId()
        {
            var lobby = bz.dfv<py>();
            return lobby != null ? lobby.bqul.m_SteamID : 0UL;
        }

        public static ulong MySteamId() => cik.vbh().m_SteamID;

        // chv.uni = GetLobbyOwner（游戏在 px 里用它；同类里还有没人调用的副本）
        public static ulong LobbyOwner(ulong lobby) => chv.uni(new Steamworks.CSteamID(lobby)).m_SteamID;

        // 成员数据：每个成员只能写自己的，所有成员都能读。游戏不用它，py.grs 收到成员数据的更新时直接返回
        public static void SetLobbyMemberData(ulong lobby, string key, string value)
            => chv.umx(new Steamworks.CSteamID(lobby), key, value);

        // 离开房间时游戏会先关掉 Steam 联机（Player.log 里的"steam结束"），之后写成员数据会抛异常；那时已经离开大厅，不用清
        public static void TryClearLobbyMemberData(ulong lobby, string key)
        {
            try
            {
                chv.umx(new Steamworks.CSteamID(lobby), key, "");
            }
            catch (InvalidOperationException)
            {
            }
        }

        public static string LobbyMemberData(ulong lobby, ulong member, string key)
            => chv.umw(new Steamworks.CSteamID(lobby), new Steamworks.CSteamID(member), key);

        public static List<ulong> LobbyMembers(ulong lobby)
        {
            var id = new Steamworks.CSteamID(lobby);
            var list = new List<ulong>();
            int count = chv.ump(id);
            for (int i = 0; i < count; i++) list.Add(chv.umq(id, i).m_SteamID);
            return list;
        }

        // ---- 自定义被动（PassiveSkills）----
        // 被动表 cx.bauu["passsiveSkill"]：编号 → 行（列名 → 值）。学习时 yl.jcu / iuz 读 class 列 → ey.edk(类名) 创建 qp 实例，
        // 再 msz(行) 解析数值、gou() 生效、ey.ecb 放进技能栏（roleSkillList，最多 nr.MaxSkillNum 格，被动也占格）。
        // 技能书候选池是 pj（cj.bqpx）的私有字段 bcxj：品级 → 行，在 pj 构造时（gjs）从被动表建好，之后加的行要自己放进去
        private static readonly AccessTools.FieldRef<pt, Dictionary<string, List<object>>> passivePool =
            AccessTools.FieldRefAccess<pt, Dictionary<string, List<object>>>("bdyb");
        private static readonly Dictionary<int, Dictionary<string, object>> injectedPassives = new Dictionary<int, Dictionary<string, object>>();

        public const string ModPassiveClassPrefix = "DBModKit.Passive:";

        private static Dictionary<string, object> PassiveTable()
            => cx.bauu.TryGetValue("passsiveSkill", out var t) ? t as Dictionary<string, object> : null;

        public static void InjectPassive(PassiveSkillInfo info)
        {
            var table = PassiveTable();
            if (table == null) return;
            var def = info.Def;
            // 以现有的一行为模板，保证列齐全、类型和游戏读到的一致
            var template = table.Values.OfType<Dictionary<string, object>>().FirstOrDefault();
            var row = template != null ? new Dictionary<string, object>(template) : new Dictionary<string, object>();
            void Set(string column, object value)
            {
                if (template != null && template.TryGetValue(column, out var old) && old != null && value != null && old.GetType() != value.GetType())
                {
                    try
                    {
                        value = Convert.ChangeType(value, old.GetType(), System.Globalization.CultureInfo.InvariantCulture);
                    }
                    catch (Exception)
                    {
                        // 类型转不过去就保留原值，游戏读的时候会再转换
                    }
                }
                row[column] = value;
            }
            // 空的写 ""，和游戏读表的结果一致：bir.njl 用 dwb<string>("value") 直接 ToString，null 会空引用
            string Join(float[] values) => values == null || values.Length == 0 ? ""
                : string.Join("|", values.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Set("id", info.Id.ToString());
            Set("class", ModPassiveClassPrefix + info.Id);
            Set("name", def.Name);
            Set("lock", !info.InSkillBook);
            // 国王对决里的拟人玩家不会拿到（构筑数据本来也会去掉自定义被动）
            Set("kingLock", true);
            Set("repeat", def.Repeatable);
            Set("quality", def.Quality.ToString());
            Set("attribute", "无属性");
            Set("tags", string.Join(",", def.Tags ?? new string[0]));
            Set("icon", def.Icon);
            Set("info", def.Description);
            Set("cost", 0);
            Set("cd", 0);
            Set("range", 0);
            // value / levelup 是 "|" 分隔的字符串（bir.njl → ey.dyp），不按模板转换
            row["value"] = Join(def.Values);
            row["levelup"] = Join(def.LevelUp);

            EjectPassive(info.Id);
            table[info.Id.ToString()] = row;
            injectedPassives[info.Id] = row;
            if (info.InSkillBook && cj.bqpx != null)
            {
                var pool = passivePool(cj.bqpx);
                string quality = def.Quality.ToString();
                if (!pool.TryGetValue(quality, out var list)) pool[quality] = list = new List<object>();
                list.Add(row);
            }
        }

        public static bool PassiveInjected(int id)
        {
            if (!injectedPassives.TryGetValue(id, out var row)) return false;
            var table = PassiveTable();
            if (table == null || !table.TryGetValue(id.ToString(), out var current) || current != row) return false;
            // pj 重建时会从被动表重新建池（gjs），我们的行还在表里就也在池里
            bool wantPool = !(row.TryGetValue("lock", out var l) && Convert.ToBoolean(l));
            return !wantPool || cj.bqpx == null || passivePool(cj.bqpx).Values.Any(list => list.Contains(row));
        }

        public static void EjectPassive(int id)
        {
            var table = PassiveTable();
            string key = id.ToString();
            if (table != null && table.TryGetValue(key, out var row) && IsModPassiveRow(row))
                table.Remove(key);
            if (cj.bqpx != null)
                foreach (var list in passivePool(cj.bqpx).Values)
                    list.RemoveAll(r => r is Dictionary<string, object> d && d.TryGetValue("id", out var v) && v?.ToString() == key && IsModPassiveRow(d));
            injectedPassives.Remove(id);
        }

        private static bool IsModPassiveRow(object row)
            => row is Dictionary<string, object> d && d.TryGetValue("class", out var c) && c is string s && s.StartsWith(ModPassiveClassPrefix);

        // ey.edk 的后缀：类名是我们的前缀时创建 ModPassive（原版对未知类名返回 null）
        public static qw CreateModPassive(string className)
        {
            if (className == null || !className.StartsWith(ModPassiveClassPrefix)) return null;
            if (!int.TryParse(className.Substring(ModPassiveClassPrefix.Length), out int id) || !PassiveSkills.IsCustom(id)) return null;
            return new ModPassive(id);
        }

        // yl.jcu(被动, 要替换的格子)：技能书学被动走它（真方法）。技能栏满、又不指定格子时，ey.ecb 会替换第 2 格，所以这里先判断
        public static bool GivePassive(int id, int replaceSlot)
        {
            var p = nr.bqtm;
            if (p == null) return false;
            bir replace = null;
            if (p.roleSkillList.Count >= nr.MaxSkillNum)
            {
                if (replaceSlot < 0 || replaceSlot >= p.roleSkillList.Count) return false;
                replace = p.roleSkillList[replaceSlot];
            }
            p.jcu((PasssiveSkillEnum)id, replace);
            return true;
        }

        // ey.ebz：游戏的移除（dzp 里调 qw.gus，再刷新技能栏）
        public static void RemoveLocalPassives(int id)
        {
            var p = nr.bqtm;
            if (p == null) return;
            foreach (var s in p.roleSkillList.OfType<ModPassive>().Where(s => s.DefId == id).ToList())
                ey.ebz(s);
        }

        public static void LocalModPassives(List<object> output)
        {
            output.Clear();
            var p = nr.bqtm;
            if (p == null) return;
            foreach (var s in p.roleSkillList)
                if (s is ModPassive m) output.Add(m);
        }

        public static int ModPassiveInstance(object native) => ((ModPassive)native).Instance;
        public static Component PassiveOwner(object native) => ((bir)native).bixd;
        // 等级 bht.kb03f1…；数值 bhnp = value + levelup × 等级（msz → mtm 算好）
        public static int PassiveLevel(object native) => ((bir)native).k1a61433033fa4ec8a46cdab5c5e5e6dc;

        public static float PassiveValue(object native, int i)
        {
            var values = ((bir)native).bixp;
            return values != null && i >= 0 && i < values.Length ? values[i] : 0f;
        }

        // ---- 自定义遗物（CustomRelics）----
        // 原始表 cx.bauu["remains"]：编号 → 行（skill 列是效果名，bbx.lbx 按它创建 rn 子类，未知名字返回什么都不做的 new rn()）。
        // oo 构造时转换成 bcry（ItemType → RemainsData，三选一的候选池 ey.ecp 从这里按品级筛）和 bcrx（物品表）。
        // 发放 bbx.lbw(编号, 等级)：读行 → kql → 填 values/levelup/valueTypes → kqm 放进 playerAttribute.bfna 并调 gtb()。
        // runAcquireLimit 为 0 时 kto 不发 CmdClaimRelicRunAcquisition；isTeam 为假时 ey.ecg 不发 CmdAddAllPlayerItem，所以选遗物不会发消息
        private static readonly Dictionary<int, Dictionary<string, object>> injectedRelics = new Dictionary<int, Dictionary<string, object>>();

        public const string ModRelicSkillPrefix = "DBModKit.Relic:";

        private static Dictionary<string, object> RelicTable()
            => cx.bauu.TryGetValue("remains", out var t) ? t as Dictionary<string, object> : null;

        public static bool IsRelicValueType(string name) => Enum.TryParse(name, false, out RelicValueType _);

        public static void InjectRelic(CustomRelicInfo info)
        {
            var table = RelicTable();
            if (table == null || cj.bqpv == null) return;
            var def = info.Def;
            var template = table.Values.OfType<Dictionary<string, object>>().FirstOrDefault();
            var row = template != null ? new Dictionary<string, object>(template) : new Dictionary<string, object>();
            // 空格子写 ""（游戏读表的结果就是 ""，按 dwb<string> 读的地方遇到 null 会空引用）
            string Join<T>(IEnumerable<T> values) => values == null || !values.Any() ? ""
                : string.Join("|", values.Select(v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)));
            string grade = def.Quality.ToString();
            row["id"] = info.Id;
            row["name"] = def.Name;
            row["grade"] = grade;
            row["icon"] = def.Icon;
            row["isTeam"] = 0;
            row["skill"] = ModRelicSkillPrefix + info.Id;
            // 国王对决里的拟人玩家不会拿到（构筑数据本来也会去掉自定义遗物）
            row["kingLock"] = true;
            row["info"] = def.Description;
            row["condition"] = "";
            // values / levelup / valueTypes 都是 "|" 分隔的字符串（rv.hai → ey.dyp，rv.haj）
            row["values"] = Join(def.Values);
            row["levelup"] = Join(def.LevelUp);
            row["valueTypes"] = Join(def.ValueTypes);
            row["maxLevel"] = def.MaxLevel;
            row["isUnique"] = def.Unique;
            row["mapTypes"] = "";
            row["runAcquireLimit"] = 0;

            EjectRelic(info.Id);
            table[info.Id.ToString()] = row;
            // 和 oo 的转换一致：grade 是 oc.bdkk（D C B A S）里的下标
            int gradeIndex = oc.bdkk.IndexOf(grade);
            cj.bqpv.bdsj[(ItemType)info.Id] = new RemainsData
            {
                grade = gradeIndex,
                conditions = EntryConditions.None,
                isUnique = def.Unique,
                isTeam = false,
                runAcquireLimit = 0,
                mapTypes = "",
            };
            cj.bqpv.bdsi[(ItemType)info.Id] = new ItemData { name = "pickitem_" + info.Id, quality = gradeIndex, model = "Remains" };
            injectedRelics[info.Id] = row;
        }

        public static bool RelicInjected(int id)
        {
            if (!injectedRelics.TryGetValue(id, out var row)) return false;
            var table = RelicTable();
            return table != null && table.TryGetValue(id.ToString(), out var current) && current == row
                   && cj.bqpv != null && cj.bqpv.bdsj.ContainsKey((ItemType)id);
        }

        public static void EjectRelic(int id)
        {
            var table = RelicTable();
            if (table != null && injectedRelics.TryGetValue(id, out var ours) && table.TryGetValue(id.ToString(), out var row) && row == ours)
                table.Remove(id.ToString());
            // 只删我们自己的编号段（3000000 起），不会碰到游戏的遗物
            if (id >= CustomRelics.FirstId && cj.bqpv != null)
            {
                cj.bqpv.bdsj.Remove((ItemType)id);
                cj.bqpv.bdsi.Remove((ItemType)id);
            }
            injectedRelics.Remove(id);
        }

        // bbx.lbx 的后缀：效果名是我们的前缀时创建 ModRelic（原版对未知名字返回空的 rn）
        public static rv CreateModRelic(string skill)
        {
            if (skill == null || !skill.StartsWith(ModRelicSkillPrefix)) return null;
            if (!int.TryParse(skill.Substring(ModRelicSkillPrefix.Length), out int id) || !CustomRelics.IsCustom(id)) return null;
            return new ModRelic(id);
        }

        // ey.ecp 的后缀：三选一候选池（品级 → 遗物）里去掉不该出现的自定义遗物
        public static void FilterRelicPool(Dictionary<int, List<ItemType>> pool)
        {
            if (pool == null) return;
            foreach (var list in pool.Values)
                list.RemoveAll(t => (int)t >= CustomRelics.FirstId && CustomRelics.IsCustom((int)t) && !CustomRelics.InPool((int)t));
        }

        public static bool GiveRelic(Component p, int id, int level) => cj.bqpv.bdsj.ContainsKey((ItemType)id) && ((bbx)p).lbw(id, level) != null;

        // bbx.lbz：游戏的移除（调 gtc，再刷新遗物栏）
        public static void RemoveLocalRelics(int id)
        {
            var p = nr.bqtm;
            if (p == null) return;
            foreach (var r in p.playerAttribute.bgsj.OfType<ModRelic>().Where(r => r.DefId == id).ToList())
                p.lbz(r);
        }

        public static void LocalModRelics(List<object> output)
        {
            output.Clear();
            var p = nr.bqtm;
            if (p == null || p.playerAttribute == null) return;
            foreach (var r in p.playerAttribute.bgsj)
                if (r is ModRelic m) output.Add(m);
        }

        public static int ModRelicInstance(object native) => ((ModRelic)native).Instance;
        public static Component RelicOwner(object native) => ((rv)native).behq;
        public static int RelicLevel(object native) => ((rv)native).behu;

        // 同 rv.gzi：values[i] + levelup[i] × 等级
        public static float RelicValue(object native, int i, int level)
        {
            var r = (rv)native;
            if (r.behv == null || i < 0 || i >= r.behv.Length) return 0f;
            float up = r.behx != null && i < r.behx.Length ? r.behx[i] : 0f;
            return r.behv[i] + up * level;
        }

        // ey.edw 的后缀：构筑数据（同步给队友、上传国王对决、信息面板、结算）去掉自定义遗物
        public static void FilterKingRelics(ref de.PlayerKingData data)
        {
            if (data.relic == null || !data.relic.Any(r => CustomRelics.IsCustom(r.relicName))) return;
            data.relic = data.relic.Where(r => !CustomRelics.IsCustom(r.relicName)).ToArray();
        }

        // 去掉构筑数据里的自定义被动：ey.eds（技能列表，"p_"+编号）给 ey.edw 用，ebj 的结果会同步给队友、上传成国王对决数据；
        // bbn.kpq 的镜像 Boss 快照另有一份技能来源（activeSkill = None 的是被动）
        public static de.PlayerKingSkillData[] FilterKingSkills(de.PlayerKingSkillData[] skills)
        {
            if (skills == null || !skills.Any(s => IsCustomSkillName(s.skillName))) return skills;
            return skills.Where(s => !IsCustomSkillName(s.skillName)).ToArray();
        }

        public static void FilterMirrorSnapshot(MirrorBossPlayerSnapshot snapshot)
        {
            if (snapshot == null) return;
            bool Custom(MirrorBossSkillSource s) => s.activeSkill == ActiveSkillEnum.None ? IsCustomSkillName(s.skillName) : ActiveSkills.IsCustom((int)s.activeSkill);
            if (snapshot.skills != null && snapshot.skills.Any(Custom))
                snapshot.skills = snapshot.skills.Where(s => !Custom(s)).ToArray();
            snapshot.player.skill = FilterKingSkills(snapshot.player.skill);
            // 自定义遗物：镜像 Boss 按 relics 和 player.relic 重新发遗物，没装 Mod 的人那边不认识
            if (snapshot.relics != null && snapshot.relics.Any(r => CustomRelics.IsCustom(r.relic.relicName)))
                snapshot.relics = snapshot.relics.Where(r => !CustomRelics.IsCustom(r.relic.relicName)).ToArray();
            FilterKingRelics(ref snapshot.player);
        }

        // "p_" + 被动编号 / "a_" + 主动技能编号
        private static bool IsCustomSkillName(string name)
            => name != null && name.Length > 2 && (name.StartsWith("p_") && PassiveSkills.IsCustom(name.Substring(2))
                                                   || name.StartsWith("a_") && ActiveSkills.IsCustom(name.Substring(2)));

        // 本地化 cl.dhi 的前缀："p_<编号>" / "a_<编号>" 是名字，后面加 "_m" 是说明。游戏的编号都很小，先按长度和前缀排除
        public static string SkillText(string key)
        {
            // 自定义遗物："pickitem_<编号>" 是名字，后面加 "_m" 是说明
            if (key != null && key.Length >= 16 && key.StartsWith("pickitem_"))
            {
                bool desc = key.EndsWith("_m");
                string rest = desc ? key.Substring(9, key.Length - 11) : key.Substring(9);
                return int.TryParse(rest, out int rid) && rid >= CustomRelics.FirstId ? CustomRelics.Text(rid, desc) : null;
            }
            // 自定义装备特效："equipSkill_<编号>"；自定义装备："equip_<编号>"。后面加 "_m" 是说明
            if (key != null && key.Length >= 13 && key.StartsWith("equip"))
            {
                bool desc = key.EndsWith("_m");
                if (key.StartsWith("equipSkill_"))
                {
                    string rest = desc ? key.Substring(11, key.Length - 13) : key.Substring(11);
                    return int.TryParse(rest, out int eid) && eid >= CustomEquipEffects.FirstId ? CustomEquipEffects.Text(eid, desc) : null;
                }
                if (key.StartsWith("equip_"))
                {
                    string rest = desc ? key.Substring(6, key.Length - 8) : key.Substring(6);
                    return int.TryParse(rest, out int qid) && qid >= CustomEquipment.FirstId ? CustomEquipment.Text(qid, desc) : null;
                }
                return null;
            }
            if (key == null || key.Length < 9 || key[1] != '_' || key[0] != 'p' && key[0] != 'a') return null;
            bool description = key.EndsWith("_m");
            string id = description ? key.Substring(2, key.Length - 4) : key.Substring(2);
            if (!int.TryParse(id, out int n)) return null;
            if (key[0] == 'p') return n >= PassiveSkills.FirstId ? PassiveSkills.Text(n, description) : null;
            return n >= ActiveSkills.FirstId ? ActiveSkills.Text(n, description) : null;
        }

        // ---- 自定义装备（CustomEquipEffects、CustomEquipment）----
        // 装备表 cx.bauu["equipment"]：编号 → 行。kb.Init(RoleType) 读行：skill 列 Enum.Parse 成 EquipSkillType（数字字符串也行，所以枚举外的编号可用）、
        // skillValue / skillValueUp 是 "|" 分隔的数值；不是 King 时 etc() 读图标、说明（"equip_"+编号+"_m"）、强化配置，属性优先读 ov.bdsw（kb.eve(行)）。
        // 特效配置表 cx.bauu["equipmentSkill"]：特效名 → 行（onlySkill、kingLock）。穿上 bba.k08fba…(jy)：对 kb.ewg() 的每个特效 kdc → khf 按类型创建
        // lm（全身共享）或 ke（每件装备一个，khh 设拥有者、等级、数值后 Init），卸下 bba.kb163…(jy) 对每个 ke 调 Clear。
        // 特效说明 kb.eyb 先查本地化表 cl.barb 里有没有 "equipSkill_"+编号+"_m"（没有就不显示），再按 dfe 取文字，所以两边都要有
        private static readonly Dictionary<int, Dictionary<string, object>> injectedEquipEffects = new Dictionary<int, Dictionary<string, object>>();
        private static readonly Dictionary<int, Dictionary<string, object>> injectedEquipment = new Dictionary<int, Dictionary<string, object>>();
        private static readonly Dictionary<int, List<kb.EquipAttributeData>> injectedEquipAttributes = new Dictionary<int, List<kb.EquipAttributeData>>();

        private static Dictionary<string, object> EquipTable()
            => cx.bauu.TryGetValue("equipment", out var t) ? t as Dictionary<string, object> : null;

        private static Dictionary<string, object> EquipEffectTable()
            => cx.bauu.TryGetValue("equipmentSkill", out var t) ? t as Dictionary<string, object> : null;

        public static IReadOnlyList<string> EquipAttributeNames() => kb.bbvz;

        // 属性名的别名（例如 physicalDamage → normalAttackAddDamage），和游戏读表时一样
        public static string NormalizeEquipAttribute(string name) => kb.evm(name);

        public static bool IsGameEquipEffect(string name)
            => !string.IsNullOrEmpty(name) && !char.IsDigit(name[0]) && name[0] != '-'
               && Enum.TryParse(name, false, out EquipSkillType t) && t != EquipSkillType.None;

        // 按模板行的类型写格子（表里同一列的类型一致，游戏读的时候按 dwb<T> 转换）
        private static Dictionary<string, object> CopyRow(Dictionary<string, object> template, IEnumerable<(string column, object value)> cells)
        {
            var row = template != null ? new Dictionary<string, object>(template) : new Dictionary<string, object>();
            foreach (var (column, raw) in cells)
            {
                object value = raw ?? "";
                if (template != null && template.TryGetValue(column, out var old) && old != null && old.GetType() != value.GetType()
                    && !(old is string) && !(value is string && ((string)value).Length == 0))
                {
                    try
                    {
                        value = Convert.ChangeType(value, old.GetType(), System.Globalization.CultureInfo.InvariantCulture);
                    }
                    catch (Exception)
                    {
                        // 类型转不过去就保留原值，游戏读的时候会再转换
                    }
                }
                row[column] = value;
            }
            return row;
        }

        private static string JoinValues(IEnumerable<float> values) => values == null || !values.Any() ? ""
            : string.Join("|", values.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        private static void LocalizationKey(string key, string text)
        {
            var zuj = cj.bqps?.barb;
            // 只是让 kb.evs 认这个键；文字由 cl.dhi 的前缀（SkillText）给，这里写中文原文兜底
            if (zuj != null) zuj[key] = new Dictionary<string, object> { ["Chinese"] = text ?? "" };
        }

        private static void RemoveLocalizationKey(string key)
        {
            var zuj = cj.bqps?.barb;
            if (zuj != null && zuj.TryGetValue(key, out var v) && v is Dictionary<string, object> d && d.Count == 1 && d.ContainsKey("Chinese"))
                zuj.Remove(key);
        }

        public static void InjectEquipEffect(CustomEquipEffectInfo info)
        {
            var table = EquipEffectTable();
            if (table == null || cj.bqps == null) return;
            string key = info.Id.ToString();
            var template = table.Values.OfType<Dictionary<string, object>>().FirstOrDefault();
            // 国王对决里的拟人玩家不创建（kdc 里 King 且 kingLock 时跳过）
            var row = CopyRow(template, new (string, object)[] { ("skill", key), ("onlySkill", false), ("kingLock", true), ("note", "") });
            EjectEquipEffect(info.Id);
            table[key] = row;
            injectedEquipEffects[info.Id] = row;
            LocalizationKey("equipSkill_" + key, info.Def.Name);
            LocalizationKey("equipSkill_" + key + "_m", info.Def.Description);
        }

        public static bool EquipEffectInjected(int id)
        {
            if (!injectedEquipEffects.TryGetValue(id, out var row)) return false;
            var table = EquipEffectTable();
            string key = id.ToString();
            return table != null && table.TryGetValue(key, out var current) && current == row
                   && cj.bqps != null && cj.bqps.barb.ContainsKey("equipSkill_" + key + "_m");
        }

        public static void EjectEquipEffect(int id)
        {
            var table = EquipEffectTable();
            string key = id.ToString();
            if (table != null && injectedEquipEffects.TryGetValue(id, out var ours) && table.TryGetValue(key, out var row) && row == ours)
                table.Remove(key);
            if (id >= CustomEquipEffects.FirstId)
            {
                RemoveLocalizationKey("equipSkill_" + key);
                RemoveLocalizationKey("equipSkill_" + key + "_m");
            }
            injectedEquipEffects.Remove(id);
        }

        // 游戏特效的数值：用装备表里第一件带这个特效的装备的 skillValue / skillValueUp
        private static (float[] values, float[] up) GameEquipEffectValues(Dictionary<string, object> table, string name)
        {
            float[] Parse(object cell)
            {
                string s = cell?.ToString();
                if (string.IsNullOrEmpty(s)) return new float[0];
                return s.Split('|').Select(x => float.TryParse(x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f).ToArray();
            }
            foreach (var kv in table)
                if (kv.Value is Dictionary<string, object> r && r.TryGetValue("skill", out var s) && s?.ToString() == name
                    && !(int.TryParse(kv.Key, out int n) && n >= CustomEquipment.FirstId))
                    return (Parse(r.TryGetValue("skillValue", out var v) ? v : null), Parse(r.TryGetValue("skillValueUp", out var u) ? u : null));
            return (new float[0], new float[0]);
        }

        // 自定义装备的特效：(EquipSkillType 编号, 数值, 成长)，第一个写进 skill 列，其余在 kb.Init 的后缀里追加
        private static List<(int type, float[] values, float[] up)> EquipmentEffects(CustomEquipmentInfo info, Dictionary<string, object> table)
        {
            var list = new List<(int, float[], float[])>();
            foreach (var e in info.Effects)
                if (CustomEquipEffects.IsCustom(e.Id))
                    list.Add((e.Id, (e.Def.Values ?? new float[0]).ToArray(), (e.Def.LevelUp ?? new float[0]).ToArray()));
            foreach (var name in info.GameEffects)
                if (IsGameEquipEffect(name) && Enum.TryParse(name, false, out EquipSkillType t))
                {
                    var (values, up) = GameEquipEffectValues(table, name);
                    list.Add(((int)t, values, up));
                }
            return list;
        }

        public static void InjectEquipment(CustomEquipmentInfo info)
        {
            var table = EquipTable();
            if (table == null || cj.bqpv == null) return;
            var def = info.Def;
            string key = info.Id.ToString();
            var template = (table.TryGetValue("1", out var first) ? first as Dictionary<string, object> : null)
                           ?? table.Values.OfType<Dictionary<string, object>>().FirstOrDefault();
            var attributes = (def.Attributes ?? new Dictionary<string, float>()).Where(kv => !string.IsNullOrEmpty(kv.Key)).ToList();
            var effects = EquipmentEffects(info, table);
            var main = effects.Count > 0 ? effects[0] : (0, new float[0], new float[0]);
            // 空格子写 ""（游戏读表的结果就是 ""，按 dwb<string> 读的地方遇到 null 会空引用）
            var row = CopyRow(template, new (string, object)[]
            {
                ("id", key),
                ("name", def.Name),
                ("equipmentIcon", def.Icon ?? ""),
                ("info", def.Description),
                ("skill", effects.Count > 0 ? main.Item1.ToString() : ""),
                ("skillValue", JoinValues(main.Item2)),
                ("skillValueUp", JoinValues(main.Item3)),
                // 第一版不能强化：maxLevel = 0；etc() 会把 levelUpGold 按 "|" 拆成 2 段、levelUpChance 拆成 3 段直接取下标，不能写空
                ("maxLevel", 0),
                ("levelUpData", ""),
                ("levelUpValue", ""),
                ("levelUpRatio", ""),
                ("levelUpGold", "0|0"),
                ("levelUpGem", 0),
                ("levelUpChance", "0|0|0"),
                ("shopStreng", false),
                ("attributeType", string.Join("|", attributes.Select(kv => kv.Key))),
                ("attributeValue", JoinValues(attributes.Select(kv => kv.Value))),
                ("conflict", ""),
                ("mapTypes", ""),
                ("humanityType", ""),
                ("humanityValue", 0),
                ("isArtifact", def.Artifact),
                ("artifactFaction", ""),
                ("canTransmute", false),
            });
            EjectEquipment(info.Id);
            table[key] = row;
            // 和 oo 构造时一样，属性从原始行转换
            var parsed = kb.eve(row);
            cj.bqpv.bdsw[key] = parsed;
            injectedEquipment[info.Id] = row;
            injectedEquipAttributes[info.Id] = parsed;
        }

        public static bool EquipmentInjected(int id)
        {
            if (!injectedEquipment.TryGetValue(id, out var row)) return false;
            var table = EquipTable();
            string key = id.ToString();
            return table != null && table.TryGetValue(key, out var current) && current == row
                   && cj.bqpv != null && cj.bqpv.bdsw.TryGetValue(key, out var attrs) && injectedEquipAttributes.TryGetValue(id, out var ours) && attrs == ours;
        }

        public static void EjectEquipment(int id)
        {
            var table = EquipTable();
            string key = id.ToString();
            if (table != null && injectedEquipment.TryGetValue(id, out var ours) && table.TryGetValue(key, out var row) && row == ours)
                table.Remove(key);
            // 只删我们自己的编号段（5000000 起），不会碰到游戏的装备
            if (id >= CustomEquipment.FirstId && cj.bqpv != null) cj.bqpv.bdsw.Remove(key);
            injectedEquipment.Remove(id);
            injectedEquipAttributes.Remove(id);
        }

        // bbq.ksh 的后缀：特效编号是我们的时创建 ModEquipEffect（原版对未知类型两个都返回 null）
        public static kh CreateModEquipEffect(EquipSkillType type)
        {
            int id = (int)type;
            if (id < CustomEquipEffects.FirstId || !CustomEquipEffects.IsCustom(id)) return null;
            return new ModEquipEffect(id);
        }

        // kb.Init(RoleType) 的后缀：自定义装备追加第二个起的特效；游戏装备追加 AttachTo 里写了它的自定义特效（联机握手允许时）
        public static void AttachEquipEffects(kb item, RoleType role)
        {
            if (item == null || role == RoleType.King || !int.TryParse(item.bbvl, out int id)) return;
            if (id >= CustomEquipment.FirstId)
            {
                var info = CustomEquipment.Get(id);
                var table = EquipTable();
                if (info == null || table == null) return;
                var effects = EquipmentEffects(info, table);
                for (int i = 1; i < effects.Count; i++)
                    item.k1eba8d5a73c94e5ca750525c90233eef((EquipSkillType)effects[i].type, effects[i].values, effects[i].up);
                return;
            }
            if (!Multiplayer.GameplayAllowed) return;
            foreach (var e in CustomEquipEffects.AttachedTo(id))
                // 一定要传数组（可以是空的）：esg 查不到时会退回主特效的数值
                item.k1eba8d5a73c94e5ca750525c90233eef((EquipSkillType)e.Id, (e.Def.Values ?? new float[0]).ToArray(), (e.Def.LevelUp ?? new float[0]).ToArray());
        }

        // pr.gnf(编号, 行) 的后缀：神器三选一的候选条件，联机握手不允许时去掉自定义装备
        public static bool ArtifactAllowed(int id) => id < CustomEquipment.FirstId || !CustomEquipment.IsCustom(id) || CustomEquipment.InPool(id);

        // ey.edt 的后缀：构筑数据（同步给队友、上传国王对决、镜像 Boss）去掉自定义装备；没有装备时原版返回 null
        public static de.PlayerKingEquipData[] FilterKingEquip(de.PlayerKingEquipData[] equip)
        {
            if (equip == null) return null;
            // 魔剑吞噬过的装备编号也会带上（kp.fdv）
            for (int i = 0; i < equip.Length; i++)
            {
                var devoured = equip[i].demonBladeDevouredEquipIds;
                if (devoured != null && devoured.Any(CustomEquipment.IsCustom))
                {
                    equip[i].demonBladeDevouredEquipIds = devoured.Where(e => !CustomEquipment.IsCustom(e)).ToArray();
                }
            }
            if (!equip.Any(e => CustomEquipment.IsCustom(e.equip))) return equip;
            var kept = equip.Where(e => !CustomEquipment.IsCustom(e.equip)).ToArray();
            return kept.Length == 0 ? null : kept;
        }

        // pr.gnb("equip_"+编号)：new jy → Init → 穿上 → 刷新装备栏。它不检查装备栏满没满（上限 nr.MaxEquipNum）
        public static bool GiveEquipment(int id)
        {
            var p = nr.bqtm;
            if (p == null || p.playerAttribute == null || p.playerAttribute.bgsi.Count >= nr.MaxEquipNum) return false;
            return pr.gnb("equip_" + id) != null;
        }

        public static int LocalEquipmentCount(int id)
        {
            var p = nr.bqtm;
            if (p == null || p.playerAttribute == null) return 0;
            string key = id.ToString();
            return p.playerAttribute.bgsi.Count(e => e != null && e.bbvl == key);
        }

        // bba.kb163…：游戏的卸下（对每个 ke 调 Clear，再同步），然后刷新装备栏
        public static void RemoveLocalEquipment(int id)
        {
            var p = nr.bqtm;
            if (p == null || p.playerAttribute == null) return;
            string key = id.ToString();
            var list = p.playerAttribute.bgsi.Where(e => e != null && e.bbvl == key).ToList();
            foreach (var e in list) p.playerAttribute.kf96f68101a514b2b881d72b87bf2b1f2(e);
            if (list.Count > 0) cj.bqpl?.dst<bqz>()?.pgj();
        }

        public static void LocalModEquipEffects(List<object> output)
        {
            output.Clear();
            var p = nr.bqtm;
            if (p == null || p.playerAttribute == null) return;
            foreach (var equip in p.playerAttribute.bgsi)
            {
                if (equip == null) continue;
                foreach (var k in equip.exd())
                    if (k is ModEquipEffect m && m.Instance != 0) output.Add(m);
            }
        }

        public static int ModEquipEffectInstance(object native) => ((ModEquipEffect)native).Instance;
        public static Component EquipEffectOwner(object native) => ((kh)native).bcae;
        public static int EquipEffectLevel(object native) => ((kh)native).bcaf;

        public static int EquipEffectEquipment(object native)
        {
            var equip = ((kh)native).bcad;
            return equip != null && int.TryParse(equip.bbvl, out int id) ? id : 0;
        }

        // 同 kb.eyj：数值 bbco[i] + 成长 bbcp[i] × 等级（khh 从 esg / esh 填）
        public static float EquipEffectValue(object native, int i, int level)
        {
            var k = (kh)native;
            if (k.bcag == null || i < 0 || i >= k.bcag.Length) return 0f;
            float up = k.bcah != null && i < k.bcah.Length ? k.bcah[i] : 0f;
            return k.bcag[i] + up * level;
        }

        // ---- 自定义主动技能（ActiveSkills）----
        // 技能表两份：原始表 cx.bauu["activeSkill"]（技能书候选池、图鉴读它）和 ov.bdsh（ActiveSkillEnum → ActiveSkillData，施法、冷却、描述读它，
        // 由 oo 构造时从原始表转换，见 oo.cs 读 activeSkill 的循环）。学技能 yl.jct → new bht → ey.ecb 进技能栏。
        // 释放：ey.eap → dyf（默认分支：切 RoleState.Skill、CmdCreateSkill）→ 服务器不校验直接 RpcCreateSkill → 每个客户端 ey.ebn
        // 按编号的大 switch 创建 bfe 实例放进 cj.bqpx.bcxi，pt.dda 每帧推进（bgwg 剩余时间 < 0 时移除并 Clear）。
        // 技能书主动候选池是 pj 的私有字段 bcxk（品级 → 原始表的行）
        private static readonly AccessTools.FieldRef<pt, Dictionary<string, List<object>>> skillPool =
            AccessTools.FieldRefAccess<pt, Dictionary<string, List<object>>>("bdyc");
        private static readonly Dictionary<int, Dictionary<string, object>> injectedSkills = new Dictionary<int, Dictionary<string, object>>();

        private static Dictionary<string, object> SkillTable()
            => cx.bauu.TryGetValue("activeSkill", out var t) ? t as Dictionary<string, object> : null;

        private static readonly string[] IndicatorNames = { "None", "Range", "Vector", "Target" };

        public static void InjectSkill(ActiveSkillInfo info)
        {
            var table = SkillTable();
            if (table == null || cj.bqpv == null) return;
            var def = info.Def;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string Num(float v) => v.ToString(inv);
            string indicator = IndicatorNames[(int)def.Indicator];
            string tags = string.Join(",", def.Tags ?? new string[0]);
            string values = def.Values == null || def.Values.Length == 0 ? null : string.Join("|", def.Values.Select(Num));

            var template = table.Values.OfType<Dictionary<string, object>>().FirstOrDefault();
            var row = template != null ? new Dictionary<string, object>(template) : new Dictionary<string, object>();
            row["id"] = info.Id.ToString();
            row["name"] = def.Name;
            row["quality"] = def.Quality.ToString();
            row["attribute"] = "无属性";
            row["tags"] = tags;
            row["icon"] = def.Icon;
            row["info"] = def.Description;
            row["cost"] = def.Cost;
            row["cd"] = def.Cooldown;
            row["indicator"] = indicator;
            row["castingRange"] = def.CastingRange;
            row["range"] = def.Range;
            row["lock"] = !info.InSkillBook;
            row["kingLock"] = true;
            row["auto"] = def.CanAuto;
            row["chargingNum"] = def.Charges;
            row["chargingCd"] = def.ChargeTime;
            row["total"] = false;
            row["damageBase"] = def.DamageBase;
            row["damageType"] = def.DamageType;
            row["damageValue"] = Num(def.DamageValue);
            row["interval"] = Num(def.Interval);
            row["duration"] = Num(def.Duration);
            row["damageExValue"] = values ?? "";

            // 和 oo 的转换一致
            var data = new ActiveSkillData
            {
                name = def.Name,
                id = info.Id.ToString(),
                info = def.Description,
                quality = def.Quality.ToString(),
                cost = def.Cost,
                cd = def.Cooldown,
                indicator = indicator,
                range = def.Range,
                castingRange = def.CastingRange,
                icon = def.Icon,
                damageBase = def.DamageBase,
                damageType = def.DamageType,
                damageValue = def.DamageValue,
                interval = def.Interval,
                duration = def.Duration,
                attribute = ov.gey("无属性"),
                tags = bis.nkj(tags),
                canAuto = def.CanAuto,
                total = false,
                chargingNum = def.Charges,
                chargingCd = def.ChargeTime,
                isKingLock = true,
            };
            if (values != null)
            {
                data.damageExStr = values.Split('|');
                data.damageExValue = def.Values.ToArray();
            }

            EjectSkill(info.Id);
            table[info.Id.ToString()] = row;
            cj.bqpv.bdsh[(ActiveSkillEnum)info.Id] = data;
            injectedSkills[info.Id] = row;
            if (info.InSkillBook && cj.bqpx != null)
            {
                var pool = skillPool(cj.bqpx);
                string quality = def.Quality.ToString();
                if (!pool.TryGetValue(quality, out var list)) pool[quality] = list = new List<object>();
                list.Add(row);
            }
        }

        public static bool SkillInjected(int id)
        {
            if (!injectedSkills.TryGetValue(id, out var row)) return false;
            var table = SkillTable();
            if (table == null || !table.TryGetValue(id.ToString(), out var current) || current != row) return false;
            if (cj.bqpv == null || !cj.bqpv.bdsh.ContainsKey((ActiveSkillEnum)id)) return false;
            bool wantPool = !(row.TryGetValue("lock", out var l) && Convert.ToBoolean(l));
            return !wantPool || cj.bqpx == null || skillPool(cj.bqpx).Values.Any(list => list.Contains(row));
        }

        public static void EjectSkill(int id)
        {
            string key = id.ToString();
            var table = SkillTable();
            if (table != null && injectedSkills.TryGetValue(id, out var ours) && table.TryGetValue(key, out var row) && row == ours)
                table.Remove(key);
            // 只删我们自己的编号段（2000000 起），不会碰到游戏的技能
            if (id >= ActiveSkills.FirstId && cj.bqpv != null) cj.bqpv.bdsh.Remove((ActiveSkillEnum)id);
            if (cj.bqpx != null)
                foreach (var list in skillPool(cj.bqpx).Values)
                    list.RemoveAll(r => r is Dictionary<string, object> d && d.TryGetValue("id", out var v) && v?.ToString() == key);
            injectedSkills.Remove(id);
        }

        // yl.jct(技能, 要替换的格子)：技能书学主动技能走它（真方法；yh.njw 是诱饵）
        public static bool GiveSkill(int id, int replaceSlot)
        {
            var p = nr.bqtm;
            if (p == null) return false;
            bir replace = null;
            if (p.roleSkillList.Count >= nr.MaxSkillNum)
            {
                if (replaceSlot < 0 || replaceSlot >= p.roleSkillList.Count) return false;
                replace = p.roleSkillList[replaceSlot];
            }
            p.jct((ActiveSkillEnum)id, replace);
            return true;
        }

        // 被动（qp）也是 bht，bhne 是 None；主动技能格子的 bhne 是技能编号
        public static void RemoveLocalSkills(int id)
        {
            var p = nr.bqtm;
            if (p == null) return;
            foreach (var s in p.roleSkillList.Where(s => !(s is qw) && (int)s.bixe == id).ToList())
                ey.ebz(s);
            // 正在生效的实例也结束掉（Clear 里会调 OnEnd）
            var effects = cj.bqpx?.bdya;
            if (effects == null) return;
            foreach (var kv in effects.Where(kv => kv.Value is ModSkill m && m.DefId == id).ToList())
            {
                effects.Remove(kv.Key);
                kv.Value.Clear(-1);
            }
        }

        // ey.ebn 的后缀。原方法会先做通用的事（检查表、触发"技能创建"事件），再在大 switch 里按编号创建实例；
        // switch 没有我们的分支，所以在后缀里创建 ModSkill 放进 bcxi。原方法开头的两个提前返回（pt.bdym、主机 bpue）这里照样判断。
        // dzc(实例编号, 技能, 位置, 施法者, _, 朝向, 目标 netId, 技能书编号, 工厂)
        public static void CreateModSkill(uint instanceId, ActiveSkillEnum skill, Vector3 position, yl caster, float rotation, int targetId)
        {
            int id = (int)skill;
            if (id < ActiveSkills.FirstId || !ActiveSkills.IsCustom(id)) return;
            if (caster == null || cj.bqpx == null || cj.bqpx.bdym || cj.bqpv == null || !cj.bqpv.bdsh.TryGetValue(skill, out var data)) return;
            if (Mirror.NetworkManager.booz is MyServerNetworkManager server && server.broa) return;
            if (cj.bqpx.bdya.ContainsKey(instanceId)) return;
            // ckm.bomw：Mirror 客户端的 netId → NetworkIdentity（游戏的处决技能 bhe.ncn 也这样找目标）
            Component target = null;
            if (targetId > 0 && ckm.bomw.TryGetValue((uint)targetId, out var identity) && identity != null)
                target = identity.GetComponent<yl>();
            var s = new ModSkill(id)
            {
                bify = instanceId,
                bigc = skill,
                bigd = data,
                bifz = caster,
                // 没有持续时间的技能也要活过 OnCast 这一帧
                biga = data.duration > 0f ? data.duration : 0.01f,
            };
            cj.bqpx.bdya.Add(instanceId, s);
            // yl.jfu(range)：表里的范围 × 施法者的技能范围加成（dzc 里给所有技能用的就是它）
            s.Instance = ActiveSkills.Cast(id, s, position, rotation, target, caster.jfu(data.range));
        }

        // ---- 施法动作：照游戏的 bfh（旋风斩、鸡舞、处决都用它）----
        // 新建 AnimatorOverrideController，把待机片段 H_Idle2（nw.bdcs）换成要播的片段，重新赋值控制器（动画机回到入口的待机状态，
        // 于是播的就是新片段），再 yl.jgf() 锁住动画（bepi：ive 不再切别的动作，bbq.krg 也不会把施法状态切回待机）。
        // 结束时还原控制器、yl.jdb() 解锁、切回待机。overrideAnimSkillId 记着是哪个技能实例换的，变身（bgt）会先结束它
        public static bool PlaySkillAnimation(object native, AnimationClip clip, float speed)
        {
            var s = (ModSkill)native;
            var caster = s.bifz;
            var anim = caster?.brai?.myAnim;
            if (anim == null || clip == null) return false;
            // 别的换动作技能还在生效（游戏的或者另一个 Mod 技能）：先结束它，免得两边互相把对方的控制器当成原版
            if (caster.overrideAnimSkillId >= 0 && caster.overrideAnimSkillId != (int)s.bify && cj.bqpx != null)
                cj.bqpx.gqj((uint)caster.overrideAnimSkillId);
            if (s.SavedController == null || s.SavedAnimator != anim)
            {
                s.SavedAnimator = anim;
                s.SavedController = anim.runtimeAnimatorController;
            }
            var controller = new AnimatorOverrideController(s.SavedController);
            controller[nw.bdcs] = clip;
            s.OurController = controller;
            caster.jda(speed);
            anim.runtimeAnimatorController = null;
            anim.runtimeAnimatorController = controller;
            caster.jgf();
            caster.overrideAnimSkillId = (int)s.bify;
            return true;
        }

        public static void StopSkillAnimation(object native)
        {
            var s = (ModSkill)native;
            var caster = s.bifz;
            if (s.SavedController == null || caster == null) return;
            // 换了模型（皮肤）后 myAnim 是新的动画机，旧的已经不用了，只还原我们改过的那个
            bool stacked = caster.overrideAnimSkillId != (int)s.bify;
            if (s.SavedAnimator != null)
            {
                if (!stacked) s.SavedAnimator.runtimeAnimatorController = s.SavedController;
                // 游戏自己的换动作技能（bfh，不检查别人）在我们之后开始了：它把我们的控制器当成原版存着，结束时会换回来。
                // 现在不动它，记下来，等它还原之后再换回真正的原版（见 FixStrayAnimations）
                else strayAnimations.Add((s.SavedAnimator, s.OurController, s.SavedController, caster));
            }
            s.SavedController = null;
            s.SavedAnimator = null;
            s.OurController = null;
            if (stacked) return;
            caster.jdb();
            if (!caster.jgh()) caster.jde(RoleState.Idle);
            caster.overrideAnimSkillId = -1;
        }

        private static readonly List<(Animator Anim, RuntimeAnimatorController Ours, RuntimeAnimatorController Original, yl Caster)> strayAnimations =
            new List<(Animator, RuntimeAnimatorController, RuntimeAnimatorController, yl)>();

        // 每帧：上面那种情况下，等叠在后面的换动作技能结束（overrideAnimSkillId 回到 -1）后，如果控制器又被换回成我们的，就还原成原版
        public static void FixStrayAnimations()
        {
            for (int i = strayAnimations.Count - 1; i >= 0; i--)
            {
                var (anim, ours, original, caster) = strayAnimations[i];
                if (anim == null || caster == null)
                {
                    strayAnimations.RemoveAt(i);
                    continue;
                }
                if (caster.overrideAnimSkillId >= 0) continue;
                if (anim.runtimeAnimatorController == ours) anim.runtimeAnimatorController = original;
                strayAnimations.RemoveAt(i);
            }
        }

        public static bool HasStrayAnimations => strayAnimations.Count > 0;

        // 游戏自带的动作片段（Resources/Bundles/Animator/ 下），见 ActiveSkill.PlayAnimation
        public static AnimationClip GameAnimation(string name) => Resources.Load<AnimationClip>("Bundles/Animator/" + name);

        public static Component SkillCaster(object native) => ((bfz)native).bifz;
        // yl.bqzk：本机的角色（bfz.ncw 也用它决定是否结算伤害 mmn）
        public static bool SkillIsMine(object native) => ((bfz)native).bifz != null && ((bfz)native).bifz.bqzk;
        public static float SkillRemaining(object native) => ((bfz)native).biga;
        public static void SetSkillRemaining(object native, float value) => ((bfz)native).biga = value;
        public static long SkillFormulaDamage(object native) => ((bfz)native).bifz == null ? 0 : ey.eav(((bfz)native).bigd, ((bfz)native).bifz);

        // 照 bfi（闪电）的结算：施法者的敌人（izk）里活着的、在圆内的（加上对方体型 addRange），ey.dzr 造成技能伤害
        // dxt(攻击者, 目标, 伤害, 方向角, 类型, 是否技能暴击 izi, 暴伤 dyl, 来源, 技能, 实例)
        public static int SkillDamageArea(object native, Vector3 center, float radius, long? damage)
        {
            var s = (bfz)native;
            var caster = s.bifz;
            if (caster == null) return 0;
            long amount = damage ?? ey.eav(s.bigd, caster);
            bool crit = caster.jhe(AttackType.Skill);
            int hits = 0;
            var list = caster.jhg();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var t = list[i];
                if (t == null || !t.gameObject.activeSelf || t.jgh() || t.brai == null) continue;
                if (!ey.eab(center, t.braf.position, radius + t.brai.addRange)) continue;
                ey.eae(caster, t, amount, ey.eag(t.braf.position, center), AttackType.Skill, crit, ey.eaw(s.bigd, caster), DamageSourceKind.ActiveSkill, ActiveSkillEnum.None, s);
                hits++;
            }
            return hits;
        }

        public static long SkillDamage(object native, Component target, long? damage)
        {
            var s = (bfz)native;
            var caster = s.bifz;
            if (caster == null || !(target is yl t) || t.jgh()) return 0;
            long amount = damage ?? ey.eav(s.bigd, caster);
            return ey.eae(caster, t, amount, ey.eag(t.braf.position, caster.braf.position), AttackType.Skill, caster.jhe(AttackType.Skill),
                ey.eaw(s.bigd, caster), DamageSourceKind.ActiveSkill, ActiveSkillEnum.None, s);
        }

        // oq.gae(路径, 时长, 位置, 缩放)：从对象池取 Bundles/路径 的预制体，到时自动回收（游戏的闪电等技能都用它）
        public static void SpawnEffect(string path, Vector3 position, float duration, float scale) => cj.bqpt?.gae(path, duration, position, scale);

        // ---- Mod 资源（ModAssets）----
        // 游戏的预制体对象池 m：zop 路径 → 模板（第一次用时 m.dap 从 Resources 加载），zoq 路径 → 回收的实例。
        // 只在切场景时整体清空（m.Clear），所以 Mod 注销特效时要自己把这条路径的缓存清掉
        private static readonly AccessTools.FieldRef<Dictionary<string, GameObject>> pooledTemplates =
            AccessTools.StaticFieldRefAccess<Dictionary<string, GameObject>>(AccessTools.Field(typeof(m), "bale"));
        private static readonly AccessTools.FieldRef<Dictionary<string, Queue<GameObject>>> pooledInstances =
            AccessTools.StaticFieldRefAccess<Dictionary<string, Queue<GameObject>>>(AccessTools.Field(typeof(m), "balf"));

        public static void ForgetPooledEffect(string path)
        {
            pooledTemplates()?.Remove(path);
            var pools = pooledInstances();
            if (pools == null || !pools.TryGetValue(path, out var queue)) return;
            pools.Remove(path);
            foreach (var go in queue)
                if (go != null) UnityEngine.Object.Destroy(go);
        }

        // 技能按钮 bra.pix(技能, 按键) 里 bjgj.sprite = Resources.Load("Bundles/UI/Icon/Skill/" + bir.bixf)；
        // Resources.Load 补丁没装上时，技能栏这里单独补上（最常看到的地方）
        public static void FixSkillButtonIcon(object button, object skill)
        {
            var s = (bir)skill;
            if (s == null || !ModAssets.IsOurs(s.bixf)) return;
            var sprite = ModAssets.IconFor(s.bixf);
            if (sprite != null) ((bra)button).bkun.sprite = sprite;
        }

        // ---- 变体英雄（HeroVariants）----
        // 出生 bbx.lmq：属性取 cj.bqpv.bcru[英雄编号] 传给 yl.jca（成长值在这里拷进角色字段，之后不再读表），
        // 本地玩家再读原始表 cx.bauu["hero"][编号] 的 skill / zhuDong 列发专属技能（iuz 被动 / iuy 主动）。
        // kzz 期间把这两行临时换成改过的副本，结束后换回原版，所以别的玩家、下一次出生都不受影响。
        // 防篡改的影子副本（nr.fww）在 iuh 之后才建立，改过的值和影子一致
        private sealed class VariantRestore
        {
            public string Key;
            public RoleAttribute Attribute;
            public bool HasAttribute;
            public Dictionary<string, object> HeroTable;
            public object HeroRow;
        }

        public static object ApplyVariantTables(int heroId, HeroVariantInfo variant)
        {
            string key = heroId.ToString();
            var r = new VariantRestore { Key = key };
            var d = variant.Def;
            if (cj.bqpv != null && cj.bqpv.bdsf.TryGetValue(key, out var attr))
            {
                r.Attribute = attr;
                r.HasAttribute = true;
                if (d.Strength.HasValue) attr.STR = d.Strength.Value;
                if (d.Agility.HasValue) attr.AGI = d.Agility.Value;
                if (d.Stamina.HasValue) attr.STA = d.Stamina.Value;
                if (d.StrengthGrowth.HasValue) attr.STRadd = d.StrengthGrowth.Value;
                if (d.AgilityGrowth.HasValue) attr.AGIadd = d.AgilityGrowth.Value;
                if (d.StaminaGrowth.HasValue) attr.STAadd = d.StaminaGrowth.Value;
                if (d.Attack.HasValue) attr.attackPower = d.Attack.Value;
                if (d.Armor.HasValue) attr.armor = d.Armor.Value;
                if (d.Hp.HasValue) attr.hp = d.Hp.Value;
                if (d.Mp.HasValue) attr.mp = d.Mp.Value;
                if (d.HpRecover.HasValue) attr.hpRecover = d.HpRecover.Value;
                if (d.MpRecover.HasValue) attr.mpRecover = d.MpRecover.Value;
                if (d.MoveSpeed.HasValue) attr.moveSpeed = d.MoveSpeed.Value;
                if (d.AttackSpeed.HasValue) attr.attackSpeed = d.AttackSpeed.Value;
                cj.bqpv.bdsf[key] = attr;
            }
            if (variant.SkillOverride(out int skill, out bool active)
                && cx.bauu.TryGetValue("hero", out var t) && t is Dictionary<string, object> heroes
                && heroes.TryGetValue(key, out var row) && row is Dictionary<string, object> original)
            {
                // 整行换成副本，原来的行对象不动
                var copy = new Dictionary<string, object>(original)
                {
                    ["skill"] = skill.ToString(),
                    ["zhuDong"] = active,
                };
                r.HeroTable = heroes;
                r.HeroRow = original;
                heroes[key] = copy;
            }
            return r;
        }

        public static void RestoreVariantTables(object restore)
        {
            if (!(restore is VariantRestore r)) return;
            if (r.HasAttribute && cj.bqpv != null) cj.bqpv.bdsf[r.Key] = r.Attribute;
            if (r.HeroTable != null) r.HeroTable[r.Key] = r.HeroRow;
        }

        public static bool IsLocalSpawn(Component player) => player is bbx p && p.bqzk;

        // 出生结束后：血条上的名字（yl.jca 里按 ey.dxx 算过一次，之后每帧读 roleName）
        public static void ApplyVariantName(Component player)
        {
            var p = (bbx)player;
            string name = HeroVariants.LocalName((int)p.heroType);
            if (name != null && p.roleType == RoleType.Player) p.roleName = name;
        }

        // ---- 技能实例 bht ----
        // 被动（qp）的 bhne 是默认值 0，而 0 是 D_SpellThunder，所以被动返回 None（-1）
        public static int SkillId(object s) => s is qw ? (int)ActiveSkillEnum.None : (int)((bir)s).bixe;
        public static bool IsAutoCastOn(object s) => ((bir)s).bixy;

        // 在技能栏上：按钮 bhng 正显示这个技能
        public static bool IsOnSkillBar(object s) => ((bir)s).bixg is bra b && ReferenceEquals(b.bkuo, s);

        // 自动释放开关：右键技能图标时游戏调 bir.nkf()（翻转 bhny 并显示按钮上的自动标记 bhng.bjgl）；不在技能栏上（没有按钮）时只改字段
        public static void SetAutoCast(object s, bool on)
        {
            var b = (bir)s;
            if (b.bixy == on) return;
            // 按钮换绑到别的技能后旧技能的 bhng 不清，这时 mtt 会改错格子的标记：只在按钮正显示这个技能时走 mtt
            if (b.bixg != null && b.bixg.bkup != null && ReferenceEquals(b.bixg.bkuo, b)) b.nkf();
            else b.bixy = on;
        }

        // 免费放一个现成技能：yl.CmdCreateSkill(技能, 位置, 朝向角度, 目标, 技能书) → 服务器 → RpcCreateSkill，每个客户端都生成技能实例。
        // 不扣蓝、不进冷却、不播动作、不触发施法事件，身上也不需要有这个技能。原版用法：装备落雷 sc.cs（D_SpellThunder 落在敌人位置，目标 -1）、
        // 月光大剑 mh.cs（在自己位置朝敌人方向，角度 ey.eag(目标, 自己)）。伤害在施法者本机算、归施法者
        public static void CastSkillFree(Component me, int skillId, Vector3 pos, float rotation) => ((yl)me).CmdCreateSkill((ActiveSkillEnum)skillId, pos, rotation, -1, 0);

        // 从 from 看向 to 的朝向角度（游戏的 ey.eag）
        public static float Facing(Vector3 from, Vector3 to) => ey.eag(to, from);

        // 雷之一闪的雷球上限：PlayerThunderMode.maxOrbCount（序列化字段，默认 10，读的时候限制在 1~32），英雄模式在 yl.brai。
        // 改过的实例都记下原值：英雄模型从对象池取（下一局可能还是这个实例）、变身时 bpia 会换成别的组件，所以撤销时把记下的全部改回去，
        // 不依赖撤销那一刻的本地玩家
        private static readonly List<(WeakReference mode, int original)> thunderTouched = new List<(WeakReference, int)>();
        private static AccessTools.FieldRef<PlayerThunderMode, int> maxOrbCount;

        // 本地玩家当前的雷电模式（变身中、不是雷电时为 null）
        public static object ThunderMode(Component p) => ((yl)p).brai as PlayerThunderMode;

        public static void SetThunderOrbs(object target, int extra)
        {
            if (maxOrbCount == null)
                using (AsciiPath.Enter())
                    maxOrbCount = AccessTools.FieldRefAccess<PlayerThunderMode, int>("maxOrbCount");
            // 先把其他改过的实例改回原值、清掉已经销毁的
            for (int i = thunderTouched.Count - 1; i >= 0; i--)
            {
                var mode = thunderTouched[i].mode.Target as PlayerThunderMode;
                if (mode == null)
                {
                    thunderTouched.RemoveAt(i);
                    continue;
                }
                if (ReferenceEquals(mode, target) && extra > 0) continue;
                maxOrbCount(mode) = thunderTouched[i].original;
                thunderTouched.RemoveAt(i);
            }
            if (!(target is PlayerThunderMode current) || extra <= 0) return;
            int index = thunderTouched.FindIndex(t => ReferenceEquals(t.mode.Target, current));
            if (index < 0)
            {
                thunderTouched.Add((new WeakReference(current), maxOrbCount(current)));
                index = thunderTouched.Count - 1;
            }
            maxOrbCount(current) = thunderTouched[index].original + extra;
        }

        // 攻击距离：yl.iwv(加量)，遗物"瞄准镜"用的同一个，会同步给服务器（fbk 是诱饵）
        public static void AddAttackRange(Component p, float amount) => ((yl)p).ixz(amount);
        public static float CooldownRemaining(object s) => ((bir)s).k18d428a62c6a4a89a1c9e898b51c307d;
        public static int Cost(object s, Component p) => ((bir)s).k3b58ed391382412ca53e330e0cb7d426((bbx)p);
        // PayToWin 技能花的是金币不是蓝
        public static bool CostsGold(object s) => ((bir)s).brjw;
        // 开关类技能（献祭等）当前开着
        public static bool IsToggledOn(object s) => ((bir)s).bixv;
        public static int Charges(object s) => ((bir)s).kdcc53ba24803444e8ab688e8fc3b2bf0;
        public static int MaxCharges(object s) => ((bir)s).k3d8a1569d86242c188efa8b7ab5991f0;
        // bir.bixg 技能按钮、bhnd 所属玩家
        public static object ButtonSkill(object s) => ((bir)s).bixg?.bkuo;
        public static Component SkillOwner(object s) => ((bir)s).bixd;

        // ---- 英雄专属技能：hero 表 skill 列是编号，zhuDong 为真时是主动技能（ActiveSkillEnum），否则是被动（PasssiveSkillEnum）。
        // 出生时 bbx.lmq 按它发放（主动 yl.jcr、被动 yl.jcs） ----
        public static bool ReadHeroSkill(int heroId, out int skillId, out bool active)
        {
            skillId = 0;
            active = false;
            if (!cx.bauu.TryGetValue("hero", out var t) || !(t is Dictionary<string, object> rows)
                || !rows.TryGetValue(heroId.ToString(), out var r) || !(r is Dictionary<string, object> row))
                return false;
            if (!int.TryParse(Cell(row, "skill"), out skillId)) return false;
            active = row.TryGetValue("zhuDong", out var z) && z != null && (z is bool b ? b : bool.TryParse(z.ToString(), out b) && b);
            return true;
        }

        // 被动技能表 passsiveSkill：class（脚本类名）、icon、value（"|" 分隔）。名字的本地化 key 是 "p_" + 编号（yl.jcs）
        public static bool ReadPassive(int id, out string icon, out float[] values)
        {
            icon = null;
            values = new float[0];
            if (!cx.bauu.TryGetValue("passsiveSkill", out var t) || !(t is Dictionary<string, object> rows)
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
            if (!cj.bqpv.bdsh.TryGetValue((ActiveSkillEnum)id, out var d)) return false;
            cost = d.cost;
            duration = d.duration;
            charges = d.chargingNum;
            damageBase = d.damageBase;
            damageValue = d.damageValue;
            return true;
        }

        public static string SkillIcon(int id) => cj.bqpv.bdsh.TryGetValue((ActiveSkillEnum)id, out var d) ? IconPath("Skill/", d.icon) : null;

        // 玩家身上的技能 bbx.roleSkillList：主动技能看 bhne，被动技能（qp）看 bhnj（= 被动表的 id，见 bir.njl）
        public static object FindActiveSkill(Component p, int id)
        {
            foreach (var s in ((bbx)p).roleSkillList)
                if (s != null && !(s is qw) && (int)s.bixe == id) return s;
            return null;
        }

        // 游戏后来替换掉的专属技能：拿到"鸡舞"卡牌后 ja.eqz 把 ChickenDance 换成 RealChickenDance
        public static object FindReplacedSkill(Component p, int id)
            => id == (int)ActiveSkillEnum.ChickenDance ? FindActiveSkill(p, (int)ActiveSkillEnum.RealChickenDance) : null;

        public static object FindPassiveSkill(Component p, int id)
        {
            string key = id.ToString();
            foreach (var s in ((bbx)p).roleSkillList)
                if (s is qw && s.bixj == key) return s;
            return null;
        }

        // ---- bht 上从表里拷来的数值（yl.jct / yl.jcr 获得技能时拷贝；yh.njw 是诱饵）。nr.fxd 不校验这些，游戏自己（iy.erf）也直接写 ----
        public static float CooldownTotal(object s) => ((bir)s).kcb5414097f184c63aec73da2482ce6f4;
        public static void SetCooldownTotal(object s, float v) => ((bir)s).kcb5414097f184c63aec73da2482ce6f4 = v;
        public static void SetCooldownRemaining(object s, float v) => ((bir)s).k18d428a62c6a4a89a1c9e898b51c307d = v;
        // 按键时判断蓝够不够用它；真正扣蓝时 ey.ear 重新查表（所以 TuningHooks 还要临时替换表）
        public static int BaseCost(object s) => ((bir)s).k3ff90762c3e740f1ba56b4c49d492645;
        public static void SetBaseCost(object s, int v) => ((bir)s).k3ff90762c3e740f1ba56b4c49d492645 = v;
        public static void SetMaxCharges(object s, int v)
        {
            var skill = (bir)s;
            skill.k3d8a1569d86242c188efa8b7ab5991f0 = v;
            if (skill.kdcc53ba24803444e8ab688e8fc3b2bf0 > v) skill.kdcc53ba24803444e8ab688e8fc3b2bf0 = v;
        }

        // 被动技能的数值：获得时 bir.njl 把表里的 value 解析进私有 bhnq，再由 mtm 算出 bhnp = bhnq + levelup × 等级；
        // 多数被动每次触发都读 bhnp。升级（grs）会用 bhnq 重算覆盖，所以两个一起改
        private static readonly AccessTools.FieldRef<bir, float[]> passiveBase = AccessTools.FieldRefAccess<bir, float[]>("bixq");

        public static float[] PassiveValues(object s) => ((bir)s).bixp;

        // 504 内部折扣在 gou 里把折扣加进 shopDiscount、gov 按值删除；511 电锯恶魔在 gou 里把数值缓存到私有字段。
        // 这两个要先 gov（按旧值撤销），改完再 gou（按新值生效）
        private static readonly HashSet<string> rebindPassives = new HashSet<string> { "504", "511" };

        public static void SetPassiveValues(object s, float[] values)
        {
            var skill = (qw)s;
            bool rebind = rebindPassives.Contains(skill.bixj);
            if (rebind) skill.gus();
            var current = skill.bixp;
            var baseValues = passiveBase(skill);
            for (int i = 0; i < values.Length; i++)
            {
                if (current != null && i < current.Length) current[i] = values[i];
                if (baseValues != null && i < baseValues.Length) baseValues[i] = values[i];
            }
            if (rebind) skill.gur();
        }

        // ---- 只改本地玩家的技能参数：施法者是本地玩家时，在 ey.ebn / dye / dyf 执行期间把 bcrw[技能] 换成调过的副本，结束后换回原版。
        // 表本身一直是原版，镜像 Boss（zq 复制英雄技能）和其他玩家的同名技能不受影响 ----
        private static readonly List<(ActiveSkillEnum key, ActiveSkillData original)> swaps = new List<(ActiveSkillEnum, ActiveSkillData)>();

        public static bool BeginSwap(Component caster, int id)
        {
            if (caster == null || caster != nr.bqtm) return false;
            var tune = SkillTuning.Get(id);
            if (tune.Cost == 1f && tune.Duration == 1f) return false;
            var key = (ActiveSkillEnum)id;
            // 嵌套调用（dyf 里会再走到 dzc）时表已经换过了
            foreach (var s in swaps)
                if (s.key == key) return false;
            if (!cj.bqpv.bdsh.TryGetValue(key, out var original)) return false;
            var tuned = original;
            tuned.cost = Mathf.Max(0, Mathf.RoundToInt(original.cost * tune.Cost));
            tuned.duration = original.duration * tune.Duration;
            cj.bqpv.bdsh[key] = tuned;
            swaps.Add((key, original));
            return true;
        }

        public static void EndSwap()
        {
            if (swaps.Count == 0) return;
            var (key, original) = swaps[swaps.Count - 1];
            swaps.RemoveAt(swaps.Count - 1);
            cj.bqpv.bdsh[key] = original;
        }

        // 换过之后 dyf 的参数也要换成调过的那份
        public static ActiveSkillData CurrentRow(ActiveSkillEnum key) => cj.bqpv.bdsh[key];

        // ey.eaq(玩家, 技能, 目标点, 技能数据, 朝向, 目标 netId, 技能书编号, h = true)：游戏的免费释放（被动"长老的多重施法"就这么用）。
        // 不查蓝、不扣蓝、不进冷却，技能自己的检查和状态切换照常。不要用 h = false：冷却会扣到最后按下的那一格
        public static bool FreeCast(Component p, int skill, Vector3 pos, float rotation, int target, int book)
            => cj.bqpv.bdsh.TryGetValue((ActiveSkillEnum)skill, out var d)
               && ey.eaq((bbx)p, (ActiveSkillEnum)skill, pos, d, rotation, target, book, h: true);

        // ---- 属性加成（都是累加，负数撤销；不在 nr.fxd 的校验范围内）----
        // yl.iys：攻速加成（比例，0.25 = +25%）；ey.eeg 的 "AttackSpeed" 就是它
        public static void AddAttackSpeed(Component p, float ratio) => ((yl)p).iys(ratio);
        // yl.izo：护甲（会刷新属性面板）
        public static void AddArmor(Component p, int amount) => ((yl)p).izo(amount);

        // ---- 男枪（PlayerShotgunMode，普攻组件）的弹药 bcc ----
        // bgac 弹数（私有 setter）、bgad 装填中、bgae 装填进度；上限是编译期常量 2（bcw.bhik）。真方法：lpy 消耗、lpz 开始装填、
        // lqb 推进装填（满了直接 bgac = 2）、lqc 冲锋 +1（Min(2, …)）、lpx 补满、lqd 远端同步（Clamp 0..2）；其余同签名的都是诱饵。
        // huq 里空闲时"弹数 < 2 且停了 tacticalReloadDelay 秒"就战术装填；llk 把 bgac | (装填中 ? 4 : 0) 发给别人（弹数 ≥ 4 会溢出到装填位）。
        // 弹药 UI bij：mwj 建 2 个弹壳（bhro / bhrp，readonly 数组），LateUpdate 按 bhro.Length 逐个上色
        private static readonly AccessTools.FieldRef<PlayerShotgunMode, bcw> shotgunAmmo = AccessTools.FieldRefAccess<PlayerShotgunMode, bcw>("bhba");
        private static readonly AccessTools.FieldRef<PlayerShotgunMode, float> shotgunIdle = AccessTools.FieldRefAccess<PlayerShotgunMode, float>("bhbh");
        private static readonly AccessTools.FieldRef<PlayerShotgunMode, float> tacticalDelay = AccessTools.FieldRefAccess<PlayerShotgunMode, float>("tacticalReloadDelay");
        private static readonly AccessTools.FieldRef<bcw, bool> ammoReloading = AccessTools.FieldRefAccess<bcw, bool>("<bhio>k__BackingField");
        private static readonly AccessTools.FieldRef<bcw, float> ammoProgress = AccessTools.FieldRefAccess<bcw, float>("<bhip>k__BackingField");
        private static readonly AccessTools.FieldRef<bcw, int> ammoCount = AccessTools.FieldRefAccess<bcw, int>("<bhin>k__BackingField");
        private static readonly AccessTools.FieldRef<yl, RoleModeBase> roleMode = AccessTools.FieldRefAccess<yl, RoleModeBase>("roleModeBase");

        // 本地玩家的弹药对象（不是男枪时为 null）
        public static object LocalShotgunAmmo()
        {
            var p = nr.bqtm;
            return p != null && roleMode(p) is PlayerShotgunMode m ? shotgunAmmo(m) : null;
        }

        public static int AmmoCount(object ammo) => ammoCount((bcw)ammo);

        // 以下几个替换 bcc 的真方法（补丁前缀里调用，只用于本地玩家的弹药），max 换掉原来的 2
        public static void AmmoStartReload(object a, int max)
        {
            var b = (bcw)a;
            if (ammoCount(b) >= max || ammoReloading(b)) return;
            ammoReloading(b) = true;
            ammoProgress(b) = 0f;
        }

        public static bool AmmoAdvanceReload(object a, float dt, float speed, float duration, int max)
        {
            var b = (bcw)a;
            if (!ammoReloading(b)) return false;
            ammoProgress(b) = Mathf.Clamp01(ammoProgress(b) + Mathf.Max(0f, dt) * Mathf.Max(0.1f, speed) / Mathf.Max(0.1f, duration));
            if (ammoProgress(b) < 1f) return false;
            ammoCount(b) = max;
            b.mfj();
            return true;
        }

        public static void AmmoAdd(object a, int n, int max)
        {
            var b = (bcw)a;
            ammoCount(b) = Math.Min(max, ammoCount(b) + n);
            b.mfj();
        }

        public static void AmmoFill(object a, int max)
        {
            var b = (bcw)a;
            ammoCount(b) = max;
            b.mfj();
        }

        // huq 之后：弹数在 2 以上但没满时，原版不会战术装填，这里补上
        public static void AmmoTacticalReload(object mode, int max)
        {
            var m = (PlayerShotgunMode)mode;
            var b = shotgunAmmo(m);
            var p = m.playerBase;
            if (p == null || p != nr.bqtm || p.jgh() || ammoReloading(b)) return;
            if (p.brag != RoleState.Idle && p.brag != RoleState.Run) return;
            int n = ammoCount(b);
            if (n >= 2 && n < max && shotgunIdle(m) >= tacticalDelay(m)) AmmoStartReload(b, max);
        }

        public static bool IsLocalShotgun(object mode) => mode is PlayerShotgunMode m && m.playerBase != null && m.playerBase == nr.bqtm;

        // llk 的替代：发给别人的弹数截到 3，免得溢出到"装填中"那一位
        private static readonly AccessTools.FieldRef<PlayerShotgunMode, int> ammoSent = AccessTools.FieldRefAccess<PlayerShotgunMode, int>("bhbn");

        public static void AmmoSync(object mode)
        {
            var m = (PlayerShotgunMode)mode;
            if (!m.playerBase.brvt) return;
            var b = shotgunAmmo(m);
            int num = Math.Min(3, ammoCount(b)) | (ammoReloading(b) ? 4 : 0);
            if (num != ammoSent(m) && m.playerBase.jhi(num)) ammoSent(m) = num;
        }

        // 弹药 UI：弹壳不够 max 个时照 mwj 的样子补上（多的删掉，最少 2 个）
        private static readonly FieldInfo uiShells = AccessTools.Field(typeof(bkd), "bjez");
        private static readonly FieldInfo uiBrass = AccessTools.Field(typeof(bkd), "bjfa");
        private static readonly AccessTools.FieldRef<bkd, RectTransform> uiRoot = AccessTools.FieldRefAccess<bkd, RectTransform>("bjex");

        public static void AmmoUi(object ui, int max)
        {
            var u = (bkd)ui;
            var shells = (UnityEngine.UI.Image[])uiShells.GetValue(u);
            int want = Math.Max(2, max);
            if (shells == null || shells.Length == want) return;
            var brass = (UnityEngine.UI.Image[])uiBrass.GetValue(u);
            var newShells = new UnityEngine.UI.Image[want];
            var newBrass = new UnityEngine.UI.Image[want];
            for (int n = 0; n < want; n++)
            {
                if (n < shells.Length)
                {
                    newShells[n] = shells[n];
                    newBrass[n] = brass[n];
                    continue;
                }
                var socket = NewImage("ShellSocket" + n, uiRoot(u), new Vector2(-31f + n * 23f, 0f), new Vector2(13f, 22f), new Color(0.045f, 0.06f, 0.065f, 0.95f));
                socket.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -38f);
                newShells[n] = NewImage("Shell", socket.rectTransform, new Vector2(0f, 2f), new Vector2(9f, 14f), new Color(0.9f, 0.32f, 0.1f));
                newBrass[n] = NewImage("Brass", socket.rectTransform, new Vector2(0f, -7f), new Vector2(11f, 4f), new Color(1f, 0.73f, 0.3f));
            }
            for (int n = want; n < shells.Length; n++)
                if (shells[n] != null) UnityEngine.Object.Destroy(shells[n].transform.parent.gameObject);
            uiShells.SetValue(u, newShells);
            uiBrass.SetValue(u, newBrass);
        }

        private static UnityEngine.UI.Image NewImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = color;
            img.rectTransform.sizeDelta = size;
            img.rectTransform.anchoredPosition = pos;
            return img;
        }

        public static bool IsLocalAmmoUi(object ui) => ui is bkd u && u != null && LocalShotgunAmmo() != null;

        // 多重攻击：多重攻击被动（B多重攻击）就是 attackNum += n，再 CmdAttackNum 同步
        public static void AddAttackNum(Component p, int n)
        {
            var b = (bbx)p;
            b.attackNum += n;
            if (b.brvt) b.CmdAttackNum(b.attackNum);
        }

        public static int AttackNum(Component p) => ((yl)p).attackNum;

        // 在地上掉道具（ItemType 编号）：bbx.CmdCreateItemByPosWithNum，Remains_19、转变之石都这么掉
        public static void DropItem(Component p, int itemType, Vector3 pos, int count) => ((bbx)p).CmdCreateItemByPosWithNum((ItemType)itemType, pos, count, false);

        // 范围伤害：nr.fwv（死灵祭司"对死者的供奉"用的）——范围内的敌人各吃一次技能伤害（暴击照常判定），再播特效
        public static void DamageArea(Component p, float damage, Vector3 center, float radius, string effect, float effectScale, bool spareTeammates = false)
        {
            // 游戏按 (int) 转：float 的 int.MaxValue 是 2147483648，转过去溢出成负数
            damage = Mathf.Min(damage, 2e9f);
            if (effect != null && !spareTeammates)
            {
                nr.fwv((yl)p, damage, center, radius, effect, effectScale);
                return;
            }
            // 照抄 fsp 的伤害部分：不播特效（频繁调用时每次都 CmdPlayEffect 会刷屏，也占网络），或者跳过队友的怪
            var a = (yl)p;
            bool crit = a.jhe(AttackType.Skill);
            foreach (yl item in cj.bqpy.gbg(radius, center))
            {
                if (spareTeammates && IsTeammateMonster(p, item)) continue;
                ey.eae(a, item, (int)damage, ey.eag(item.braf.position, center), AttackType.Skill, crit, 0f);
            }
            if (effect != null) ((bbx)p).CmdPlayEffect(effect, 1f, center, effectScale);
        }

        // 联机时每个玩家有自己的怪（authorityId = 主人的 netId），打别人的怪收益归动手的人。bez.mun 是原版"队友的怪"判断
        public static bool IsTeammateMonster(Component me, Component enemy) => bez.mun((bbx)me, ((yl)enemy).authorityId);

        // 送金币：照抄商店"丢金币"（bbx.lcz）——先用官方方法扣自己的钱，再在指定位置生成无主金币（谁都能捡）。
        // drop 可以大于 pay：服务器不校验生成的数量（伙伴之刃分收益也是凭空发）
        public static void GiveGold(Component me, Vector3 pos, int pay, int drop)
        {
            var p = (bbx)me;
            if (pay > 0) p.kad2a3d5a59724b85bfc565c5f7d7246e(p.jea(), -pay, c: false);
            if (drop > 0) p.CmdCreateItemByPosWithNum(ItemType.Gold, pos, drop, false);
        }

        // 特效：bbx.CmdPlayEffect(路径, 速度, 位置, 缩放)，广播给所有人（fsp 也这么播）
        public static void PlayEffect(Component p, string effect, Vector3 pos, float scale) => ((bbx)p).CmdPlayEffect(effect, 1f, pos, scale);

        // ---- 队友（都是原版的命令，队友没装 Mod 也能正常处理）----
        // yl.jes(量, 目标)：治疗目标，内部 CmdUpdateHp(+量, 目标 netId)，服务器不校验发送者（装备 MadMan 治疗队友就这么用）
        public static void HealOther(Component me, Component target, double amount) => ((yl)me).jes(amount, (yl)target);

        // bbx.CmdAddNearbyHumanityShield(范围, 比例)：服务器 kxp 给自己周围的所有玩家（含自己）各加 最大生命 × 比例 的护盾，范围 ≤ 50、比例 ≤ 1
        public static void ShieldNearbyPlayers(Component me, float radius, float ratio) => ((bbx)me).CmdAddNearbyHumanityShield(radius, ratio);

        // bbx.CmdAddBuff(目标, 来源, 类型, 值, 时长, 等级) → 服务器 lcp → 目标 RpcAddBuff → yb 里按类型 new 效果类（yb.cs 4240 起的 switch）：
        // SkillHaste → hy：skillCdReduce += 值（技能急速）；IronArmor → ha：护甲 bepy += 值；
        // SacredEmberBlessing → hu：值加在 buffAddDamage 上——只有 AttackType.Buff（持续伤害）读它，普攻和技能不吃
        public static void GiveBuff(Component me, Component target, PlayerBuff buff, float value, float seconds)
        {
            LocalBuffType type = buff == PlayerBuff.SkillHaste ? LocalBuffType.SkillHaste
                : buff == PlayerBuff.Armor ? LocalBuffType.IronArmor : LocalBuffType.SacredEmberBlessing;
            ((bbx)me).CmdAddBuff(((yl)target).brvu, ((yl)me).brvu, type, value, seconds, 0);
        }

        // ---- 通用加成（海克斯通用强化）----
        // ey.eeg(键, 整数)：遗物、卡牌、锻体读"键|值"时走的就是它，作用于本地玩家 nr.bqtm。百分比类的键传 25 表示 25%。
        // 有的分支直接写字段（skillCdReduce、addDamagePercent……），不经过防篡改校验，也不会自动撤销，撤销就传负数
        private static Action<string, int> addAttribute;

        public static void AddAttribute(string key, int amount)
        {
            addAttribute = addAttribute ?? AccessTools.MethodDelegate<Action<string, int>>(
                AccessTools.Method(typeof(ey), "eeg", new[] { typeof(string), typeof(int) }));
            addAttribute(key, amount);
        }

        // 身上的主动技能（roleSkillList 里除了被动 qp 的都算，包括技能书学到的）
        public static List<object> ActiveSkillsOf(Component p)
        {
            var result = new List<object>();
            foreach (var s in ((bbx)p).roleSkillList)
                if (s != null && !(s is qw) && s.bixe != ActiveSkillEnum.None) result.Add(s);
            return result;
        }

        // 属性锻造器：商店买锻造器走 pr.gna → 检查 ey.ebh（三选一界面空闲）和 nr.bcrh → pr.gpd(ForgingTier)。
        // gjl 抽稀有度、弹"属性选择"三选一，选中后经 pr.gpe 加属性（官方路径，过防篡改）。直接调 gjl 和买一个一样，只是价格不涨
        private static Action<pr, pr.ForgingTier> forge;

        public static bool CanForge() => !nr.bcrh && !ChoiceBusy() && bz.dfv<pr>() != null;

        public static void Forge(int tier)
        {
            forge = forge ?? AccessTools.MethodDelegate<Action<pr, pr.ForgingTier>>(
                AccessTools.Method(typeof(pr), "gpd", new[] { typeof(pr.ForgingTier) }));
            forge(bz.dfv<pr>(), tier == 2 ? pr.ForgingTier.Epic : tier == 1 ? pr.ForgingTier.Gold : pr.ForgingTier.Normal);
        }

        // 沙漠死神的普攻组件 PlayerKoboldMode：噬魂一击把 bftv 置为 true（下一次普攻强化），lko 在出手时结算：
        // 伤害 kcm(kse(out 普攻增伤))，dxt 返回值 ≥ 目标生命 + 护盾算击杀，击杀调 lkp(目标) 叠层（普通 4 / 精英 12 / Boss 75），
        // 多重攻击的额外目标是 kqs 返回的共享列表 nr.bcrm。结算完调 lkt 清掉强化——补丁挂在 lkt 前面，这时 bftv 还是 true
        private static readonly AccessTools.FieldRef<PlayerKoboldMode, bool> koboldEmpowered = AccessTools.FieldRefAccess<PlayerKoboldMode, bool>("bhah");
        private static Action<PlayerKoboldMode, yl> koboldKillStack;
        private static Action<PlayerKoboldMode, int> koboldAddStacks;
        private static Func<PlayerKoboldMode, long, long> koboldDamage;
        private static readonly List<yl> splashTargets = new List<yl>();

        // 镜像 Boss 用的子类、别人的英雄、没有强化的普攻都不算
        public static bool IsLocalEmpoweredStrike(object mode)
            => mode is PlayerKoboldMode m && m.GetType() == typeof(PlayerKoboldMode) && koboldEmpowered(m)
               && m.playerBase != null && m.playerBase == nr.bqtm && m.playerBase.brvt && m.playerBase.trackRoleBase != null;

        // 对主目标周围的敌人再打一次（伤害 × ratio），击杀照常叠层；hitStacks > 0 时没打死也加这么多层（休息阶段不加）。返回打到几个
        public static int SoulSplash(object mode, float radius, float ratio, int hitStacks)
        {
            var m = (PlayerKoboldMode)mode;
            koboldKillStack = koboldKillStack ?? AccessTools.MethodDelegate<Action<PlayerKoboldMode, yl>>(AccessTools.Method(typeof(PlayerKoboldMode), "lyg"));
            koboldAddStacks = koboldAddStacks ?? AccessTools.MethodDelegate<Action<PlayerKoboldMode, int>>(AccessTools.Method(typeof(PlayerKoboldMode), "lyh"));
            koboldDamage = koboldDamage ?? AccessTools.MethodDelegate<Func<PlayerKoboldMode, long, long>>(AccessTools.Method(typeof(PlayerKoboldMode), "knm"));
            var p = m.playerBase;
            var main = p.trackRoleBase;
            Vector3 center = main.braf.position;
            splashTargets.Clear();
            foreach (var e in p.jhg())
            {
                if (e == null || e == main || !e.gameObject.activeInHierarchy || e.brai == null || !e.jgi()) continue;
                if (p.attackNum > 1 && nr.bcrm.Contains(e)) continue;
                if (ey.eah(e.braf.position, center) <= radius + e.brai.addRange) splashTargets.Add(e);
            }
            if (splashTargets.Count == 0) return 0;
            long damage = (long)Math.Round(koboldDamage(m, p.lds(out float bonus)) * (double)ratio);
            float angle = p.braf.eulerAngles.y;
            foreach (var e in splashTargets)
            {
                long before = Hp(e) + e.bqzh;
                long dealt = ey.eae(p, e, damage, angle, AttackType.Normal, p.jhe(AttackType.Normal), bonus);
                if (dealt >= before) koboldKillStack(m, e);
                else if (hitStacks > 0 && !nr.bcqy && e is xy) koboldAddStacks(m, hitStacks);
            }
            return splashTargets.Count;
        }
        // 游戏的无敌药水 buff（roleBuffManager.mim<T>(名字, 秒)，同名 buff 重复添加时刷新时长）
        public static void GiveInvulnerable(Component p, float seconds) => ((yl)p).roleBuffManager.myy<Buff无敌>("Buff无敌", seconds);

        // ---- 悬停提示框 bka：技能（wh）、遗物和 buff（iq）、三选一卡片（brx.pmo）都用它。njb(true, 信息) 显示、njb(false, default) 隐藏；
        // 还没创建时用 dqk 打开（同 brx.pmo）----
        public static void ShowTip(string name, string info, string icon, Vector3 position, int quality)
        {
            var tip = cj.bqpl?.dst<blv>() ?? cj.bqpl?.dsm<blv>() as blv;
            tip?.ocr(true, new blv.TipInfo
            {
                nameStr = name ?? "",
                info = info ?? "",
                iconPath = icon ?? "",
                showPos = position,
                quality = quality,
                isRelic = false,
            });
        }

        public static void HideTip() => cj.bqpl?.dst<blv>()?.ocr(false, default(blv.TipInfo));

        // bir.biyf：技能悬停提示末尾的附加说明（wo.OnPointerEnter 拼在描述后面）。游戏自己也会写（五五开的外挂、土豆兄弟），所以只追加
        public static string SkillExtraInfo(object s) => ((bir)s).biyf;
        public static void SetSkillExtraInfo(object s, string v) => ((bir)s).biyf = v;

        // ---- 波次配置 nr.bcqm（SOSpawnConfig）：enemySpawnData[i] 是第 i+1 波，eventList 带 Roguelike 时，
        // 进入这一波前的休息阶段弹遗物三选一（服务器先 bbsr++ 再发 OnStartRest）----
        public static int TotalWaves() => nr.bcqm?.enemySpawnData?.Length ?? 0;

        public static bool RelicBeforeWave(int index)
        {
            var waves = nr.bcqm?.enemySpawnData;
            if (waves == null || index < 0 || index >= waves.Length) return false;
            var events = waves[index].eventList;
            return events != null && Array.IndexOf(events, RoguelikeEventType.Roguelike) >= 0;
        }

        // 开场界面 bkh：关闭时（bift = MyServerNetworkManager.rcz）游戏弹开局遗物
        public static bool OpeningPending()
        {
            var ui = cj.bqpl?.dst<bmc>();
            return ui != null && ui.bayx;
        }

        // 重试波次的快照有效（wi.bquu）
        public static bool CheckpointValid() => wi.bquu;

        // ---- 技能栏 bqz.bksw（bpf 按钮，bra.bkuo 技能实例，bra.bkuq 图标上的 bpa 组件） ----
        public static void ReadSkillBar(List<(object button, object skill, RectTransform icon)> output)
        {
            output.Clear();
            var bar = cj.bqpl?.dst<bqz>();
            if (bar?.bksw == null) return;
            foreach (var b in bar.bksw)
            {
                if (b == null || b.bkuq == null) continue;
                object skill = b.bkuo != null && b.bkuo.bixe != ActiveSkillEnum.None ? b.bkuo : null;
                output.Add((b, skill, (RectTransform)b.bkuq.transform));
            }
        }
    }

    // 自定义被动在游戏里的实例：游戏按被动表的 class 列（ey.edk）创建它，它把获得、移除、每帧转给 PassiveSkills 里 Mod 的行为对象。
    // 字段只放整数：重试波次（wb）会反射深拷贝技能实例的全部字段
    internal sealed class ModPassive : qw
    {
        public readonly int DefId;
        public readonly int Instance;

        public ModPassive(int defId)
        {
            DefId = defId;
            Instance = PassiveSkills.Created(defId);
        }

        public override void gur()
        {
            base.gur();
            PassiveSkills.Added(Instance, this);
        }

        public override void gus()
        {
            base.gus();
            PassiveSkills.Removed(Instance);
        }

        public override void guz()
        {
            base.guz();
            if (bixd != null && bixd == nr.bqtm) PassiveSkills.Update(Instance, this);
        }
    }

    // 自定义遗物在游戏里的实例：bbx.lbx 的后缀创建，kqm 放进 bfna 后调 gtb；bba 每帧对列表里的每个遗物调 gtf
    internal sealed class ModRelic : rv
    {
        public readonly int DefId;
        public readonly int Instance;

        public ModRelic(int defId)
        {
            DefId = defId;
            Instance = CustomRelics.Created(defId);
        }

        public override void gzb()
        {
            base.gzb();
            CustomRelics.Added(Instance, this);
        }

        public override void gzc()
        {
            base.gzc();
            CustomRelics.Removed(Instance);
        }

        protected override void gzd(int a, int b)
        {
            base.gzd(a, b);
            CustomRelics.LevelChanged(Instance, this, a, b);
        }

        public override void gzf()
        {
            base.gzf();
            if (behq != null && behq == nr.bqtm) CustomRelics.Update(Instance, this);
        }
    }

    // 自定义装备特效在游戏里的实例：bbq.ksh 的后缀创建，khh 设好拥有者 bbcm、等级 bbcn、数值 bbco / bbcp 后调 Init；
    // 卸下时 Clear，强化 evl / 降级 evm，装备每帧调 euv。远程玩家、镜像 Boss 身上的也会创建，只有本地玩家的才接 Mod 的行为。
    // 字段只放整数：重试波次（bcr.mel）会把快照里的字段写回特效对象
    internal sealed class ModEquipEffect : kh
    {
        public readonly int DefId;
        public int Instance;

        public ModEquipEffect(int defId) => DefId = defId;

        private bool Local => bcae != null && bcae == nr.bqtm;

        public override void Init(kb equipBaseValue)
        {
            base.Init(equipBaseValue);
            if (!Local) return;
            if (Instance == 0) Instance = CustomEquipEffects.Created(DefId);
            CustomEquipEffects.Added(Instance, this);
        }

        public override void Clear()
        {
            base.Clear();
            if (Instance != 0) CustomEquipEffects.Removed(Instance);
        }

        public override void ezn()
        {
            int old = bcaf;
            base.ezn();
            if (Instance != 0) CustomEquipEffects.LevelChanged(Instance, this, old, bcaf);
        }

        public override void ezo(int a)
        {
            int old = bcaf;
            base.ezo(a);
            if (Instance != 0) CustomEquipEffects.LevelChanged(Instance, this, old, bcaf);
        }

        public override void eyx()
        {
            base.eyx();
            if (Instance != 0 && Local) CustomEquipEffects.Update(Instance, this);
        }
    }

    // 自定义主动技能在游戏里的实例：ey.ebn 的后缀创建它并放进 bcxi，pt.dda 每帧推进。
    // kea 每个客户端都执行（mmn 只在施法者那边执行，ModKit 的伤害方法自己判断 IsMine，所以不用它）
    internal sealed class ModSkill : bfz
    {
        public readonly int DefId;
        public int Instance;
        // 换动作前的控制器和动画机（游戏的 bfh 也存控制器字段）
        public RuntimeAnimatorController SavedController;
        public Animator SavedAnimator;
        public RuntimeAnimatorController OurController;

        public ModSkill(int defId) => DefId = defId;

        protected override void koz(float a) => ActiveSkills.Update(Instance, a);

        public override void Clear(int clearData)
        {
            // 先通知 Mod（这时施法者还在），再还原动作，最后让基类清字段
            ActiveSkills.End(Instance);
            try
            {
                GameRaw.StopSkillAnimation(this);
            }
            catch (Exception e)
            {
                Safe.Report("施法动作（还原）", e);
            }
            base.Clear(clearData);
        }
    }
}
