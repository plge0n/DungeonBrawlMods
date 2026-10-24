using BepInEx;
using BepInEx.Configuration;
using DBModKit;
using UnityEngine;

namespace Hextech
{
    // 海克斯强化：每局 4 次三选一（开局 + 约 25%/50%/75% 波次附近的遗物回合之后）。
    // 专属卡由积木按品质点数自动生成（加少量手写招牌），通用卡参照 LoL 海克斯大乱斗手写；
    // 单人，或者联机时房间里每个人都装了同样的 Mod（或房主允许）时生效。企划见 docs/设计/海克斯企划.md
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, "0.31.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "plge0n.dungeonbrawl.hextech";
        public const string Name = "Hextech";
        // 发布时同步修改 CHANGELOG.md；pack.ps1 从这里读取版本号
        public const string Version = "0.31.0";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> Refreshes;
        internal static ConfigEntry<bool> EnemyAugments;
        internal static ConfigEntry<KeyboardShortcut> ListKey;
        internal static ConfigEntry<KeyboardShortcut> PreviewKey;

        // 必须直接继承 BaseUnityPlugin（不能继承 ModKit 里的类），否则 ScriptEngine 热重载会出错
        private ModContext mod;

        private void Awake() => mod = ModContext.Start(this, Logger, OnLoad, OnUnload);

        private void OnDestroy() => mod?.Dispose();

        private void OnLoad(ModContext mod)
        {
            Enabled = Config.Bind("General", "Enabled", true, "总开关（联机时要房间里每个人都装了同样的 Mod，或者房主在 DBStudio 的 Mod 页允许）");
            Refreshes = Config.Bind("General", "Refreshes", 2,
                new ConfigDescription("每局可以刷新几次（三选一里的\"刷新\"选项，不消耗游戏自己的刷新次数）", new AcceptableValueRange<int>(0, 10)));
            EnemyAugments = Config.Bind("General", "EnemyAugments", false,
                "敌人海克斯：你每选一张强化，之后刷出来的敌人也随机拿一张同品质的强化（生命、攻击变高）；作为补偿，每次三选一多给 1 次刷新。联机时只看房主的设置：生命对整个房间生效，攻击只对装了 Hextech 的人生效");
            ListKey = Config.Bind("Keys", "ListKey", new KeyboardShortcut(KeyCode.F7), "在屏幕上列出本局已选的强化");
            PreviewKey = Config.Bind("Keys", "PreviewKey", new KeyboardShortcut(KeyCode.F7, KeyCode.LeftControl),
                "把所有英雄会生成的强化写进 BepInEx/Hextech/preview.txt（调平衡用）");

            if (!SkillTuning.Available) Logger.LogWarning("ModKit 的技能调整补丁没有装上，主动技能的数值强化不会生效");

            // 计入联机握手（总开关开着时）：别人没装、或者版本不同时，本局按 ModKit 的规则停用。
            // 关着的人不算"装了改玩法的内容"，免得把整个房间的改玩法内容都挡掉；战斗中改开关等这局结束再生效（握手已锁定）
            void Declare()
            {
                if (Enabled.Value) mod.DeclareGameplay();
                else mod.RetractGameplay();
            }
            Declare();
            Enabled.SettingChanged += (_, __) => { if (!Game.InBattle) Declare(); };
            mod.OnBattleEnded(Declare);
            Augments.LoadIcons(mod);
            Engine.Attach(mod);
            mod.OnUpdate(() =>
            {
                if (PreviewKey.Value.IsDown())
                {
                    string path = Preview.Dump(Random.Range(1, int.MaxValue));
                    ScreenNotice.Show($"海克斯生成预览已写入 {path}", 6f);
                }
                else if (ListKey.Value.IsDown() && Game.InBattle)
                {
                    ScreenNotice.Show(Engine.Summary(), 8f);
                }
                if (Game.InBattle) Hud.Tick();
            });
        }

        private void OnUnload()
        {
            Engine.Unload();
            Hud.RemoveAll();
        }
    }
}
