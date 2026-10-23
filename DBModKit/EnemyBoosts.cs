using System.Collections.Generic;

namespace DBModKit
{
    /// <summary>
    /// 各个 Mod 叠加在怪物身上的倍率，乘在 <see cref="GameData"/> 的怪物数值之上，不会覆盖玩家自己的数值包。
    /// 生命、护盾在刷怪时乘（已经在场上的怪不变）；攻击是怪物打到本地玩家时直接放大伤害（小怪表里的攻击只有个位数，乘表取整等于没乘）。
    /// 多个 Mod 的倍率相乘。用 <see cref="ModContext.SetEnemyBoost"/> 设置，卸载时自动撤销。
    /// </summary>
    public static class EnemyBoosts
    {
        private static readonly Dictionary<string, (float hp, float attack, float shield)> entries = new Dictionary<string, (float, float, float)>();

        /// <summary>刷怪补丁装上了（和 GameData 共用一个补丁）。</summary>
        public static bool Available => GameData.Available;

        internal static bool Active => entries.Count > 0;

        public static float Hp { get; private set; } = 1f;
        public static float Attack { get; private set; } = 1f;
        public static float Shield { get; private set; } = 1f;

        internal static void Set(string owner, float hp, float attack, float shield)
        {
            if (hp == 1f && attack == 1f && shield == 1f)
            {
                if (!entries.Remove(owner)) return;
            }
            else
            {
                if (entries.TryGetValue(owner, out var old) && old == (hp, attack, shield)) return;
                entries[owner] = (hp, attack, shield);
            }
            Combine();
            RunLog.Info(owner, "怪物倍率", "叠加倍率", ("生命", hp), ("攻击", attack), ("护盾", shield), ("合计", $"{Hp:0.##}/{Attack:0.##}/{Shield:0.##}"));
        }

        internal static void ClearOwner(string owner)
        {
            if (entries.Remove(owner)) Combine();
        }

        private static void Combine()
        {
            float hp = 1f, attack = 1f, shield = 1f;
            foreach (var e in entries.Values)
            {
                hp *= e.hp;
                attack *= e.attack;
                shield *= e.shield;
            }
            Hp = hp;
            Attack = attack;
            Shield = shield;
        }
    }
}
