using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DBModKit
{
    // 游戏混淆名只出现在这个文件（以及 Compat 的检查清单和 Hooks 的补丁签名）。
    // 游戏更新后对照 _DungeonBrawl_tools/NOTES.md 修改这里即可，Mod 无需改动。
    // 只在 ModKit.Compatible 时调用；每个方法单独 JIT，类型缺失时不会提前崩溃。
    internal static class GameRaw
    {
        // 本地玩家 nn.bpbu（bbe : yb）
        public static Component LocalPlayer() => nn.bpbu;

        // cj.boyi.dff：找不到 key 时返回默认值，不打警告
        public static string Localize(string key, string fallback) => cj.boyi.dff(key, fallback);

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

        // ---- 玩家 bbe ----
        public static int Mana(Component p) => ((bbe)p).bfru;
        public static int MaxMana(Component p) => ((bbe)p).bfrv;
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
        public static void SubscribeHits(System.Action<Component, Component, long, long, long, int, bool, bool> handler)
        {
            bdc.bgfu += hit => handler(hit.source, hit.target, hit.fact.Amount + System.Math.Max(0L, hit.fact.ShieldDamage),
                hit.targetHp, hit.targetShield, hit.fact.SkillId, hit.fact.Active, hit.fact.Critical);
        }

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
