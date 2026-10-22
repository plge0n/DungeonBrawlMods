using System;
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
        public static float MoveSpeed(Component r) => ((yb)r).bpgy;

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
