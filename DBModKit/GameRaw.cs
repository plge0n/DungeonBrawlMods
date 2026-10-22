using System.Collections.Generic;
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

        // ---- 玩家 bbe ----
        public static int Mana(Component p) => ((bbe)p).bfru;
        public static int MaxMana(Component p) => ((bbe)p).bfrv;

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
