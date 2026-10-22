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

        /// <summary>Mod 已启动（ModKit 兼容且 onLoad 成功）。</summary>
        public bool Loaded { get; private set; }

        private ModContext(BepInPlugin metadata, ManualLogSource logger, Action onUnload)
        {
            Metadata = metadata;
            Logger = logger;
            this.onUnload = onUnload;
        }

        /// <summary>
        /// 在插件的 Awake 里调用。ModKit 兼容时执行 onLoad；不兼容或 onLoad 抛异常时 Mod 不启动（已注册的内容会被清理）。
        /// </summary>
        public static ModContext Start(BaseUnityPlugin plugin, ManualLogSource logger, Action<ModContext> onLoad, Action onUnload = null)
        {
            var ctx = new ModContext(MetadataHelper.GetMetadata(plugin), logger, onUnload);
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
            }
            catch (Exception e)
            {
                logger.LogError($"{ctx.Metadata.Name} 启动失败：{e}");
                ctx.RunCleanups();
            }
            return ctx;
        }

        /// <summary>在插件的 OnDestroy 里调用。</summary>
        public void Dispose()
        {
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

        /// <summary>打开 BepInEx/config 下的 JSON 配置；卸载时自动保存并释放。</summary>
        public JsonConfig<T> LoadJson<T>(string fileName) where T : new()
        {
            var config = new JsonConfig<T>(fileName);
            AddCleanup(config.Dispose);
            return config;
        }
    }
}
