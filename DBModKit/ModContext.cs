using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;

namespace DBModKit
{
    /// <summary>
    /// 一个 Mod 的运行上下文。通过它订阅的事件、注册的处理器、打开的配置都会在 <see cref="Dispose"/> 时自动清理，
    /// 所以 Mod 可以用 ScriptEngine 热重载。自己创建的游戏对象（比如挂在游戏 UI 上的按钮）在 onUnload 里销毁。
    /// </summary>
    /// <remarks>
    /// 用组合而不是基类：ScriptEngine 用 Cecil 检查插件是否继承 BaseUnityPlugin，解析继承链时找不到 plugins 目录里的 DBModKit，
    /// 继承自 ModKit 里基类的插件会被登记成空的 PluginInfo，导致插件和 ScriptEngine 自己都崩溃。
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Plugin : BaseUnityPlugin
    /// {
    ///     private ModContext mod;
    ///     private void Awake() => mod = ModContext.Start(this, Logger, OnLoad, OnUnload);
    ///     private void OnDestroy() => mod?.Dispose();
    ///     private void OnLoad(ModContext mod) { mod.OnUpdate(...); }
    ///     private void OnUnload() { }
    /// }
    /// </code>
    /// </example>
    public sealed class ModContext : IDisposable
    {
        private readonly List<Action> cleanups = new List<Action>();
        private readonly Action onUnload;

        public BepInPlugin Metadata { get; }
        public ManualLogSource Logger { get; }

        /// <summary>写运行记录（见 <see cref="DBModKit.RunLog"/>），来源是这个 Mod 的名字。</summary>
        public ModRunLog RunLog { get; }

        /// <summary>这个 Mod 的多语言文本（翻译放在插件 dll 旁边的 lang 文件夹，见 <see cref="ModText"/>）。</summary>
        public ModText Text { get; }

        /// <summary>Mod 已启动（ModKit 兼容且 onLoad 成功）。</summary>
        public bool Loaded { get; private set; }

        private ModContext(BepInPlugin metadata, ManualLogSource logger, Action onUnload, string folder)
        {
            Metadata = metadata;
            Logger = logger;
            RunLog = new ModRunLog(metadata.Name);
            Text = new ModText(folder == null ? null : System.IO.Path.Combine(folder, "lang"));
            this.onUnload = onUnload;
        }

        /// <summary>
        /// 在插件的 Awake 里调用。ModKit 兼容时执行 onLoad；不兼容或 onLoad 抛异常时 Mod 不启动（已注册的内容会被清理）。
        /// </summary>
        public static ModContext Start(BaseUnityPlugin plugin, ManualLogSource logger, Action<ModContext> onLoad, Action onUnload = null)
        {
            // ScriptEngine 加载的插件 Info.Location 可能为空，就从程序集位置找
            string location = plugin.Info?.Location;
            if (string.IsNullOrEmpty(location)) location = plugin.GetType().Assembly.Location;
            string folder = string.IsNullOrEmpty(location) ? null : System.IO.Path.GetDirectoryName(location);
            var ctx = new ModContext(MetadataHelper.GetMetadata(plugin), logger, onUnload, folder);
            if (!ModKit.Compatible)
            {
                // ModKit 不兼容时它自己会在屏幕上提示
                logger.LogWarning($"{ctx.Metadata.Name} 未启动：ModKit 与当前游戏版本不兼容");
                return ctx;
            }
            try
            {
                onLoad(ctx);
                ctx.Loaded = true;
                logger.LogInfo($"{ctx.Metadata.Name} {ctx.Metadata.Version} 已加载");
                ctx.RunLog.Info("生命周期", "已加载", ("版本", ctx.Metadata.Version.ToString()));
            }
            catch (Exception e)
            {
                logger.LogError($"{ctx.Metadata.Name} 启动失败：{e}");
                DBModKit.RunLog.WriteCore(ctx.Metadata.Name, "生命周期", RunLogLevel.Error, "启动失败", new (string, object)[] { ("异常", e.ToString()) }, mirror: false);
                ctx.RunCleanups();
            }
            return ctx;
        }

        /// <summary>在插件的 OnDestroy 里调用。</summary>
        public void Dispose()
        {
            if (Loaded) RunLog.Info("生命周期", "卸载");
            if (Loaded && onUnload != null) Safe.Run($"{Metadata.Name} 卸载", onUnload);
            Loaded = false;
            RunCleanups();
        }

        private void RunCleanups()
        {
            for (int i = cleanups.Count - 1; i >= 0; i--)
                Safe.Run($"{Metadata.Name} 清理", cleanups[i]);
            cleanups.Clear();
        }

        /// <summary>登记卸载时要执行的清理。</summary>
        public void AddCleanup(Action cleanup) => cleanups.Add(cleanup);

        public void OnUpdate(Action handler)
        {
            ModEvents.Update += handler;
            AddCleanup(() => ModEvents.Update -= handler);
        }

        public void OnBattleStarted(Action handler)
        {
            ModEvents.BattleStarted += handler;
            AddCleanup(() => ModEvents.BattleStarted -= handler);
        }

        public void OnBattleEnded(Action handler)
        {
            ModEvents.BattleEnded += handler;
            AddCleanup(() => ModEvents.BattleEnded -= handler);
        }

        public void OnQuitting(Action handler)
        {
            ModEvents.Quitting += handler;
            AddCleanup(() => ModEvents.Quitting -= handler);
        }

        /// <summary>在设置界面（DBStudio 的 Mod 页）里加一块自定义设置，见 <see cref="ModSettings"/>。</summary>
        public SettingsSection AddSettings(string title, Action<UnityEngine.RectTransform> build, Action refresh = null)
        {
            var section = ModSettings.Register(Metadata.GUID, title, build, refresh);
            AddCleanup(() => ModSettings.Unregister(section));
            return section;
        }

        /// <summary>订阅战斗事件（见 <see cref="GameEvents"/>）。</summary>
        public void OnGameEvent(Action<GameEvent> handler)
        {
            GameEvents.Raised += handler;
            AddCleanup(() => GameEvents.Raised -= handler);
        }

        /// <summary>接管自动释放（见 <see cref="AutoCast"/>）。</summary>
        public void UseAutoCast(AutoCastHandler handler)
        {
            AutoCast.SetHandler(Metadata.GUID, handler);
            AddCleanup(() => AutoCast.ClearHandler(Metadata.GUID));
        }

        public void OnAutoCastResolved(Action<AutoCastResult> handler)
        {
            AutoCast.Resolved += handler;
            AddCleanup(() => AutoCast.Resolved -= handler);
        }

        /// <summary>接管没有移动输入时的走位（见 <see cref="MoveControl"/>）。</summary>
        public void UseMovement(MoveHandler handler)
        {
            MoveControl.SetHandler(Metadata.GUID, handler);
            AddCleanup(() => MoveControl.ClearHandler(Metadata.GUID));
        }

        /// <summary>订阅命中事件（见 <see cref="Combat.Hit"/>）。</summary>
        public void OnHit(Action<HitInfo> handler)
        {
            Combat.Hit += handler;
            AddCleanup(() => Combat.Hit -= handler);
        }

        /// <summary>订阅本地玩家放技能（见 <see cref="Combat.Cast"/>）。</summary>
        public void OnSkillCast(Action<SkillCast> handler)
        {
            Combat.Cast += handler;
            AddCleanup(() => Combat.Cast -= handler);
        }

        /// <summary>重试波次的快照、还原（见 <see cref="ModEvents.CheckpointSaved"/>）。</summary>
        public void OnCheckpointSaved(Action handler)
        {
            ModEvents.CheckpointSaved += handler;
            AddCleanup(() => ModEvents.CheckpointSaved -= handler);
        }

        public void OnCheckpointRestored(Action handler)
        {
            ModEvents.CheckpointRestored += handler;
            AddCleanup(() => ModEvents.CheckpointRestored -= handler);
        }

        private bool tuned;

        /// <summary>
        /// 调整本地玩家某个主动技能（见 <see cref="SkillTuning"/>），传 <see cref="SkillTune.None"/> 撤销。
        /// 同一个 Mod 对同一个技能再次设置会覆盖上一次；卸载时自动撤销。
        /// </summary>
        public void TuneSkill(int skillId, SkillTune tune)
        {
            if (!tuned)
            {
                tuned = true;
                AddCleanup(() => SkillTuning.ClearOwner(Metadata.Name));
            }
            SkillTuning.Set(Metadata.Name, skillId, tune);
        }

        private bool tooltips;

        /// <summary>在本地玩家某个主动技能的悬停提示末尾追加文字（见 <see cref="SkillTooltips"/>），传 null 去掉；卸载时自动去掉。</summary>
        public void SetSkillTooltip(int skillId, string text)
        {
            if (!tooltips)
            {
                tooltips = true;
                AddCleanup(() => SkillTooltips.ClearOwner(Metadata.Name));
            }
            SkillTooltips.Set(Metadata.Name, skillId, text);
        }

        /// <summary>撤销这个 Mod 的所有技能调整。</summary>
        public void ClearSkillTunes() => SkillTuning.ClearOwner(Metadata.Name);

        /// <summary>打开 BepInEx/config 下的 JSON 配置；卸载时自动保存并释放。</summary>
        public JsonConfig<T> LoadJson<T>(string fileName) where T : new()
        {
            var config = new JsonConfig<T>(fileName);
            AddCleanup(config.Dispose);
            return config;
        }
    }
}
