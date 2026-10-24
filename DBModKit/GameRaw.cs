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
        // 本地玩家 nr.bqcj（bbe : yb）
        public static Component LocalPlayer() => nr.bqcj;

        // cj.bpyr.dff：找不到 key 时返回默认值，不打警告
        public static string Localize(string key, string fallback) => cj.bpyr.dgq(key, fallback);
        // cl.bpzd：游戏设置里的语言（LanguageType）。不在兼容检查清单里：读不到时 Localization 退回中文，不必停用整个 ModKit
        public static int LanguageIndex() => (int)cj.bpyr.bpzd;

        // ---- 角色 yb ----
        public static long Hp(Component r) => ((yi)r).hp;
        public static long MaxHp(Component r) => ((yi)r).maxHp;
        public static bool IsAlive(Component r) => r != null && ((yi)r).bqjc != null && ((yi)r).jee();
        public static Vector3 Position(Component r) => ((yi)r).bqjc.position;
        // 水平距离
        public static float Distance(Component from, Vector3 to) => ((yi)from).jba(to);
        // 体型补偿：大体型敌人判定距离更远
        public static float HitRadius(Component r) => ((yi)r).bqjf != null ? ((yi)r).bqjf.addRange : 0f;
        public static bool IsBoss(Component r) => r is xv e && e.isBoss;
        public static bool IsElite(Component r) => r is xv e && e.isElite;
        // 召唤物 roleType = Summon，authorityId = 召唤者的 netId（xv.hpb）
        public static bool IsSummon(Component r) => ((yi)r).roleType == RoleType.Summon;
        public static uint AuthorityId(Component r) => ((yi)r).authorityId;
        public static List<yi> Enemies(Component r) => ((yi)r).jfc();
        // 护盾 bpgz；无敌 beqd（受到的伤害记为 0）
        public static long Shield(Component r) => ((yi)r).bqie;
        public static bool IsInvulnerable(Component r) => ((yi)r).bfij;
        // 普攻距离 roleModeBase.ico()（判定时还要加上对方体型半径）
        public static float AttackRange(Component r) => ((yi)r).bqjf != null ? ((yi)r).bqjf.ihi() : 0f;
        public static float MoveSpeed(Component r) => ((yi)r).jbb();
        public static string State(Component r) => ((yi)r).bqjd.ToString();
        // 变身中 henShinSkillId > 0（默认 -1）
        public static bool IsTransformed(Component r) => ((yi)r).henShinSkillId > 0;

        // ---- 普攻节奏：yi.timer 在开始攻击时清零，> iyi() 时可以再攻击；攻击动作长 iyj()。
        // isCheckAttack 由近战/弹道模式在出手时置位；远程模式不置位，按动作进度 0.41 出手，这里用 0.5 兜底 ----
        public static float AttackTimer(Component r) => ((yi)r).timer;
        public static float AttackInterval(Component r) => ((yi)r).jdz();
        public static bool IsAttacking(Component r) => ((yi)r).bqjd == RoleState.Attack;
        public static bool AttackHitDone(Component r)
        {
            var role = (yi)r;
            return role.isCheckAttack || role.timer / role.jea() >= 0.5f;
        }

        // ---- 属性（yb，bbe 有部分重写）----
        public static long Attack(Component r) => ((yi)r).bqje;
        public static int Strength(Component r) => ((yi)r).bqiv;
        public static int Agility(Component r) => ((yi)r).bqiu;
        public static int Stamina(Component r) => ((yi)r).bqit;
        public static float AttackSpeed(Component r) => ((yi)r).bqig;
        // 暴击率是 0~1 的概率（Random.value < bphl）
        public static float CriticalRate(Component r) => ((yi)r).bqiq;
        public static float CriticalDamage(Component r) => ((yi)r).bqir;
        public static int Armor(Component r) => ((yi)r).bqip;
        public static int Dodge(Component r) => ((yi)r).bqiw;
        public static float LifeSteal(Component r) => ((yi)r).bqja;

        // ---- 玩家 bbe ----
        public static int Mana(Component p) => ((bbs)p).bgnn;
        public static int MaxMana(Component p) => ((bbs)p).bgno;
        public static int Gold(Component p) => ((bbs)p).k234ec3ea02364731a43b3e9a91b92631;

        // 以下都是游戏自己加数值的方法：金币、力敏体等有影子副本校验（nr.fvw），不能直接写字段
        // yi.jcq(量, 来源)：回血，内部走 CmdUpdateHp 同步
        public static void Heal(Component r, double amount) => ((yi)r).jcq(amount, (yi)r);
        // bbs.lba：加蓝（自动限制在 0~上限）
        public static void AddMana(Component p, int amount) => ((bbs)p).lba(amount);
        // bbe.kfd63a19…(飘字位置, 数量, 吃金币加成, 吃关卡倍率, 允许负债, 来源)：返回实际到账数量
        public static int AddGold(Component p, int amount)
            => ((bbs)p).k9920daae7c484b21bc3c60afd0889cb2(((bbs)p).jby(), amount, c: false, d: false, e: false, GoldIncomeSource.Gameplay);

        // bbs.ktg(力, 敏, 体, 攻击)：加到加成字段 bfpj/bfpk/bfpl/bfpi（不是被防篡改校验的基础值），负数撤销；
        // 体力变化会自动同步生命上限。游戏的卡牌、装备也用它
        public static void AddStats(Component p, int str, int agi, int sta, long attack) => ((bbs)p).ktg(str, agi, sta, attack);

        // 当前波次，从 0 开始（游戏显示 nr.bchg + 1）
        public static int Wave() => nr.bchg;

        // ---- 本地玩家的战斗回调（多播委托字段，只在拥有者客户端触发）----
        // 只挂没有返回值、不带 ref 的回调：damageEvent 之类的返回值会替换伤害，多挂一个就会改掉游戏结果
        private sealed class PlayerBinding
        {
            public bbs Player;
            public yi.EnemyDefeated Defeated;
            public yi.ActiveSkillCastEvent SkillCast;
            public yi.Critical Critical;
            public yi.DogeEvent Dodge;
            public Action LevelUp;
            public Action Relife;
            public Action<bbs> TeammateLevelUp;
            public yi.DieEvent Died;
            public bbs.AddBagItem BagItem;
            public yi.LocalActiveSkillCastEvent LocalCast;
        }

        public static object BindPlayerEvents(Component player)
        {
            var p = (bbs)player;
            var b = new PlayerBinding
            {
                Player = p,
                Defeated = (killer, dead, ctx) =>
                {
                    if (dead is xv) GameEvents.Raise(new GameEvent(GameEventKind.EnemyKilled, Role.Wrap(dead), isBoss: ctx.IsBoss, isElite: ctx.IsElite));
                },
                SkillCast = (caster, skill) => GameEvents.Raise(new GameEvent(GameEventKind.SkillCast, skill: SkillInfo.Get((int)skill))),
                Critical = (hurt, damage, type) => GameEvents.Raise(new GameEvent(GameEventKind.Critical, Role.Wrap(hurt), amount: damage)),
                Dodge = () => GameEvents.Raise(new GameEvent(GameEventKind.Dodged)),
                LevelUp = () => GameEvents.Raise(new GameEvent(GameEventKind.LevelUp)),
                Relife = () => GameEvents.Raise(new GameEvent(GameEventKind.Revived)),
                // onTeammateLevelUp：队友升级时本地玩家身上触发（bbe.cs 10844，伙伴之刃被动用它）
                TeammateLevelUp = mate => GameEvents.Raise(new GameEvent(GameEventKind.TeammateLevelUp, Role.Wrap(mate))),
                // dieEvent(参数名叫 attackRole，实际传的是死者自己：yi.hpi 里 dieEvent?.Invoke(this))
                Died = dead => GameEvents.Raise(new GameEvent(GameEventKind.Died, Role.Wrap(dead))),
                BagItem = item => GameEvents.Raise(new GameEvent(GameEventKind.ItemPicked, itemId: (int)item)),
                // localActiveSkillCastEvent(玩家, 技能, 目标点, 朝向, 目标 netId, 技能书编号, 是否免费)：只在 ez.dzr 末尾触发（真正放出技能时），
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
            foreach (var kv in cj.bpyu.bdjb.OrderBy(kv => (int)kv.Key))
            {
                int id = (int)kv.Key;
                string icon = null;
                if (cx.bamb.TryGetValue("remains", out var t) && t is Dictionary<string, object> rows
                    && rows.TryGetValue(id.ToString(), out var r) && r is Dictionary<string, object> row)
                    icon = Cell(row, "icon");
                list.Add(new RelicInfo(id, cj.bpyr.dgq("pickitem_" + id, kv.Key.ToString()), kv.Value.grade, IconPath("Remains/", icon)));
            }
            return list;
        }

        // bbs.kzh(编号, 等级)：游戏的遗物三选一（ez.ebh）就调它；超过本局获取上限、或王者模式禁用时返回 null
        public static bool GiveRelic(Component p, int id) => cj.bpyu.bdjb.ContainsKey((ItemType)id) && ((bbs)p).kzh(id, 0) != null;

        // ---- 掉落物：cj.bpyz（ot）.bctu，ou 的 bcty = 编号、bctz = 所属玩家 netId（0 = 公共）、bcua = ItemType、bcuc = 位置 ----
        public static void ReadLoot(List<LootItem> output)
        {
            output.Clear();
            var items = cj.bpyz?.bdld;
            if (items == null) return;
            foreach (var kv in items)
            {
                var o = kv.Value;
                if (o == null) continue;
                // pa.ghg：公共物品、自己的物品，或物主允许共享时能捡（服务器 bbs.lks 用同样的判断）
                output.Add(new LootItem(o.bdlh, (int)o.bdlj, o.bdll, pa.ghg(o), ez.ecn(o.bdlj), ez.ecq(o.bdlj)));
            }
        }

        public static string ItemName(int itemType) => cj.bpyr.dgq("pickitem_" + itemType, ((ItemType)itemType).ToString());

        // bjw.Pick（点击单个物品）的判断：死了不能捡（nr.bchw 时除外）
        public static bool CanPickNow(Component p) => nr.bchw || (((yi)p).hp > 0 && ((yi)p).bqjd != RoleState.Dead);

        // 背包 bbm.bgil，上限 6 格；金币、宝石、阳光、力量/敏捷/耐力之书、护符不占格子（ez.ecn；全属性之书、金币袋占格子）
        public static int BagCount(Component p) => ((bbs)p).playerAttribute.kpn();

        // 护符需按键拾取（设置项 IsKeyPickTalisman）；关掉时护符靠碰到自动拾取，按 C 也不捡
        public static bool KeyPickTalisman() => nr.bcio;

        // CmdPickItem 由服务器 bbs.lks 处理：只检查物主，不检查距离，距离要自己限制
        public static void PickItem(Component p, uint id) => ((bbs)p).CmdPickItem(id);

        public static void PlayPickSound() => bz.dfe<AudioManager>()?.dch("Audio/Battle_Audio/UI/拾取物品", 0.9f);

        // ---- 三选一界面 bqa（ui_roguelike）----
        // 和游戏自己弹遗物前的判断一致（MyServerNetworkManager.qvf）：三选一界面开着，或技能栏的子面板（bqm.bqtk）开着；
        // 另外开场界面开着时也算忙（它一关游戏就弹开局遗物，抢在前面打开会被游戏随机选掉）
        public static bool ChoiceBusy()
        {
            var ui = cj.bpyk?.dsc<brk>();
            if (ui != null && ui.baqf) return true;
            var bar = cj.bpyk?.dsc<bqm>();
            return (bar != null && bar.bqtk) || OpeningPending();
        }

        // brk.pes(选项, 选中回调, 标题, 刷新(null = 不能刷新), 关闭回调, 延迟, 队伍数据, 来源)：ez.ebg 用的真方法。
        // 来源传 null：非空时 cs.dhi 会把选项计入游戏的统计
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
            var ui = (brk)cj.bpyk.drv<brk>();
            ui.pes(data, d => onPick(int.TryParse(d.data, out int i) ? i : 0), title, null, null, 0f, null, null);
        }

        // ---- 联机（Mirror：ciy = NetworkServer，cik = NetworkClient）----
        public static bool ServerActive() => ckj.bnys;
        public static bool ClientActive() => cjv.brdy;
        // 主机上的连接数，包含主机自己
        public static int ServerConnections() => ckj.bnyl.Count;

        // ---- 输入、UI ----
        // oz.gff()：持有期间屏蔽移动、技能、Tab 商店、ESC 菜单（oz.dcj 在计数非 0 时提前返回）
        public static IDisposable BlockInput() => oz.gff();
        // 游戏 UI 的 Canvas（UIRoot 第 0 个子节点）；主菜单之前可能还没创建
        public static Canvas UiCanvas() => cj.bpyk?.baqs != null ? cj.bpyk.baqs.GetComponent<Canvas>() : null;
        // 主菜单（ui_startGame/trans_bg/trans_main，竖排布局）里的"创意工坊"按钮；主菜单没显示时为 null。
        // 只用预制体里的对象名、不碰混淆代码，ModKit 不兼容时也能用
        public static GameObject MainMenuWorkshopButton()
        {
            var go = GameObject.Find("btn_workShop");
            return go != null && go.transform.parent != null && go.transform.parent.name == "trans_main" ? go : null;
        }

        // 普攻、技能共用的增伤，以及对 Boss / 精英 / 普通敌人的额外增伤（见 yi.jcb）
        public static float DamageBonus(Component p, Component target)
        {
            var player = (bbs)p;
            float bonus = player.addDamagePercent;
            if (target is xv e)
                bonus += e.isBoss ? player.addBossEnemy : e.isElite ? player.addEliteEnemy : player.addNormalEnemy;
            return bonus;
        }

        // ---- 全局开关 ----
        public static bool AutoBattleOn() => nr.bcik;
        // nr.bcib：角色被锁定（过场等），kqi 里直接站着不动
        public static bool ControlLocked() => nr.bcib;
        public static bool IsKing(Component p) => ((yi)p).roleType == RoleType.King;
        // 场地边界（圆形/多边形/矩形）内且离边缘至少 margin
        public static bool InsideArena(Vector3 pos, float margin) => nr.ftr(pos, margin);
        // 玩家的 CharacterController（bbs.bgne，公开访问器 bpmb），移动时被场景碰撞体挡住
        public static CharacterController Controller(Component p) => ((bbs)p).bqnr;
        // 碰撞体属于某个角色（玩家、敌人、召唤物），不算障碍物
        public static bool IsRoleCollider(Collider c) => c.GetComponentInParent<yi>() != null;

        // ---- 技能伤害估算（未计暴击、减伤、各种事件），各技能的实际公式见 NOTES.md ----
        public static long? BaseSkillDamage(int id, Component p, Component target)
        {
            var skill = (ActiveSkillEnum)id;
            if (!cj.bpyu.bdiz.TryGetValue(skill, out var d)) return null;
            var player = (bbs)p;
            switch (skill)
            {
                case ActiveSkillEnum.D_Execution:
                case ActiveSkillEnum.C_Execution:
                case ActiveSkillEnum.B_Execution:
                case ActiveSkillEnum.A_Execution:
                case ActiveSkillEnum.S_Execution:
                    // bgw.mws：表内公式 + 目标最大血量 20%
                    return ez.dzw(d, player) + (long)nr.fuj((yi)target, ((yi)target).maxHp * 0.2);
                case ActiveSkillEnum.D_Alchemy:
                case ActiveSkillEnum.C_Alchemy:
                case ActiveSkillEnum.B_Alchemy:
                case ActiveSkillEnum.A_Alchemy:
                case ActiveSkillEnum.S_Alchemy:
                    return ez.dzw(d, player);
                case ActiveSkillEnum.Bite:
                    // bga.mxf：自身最大血量 × exV[0]% + bphq × damageValue + damageBase
                    return (long)bga.mxf(player.maxHp, player.bqiv, d);
                default:
                    return null;
            }
        }

        // ---- 正在生效的技能实例 cj.bpyw.bcxi（bfe：bgwf 施法者、bgwi 技能、bgwg 剩余时间） ----
        public static float SkillEffectRemaining(Component p, int id)
        {
            var effects = cj.bpyw?.bdor;
            if (effects == null) return 0f;
            float best = 0f;
            foreach (var e in effects.Values)
                if (e != null && e.bhsg == (yi)p && (int)e.bhsj == id && e.bhsh > best)
                    best = e.bhsh;
            return best;
        }

        // ---- 地面预警 cj.bpys.bcqp（ok）：bcqt 变换、bcqw 已过时间、bcqx 总时长、bcqy 0=圆/扇形 1/2=矩形 ----
        private static readonly AccessTools.FieldRef<oq, List<or>> warnings = AccessTools.FieldRefAccess<oq, List<or>>("bdhs");

        public static void ReadTelegraphs(List<Telegraph> output)
        {
            output.Clear();
            var mgr = cj.bpys;
            if (mgr == null) return;
            foreach (var w in warnings(mgr))
            {
                if (w?.bdhw == null || w.bdhv == null || !w.bdhv.activeInHierarchy) continue;
                var t = w.bdhw;
                float left = w.bdia - w.bdhz;
                if (w.bdib == 0)
                {
                    // fvj：缩放 = 直径，_Range = 扇形全角，旋转 (90, 朝向+180, 0)，所以 -up 指向扇形朝向
                    Vector3 dir = -t.up;
                    dir.y = 0f;
                    float angle = w.bdhy != null ? w.bdhy.GetFloat(oa.bdau) : 360f;
                    output.Add(Telegraph.Sector(t.position, t.localScale.x / 2f, dir, angle, left));
                }
                else
                {
                    // fvl / fvr：从 position 沿朝向延伸 scale.z，宽 scale.x
                    output.Add(Telegraph.Rect(t.position, t.forward, t.localScale.x, t.localScale.z, left));
                }
            }
        }

        // ---- 命中事件 bdr.bhbv：攻击方客户端上每次造成伤害时触发 ----
        // 伤害类型、来源、技能实例取自外层的 yi.jcb / yi.jci（DamageContextHooks）：bgfu 在 iwk → iwl → bdr.mgf 里触发，
        // fact 只有来源标成 ActiveSkill 时才带技能，很多技能结算伤害时没标
        public static void SubscribeHits(System.Action<RawHit> handler)
        {
            bdr.bhbv += hit =>
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
                    if (ctx.Skill != null && ctx.Skill.bhsj != ActiveSkillEnum.None) raw.SourceSkillId = (int)ctx.Skill.bhsj;
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
               || typeof(rv).IsAssignableFrom(t) || typeof(ho).IsAssignableFrom(t);

        // 被动、装备、遗物的名字按类型缓存（Buff 每次按目标查）；进战斗时清空
        private static readonly Dictionary<Type, (HitEffectKind kind, string name)> effectNames = new Dictionary<Type, (HitEffectKind, string)>();
        private static Component effectNamesFor;

        private static void ResolveEffect(Type t, yi target, ref RawHit raw)
        {
            if (typeof(ho).IsAssignableFrom(t))
            {
                raw.EffectKind = HitEffectKind.Buff;
                // 同一个目标身上同类型的 Buff 只有一个（localRoleBuffDic 以 LocalBuffType 为键）
                foreach (var b in target.localRoleBuffDic.Values)
                    if (b != null && b.GetType() == t)
                    {
                        raw.EffectName = cj.bpyr.dgq("Buff_" + b.bbfm, b.bbfm.ToString());
                        return;
                    }
                return;
            }
            var me = nr.bqcj;
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

        private static (HitEffectKind, string) FindEffect(bbs me, Type t)
        {
            if (me == null) return (HitEffectKind.None, null);
            // 被动：bij.biju 是本地化后的名字（"p_" + 编号）
            if (typeof(qw).IsAssignableFrom(t))
            {
                foreach (var s in me.roleSkillList)
                    if (s != null && s.GetType() == t) return (HitEffectKind.Passive, s.biju);
                return (HitEffectKind.Passive, null);
            }
            var attr = me.playerAttribute;
            if (attr == null) return (HitEffectKind.None, null);
            // 装备技能 lm：同类型全身共享一个，用技能名（"equipSkill_" + EquipSkillType）
            if (typeof(lq).IsAssignableFrom(t))
            {
                foreach (var kv in attr.bgim)
                    if (kv.Value != null && kv.Value.GetType() == t)
                        return (HitEffectKind.Equipment, cj.bpyr.dgq("equipSkill_" + kv.Key, kv.Key.ToString()));
                return (HitEffectKind.Equipment, null);
            }
            // 装备特效 ke：属于某件装备（bbcl），用装备名 kb.bbmk
            if (typeof(kh).IsAssignableFrom(t))
            {
                foreach (var equip in attr.bgij)
                {
                    if (equip == null) continue;
                    foreach (var k in equip.evw())
                        if (k != null && k.GetType() == t) return (HitEffectKind.Equipment, equip.bbmk);
                }
                return (HitEffectKind.Equipment, null);
            }
            // 遗物：名字是 "pickitem_" + 编号
            if (typeof(rv).IsAssignableFrom(t))
            {
                foreach (var r in attr.bgik)
                    if (r != null && r.GetType() == t) return (HitEffectKind.Relic, cj.bpyr.dgq("pickitem_" + r.bdxy, r.bdxy));
                return (HitEffectKind.Relic, null);
            }
            return (HitEffectKind.None, null);
        }

        // 按名字对应游戏的枚举：游戏更新时就算调整了顺序也不会对错
        private static TOut MapEnum<TIn, TOut>(TIn value, TOut fallback) where TOut : struct
            => Enum.TryParse(value.ToString(), out TOut result) ? result : fallback;

        // ---- 技能数据 ActiveSkillData，表在 cj.bpyu.bcrw ----
        public static SkillInfo ReadSkillInfo(int id)
        {
            var skill = (ActiveSkillEnum)id;
            if (!cj.bpyu.bdiz.TryGetValue(skill, out var d)) return null;
            var tags = new List<string>();
            if (d.tags != null)
                foreach (var t in d.tags) tags.Add(t.ToString());
            return new SkillInfo(id, skill.ToString(), d.indicator, d.castingRange, d.range, d.cd, d.canAuto, tags);
        }

        // ---- 可改数值的配置表：技能 bcrw、英雄/怪物 bcru（都是 struct，改完要写回字典）----
        // 第一次改某一行时记下原版，之后每次都从原版重新计算，恢复时写回原版
        private static readonly Dictionary<(DataTable, string), object> originals = new Dictionary<(DataTable, string), object>();
        private static readonly Dictionary<(DataTable, string), FieldInfo> fieldInfos = new Dictionary<(DataTable, string), FieldInfo>();

        // 配置表在 cj.Init 里创建（bz.dfe 不会懒创建，之前是 null）
        public static bool TablesReady() => cj.bpyu != null && cx.bamb.Count > 0;

        public static List<DataEntry> ReadEntries(DataTable table)
        {
            var list = new List<DataEntry>();
            switch (table)
            {
                case DataTable.Skill:
                    foreach (var kv in cj.bpyu.bdiz.OrderBy(kv => (int)kv.Key))
                    {
                        if (kv.Key == ActiveSkillEnum.None) continue;
                        string name = cj.bpyr.dgq("a_" + (int)kv.Key, kv.Key.ToString());
                        list.Add(new DataEntry(table, kv.Key.ToString(), name, IconPath("Skill/", kv.Value.icon)));
                    }
                    break;
                case DataTable.Hero:
                    // 英雄的键是 HeroType 的数字，头像是 Player_<HeroType 名字最后一段>
                    foreach (var row in RawRows("hero"))
                    {
                        string id = Cell(row, "id");
                        if (id == null || !cj.bpyu.bdix.ContainsKey(id) || !int.TryParse(id, out int n)) continue;
                        string head = ((HeroType)n).ToString().Split('_').Last();
                        // 英雄名的本地化 key 是 "hero_" + 编号（见 ez.dwy；不直接调 dvm，它会返回工坊模型的名字）
                        list.Add(new DataEntry(table, id, cj.bpyr.dgq("hero_" + id, id), IconPath("HeadIcon/Player_", head)));
                    }
                    break;
                case DataTable.Enemy:
                    // 怪物在每局开始时才加入 bcru，这里从原始表列出
                    foreach (var row in RawRows("enemy"))
                    {
                        string id = Cell(row, "id");
                        if (id == null) continue;
                        // 怪物名的本地化 key 就是 id（EnemyType 名字，见 EnemyDragonBoss 的 Boss 提示）；没有翻译的（多半是没用到的怪）显示 id
                        list.Add(new DataEntry(table, id, cj.bpyr.dgq(id, id), IconPath("HeadIcon/Enemy_", Cell(row, "model"))));
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
            if (table == DataTable.Skill) cj.bpyu.bdiz[(ActiveSkillEnum)Enum.Parse(typeof(ActiveSkillEnum), key)] = (ActiveSkillData)row;
            else cj.bpyu.bdix[key] = (RoleAttribute)row;
            if (restore) originals.Remove((table, key));
        }

        private static bool TryGetRow(DataTable table, string key, out object row)
        {
            row = null;
            if (table == DataTable.Skill)
            {
                if (!Enum.TryParse(key, out ActiveSkillEnum skill) || !cj.bpyu.bdiz.TryGetValue(skill, out var d)) return false;
                row = d;
                return true;
            }
            if (!cj.bpyu.bdix.TryGetValue(key, out var r)) return false;
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

        // 原始表 cx.bamb[表名]：id → 行（列名 → 值）
        private static IEnumerable<Dictionary<string, object>> RawRows(string table)
        {
            if (!cx.bamb.TryGetValue(table, out var t) || !(t is Dictionary<string, object> rows)) yield break;
            foreach (var row in rows.Values)
                if (row is Dictionary<string, object> r) yield return r;
        }

        private static string Cell(Dictionary<string, object> row, string column)
            => row.TryGetValue(column, out var v) && v != null && v.ToString() != "" ? v.ToString() : null;

        private static string IconPath(string folder, string name) => string.IsNullOrEmpty(name) ? null : "Bundles/UI/Icon/" + folder + name;

        // ---- 创意工坊英雄模型（buj 换模型，bup 读 manifest.json，buu 工坊服务）----
        // 启动参数 -disable-mods 时为 true，游戏不加载任何工坊内容
        public static bool ModsDisabled() => bvy.blki;

        // 同 bvt.qjy：manifest 的英雄编号对得上，模型包文件存在；另外排除召唤物模型（bvw.qll，它的 manifest 也可能带 heroId）
        public static bool IsValidSkinFolder(string dir, int heroId)
            => ReadSkinManifest(dir, out var m) && m.heroId == heroId && !bvw.qll(m.contentType);

        private static bool ReadSkinManifest(string dir, out LocalWorkshopManifest m)
        {
            m = null;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            if (!bvz.qma(dir, out m) || m == null || m.heroId <= 0) return false;
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

        // 召唤物模型（bvw.qll）也带 manifest，但不是英雄模型
        public static HeroSkin ReadSkin(string id, string dir)
        {
            if (!ReadSkinManifest(dir, out var m) || bvw.qll(m.contentType)) return null;
            string title = !string.IsNullOrWhiteSpace(m.title) ? m.title.Trim()
                : !string.IsNullOrWhiteSpace(m.heroName) ? m.heroName.Trim()
                : Path.GetFileName(dir);
            return new HeroSkin(id, m.heroId, title, m.author?.Trim() ?? "", dir);
        }

        // bwe.qnt()：已订阅的物品和状态（游戏自己每秒也调用一次）；bptc：Steam 可用
        public static List<HeroSkin> ReadWorkshopSkins()
        {
            var list = new List<HeroSkin>();
            var service = cj.bpzc;
            if (service == null || !service.bquy) return list;
            foreach (var item in service.qnt())
            {
                if (item == null || !item.installed || item.heroId <= 0) continue;
                var skin = ReadSkin(HeroSkins.WorkshopId(item.publishedFileId), item.installFolder);
                if (skin != null) list.Add(skin);
            }
            return list;
        }

        // bwe.qnu：已安装物品的文件夹
        public static string WorkshopFolder(ulong id)
            => cj.bpzc != null && cj.bpzc.qnu(id, out var item) && item != null ? item.installFolder : null;

        // bvt.qji(文件夹, manifest, out 图)：manifest.previewImage，游戏的工坊列表也用它。
        // 很多工坊物品没填 previewImage，但文件夹里有 preview.png / hero_icon.png（bvt.qle 按相对路径读图）
        public static Sprite SkinPreview(string dir)
        {
            if (!bvz.qma(dir, out var m)) return null;
            if (bvt.qji(dir, m, out var sprite)) return sprite;
            foreach (var name in new[] { "preview.png", m.heroIcon, "hero_icon.png" })
                if (!string.IsNullOrWhiteSpace(name) && File.Exists(Path.Combine(dir, name)) && bvt.qle(dir, name, out sprite))
                    return sprite;
            return null;
        }

        public static int LocalHeroId() => nr.bqcj is bbs p ? (int)p.heroType : 0;

        // bvt.qjp：本地玩家正在用这个英雄时，撤掉当前模型（bvt.qjq）再按 bvt.qjx 的结果重新换（bvt.qjk）
        // 血条上的英雄名 roleName 只在出生时按模型的 heroName 算一次（yi.izy → ez.dwy），换完重新算；
        // 王者模式的 roleName 是 kingName，不动
        public static void ReapplyLocalHeroModel(int heroId)
        {
            bvt.qjp(heroId);
            if (nr.bqcj is bbs p && (int)p.heroType == heroId && p.roleType == RoleType.Player)
                p.roleName = HeroVariants.LocalName(heroId) ?? ez.dwy(p.heroType);
        }

        // 在 HeroSkinHooks 的角色上下文里撤掉模型（bvt.qjq）再换（bvt.qjk），名字（roleName）也按这个角色重新算
        public static void ReapplyHeroModel(Component p)
        {
            var player = (bbs)p;
            if (player.bqjf == null) return;
            HeroSkinHooks.WithOwner(player, () =>
            {
                bvt.qjq(player.bqjf);
                bvt.qjk(player);
                if (player.roleType == RoleType.Player)
                    player.roleName = (player.bqih ? HeroVariants.LocalName((int)player.heroType) : null) ?? ez.dwy(player.heroType);
            });
        }

        // 游戏自己的工坊绑定（主菜单"工坊 Mod"里启用的物品），没有时为 0
        public static ulong GameSkinBinding(int heroId) => cj.bpzb != null && cj.bpzb.qmn(heroId, out ulong id) ? id : 0UL;

        // bwe.qno：打开物品的创意工坊页面（游戏的工坊界面也用它）
        public static void OpenWorkshopPage(ulong id) => cj.bpzc?.qno(id);

        // ---- 玩家 ----
        // yi.bqih：Mirror 的 isLocalPlayer
        public static bool IsLocalPlayer(Component r) => r is yi role && role.bqih;
        // Mirror 的 netId（NetworkBehaviour.bqbx），同一局里每个客户端看到的都一样
        public static uint NetId(Component r) => ((yi)r).brdu;
        public static int HeroIdOf(Component p) => (int)((bbs)p).heroType;
        // bbs.bqos：Steam 昵称（服务器生成角色时写入的 SyncVar）
        public static string PlayerName(Component p) => ((bbs)p).bqos;
        public static bbs[] AllPlayers() => UnityEngine.Object.FindObjectsOfType<bbs>();

        // ---- Steam 大厅（cft = SteamMatchmaking，cgi = SteamUser）----
        // py.bqdi：当前大厅，房主是 nr.bckp、客户端是 nr.bckq；进地牢后不离开大厅。本地（KCP）模式没有大厅，返回 0
        public static ulong LobbyId()
        {
            var lobby = bz.dfe<py>();
            return lobby != null ? lobby.bqdi.m_SteamID : 0UL;
        }

        public static ulong MySteamId() => cht.uso().m_SteamID;

        // che.uep = GetLobbyOwner（游戏在 px 里用它；同类里还有没人调用的副本）
        public static ulong LobbyOwner(ulong lobby) => che.uep(new Steamworks.CSteamID(lobby)).m_SteamID;

        // 成员数据：每个成员只能写自己的，所有成员都能读。游戏不用它，py.gqf 收到成员数据的更新时直接返回
        public static void SetLobbyMemberData(ulong lobby, string key, string value)
            => che.uef(new Steamworks.CSteamID(lobby), key, value);

        // 离开房间时游戏会先关掉 Steam 联机（Player.log 里的"steam结束"），之后写成员数据会抛异常；那时已经离开大厅，不用清
        public static void TryClearLobbyMemberData(ulong lobby, string key)
        {
            try
            {
                che.uef(new Steamworks.CSteamID(lobby), key, "");
            }
            catch (InvalidOperationException)
            {
            }
        }

        public static string LobbyMemberData(ulong lobby, ulong member, string key)
            => che.uee(new Steamworks.CSteamID(lobby), new Steamworks.CSteamID(member), key);

        public static List<ulong> LobbyMembers(ulong lobby)
        {
            var id = new Steamworks.CSteamID(lobby);
            var list = new List<ulong>();
            int count = che.udx(id);
            for (int i = 0; i < count; i++) list.Add(che.udy(id, i).m_SteamID);
            return list;
        }

        // ---- 自定义被动（PassiveSkills）----
        // 被动表 cx.bamb["passsiveSkill"]：编号 → 行（列名 → 值）。学习时 yi.jas / iuz 读 class 列 → ez.ecl(类名) 创建 qp 实例，
        // 再 msz(行) 解析数值、gou() 生效、ez.ebc 放进技能栏（roleSkillList，最多 nr.MaxSkillNum 格，被动也占格）。
        // 技能书候选池是 pj（cj.bpyw）的私有字段 bcxj：品级 → 行，在 pj 构造时（gjs）从被动表建好，之后加的行要自己放进去
        private static readonly AccessTools.FieldRef<pt, Dictionary<string, List<object>>> passivePool =
            AccessTools.FieldRefAccess<pt, Dictionary<string, List<object>>>("bdos");
        private static readonly Dictionary<int, Dictionary<string, object>> injectedPassives = new Dictionary<int, Dictionary<string, object>>();

        public const string ModPassiveClassPrefix = "DBModKit.Passive:";

        private static Dictionary<string, object> PassiveTable()
            => cx.bamb.TryGetValue("passsiveSkill", out var t) ? t as Dictionary<string, object> : null;

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
            // 空的写 ""，和游戏读表的结果一致：bij.ndd 用 dwb<string>("value") 直接 ToString，null 会空引用
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
            // value / levelup 是 "|" 分隔的字符串（bij.ndd → ez.dxq），不按模板转换
            row["value"] = Join(def.Values);
            row["levelup"] = Join(def.LevelUp);

            EjectPassive(info.Id);
            table[info.Id.ToString()] = row;
            injectedPassives[info.Id] = row;
            if (info.InSkillBook && cj.bpyw != null)
            {
                var pool = passivePool(cj.bpyw);
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
            return !wantPool || cj.bpyw == null || passivePool(cj.bpyw).Values.Any(list => list.Contains(row));
        }

        public static void EjectPassive(int id)
        {
            var table = PassiveTable();
            string key = id.ToString();
            if (table != null && table.TryGetValue(key, out var row) && IsModPassiveRow(row))
                table.Remove(key);
            if (cj.bpyw != null)
                foreach (var list in passivePool(cj.bpyw).Values)
                    list.RemoveAll(r => r is Dictionary<string, object> d && d.TryGetValue("id", out var v) && v?.ToString() == key && IsModPassiveRow(d));
            injectedPassives.Remove(id);
        }

        private static bool IsModPassiveRow(object row)
            => row is Dictionary<string, object> d && d.TryGetValue("class", out var c) && c is string s && s.StartsWith(ModPassiveClassPrefix);

        // ez.ecl 的后缀：类名是我们的前缀时创建 ModPassive（原版对未知类名返回 null）
        public static qw CreateModPassive(string className)
        {
            if (className == null || !className.StartsWith(ModPassiveClassPrefix)) return null;
            if (!int.TryParse(className.Substring(ModPassiveClassPrefix.Length), out int id) || !PassiveSkills.IsCustom(id)) return null;
            return new ModPassive(id);
        }

        // yi.jas(被动, 要替换的格子)：技能书学被动走它（真方法）。技能栏满、又不指定格子时，ez.ebc 会替换第 2 格，所以这里先判断
        public static bool GivePassive(int id, int replaceSlot)
        {
            var p = nr.bqcj;
            if (p == null) return false;
            bij replace = null;
            if (p.roleSkillList.Count >= nr.MaxSkillNum)
            {
                if (replaceSlot < 0 || replaceSlot >= p.roleSkillList.Count) return false;
                replace = p.roleSkillList[replaceSlot];
            }
            p.jas((PasssiveSkillEnum)id, replace);
            return true;
        }

        // ez.eba：游戏的移除（dzp 里调 qw.gtf，再刷新技能栏）
        public static void RemoveLocalPassives(int id)
        {
            var p = nr.bqcj;
            if (p == null) return;
            foreach (var s in p.roleSkillList.OfType<ModPassive>().Where(s => s.DefId == id).ToList())
                ez.eba(s);
        }

        public static void LocalModPassives(List<object> output)
        {
            output.Clear();
            var p = nr.bqcj;
            if (p == null) return;
            foreach (var s in p.roleSkillList)
                if (s is ModPassive m) output.Add(m);
        }

        public static int ModPassiveInstance(object native) => ((ModPassive)native).Instance;
        public static Component PassiveOwner(object native) => ((bij)native).bijd;
        // 等级 bht.kb03f1…；数值 bhnp = value + levelup × 等级（msz → mtm 算好）
        public static int PassiveLevel(object native) => ((bij)native).kce2b7db28c9f4236973348d6d911582c;

        public static float PassiveValue(object native, int i)
        {
            var values = ((bij)native).bijp;
            return values != null && i >= 0 && i < values.Length ? values[i] : 0f;
        }

        // ---- 自定义遗物（CustomRelics）----
        // 原始表 cx.bamb["remains"]：编号 → 行（skill 列是效果名，bbs.kzi 按它创建 rn 子类，未知名字返回什么都不做的 new rn()）。
        // oo 构造时转换成 bcry（ItemType → RemainsData，三选一的候选池 ez.ebq 从这里按品级筛）和 bcrx（物品表）。
        // 发放 bbs.kzh(编号, 等级)：读行 → kql → 填 values/levelup/valueTypes → kqm 放进 playerAttribute.bfna 并调 gtb()。
        // runAcquireLimit 为 0 时 kto 不发 CmdClaimRelicRunAcquisition；isTeam 为假时 ez.ebh 不发 CmdAddAllPlayerItem，所以选遗物不会发消息
        private static readonly Dictionary<int, Dictionary<string, object>> injectedRelics = new Dictionary<int, Dictionary<string, object>>();

        public const string ModRelicSkillPrefix = "DBModKit.Relic:";

        private static Dictionary<string, object> RelicTable()
            => cx.bamb.TryGetValue("remains", out var t) ? t as Dictionary<string, object> : null;

        public static bool IsRelicValueType(string name) => Enum.TryParse(name, false, out RelicValueType _);

        public static void InjectRelic(CustomRelicInfo info)
        {
            var table = RelicTable();
            if (table == null || cj.bpyu == null) return;
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
            // values / levelup / valueTypes 都是 "|" 分隔的字符串（rv.gys → ez.dxq，rv.gyt）
            row["values"] = Join(def.Values);
            row["levelup"] = Join(def.LevelUp);
            row["valueTypes"] = Join(def.ValueTypes);
            row["maxLevel"] = def.MaxLevel;
            row["isUnique"] = def.Unique;
            row["mapTypes"] = "";
            row["runAcquireLimit"] = 0;

            EjectRelic(info.Id);
            table[info.Id.ToString()] = row;
            // 和 oo 的转换一致：grade 是 oc.bdbd（D C B A S）里的下标
            int gradeIndex = oc.bdbd.IndexOf(grade);
            cj.bpyu.bdjb[(ItemType)info.Id] = new RemainsData
            {
                grade = gradeIndex,
                conditions = EntryConditions.None,
                isUnique = def.Unique,
                isTeam = false,
                runAcquireLimit = 0,
                mapTypes = "",
            };
            cj.bpyu.bdja[(ItemType)info.Id] = new ItemData { name = "pickitem_" + info.Id, quality = gradeIndex, model = "Remains" };
            injectedRelics[info.Id] = row;
        }

        public static bool RelicInjected(int id)
        {
            if (!injectedRelics.TryGetValue(id, out var row)) return false;
            var table = RelicTable();
            return table != null && table.TryGetValue(id.ToString(), out var current) && current == row
                   && cj.bpyu != null && cj.bpyu.bdjb.ContainsKey((ItemType)id);
        }

        public static void EjectRelic(int id)
        {
            var table = RelicTable();
            if (table != null && injectedRelics.TryGetValue(id, out var ours) && table.TryGetValue(id.ToString(), out var row) && row == ours)
                table.Remove(id.ToString());
            // 只删我们自己的编号段（3000000 起），不会碰到游戏的遗物
            if (id >= CustomRelics.FirstId && cj.bpyu != null)
            {
                cj.bpyu.bdjb.Remove((ItemType)id);
                cj.bpyu.bdja.Remove((ItemType)id);
            }
            injectedRelics.Remove(id);
        }

        // bbs.kzi 的后缀：效果名是我们的前缀时创建 ModRelic（原版对未知名字返回空的 rn）
        public static rv CreateModRelic(string skill)
        {
            if (skill == null || !skill.StartsWith(ModRelicSkillPrefix)) return null;
            if (!int.TryParse(skill.Substring(ModRelicSkillPrefix.Length), out int id) || !CustomRelics.IsCustom(id)) return null;
            return new ModRelic(id);
        }

        // ez.ebq 的后缀：三选一候选池（品级 → 遗物）里去掉不该出现的自定义遗物
        public static void FilterRelicPool(Dictionary<int, List<ItemType>> pool)
        {
            if (pool == null) return;
            foreach (var list in pool.Values)
                list.RemoveAll(t => (int)t >= CustomRelics.FirstId && CustomRelics.IsCustom((int)t) && !CustomRelics.InPool((int)t));
        }

        public static bool GiveRelic(Component p, int id, int level) => cj.bpyu.bdjb.ContainsKey((ItemType)id) && ((bbs)p).kzh(id, level) != null;

        // bbs.kzk：游戏的移除（调 gtc，再刷新遗物栏）
        public static void RemoveLocalRelics(int id)
        {
            var p = nr.bqcj;
            if (p == null) return;
            foreach (var r in p.playerAttribute.bgik.OfType<ModRelic>().Where(r => r.DefId == id).ToList())
                p.kzk(r);
        }

        public static void LocalModRelics(List<object> output)
        {
            output.Clear();
            var p = nr.bqcj;
            if (p == null || p.playerAttribute == null) return;
            foreach (var r in p.playerAttribute.bgik)
                if (r is ModRelic m) output.Add(m);
        }

        public static int ModRelicInstance(object native) => ((ModRelic)native).Instance;
        public static Component RelicOwner(object native) => ((rv)native).bdya;
        public static int RelicLevel(object native) => ((rv)native).bdye;

        // 同 rv.gxs：values[i] + levelup[i] × 等级
        public static float RelicValue(object native, int i, int level)
        {
            var r = (rv)native;
            if (r.bdyf == null || i < 0 || i >= r.bdyf.Length) return 0f;
            float up = r.bdyh != null && i < r.bdyh.Length ? r.bdyh[i] : 0f;
            return r.bdyf[i] + up * level;
        }

        // ez.ecx 的后缀：构筑数据（同步给队友、上传国王对决、信息面板、结算）去掉自定义遗物
        public static void FilterKingRelics(ref de.PlayerKingData data)
        {
            if (data.relic == null || !data.relic.Any(r => CustomRelics.IsCustom(r.relicName))) return;
            data.relic = data.relic.Where(r => !CustomRelics.IsCustom(r.relicName)).ToArray();
        }

        // 去掉构筑数据里的自定义被动：ez.ect（技能列表，"p_"+编号）给 ez.ecx 用，ebj 的结果会同步给队友、上传成国王对决数据；
        // bbj.knj 的镜像 Boss 快照另有一份技能来源（activeSkill = None 的是被动）
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

        // 本地化 cl.dgr 的前缀："p_<编号>" / "a_<编号>" 是名字，后面加 "_m" 是说明。游戏的编号都很小，先按长度和前缀排除
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
        // 装备表 cx.bamb["equipment"]：编号 → 行。kb.Init(RoleType) 读行：skill 列 Enum.Parse 成 EquipSkillType（数字字符串也行，所以枚举外的编号可用）、
        // skillValue / skillValueUp 是 "|" 分隔的数值；不是 King 时 etc() 读图标、说明（"equip_"+编号+"_m"）、强化配置，属性优先读 ov.bdjo（kb.etx(行)）。
        // 特效配置表 cx.bamb["equipmentSkill"]：特效名 → 行（onlySkill、kingLock）。穿上 bba.k08fba…(jy)：对 kb.euz() 的每个特效 kdc → khf 按类型创建
        // lm（全身共享）或 ke（每件装备一个，khh 设拥有者、等级、数值后 Init），卸下 bba.kb163…(jy) 对每个 ke 调 Clear。
        // 特效说明 kb.ewu 先查本地化表 cl.baii 里有没有 "equipSkill_"+编号+"_m"（没有就不显示），再按 dfe 取文字，所以两边都要有
        private static readonly Dictionary<int, Dictionary<string, object>> injectedEquipEffects = new Dictionary<int, Dictionary<string, object>>();
        private static readonly Dictionary<int, Dictionary<string, object>> injectedEquipment = new Dictionary<int, Dictionary<string, object>>();
        private static readonly Dictionary<int, List<kb.EquipAttributeData>> injectedEquipAttributes = new Dictionary<int, List<kb.EquipAttributeData>>();

        private static Dictionary<string, object> EquipTable()
            => cx.bamb.TryGetValue("equipment", out var t) ? t as Dictionary<string, object> : null;

        private static Dictionary<string, object> EquipEffectTable()
            => cx.bamb.TryGetValue("equipmentSkill", out var t) ? t as Dictionary<string, object> : null;

        public static IReadOnlyList<string> EquipAttributeNames() => kb.bbmv;

        // 属性名的别名（例如 physicalDamage → normalAttackAddDamage），和游戏读表时一样
        public static string NormalizeEquipAttribute(string name) => kb.euf(name);

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
            var zuj = cj.bpyr?.baii;
            // 只是让 kb.eul 认这个键；文字由 cl.dgr 的前缀（SkillText）给，这里写中文原文兜底
            if (zuj != null) zuj[key] = new Dictionary<string, object> { ["Chinese"] = text ?? "" };
        }

        private static void RemoveLocalizationKey(string key)
        {
            var zuj = cj.bpyr?.baii;
            if (zuj != null && zuj.TryGetValue(key, out var v) && v is Dictionary<string, object> d && d.Count == 1 && d.ContainsKey("Chinese"))
                zuj.Remove(key);
        }

        public static void InjectEquipEffect(CustomEquipEffectInfo info)
        {
            var table = EquipEffectTable();
            if (table == null || cj.bpyr == null) return;
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
                   && cj.bpyr != null && cj.bpyr.baii.ContainsKey("equipSkill_" + key + "_m");
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
            if (table == null || cj.bpyu == null) return;
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
            var parsed = kb.etx(row);
            cj.bpyu.bdjo[key] = parsed;
            injectedEquipment[info.Id] = row;
            injectedEquipAttributes[info.Id] = parsed;
        }

        public static bool EquipmentInjected(int id)
        {
            if (!injectedEquipment.TryGetValue(id, out var row)) return false;
            var table = EquipTable();
            string key = id.ToString();
            return table != null && table.TryGetValue(key, out var current) && current == row
                   && cj.bpyu != null && cj.bpyu.bdjo.TryGetValue(key, out var attrs) && injectedEquipAttributes.TryGetValue(id, out var ours) && attrs == ours;
        }

        public static void EjectEquipment(int id)
        {
            var table = EquipTable();
            string key = id.ToString();
            if (table != null && injectedEquipment.TryGetValue(id, out var ours) && table.TryGetValue(key, out var row) && row == ours)
                table.Remove(key);
            // 只删我们自己的编号段（5000000 起），不会碰到游戏的装备
            if (id >= CustomEquipment.FirstId && cj.bpyu != null) cj.bpyu.bdjo.Remove(key);
            injectedEquipment.Remove(id);
            injectedEquipAttributes.Remove(id);
        }

        // bbm.kpw 的后缀：特效编号是我们的时创建 ModEquipEffect（原版对未知类型两个都返回 null）
        public static kh CreateModEquipEffect(EquipSkillType type)
        {
            int id = (int)type;
            if (id < CustomEquipEffects.FirstId || !CustomEquipEffects.IsCustom(id)) return null;
            return new ModEquipEffect(id);
        }

        // kb.Init(RoleType) 的后缀：自定义装备追加第二个起的特效；游戏装备追加 AttachTo 里写了它的自定义特效（联机握手允许时）
        public static void AttachEquipEffects(kb item, RoleType role)
        {
            if (item == null || role == RoleType.King || !int.TryParse(item.bbmh, out int id)) return;
            if (id >= CustomEquipment.FirstId)
            {
                var info = CustomEquipment.Get(id);
                var table = EquipTable();
                if (info == null || table == null) return;
                var effects = EquipmentEffects(info, table);
                for (int i = 1; i < effects.Count; i++)
                    item.k39bb44975a7c4604a6775b7b28f87925((EquipSkillType)effects[i].type, effects[i].values, effects[i].up);
                return;
            }
            if (!Multiplayer.GameplayAllowed) return;
            foreach (var e in CustomEquipEffects.AttachedTo(id))
                // 一定要传数组（可以是空的）：esg 查不到时会退回主特效的数值
                item.k39bb44975a7c4604a6775b7b28f87925((EquipSkillType)e.Id, (e.Def.Values ?? new float[0]).ToArray(), (e.Def.LevelUp ?? new float[0]).ToArray());
        }

        // pr.gls(编号, 行) 的后缀：神器三选一的候选条件，联机握手不允许时去掉自定义装备
        public static bool ArtifactAllowed(int id) => id < CustomEquipment.FirstId || !CustomEquipment.IsCustom(id) || CustomEquipment.InPool(id);

        // ez.ecu 的后缀：构筑数据（同步给队友、上传国王对决、镜像 Boss）去掉自定义装备；没有装备时原版返回 null
        public static de.PlayerKingEquipData[] FilterKingEquip(de.PlayerKingEquipData[] equip)
        {
            if (equip == null) return null;
            // 魔剑吞噬过的装备编号也会带上（kp.fco）
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

        // pr.glo("equip_"+编号)：new jy → Init → 穿上 → 刷新装备栏。它不检查装备栏满没满（上限 nr.MaxEquipNum）
        public static bool GiveEquipment(int id)
        {
            var p = nr.bqcj;
            if (p == null || p.playerAttribute == null || p.playerAttribute.bgij.Count >= nr.MaxEquipNum) return false;
            return pr.glo("equip_" + id) != null;
        }

        public static int LocalEquipmentCount(int id)
        {
            var p = nr.bqcj;
            if (p == null || p.playerAttribute == null) return 0;
            string key = id.ToString();
            return p.playerAttribute.bgij.Count(e => e != null && e.bbmh == key);
        }

        // bba.kb163…：游戏的卸下（对每个 ke 调 Clear，再同步），然后刷新装备栏
        public static void RemoveLocalEquipment(int id)
        {
            var p = nr.bqcj;
            if (p == null || p.playerAttribute == null) return;
            string key = id.ToString();
            var list = p.playerAttribute.bgij.Where(e => e != null && e.bbmh == key).ToList();
            foreach (var e in list) p.playerAttribute.ke35090bd99034a36bfe5671e5b916a5b(e);
            if (list.Count > 0) cj.bpyk?.dsc<bqm>()?.oyt();
        }

        public static void LocalModEquipEffects(List<object> output)
        {
            output.Clear();
            var p = nr.bqcj;
            if (p == null || p.playerAttribute == null) return;
            foreach (var equip in p.playerAttribute.bgij)
            {
                if (equip == null) continue;
                foreach (var k in equip.evw())
                    if (k is ModEquipEffect m && m.Instance != 0) output.Add(m);
            }
        }

        public static int ModEquipEffectInstance(object native) => ((ModEquipEffect)native).Instance;
        public static Component EquipEffectOwner(object native) => ((kh)native).bbra;
        public static int EquipEffectLevel(object native) => ((kh)native).bbrb;

        public static int EquipEffectEquipment(object native)
        {
            var equip = ((kh)native).bbqz;
            return equip != null && int.TryParse(equip.bbmh, out int id) ? id : 0;
        }

        // 同 kb.exc：数值 bbco[i] + 成长 bbcp[i] × 等级（khh 从 esg / esh 填）
        public static float EquipEffectValue(object native, int i, int level)
        {
            var k = (kh)native;
            if (k.bbrc == null || i < 0 || i >= k.bbrc.Length) return 0f;
            float up = k.bbrd != null && i < k.bbrd.Length ? k.bbrd[i] : 0f;
            return k.bbrc[i] + up * level;
        }

        // ---- 自定义主动技能（ActiveSkills）----
        // 技能表两份：原始表 cx.bamb["activeSkill"]（技能书候选池、图鉴读它）和 ov.bdiz（ActiveSkillEnum → ActiveSkillData，施法、冷却、描述读它，
        // 由 oo 构造时从原始表转换，见 oo.cs 读 activeSkill 的循环）。学技能 yi.jar → new bht → ez.ebc 进技能栏。
        // 释放：ez.dzq → dyf（默认分支：切 RoleState.Skill、CmdCreateSkill）→ 服务器不校验直接 RpcCreateSkill → 每个客户端 ez.eao
        // 按编号的大 switch 创建 bfe 实例放进 cj.bpyw.bcxi，pt.dcj 每帧推进（bgwg 剩余时间 < 0 时移除并 Clear）。
        // 技能书主动候选池是 pj 的私有字段 bcxk（品级 → 原始表的行）
        private static readonly AccessTools.FieldRef<pt, Dictionary<string, List<object>>> skillPool =
            AccessTools.FieldRefAccess<pt, Dictionary<string, List<object>>>("bdot");
        private static readonly Dictionary<int, Dictionary<string, object>> injectedSkills = new Dictionary<int, Dictionary<string, object>>();

        private static Dictionary<string, object> SkillTable()
            => cx.bamb.TryGetValue("activeSkill", out var t) ? t as Dictionary<string, object> : null;

        private static readonly string[] IndicatorNames = { "None", "Range", "Vector", "Target" };

        public static void InjectSkill(ActiveSkillInfo info)
        {
            var table = SkillTable();
            if (table == null || cj.bpyu == null) return;
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
                attribute = ov.gdn("无属性"),
                tags = bik.neb(tags),
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
            cj.bpyu.bdiz[(ActiveSkillEnum)info.Id] = data;
            injectedSkills[info.Id] = row;
            if (info.InSkillBook && cj.bpyw != null)
            {
                var pool = skillPool(cj.bpyw);
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
            if (cj.bpyu == null || !cj.bpyu.bdiz.ContainsKey((ActiveSkillEnum)id)) return false;
            bool wantPool = !(row.TryGetValue("lock", out var l) && Convert.ToBoolean(l));
            return !wantPool || cj.bpyw == null || skillPool(cj.bpyw).Values.Any(list => list.Contains(row));
        }

        public static void EjectSkill(int id)
        {
            string key = id.ToString();
            var table = SkillTable();
            if (table != null && injectedSkills.TryGetValue(id, out var ours) && table.TryGetValue(key, out var row) && row == ours)
                table.Remove(key);
            // 只删我们自己的编号段（2000000 起），不会碰到游戏的技能
            if (id >= ActiveSkills.FirstId && cj.bpyu != null) cj.bpyu.bdiz.Remove((ActiveSkillEnum)id);
            if (cj.bpyw != null)
                foreach (var list in skillPool(cj.bpyw).Values)
                    list.RemoveAll(r => r is Dictionary<string, object> d && d.TryGetValue("id", out var v) && v?.ToString() == key);
            injectedSkills.Remove(id);
        }

        // yi.jar(技能, 要替换的格子)：技能书学主动技能走它（真方法；yh.njw 是诱饵）
        public static bool GiveSkill(int id, int replaceSlot)
        {
            var p = nr.bqcj;
            if (p == null) return false;
            bij replace = null;
            if (p.roleSkillList.Count >= nr.MaxSkillNum)
            {
                if (replaceSlot < 0 || replaceSlot >= p.roleSkillList.Count) return false;
                replace = p.roleSkillList[replaceSlot];
            }
            p.jar((ActiveSkillEnum)id, replace);
            return true;
        }

        // 被动（qp）也是 bht，bhne 是 None；主动技能格子的 bhne 是技能编号
        public static void RemoveLocalSkills(int id)
        {
            var p = nr.bqcj;
            if (p == null) return;
            foreach (var s in p.roleSkillList.Where(s => !(s is qw) && (int)s.bije == id).ToList())
                ez.eba(s);
            // 正在生效的实例也结束掉（Clear 里会调 OnEnd）
            var effects = cj.bpyw?.bdor;
            if (effects == null) return;
            foreach (var kv in effects.Where(kv => kv.Value is ModSkill m && m.DefId == id).ToList())
            {
                effects.Remove(kv.Key);
                kv.Value.Clear(-1);
            }
        }

        // ez.eao 的后缀。原方法会先做通用的事（检查表、触发"技能创建"事件），再在大 switch 里按编号创建实例；
        // switch 没有我们的分支，所以在后缀里创建 ModSkill 放进 bcxi。原方法开头的两个提前返回（pt.bdpd、主机 bpue）这里照样判断。
        // dzc(实例编号, 技能, 位置, 施法者, _, 朝向, 目标 netId, 技能书编号, 工厂)
        public static void CreateModSkill(uint instanceId, ActiveSkillEnum skill, Vector3 position, yi caster, float rotation, int targetId)
        {
            int id = (int)skill;
            if (id < ActiveSkills.FirstId || !ActiveSkills.IsCustom(id)) return;
            if (caster == null || cj.bpyw == null || cj.bpyw.bdpd || cj.bpyu == null || !cj.bpyu.bdiz.TryGetValue(skill, out var data)) return;
            if (Mirror.NetworkManager.bnxv is MyServerNetworkManager server && server.bqwa) return;
            if (cj.bpyw.bdor.ContainsKey(instanceId)) return;
            // cjv.bnvu：Mirror 客户端的 netId → NetworkIdentity（游戏的处决技能 bgw.nac 也这样找目标）
            Component target = null;
            if (targetId > 0 && cjv.bnvu.TryGetValue((uint)targetId, out var identity) && identity != null)
                target = identity.GetComponent<yi>();
            var s = new ModSkill(id)
            {
                bhsf = instanceId,
                bhsj = skill,
                bhsk = data,
                bhsg = caster,
                // 没有持续时间的技能也要活过 OnCast 这一帧
                bhsh = data.duration > 0f ? data.duration : 0.01f,
            };
            cj.bpyw.bdor.Add(instanceId, s);
            // yi.jdq(range)：表里的范围 × 施法者的技能范围加成（dzc 里给所有技能用的就是它）
            s.Instance = ActiveSkills.Cast(id, s, position, rotation, target, caster.jdq(data.range));
        }

        // ---- 施法动作：照游戏的 bfh（旋风斩、鸡舞、处决都用它）----
        // 新建 AnimatorOverrideController，把待机片段 H_Idle2（nw.bcto）换成要播的片段，重新赋值控制器（动画机回到入口的待机状态，
        // 于是播的就是新片段），再 yi.jeb() 锁住动画（bepi：ive 不再切别的动作，bbr.kqf 也不会把施法状态切回待机）。
        // 结束时还原控制器、yi.jaz() 解锁、切回待机。overrideAnimSkillId 记着是哪个技能实例换的，变身（bgt）会先结束它
        public static bool PlaySkillAnimation(object native, AnimationClip clip, float speed)
        {
            var s = (ModSkill)native;
            var caster = s.bhsg;
            var anim = caster?.bqjf?.myAnim;
            if (anim == null || clip == null) return false;
            // 别的换动作技能还在生效（游戏的或者另一个 Mod 技能）：先结束它，免得两边互相把对方的控制器当成原版
            if (caster.overrideAnimSkillId >= 0 && caster.overrideAnimSkillId != (int)s.bhsf && cj.bpyw != null)
                cj.bpyw.gow((uint)caster.overrideAnimSkillId);
            if (s.SavedController == null || s.SavedAnimator != anim)
            {
                s.SavedAnimator = anim;
                s.SavedController = anim.runtimeAnimatorController;
            }
            var controller = new AnimatorOverrideController(s.SavedController);
            controller[nw.bcto] = clip;
            s.OurController = controller;
            caster.jay(speed);
            anim.runtimeAnimatorController = null;
            anim.runtimeAnimatorController = controller;
            caster.jeb();
            caster.overrideAnimSkillId = (int)s.bhsf;
            return true;
        }

        public static void StopSkillAnimation(object native)
        {
            var s = (ModSkill)native;
            var caster = s.bhsg;
            if (s.SavedController == null || caster == null) return;
            // 换了模型（皮肤）后 myAnim 是新的动画机，旧的已经不用了，只还原我们改过的那个
            bool stacked = caster.overrideAnimSkillId != (int)s.bhsf;
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
            caster.jaz();
            if (!caster.jed()) caster.jbc(RoleState.Idle);
            caster.overrideAnimSkillId = -1;
        }

        private static readonly List<(Animator Anim, RuntimeAnimatorController Ours, RuntimeAnimatorController Original, yi Caster)> strayAnimations =
            new List<(Animator, RuntimeAnimatorController, RuntimeAnimatorController, yi)>();

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

        public static Component SkillCaster(object native) => ((bft)native).bhsg;
        // yi.bqih：本机的角色（bft.mwr 也用它决定是否结算伤害 mmn）
        public static bool SkillIsMine(object native) => ((bft)native).bhsg != null && ((bft)native).bhsg.bqih;
        public static float SkillRemaining(object native) => ((bft)native).bhsh;
        public static void SetSkillRemaining(object native, float value) => ((bft)native).bhsh = value;
        public static long SkillFormulaDamage(object native) => ((bft)native).bhsg == null ? 0 : ez.dzw(((bft)native).bhsk, ((bft)native).bhsg);

        // 照 bfi（闪电）的结算：施法者的敌人（izk）里活着的、在圆内的（加上对方体型 addRange），ez.dzf 造成技能伤害
        // dxt(攻击者, 目标, 伤害, 方向角, 类型, 是否技能暴击 izi, 暴伤 dyl, 来源, 技能, 实例)
        public static int SkillDamageArea(object native, Vector3 center, float radius, long? damage)
        {
            var s = (bft)native;
            var caster = s.bhsg;
            if (caster == null) return 0;
            long amount = damage ?? ez.dzw(s.bhsk, caster);
            bool crit = caster.jfa(AttackType.Skill);
            int hits = 0;
            var list = caster.jfc();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var t = list[i];
                if (t == null || !t.gameObject.activeSelf || t.jed() || t.bqjf == null) continue;
                if (!ez.dzc(center, t.bqjc.position, radius + t.bqjf.addRange)) continue;
                ez.dzf(caster, t, amount, ez.dzh(t.bqjc.position, center), AttackType.Skill, crit, ez.dzx(s.bhsk, caster), DamageSourceKind.ActiveSkill, ActiveSkillEnum.None, s);
                hits++;
            }
            return hits;
        }

        public static long SkillDamage(object native, Component target, long? damage)
        {
            var s = (bft)native;
            var caster = s.bhsg;
            if (caster == null || !(target is yi t) || t.jed()) return 0;
            long amount = damage ?? ez.dzw(s.bhsk, caster);
            return ez.dzf(caster, t, amount, ez.dzh(t.bqjc.position, caster.bqjc.position), AttackType.Skill, caster.jfa(AttackType.Skill),
                ez.dzx(s.bhsk, caster), DamageSourceKind.ActiveSkill, ActiveSkillEnum.None, s);
        }

        // oq.fyv(路径, 时长, 位置, 缩放)：从对象池取 Bundles/路径 的预制体，到时自动回收（游戏的闪电等技能都用它）
        public static void SpawnEffect(string path, Vector3 position, float duration, float scale) => cj.bpys?.fyv(path, duration, position, scale);

        // ---- Mod 资源（ModAssets）----
        // 游戏的预制体对象池 m：zop 路径 → 模板（第一次用时 m.czy 从 Resources 加载），zoq 路径 → 回收的实例。
        // 只在切场景时整体清空（m.Clear），所以 Mod 注销特效时要自己把这条路径的缓存清掉
        private static readonly AccessTools.FieldRef<Dictionary<string, GameObject>> pooledTemplates =
            AccessTools.StaticFieldRefAccess<Dictionary<string, GameObject>>(AccessTools.Field(typeof(m), "bacm"));
        private static readonly AccessTools.FieldRef<Dictionary<string, Queue<GameObject>>> pooledInstances =
            AccessTools.StaticFieldRefAccess<Dictionary<string, Queue<GameObject>>>(AccessTools.Field(typeof(m), "bacn"));

        public static void ForgetPooledEffect(string path)
        {
            pooledTemplates()?.Remove(path);
            var pools = pooledInstances();
            if (pools == null || !pools.TryGetValue(path, out var queue)) return;
            pools.Remove(path);
            foreach (var go in queue)
                if (go != null) UnityEngine.Object.Destroy(go);
        }

        // 技能按钮 bqn.pbh(技能, 按键) 里 bjgj.sprite = Resources.Load("Bundles/UI/Icon/Skill/" + bij.bijf)；
        // Resources.Load 补丁没装上时，技能栏这里单独补上（最常看到的地方）
        public static void FixSkillButtonIcon(object button, object skill)
        {
            var s = (bij)skill;
            if (s == null || !ModAssets.IsOurs(s.bijf)) return;
            var sprite = ModAssets.IconFor(s.bijf);
            if (sprite != null) ((bqn)button).bkfe.sprite = sprite;
        }

        // ---- 变体英雄（HeroVariants）----
        // 出生 bbs.ljm：属性取 cj.bpyu.bcru[英雄编号] 传给 yi.izy（成长值在这里拷进角色字段，之后不再读表），
        // 本地玩家再读原始表 cx.bamb["hero"][编号] 的 skill / zhuDong 列发专属技能（iuz 被动 / iuy 主动）。
        // kzz 期间把这两行临时换成改过的副本，结束后换回原版，所以别的玩家、下一次出生都不受影响。
        // 防篡改的影子副本（nr.fvp）在 iuh 之后才建立，改过的值和影子一致
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
            if (cj.bpyu != null && cj.bpyu.bdix.TryGetValue(key, out var attr))
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
                cj.bpyu.bdix[key] = attr;
            }
            if (variant.SkillOverride(out int skill, out bool active)
                && cx.bamb.TryGetValue("hero", out var t) && t is Dictionary<string, object> heroes
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
            if (r.HasAttribute && cj.bpyu != null) cj.bpyu.bdix[r.Key] = r.Attribute;
            if (r.HeroTable != null) r.HeroTable[r.Key] = r.HeroRow;
        }

        public static bool IsLocalSpawn(Component player) => player is bbs p && p.bqih;

        // 出生结束后：血条上的名字（yi.izy 里按 ez.dwy 算过一次，之后每帧读 roleName）
        public static void ApplyVariantName(Component player)
        {
            var p = (bbs)player;
            string name = HeroVariants.LocalName((int)p.heroType);
            if (name != null && p.roleType == RoleType.Player) p.roleName = name;
        }

        // ---- 技能实例 bht ----
        // 被动（qp）的 bhne 是默认值 0，而 0 是 D_SpellThunder，所以被动返回 None（-1）
        public static int SkillId(object s) => s is qw ? (int)ActiveSkillEnum.None : (int)((bij)s).bije;
        public static bool IsAutoCastOn(object s) => ((bij)s).bijy;

        // 在技能栏上：按钮 bhng 正显示这个技能
        public static bool IsOnSkillBar(object s) => ((bij)s).bijg is bqn b && ReferenceEquals(b.bkff, s);

        // 自动释放开关：右键技能图标时游戏调 bij.ndx()（翻转 bhny 并显示按钮上的自动标记 bhng.bjgl）；不在技能栏上（没有按钮）时只改字段
        public static void SetAutoCast(object s, bool on)
        {
            var b = (bij)s;
            if (b.bijy == on) return;
            // 按钮换绑到别的技能后旧技能的 bhng 不清，这时 mtt 会改错格子的标记：只在按钮正显示这个技能时走 mtt
            if (b.bijg != null && b.bijg.bkfg != null && ReferenceEquals(b.bijg.bkff, b)) b.ndx();
            else b.bijy = on;
        }

        // 免费放一个现成技能：yi.CmdCreateSkill(技能, 位置, 朝向角度, 目标, 技能书) → 服务器 → RpcCreateSkill，每个客户端都生成技能实例。
        // 不扣蓝、不进冷却、不播动作、不触发施法事件，身上也不需要有这个技能。原版用法：装备落雷 sc.cs（D_SpellThunder 落在敌人位置，目标 -1）、
        // 月光大剑 mh.cs（在自己位置朝敌人方向，角度 ez.dzh(目标, 自己)）。伤害在施法者本机算、归施法者
        public static void CastSkillFree(Component me, int skillId, Vector3 pos, float rotation) => ((yi)me).CmdCreateSkill((ActiveSkillEnum)skillId, pos, rotation, -1, 0);

        // 从 from 看向 to 的朝向角度（游戏的 ez.dzh）
        public static float Facing(Vector3 from, Vector3 to) => ez.dzh(to, from);

        // 雷之一闪的雷球上限：PlayerThunderMode.maxOrbCount（序列化字段，默认 10，读的时候限制在 1~32），英雄模式在 yi.bqjf。
        // 改过的实例都记下原值：英雄模型从对象池取（下一局可能还是这个实例）、变身时 bpia 会换成别的组件，所以撤销时把记下的全部改回去，
        // 不依赖撤销那一刻的本地玩家
        private static readonly List<(WeakReference mode, int original)> thunderTouched = new List<(WeakReference, int)>();
        private static AccessTools.FieldRef<PlayerThunderMode, int> maxOrbCount;

        // 本地玩家当前的雷电模式（变身中、不是雷电时为 null）
        public static object ThunderMode(Component p) => ((yi)p).bqjf as PlayerThunderMode;

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

        // 攻击距离：yi.ivx(加量)，遗物"瞄准镜"用的同一个，会同步给服务器（fbk 是诱饵）
        public static void AddAttackRange(Component p, float amount) => ((yi)p).ivx(amount);
        public static float CooldownRemaining(object s) => ((bij)s).kdfb5f2f52501482d81fe1c346b77db8b;
        public static int Cost(object s, Component p) => ((bij)s).k4c26e2de47124ac6a98c9c271958a0ae((bbs)p);
        // PayToWin 技能花的是金币不是蓝
        public static bool CostsGold(object s) => ((bij)s).bqry;
        // 开关类技能（献祭等）当前开着
        public static bool IsToggledOn(object s) => ((bij)s).bijv;
        public static int Charges(object s) => ((bij)s).k3637fdbcd15f41218bab00a5a9ed26d7;
        public static int MaxCharges(object s) => ((bij)s).k26d6dfcc45ef4b10954cb8159b2ea005;
        // bij.bijg 技能按钮、bhnd 所属玩家
        public static object ButtonSkill(object s) => ((bij)s).bijg?.bkff;
        public static Component SkillOwner(object s) => ((bij)s).bijd;

        // ---- 英雄专属技能：hero 表 skill 列是编号，zhuDong 为真时是主动技能（ActiveSkillEnum），否则是被动（PasssiveSkillEnum）。
        // 出生时 bbs.ljm 按它发放（主动 yi.jap、被动 yi.jaq） ----
        public static bool ReadHeroSkill(int heroId, out int skillId, out bool active)
        {
            skillId = 0;
            active = false;
            if (!cx.bamb.TryGetValue("hero", out var t) || !(t is Dictionary<string, object> rows)
                || !rows.TryGetValue(heroId.ToString(), out var r) || !(r is Dictionary<string, object> row))
                return false;
            if (!int.TryParse(Cell(row, "skill"), out skillId)) return false;
            active = row.TryGetValue("zhuDong", out var z) && z != null && (z is bool b ? b : bool.TryParse(z.ToString(), out b) && b);
            return true;
        }

        // 被动技能表 passsiveSkill：class（脚本类名）、icon、value（"|" 分隔）。名字的本地化 key 是 "p_" + 编号（yi.jaq）
        public static bool ReadPassive(int id, out string icon, out float[] values)
        {
            icon = null;
            values = new float[0];
            if (!cx.bamb.TryGetValue("passsiveSkill", out var t) || !(t is Dictionary<string, object> rows)
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
            if (!cj.bpyu.bdiz.TryGetValue((ActiveSkillEnum)id, out var d)) return false;
            cost = d.cost;
            duration = d.duration;
            charges = d.chargingNum;
            damageBase = d.damageBase;
            damageValue = d.damageValue;
            return true;
        }

        public static string SkillIcon(int id) => cj.bpyu.bdiz.TryGetValue((ActiveSkillEnum)id, out var d) ? IconPath("Skill/", d.icon) : null;

        // 玩家身上的技能 bbs.roleSkillList：主动技能看 bhne，被动技能（qp）看 bhnj（= 被动表的 id，见 bij.ndd）
        public static object FindActiveSkill(Component p, int id)
        {
            foreach (var s in ((bbs)p).roleSkillList)
                if (s != null && !(s is qw) && (int)s.bije == id) return s;
            return null;
        }

        // 游戏后来替换掉的专属技能：拿到"鸡舞"卡牌后 jb.epv 把 ChickenDance 换成 RealChickenDance
        public static object FindReplacedSkill(Component p, int id)
            => id == (int)ActiveSkillEnum.ChickenDance ? FindActiveSkill(p, (int)ActiveSkillEnum.RealChickenDance) : null;

        public static object FindPassiveSkill(Component p, int id)
        {
            string key = id.ToString();
            foreach (var s in ((bbs)p).roleSkillList)
                if (s is qw && s.bijj == key) return s;
            return null;
        }

        // ---- bht 上从表里拷来的数值（yi.jar / yi.jap 获得技能时拷贝；yh.njw 是诱饵）。nr.fvw 不校验这些，游戏自己（iz.eqb）也直接写 ----
        public static float CooldownTotal(object s) => ((bij)s).k2ef49c3285ec4ff78ed8a0f5629e1332;
        public static void SetCooldownTotal(object s, float v) => ((bij)s).k2ef49c3285ec4ff78ed8a0f5629e1332 = v;
        public static void SetCooldownRemaining(object s, float v) => ((bij)s).kdfb5f2f52501482d81fe1c346b77db8b = v;
        // 按键时判断蓝够不够用它；真正扣蓝时 ez.dzs 重新查表（所以 TuningHooks 还要临时替换表）
        public static int BaseCost(object s) => ((bij)s).k818080e29b624a3f887496ddd9b03270;
        public static void SetBaseCost(object s, int v) => ((bij)s).k818080e29b624a3f887496ddd9b03270 = v;
        public static void SetMaxCharges(object s, int v)
        {
            var skill = (bij)s;
            skill.k26d6dfcc45ef4b10954cb8159b2ea005 = v;
            if (skill.k3637fdbcd15f41218bab00a5a9ed26d7 > v) skill.k3637fdbcd15f41218bab00a5a9ed26d7 = v;
        }

        // 被动技能的数值：获得时 bij.ndd 把表里的 value 解析进私有 bhnq，再由 mtm 算出 bhnp = bhnq + levelup × 等级；
        // 多数被动每次触发都读 bhnp。升级（grs）会用 bhnq 重算覆盖，所以两个一起改
        private static readonly AccessTools.FieldRef<bij, float[]> passiveBase = AccessTools.FieldRefAccess<bij, float[]>("bijq");

        public static float[] PassiveValues(object s) => ((bij)s).bijp;

        // 504 内部折扣在 gou 里把折扣加进 shopDiscount、gov 按值删除；511 电锯恶魔在 gou 里把数值缓存到私有字段。
        // 这两个要先 gov（按旧值撤销），改完再 gou（按新值生效）
        private static readonly HashSet<string> rebindPassives = new HashSet<string> { "504", "511" };

        public static void SetPassiveValues(object s, float[] values)
        {
            var skill = (qw)s;
            bool rebind = rebindPassives.Contains(skill.bijj);
            if (rebind) skill.gtf();
            var current = skill.bijp;
            var baseValues = passiveBase(skill);
            for (int i = 0; i < values.Length; i++)
            {
                if (current != null && i < current.Length) current[i] = values[i];
                if (baseValues != null && i < baseValues.Length) baseValues[i] = values[i];
            }
            if (rebind) skill.gte();
        }

        // ---- 只改本地玩家的技能参数：施法者是本地玩家时，在 ez.eao / dye / dyf 执行期间把 bcrw[技能] 换成调过的副本，结束后换回原版。
        // 表本身一直是原版，镜像 Boss（zq 复制英雄技能）和其他玩家的同名技能不受影响 ----
        private static readonly List<(ActiveSkillEnum key, ActiveSkillData original)> swaps = new List<(ActiveSkillEnum, ActiveSkillData)>();

        public static bool BeginSwap(Component caster, int id)
        {
            if (caster == null || caster != nr.bqcj) return false;
            var tune = SkillTuning.Get(id);
            if (tune.Cost == 1f && tune.Duration == 1f) return false;
            var key = (ActiveSkillEnum)id;
            // 嵌套调用（dyf 里会再走到 dzc）时表已经换过了
            foreach (var s in swaps)
                if (s.key == key) return false;
            if (!cj.bpyu.bdiz.TryGetValue(key, out var original)) return false;
            var tuned = original;
            tuned.cost = Mathf.Max(0, Mathf.RoundToInt(original.cost * tune.Cost));
            tuned.duration = original.duration * tune.Duration;
            cj.bpyu.bdiz[key] = tuned;
            swaps.Add((key, original));
            return true;
        }

        public static void EndSwap()
        {
            if (swaps.Count == 0) return;
            var (key, original) = swaps[swaps.Count - 1];
            swaps.RemoveAt(swaps.Count - 1);
            cj.bpyu.bdiz[key] = original;
        }

        // 换过之后 dyf 的参数也要换成调过的那份
        public static ActiveSkillData CurrentRow(ActiveSkillEnum key) => cj.bpyu.bdiz[key];

        // ez.dzr(玩家, 技能, 目标点, 技能数据, 朝向, 目标 netId, 技能书编号, h = true)：游戏的免费释放（被动"长老的多重施法"就这么用）。
        // 不查蓝、不扣蓝、不进冷却，技能自己的检查和状态切换照常。不要用 h = false：冷却会扣到最后按下的那一格
        public static bool FreeCast(Component p, int skill, Vector3 pos, float rotation, int target, int book)
            => cj.bpyu.bdiz.TryGetValue((ActiveSkillEnum)skill, out var d)
               && ez.dzr((bbs)p, (ActiveSkillEnum)skill, pos, d, rotation, target, book, h: true);

        // ---- 属性加成（都是累加，负数撤销；不在 nr.fvw 的校验范围内）----
        // yi.iwq：攻速加成（比例，0.25 = +25%）；ez.edh 的 "AttackSpeed" 就是它
        public static void AddAttackSpeed(Component p, float ratio) => ((yi)p).iwq(ratio);
        // yi.ixm：护甲（会刷新属性面板）
        public static void AddArmor(Component p, int amount) => ((yi)p).ixm(amount);

        // ---- 男枪（PlayerShotgunMode，普攻组件）的弹药 bcc ----
        // bgac 弹数（私有 setter）、bgad 装填中、bgae 装填进度；上限是编译期常量 2（bcr.bgwc）。真方法：lpy 消耗、lpz 开始装填、
        // lqb 推进装填（满了直接 bgac = 2）、lqc 冲锋 +1（Min(2, …)）、lpx 补满、lqd 远端同步（Clamp 0..2）；其余同签名的都是诱饵。
        // huq 里空闲时"弹数 < 2 且停了 tacticalReloadDelay 秒"就战术装填；llk 把 bgac | (装填中 ? 4 : 0) 发给别人（弹数 ≥ 4 会溢出到装填位）。
        // 弹药 UI bij：mwj 建 2 个弹壳（bhro / bhrp，readonly 数组），LateUpdate 按 bhro.Length 逐个上色
        private static readonly AccessTools.FieldRef<PlayerShotgunMode, bcr> shotgunAmmo = AccessTools.FieldRefAccess<PlayerShotgunMode, bcr>("bgqp");
        private static readonly AccessTools.FieldRef<PlayerShotgunMode, float> shotgunIdle = AccessTools.FieldRefAccess<PlayerShotgunMode, float>("bgqw");
        private static readonly AccessTools.FieldRef<PlayerShotgunMode, float> tacticalDelay = AccessTools.FieldRefAccess<PlayerShotgunMode, float>("tacticalReloadDelay");
        private static readonly AccessTools.FieldRef<bcr, bool> ammoReloading = AccessTools.FieldRefAccess<bcr, bool>("<bgwe>k__BackingField");
        private static readonly AccessTools.FieldRef<bcr, float> ammoProgress = AccessTools.FieldRefAccess<bcr, float>("<bgwf>k__BackingField");
        private static readonly AccessTools.FieldRef<bcr, int> ammoCount = AccessTools.FieldRefAccess<bcr, int>("<bgwd>k__BackingField");
        private static readonly AccessTools.FieldRef<yi, RoleModeBase> roleMode = AccessTools.FieldRefAccess<yi, RoleModeBase>("roleModeBase");

        // 本地玩家的弹药对象（不是男枪时为 null）
        public static object LocalShotgunAmmo()
        {
            var p = nr.bqcj;
            return p != null && roleMode(p) is PlayerShotgunMode m ? shotgunAmmo(m) : null;
        }

        public static int AmmoCount(object ammo) => ammoCount((bcr)ammo);

        // 以下几个替换 bcc 的真方法（补丁前缀里调用，只用于本地玩家的弹药），max 换掉原来的 2
        public static void AmmoStartReload(object a, int max)
        {
            var b = (bcr)a;
            if (ammoCount(b) >= max || ammoReloading(b)) return;
            ammoReloading(b) = true;
            ammoProgress(b) = 0f;
        }

        public static bool AmmoAdvanceReload(object a, float dt, float speed, float duration, int max)
        {
            var b = (bcr)a;
            if (!ammoReloading(b)) return false;
            ammoProgress(b) = Mathf.Clamp01(ammoProgress(b) + Mathf.Max(0f, dt) * Mathf.Max(0.1f, speed) / Mathf.Max(0.1f, duration));
            if (ammoProgress(b) < 1f) return false;
            ammoCount(b) = max;
            b.mad();
            return true;
        }

        public static void AmmoAdd(object a, int n, int max)
        {
            var b = (bcr)a;
            ammoCount(b) = Math.Min(max, ammoCount(b) + n);
            b.mad();
        }

        public static void AmmoFill(object a, int max)
        {
            var b = (bcr)a;
            ammoCount(b) = max;
            b.mad();
        }

        // huq 之后：弹数在 2 以上但没满时，原版不会战术装填，这里补上
        public static void AmmoTacticalReload(object mode, int max)
        {
            var m = (PlayerShotgunMode)mode;
            var b = shotgunAmmo(m);
            var p = m.playerBase;
            if (p == null || p != nr.bqcj || p.jed() || ammoReloading(b)) return;
            if (p.bqjd != RoleState.Idle && p.bqjd != RoleState.Run) return;
            int n = ammoCount(b);
            if (n >= 2 && n < max && shotgunIdle(m) >= tacticalDelay(m)) AmmoStartReload(b, max);
        }

        public static bool IsLocalShotgun(object mode) => mode is PlayerShotgunMode m && m.playerBase != null && m.playerBase == nr.bqcj;

        // llk 的替代：发给别人的弹数截到 3，免得溢出到"装填中"那一位
        private static readonly AccessTools.FieldRef<PlayerShotgunMode, int> ammoSent = AccessTools.FieldRefAccess<PlayerShotgunMode, int>("bgrc");

        public static void AmmoSync(object mode)
        {
            var m = (PlayerShotgunMode)mode;
            if (!m.playerBase.brdt) return;
            var b = shotgunAmmo(m);
            int num = Math.Min(3, ammoCount(b)) | (ammoReloading(b) ? 4 : 0);
            if (num != ammoSent(m) && m.playerBase.jfe(num)) ammoSent(m) = num;
        }

        // 弹药 UI：弹壳不够 max 个时照 mwj 的样子补上（多的删掉，最少 2 个）
        private static readonly FieldInfo uiShells = AccessTools.Field(typeof(bjr), "biqa");
        private static readonly FieldInfo uiBrass = AccessTools.Field(typeof(bjr), "biqb");
        private static readonly AccessTools.FieldRef<bjr, RectTransform> uiRoot = AccessTools.FieldRefAccess<bjr, RectTransform>("bipy");

        public static void AmmoUi(object ui, int max)
        {
            var u = (bjr)ui;
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

        public static bool IsLocalAmmoUi(object ui) => ui is bjr u && u != null && LocalShotgunAmmo() != null;

        // 多重攻击：多重攻击被动（B多重攻击）就是 attackNum += n，再 CmdAttackNum 同步
        public static void AddAttackNum(Component p, int n)
        {
            var b = (bbs)p;
            b.attackNum += n;
            if (b.brdt) b.CmdAttackNum(b.attackNum);
        }

        public static int AttackNum(Component p) => ((yi)p).attackNum;

        // 在地上掉道具（ItemType 编号）：bbs.CmdCreateItemByPosWithNum，Remains_19、转变之石都这么掉
        public static void DropItem(Component p, int itemType, Vector3 pos, int count) => ((bbs)p).CmdCreateItemByPosWithNum((ItemType)itemType, pos, count, false);

        // 范围伤害：nr.fvo（死灵祭司"对死者的供奉"用的）——范围内的敌人各吃一次技能伤害（暴击照常判定），再播特效
        public static void DamageArea(Component p, float damage, Vector3 center, float radius, string effect, float effectScale, bool spareTeammates = false)
        {
            // 游戏按 (int) 转：float 的 int.MaxValue 是 2147483648，转过去溢出成负数
            damage = Mathf.Min(damage, 2e9f);
            if (effect != null && !spareTeammates)
            {
                nr.fvo((yi)p, damage, center, radius, effect, effectScale);
                return;
            }
            // 照抄 fsp 的伤害部分：不播特效（频繁调用时每次都 CmdPlayEffect 会刷屏，也占网络），或者跳过队友的怪
            var a = (yi)p;
            bool crit = a.jfa(AttackType.Skill);
            foreach (yi item in cj.bpyx.fzw(radius, center))
            {
                if (spareTeammates && IsTeammateMonster(p, item)) continue;
                ez.dzf(a, item, (int)damage, ez.dzh(item.bqjc.position, center), AttackType.Skill, crit, 0f);
            }
            if (effect != null) ((bbs)p).CmdPlayEffect(effect, 1f, center, effectScale);
        }

        // 联机时每个玩家有自己的怪（authorityId = 主人的 netId），打别人的怪收益归动手的人。bet.mof 是原版"队友的怪"判断
        public static bool IsTeammateMonster(Component me, Component enemy) => bet.mof((bbs)me, ((yi)enemy).authorityId);

        // 送金币：照抄商店"丢金币"（bbs.lai）——先用官方方法扣自己的钱，再在指定位置生成无主金币（谁都能捡）。
        // drop 可以大于 pay：服务器不校验生成的数量（伙伴之刃分收益也是凭空发）
        public static void GiveGold(Component me, Vector3 pos, int pay, int drop)
        {
            var p = (bbs)me;
            if (pay > 0) p.k9920daae7c484b21bc3c60afd0889cb2(p.jby(), -pay, c: false);
            if (drop > 0) p.CmdCreateItemByPosWithNum(ItemType.Gold, pos, drop, false);
        }

        // 特效：bbs.CmdPlayEffect(路径, 速度, 位置, 缩放)，广播给所有人（fsp 也这么播）
        public static void PlayEffect(Component p, string effect, Vector3 pos, float scale) => ((bbs)p).CmdPlayEffect(effect, 1f, pos, scale);

        // ---- 队友（都是原版的命令，队友没装 Mod 也能正常处理）----
        // yi.jcq(量, 目标)：治疗目标，内部 CmdUpdateHp(+量, 目标 netId)，服务器不校验发送者（装备 MadMan 治疗队友就这么用）
        public static void HealOther(Component me, Component target, double amount) => ((yi)me).jcq(amount, (yi)target);

        // bbs.CmdAddNearbyHumanityShield(范围, 比例)：服务器 kxp 给自己周围的所有玩家（含自己）各加 最大生命 × 比例 的护盾，范围 ≤ 50、比例 ≤ 1
        public static void ShieldNearbyPlayers(Component me, float radius, float ratio) => ((bbs)me).CmdAddNearbyHumanityShield(radius, ratio);

        // bbs.CmdAddBuff(目标, 来源, 类型, 值, 时长, 等级) → 服务器 lcp → 目标 RpcAddBuff → yb 里按类型 new 效果类（yb.cs 4240 起的 switch）：
        // SkillHaste → hy：skillCdReduce += 值（技能急速）；IronArmor → ha：护甲 bepy += 值；
        // SacredEmberBlessing → hu：值加在 buffAddDamage 上——只有 AttackType.Buff（持续伤害）读它，普攻和技能不吃
        public static void GiveBuff(Component me, Component target, PlayerBuff buff, float value, float seconds)
        {
            LocalBuffType type = buff == PlayerBuff.SkillHaste ? LocalBuffType.SkillHaste
                : buff == PlayerBuff.Armor ? LocalBuffType.IronArmor : LocalBuffType.SacredEmberBlessing;
            ((bbs)me).CmdAddBuff(((yi)target).brdu, ((yi)me).brdu, type, value, seconds, 0);
        }

        // ---- 通用加成（海克斯通用强化）----
        // ez.edh(键, 整数)：遗物、卡牌、锻体读"键|值"时走的就是它，作用于本地玩家 nr.bqcj。百分比类的键传 25 表示 25%。
        // 有的分支直接写字段（skillCdReduce、addDamagePercent……），不经过防篡改校验，也不会自动撤销，撤销就传负数
        private static Action<string, int> addAttribute;

        public static void AddAttribute(string key, int amount)
        {
            addAttribute = addAttribute ?? AccessTools.MethodDelegate<Action<string, int>>(
                AccessTools.Method(typeof(ez), "edh", new[] { typeof(string), typeof(int) }));
            addAttribute(key, amount);
        }

        // 身上的主动技能（roleSkillList 里除了被动 qp 的都算，包括技能书学到的）
        public static List<object> ActiveSkillsOf(Component p)
        {
            var result = new List<object>();
            foreach (var s in ((bbs)p).roleSkillList)
                if (s != null && !(s is qw) && s.bije != ActiveSkillEnum.None) result.Add(s);
            return result;
        }

        // 属性锻造器：商店买锻造器走 pr.gln → 检查 ez.eai（三选一界面空闲）和 nr.bcid → pr.gnq(ForgingTier)。
        // gjl 抽稀有度、弹"属性选择"三选一，选中后经 pr.gnr 加属性（官方路径，过防篡改）。直接调 gjl 和买一个一样，只是价格不涨
        private static Action<pr, pr.ForgingTier> forge;

        public static bool CanForge() => !nr.bcid && !ChoiceBusy() && bz.dfe<pr>() != null;

        public static void Forge(int tier)
        {
            forge = forge ?? AccessTools.MethodDelegate<Action<pr, pr.ForgingTier>>(
                AccessTools.Method(typeof(pr), "gnq", new[] { typeof(pr.ForgingTier) }));
            forge(bz.dfe<pr>(), tier == 2 ? pr.ForgingTier.Epic : tier == 1 ? pr.ForgingTier.Gold : pr.ForgingTier.Normal);
        }

        // 沙漠死神的普攻组件 PlayerKoboldMode：噬魂一击把 bftv 置为 true（下一次普攻强化），lko 在出手时结算：
        // 伤害 kcm(kse(out 普攻增伤))，dxt 返回值 ≥ 目标生命 + 护盾算击杀，击杀调 lkp(目标) 叠层（普通 4 / 精英 12 / Boss 75），
        // 多重攻击的额外目标是 kqs 返回的共享列表 nr.bcii。结算完调 lkt 清掉强化——补丁挂在 lkt 前面，这时 bftv 还是 true
        private static readonly AccessTools.FieldRef<PlayerKoboldMode, bool> koboldEmpowered = AccessTools.FieldRefAccess<PlayerKoboldMode, bool>("bgpw");
        private static Action<PlayerKoboldMode, yi> koboldKillStack;
        private static Action<PlayerKoboldMode, int> koboldAddStacks;
        private static Func<PlayerKoboldMode, long, long> koboldDamage;
        private static readonly List<yi> splashTargets = new List<yi>();

        // 镜像 Boss 用的子类、别人的英雄、没有强化的普攻都不算
        public static bool IsLocalEmpoweredStrike(object mode)
            => mode is PlayerKoboldMode m && m.GetType() == typeof(PlayerKoboldMode) && koboldEmpowered(m)
               && m.playerBase != null && m.playerBase == nr.bqcj && m.playerBase.brdt && m.playerBase.trackRoleBase != null;

        // 对主目标周围的敌人再打一次（伤害 × ratio），击杀照常叠层；hitStacks > 0 时没打死也加这么多层（休息阶段不加）。返回打到几个
        public static int SoulSplash(object mode, float radius, float ratio, int hitStacks)
        {
            var m = (PlayerKoboldMode)mode;
            koboldKillStack = koboldKillStack ?? AccessTools.MethodDelegate<Action<PlayerKoboldMode, yi>>(AccessTools.Method(typeof(PlayerKoboldMode), "lus"));
            koboldAddStacks = koboldAddStacks ?? AccessTools.MethodDelegate<Action<PlayerKoboldMode, int>>(AccessTools.Method(typeof(PlayerKoboldMode), "lut"));
            koboldDamage = koboldDamage ?? AccessTools.MethodDelegate<Func<PlayerKoboldMode, long, long>>(AccessTools.Method(typeof(PlayerKoboldMode), "klf"));
            var p = m.playerBase;
            var main = p.trackRoleBase;
            Vector3 center = main.bqjc.position;
            splashTargets.Clear();
            foreach (var e in p.jfc())
            {
                if (e == null || e == main || !e.gameObject.activeInHierarchy || e.bqjf == null || !e.jee()) continue;
                if (p.attackNum > 1 && nr.bcii.Contains(e)) continue;
                if (ez.dzi(e.bqjc.position, center) <= radius + e.bqjf.addRange) splashTargets.Add(e);
            }
            if (splashTargets.Count == 0) return 0;
            long damage = (long)Math.Round(koboldDamage(m, p.lbb(out float bonus)) * (double)ratio);
            float angle = p.bqjc.eulerAngles.y;
            foreach (var e in splashTargets)
            {
                long before = e.hp + e.bqie;
                long dealt = ez.dzf(p, e, damage, angle, AttackType.Normal, p.jfa(AttackType.Normal), bonus);
                if (dealt >= before) koboldKillStack(m, e);
                else if (hitStacks > 0 && !nr.bchu && e is xv) koboldAddStacks(m, hitStacks);
            }
            return splashTargets.Count;
        }
        // 游戏的无敌药水 buff（roleBuffManager.mim<T>(名字, 秒)，同名 buff 重复添加时刷新时长）
        public static void GiveInvulnerable(Component p, float seconds) => ((yi)p).roleBuffManager.msq<Buff无敌>("Buff无敌", seconds);

        // ---- 悬停提示框 bka：技能（wh）、遗物和 buff（iq）、三选一卡片（brk.pey）都用它。njb(true, 信息) 显示、njb(false, default) 隐藏；
        // 还没创建时用 dqk 打开（同 brk.pey）----
        public static void ShowTip(string name, string info, string icon, Vector3 position, int quality)
        {
            var tip = cj.bpyk?.dsc<bli>() ?? cj.bpyk?.drv<bli>() as bli;
            tip?.nva(true, new bli.TipInfo
            {
                nameStr = name ?? "",
                info = info ?? "",
                iconPath = icon ?? "",
                showPos = position,
                quality = quality,
                isRelic = false,
            });
        }

        public static void HideTip() => cj.bpyk?.dsc<bli>()?.nva(false, default(bli.TipInfo));

        // bij.bikf：技能悬停提示末尾的附加说明（wo.OnPointerEnter 拼在描述后面）。游戏自己也会写（五五开的外挂、土豆兄弟），所以只追加
        public static string SkillExtraInfo(object s) => ((bij)s).bikf;
        public static void SetSkillExtraInfo(object s, string v) => ((bij)s).bikf = v;

        // ---- 波次配置 nr.bchi（SOSpawnConfig）：enemySpawnData[i] 是第 i+1 波，eventList 带 Roguelike 时，
        // 进入这一波前的休息阶段弹遗物三选一（服务器先 bbsr++ 再发 OnStartRest）----
        public static int TotalWaves() => nr.bchi?.enemySpawnData?.Length ?? 0;

        public static bool RelicBeforeWave(int index)
        {
            var waves = nr.bchi?.enemySpawnData;
            if (waves == null || index < 0 || index >= waves.Length) return false;
            var events = waves[index].eventList;
            return events != null && Array.IndexOf(events, RoguelikeEventType.Roguelike) >= 0;
        }

        // 开场界面 bkh：关闭时（bift = MyServerNetworkManager.qvh）游戏弹开局遗物
        public static bool OpeningPending()
        {
            var ui = cj.bpyk?.dsc<blp>();
            return ui != null && ui.baqf;
        }

        // 重试波次的快照有效（wi.bqdr）
        public static bool CheckpointValid() => wi.bqdr;

        // ---- 技能栏 bqm.bkdn（bpf 按钮，bqn.bkff 技能实例，bqn.bkfh 图标上的 bpa 组件） ----
        public static void ReadSkillBar(List<(object button, object skill, RectTransform icon)> output)
        {
            output.Clear();
            var bar = cj.bpyk?.dsc<bqm>();
            if (bar?.bkdn == null) return;
            foreach (var b in bar.bkdn)
            {
                if (b == null || b.bkfh == null) continue;
                object skill = b.bkff != null && b.bkff.bije != ActiveSkillEnum.None ? b.bkff : null;
                output.Add((b, skill, (RectTransform)b.bkfh.transform));
            }
        }
    }

    // 自定义被动在游戏里的实例：游戏按被动表的 class 列（ez.ecl）创建它，它把获得、移除、每帧转给 PassiveSkills 里 Mod 的行为对象。
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

        public override void gte()
        {
            base.gte();
            PassiveSkills.Added(Instance, this);
        }

        public override void gtf()
        {
            base.gtf();
            PassiveSkills.Removed(Instance);
        }

        public override void gtm()
        {
            base.gtm();
            if (bijd != null && bijd == nr.bqcj) PassiveSkills.Update(Instance, this);
        }
    }

    // 自定义遗物在游戏里的实例：bbs.kzi 的后缀创建，kqm 放进 bfna 后调 gtb；bba 每帧对列表里的每个遗物调 gtf
    internal sealed class ModRelic : rv
    {
        public readonly int DefId;
        public readonly int Instance;

        public ModRelic(int defId)
        {
            DefId = defId;
            Instance = CustomRelics.Created(defId);
        }

        public override void gxl()
        {
            base.gxl();
            CustomRelics.Added(Instance, this);
        }

        public override void gxm()
        {
            base.gxm();
            CustomRelics.Removed(Instance);
        }

        protected override void gxn(int a, int b)
        {
            base.gxn(a, b);
            CustomRelics.LevelChanged(Instance, this, a, b);
        }

        public override void gxp()
        {
            base.gxp();
            if (bdya != null && bdya == nr.bqcj) CustomRelics.Update(Instance, this);
        }
    }

    // 自定义装备特效在游戏里的实例：bbm.kpw 的后缀创建，khh 设好拥有者 bbcm、等级 bbcn、数值 bbco / bbcp 后调 Init；
    // 卸下时 Clear，强化 evl / 降级 evm，装备每帧调 euv。远程玩家、镜像 Boss 身上的也会创建，只有本地玩家的才接 Mod 的行为。
    // 字段只放整数：重试波次（bcm.lzj）会把快照里的字段写回特效对象
    internal sealed class ModEquipEffect : kh
    {
        public readonly int DefId;
        public int Instance;

        public ModEquipEffect(int defId) => DefId = defId;

        private bool Local => bbra != null && bbra == nr.bqcj;

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

        public override void eyg()
        {
            int old = bbrb;
            base.eyg();
            if (Instance != 0) CustomEquipEffects.LevelChanged(Instance, this, old, bbrb);
        }

        public override void eyh(int a)
        {
            int old = bbrb;
            base.eyh(a);
            if (Instance != 0) CustomEquipEffects.LevelChanged(Instance, this, old, bbrb);
        }

        public override void exq()
        {
            base.exq();
            if (Instance != 0 && Local) CustomEquipEffects.Update(Instance, this);
        }
    }

    // 自定义主动技能在游戏里的实例：ez.eao 的后缀创建它并放进 bcxi，pt.dcj 每帧推进。
    // kea 每个客户端都执行（mmn 只在施法者那边执行，ModKit 的伤害方法自己判断 IsMine，所以不用它）
    internal sealed class ModSkill : bft
    {
        public readonly int DefId;
        public int Instance;
        // 换动作前的控制器和动画机（游戏的 bfh 也存控制器字段）
        public RuntimeAnimatorController SavedController;
        public Animator SavedAnimator;
        public RuntimeAnimatorController OurController;

        public ModSkill(int defId) => DefId = defId;

        protected override void kms(float a) => ActiveSkills.Update(Instance, a);

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
