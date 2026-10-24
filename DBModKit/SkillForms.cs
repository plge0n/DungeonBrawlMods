using System;
using System.Collections.Generic;

namespace DBModKit
{
    /// <summary>
    /// 改几个专属技能的形态（海克斯招牌卡用）。只对本地玩家生效；多个 Mod 同时设置时取最大的。卸载时自动撤销。
    /// <list type="bullet">
    /// <item>撕咬（哥布林之王 518）：击杀加的生命上限乘倍率。游戏在服务器上算（<c>bbr.krt</c>），所以只在单人或者自己是房主时生效。</item>
    /// <item>炸弹（炸弹客 513）：十字爆炸的长度和宽度乘倍率（<c>bhy.nbp</c> 算出来的范围）。</item>
    /// <item>雷之一闪（雷电 521）：雷球上限（= 最多几段）加几个，游戏最多 32。</item>
    /// </list>
    /// </summary>
    public static class SkillForms
    {
        public static bool BiteAvailable { get; internal set; }
        public static bool BombAvailable { get; internal set; }
        public static bool ThunderAvailable => ModKit.Compatible;

        private static readonly Dictionary<string, float> bite = new Dictionary<string, float>();
        private static readonly Dictionary<string, float> bomb = new Dictionary<string, float>();
        private static readonly Dictionary<string, int> thunder = new Dictionary<string, int>();

        internal static float BiteMultiplier { get; private set; } = 1f;
        internal static float BombMultiplier { get; private set; } = 1f;
        internal static int ThunderExtra { get; private set; }

        internal static void SetBite(string owner, float multiplier) => Set(bite, owner, multiplier, 1f, "撕咬成长", v => BiteMultiplier = v);

        internal static void SetBomb(string owner, float multiplier) => Set(bomb, owner, multiplier, 1f, "炸弹范围", v => BombMultiplier = v);

        internal static void SetThunder(string owner, int extra)
        {
            if (extra <= 0) thunder.Remove(owner);
            else thunder[owner] = extra;
            int max = 0;
            foreach (var v in thunder.Values) max = Math.Max(max, v);
            ThunderExtra = max;
            RunLog.Info(owner, "雷之一闪", "雷球上限", ("加", extra), ("合计", max));
            ApplyThunder();
        }

        private static void Set(Dictionary<string, float> d, string owner, float value, float none, string what, Action<float> combined)
        {
            if (value <= none) d.Remove(owner);
            else d[owner] = value;
            float max = none;
            foreach (var v in d.Values) max = Math.Max(max, v);
            combined(max);
            RunLog.Info(owner, what, "倍率", ("设置", value), ("合计", max));
        }

        internal static void ClearOwner(string owner)
        {
            if (bite.Remove(owner)) SetBite(ModKitPlugin.Name, 0f);
            if (bomb.Remove(owner)) SetBomb(ModKitPlugin.Name, 0f);
            if (thunder.Remove(owner)) SetThunder(ModKitPlugin.Name, 0);
        }

        private static float nextCheck;
        private static object appliedTo;
        private static int appliedExtra;

        // 设置时、以及每秒检查一次：变身结束换回原组件、新一局英雄重建、设置撤销时，把上限写到当前的雷电模式上（其他实例改回原值）
        internal static void ApplyThunder()
        {
            if (!ModKit.Compatible) return;
            var me = GameRaw.LocalPlayer();
            object mode = me != null ? GameRaw.ThunderMode(me) : null;
            if (ReferenceEquals(mode, appliedTo) && appliedExtra == ThunderExtra) return;
            appliedTo = mode;
            appliedExtra = ThunderExtra;
            Safe.Run("雷之一闪雷球上限", () => GameRaw.SetThunderOrbs(mode, ThunderExtra));
        }

        internal static void Tick()
        {
            if (thunder.Count == 0 && appliedExtra == 0) return;
            if (UnityEngine.Time.unscaledTime < nextCheck) return;
            nextCheck = UnityEngine.Time.unscaledTime + 1f;
            ApplyThunder();
        }
    }
}
