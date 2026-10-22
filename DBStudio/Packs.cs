using System.Collections.Generic;
using DBModKit;
using Newtonsoft.Json;

namespace DBStudio
{
    // 玩法包：玩家做的改动（数值、规则，之后加强化）。存在 BepInEx/config/DBStudio/packs/，复制文件就能分享
    internal sealed class Pack
    {
        public string Name = "我的玩法";
        // 玩家的总开关；即使打开，联机时握手不通过也会自动停用
        public bool Enabled = true;
        // 表 → 键（技能枚举名 / 英雄编号 / 怪物枚举名）→ 字段 → 值；怪物的值是倍率
        public Dictionary<DataTable, Dictionary<string, Dictionary<string, double>>> Values =
            new Dictionary<DataTable, Dictionary<string, Dictionary<string, double>>>();
        public List<Rule> Rules = new List<Rule>();
        public List<Augment> Augments = new List<Augment>();
        public AugmentOffer Offer = new AugmentOffer();
    }

    // 当前玩法包的加载、保存，以及联机时按 ModKit 的握手结果（Multiplayer）启停
    internal static class Packs
    {
        private static JsonConfig<Pack> config;
        private static ModContext context;
        private static float nextDeclare;

        public static Pack Current => config.Data;

        // 玩法包正在生效（打开了、单人、游戏表已加载）；数值和规则共用这个开关
        public static bool IsActive => GameData.Active;

        // 规则改了之后调用，一秒后保存
        public static void MarkDirty() => config.MarkDirty();

        // 玩法包没生效的原因；生效时为 null
        public static string InactiveReason
        {
            get
            {
                if (GameData.Active) return null;
                if (!Current.Enabled) return "已关闭";
                if (!GameData.Ready) return "等游戏加载完";
                return Multiplayer.Status + "，已停用（表已恢复原版）";
            }
        }

        public static void Init(ModContext mod)
        {
            config = mod.LoadJson<Pack>("DBStudio/packs/default.json");
            context = mod;
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
            nextDeclare = 0f;
            UpdateActive();
        }

        // 每帧检查：联机握手不允许就停用，允许了再恢复
        private static void UpdateActive()
        {
            Declare();
            bool want = Current.Enabled && Multiplayer.GameplayAllowed && GameData.Ready;
            if (GameData.Active == want) return;
            GameData.Active = want;
            if (GameData.Active) EventLog.Add("玩法包生效");
            else if (Current.Enabled && GameData.Ready) EventLog.Add($"{Multiplayer.Status}，玩法包停用，数值已恢复原版");
            else EventLog.Add("玩法包已关闭");
        }

        // 打开的玩法包向联机握手声明，指纹是内容的哈希：队友要用一模一样的玩法包才算"装的一样"。每秒最多算一次
        private static void Declare()
        {
            if (UnityEngine.Time.unscaledTime < nextDeclare) return;
            nextDeclare = UnityEngine.Time.unscaledTime + 1f;
            if (!Current.Enabled)
            {
                context.RetractGameplay();
                return;
            }
            string json = JsonConvert.SerializeObject(new { Current.Values, Current.Rules, Current.Augments, Current.Offer });
            uint hash = 2166136261;
            foreach (char c in json)
                hash = (hash ^ c) * 16777619;
            context.DeclareGameplay(hash.ToString("x8"));
        }

        private static void Save()
        {
            Current.Values = GameData.Snapshot();
            config.MarkDirty();
        }
    }
}
