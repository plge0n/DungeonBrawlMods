using System.Collections.Generic;
using UnityEngine;

namespace SmartSkill
{
    // 游戏混淆名都只在这里出现，游戏更新后对照 _DungeonBrawl_tools/NOTES.md 修改这一个文件即可
    internal static class GameApi
    {
        public static bbe LocalPlayer => nn.bpbu;

        public static float HpPercent(yb role) => (float)role.hp / Mathf.Max(1L, role.maxHp);

        public static int Mana(bbe player) => player.bfru;

        public static int MaxMana(bbe player) => player.bfrv;

        public static ActiveSkillData SkillData(ActiveSkillEnum skill) => cj.boyl.bcrw[skill];

        public static float CastRange(ActiveSkillData data) => data.castingRange > 0f ? data.castingRange : data.range;

        // indicator 为 None/Switch 的技能不需要瞄准
        public static bool IsSelfCastable(ActiveSkillData data) => data.indicator == "None" || data.indicator == "Switch";

        // 与技能提示框 bht.mtb 一致：名字 key 为 "a_" + 技能编号，描述为 "a_" + 编号 + "_m"
        public static string SkillName(ActiveSkillEnum skill) => cj.boyi.dff("a_" + (int)skill, skill.ToString());

        public static string TagName(SkillTag tag) => cj.boyi.dff("skillTag_" + tag, tag.ToString());

        public static List<yb> Enemies(yb role) => role.izk();

        public static bool IsAlive(yb role) => role != null && role.bphx != null && role.iyn();

        public static Vector3 Position(yb role) => role.bphx.position;

        // 水平距离
        public static float Distance(yb from, yb to) => from.ivj(to.bphx.position);

        // 体型补偿：大体型敌人判定距离更远
        public static float AddRange(yb role) => role.bpia != null ? role.bpia.addRange : 0f;

        public static bool IsBoss(yb role) => role is xo enemy && enemy.isBoss;

        public static bool IsElite(yb role) => role is xo enemy && enemy.isElite;

        // 技能栏里的技能格子
        public static IEnumerable<bht> SkillSlots()
        {
            var bar = cj.boyb?.dqr<bpe>();
            if (bar == null || bar.bjes == null) yield break;
            foreach (var button in bar.bjes)
                if (button != null && button.bjgk != null && button.bjgk.bhne != ActiveSkillEnum.None)
                    yield return button.bjgk;
        }

        public static ActiveSkillEnum SkillOf(bht slot) => slot.bhne;

        public static bool IsAutoCastOn(bht slot) => slot.bhny;

        public static float CooldownRemaining(bht slot) => slot.k896ff3548b31472b8b2cf8f649c25e65;

        public static int Cost(bht slot, bbe player) => slot.kacb7d091b5904139a602f04ed9552242(player);

        // PayToWin 技能花的是金币不是蓝
        public static bool CostsGold(bht slot) => slot.bpqi;
    }
}
