using System.Collections.Generic;
using DBModKit;

namespace DBStudio
{
    // 玩法包：玩家做的改动（目前只有数值，之后加规则、强化）。存在 BepInEx/config/DBStudio/packs/，复制文件就能分享
    internal sealed class Pack
    {
        public string Name = "我的玩法";
        // 玩家的总开关；即使打开，联机时也会自动停用
        public bool Enabled = true;
        // 表 → 键（技能枚举名 / 英雄编号 / 怪物枚举名）→ 字段 → 值；怪物的值是倍率
        public Dictionary<DataTable, Dictionary<string, Dictionary<string, double>>> Values =
            new Dictionary<DataTable, Dictionary<string, Dictionary<string, double>>>();
    }

    // 当前玩法包的加载、保存，以及"只在单人时生效"
    internal static class Packs
    {
        private static JsonConfig<Pack> config;

        public static Pack Current => config.Data;

        // 玩法包没生效的原因；生效时为 null
        public static string InactiveReason
        {
            get
            {
                if (GameData.Active) return null;
                if (!Current.Enabled) return "已关闭";
                if (!GameData.Ready) return "等游戏加载完";
                return "联机中，已停用（表已恢复原版）";
            }
        }

        public static void Init(ModContext mod)
        {
            config = mod.LoadJson<Pack>("DBStudio/packs/default.json");
            var owner = new object();
            GameData.Owner = owner;
            GameData.Load(Current.Values);
            GameData.Changed += Save;
            // 卸载时表恢复原版；先退订，免得把清空后的结果存回文件。
            // 热重载时新实例已经接管（Owner 变了），这时不能清空
            mod.AddCleanup(() =>
            {
                GameData.Changed -= Save;
                if (GameData.Owner != owner) return;
                GameData.Owner = null;
                GameData.Active = false;
                GameData.Load(null);
            });
            mod.OnUpdate(UpdateActive);
        }

        public static void SetEnabled(bool enabled)
        {
            if (Current.Enabled == enabled) return;
            Current.Enabled = enabled;
            config.MarkDirty();
            UpdateActive();
        }

        // 每帧检查：有人进房间就停用，回到单人再恢复
        private static void UpdateActive()
        {
            bool want = Current.Enabled && Game.IsSolo && GameData.Ready;
            if (GameData.Active == want) return;
            GameData.Active = want;
            if (GameData.Active) EventLog.Add("玩法包生效");
            else if (Current.Enabled && GameData.Ready) EventLog.Add("进入联机，玩法包停用，数值已恢复原版");
            else EventLog.Add("玩法包已关闭");
        }

        private static void Save()
        {
            Current.Values = GameData.Snapshot();
            config.MarkDirty();
        }
    }
}
