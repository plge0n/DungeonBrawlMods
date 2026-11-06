using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace DBModKit
{
    /// <summary>排行榜资格。在 Unity 主线程读取；每次读取重新检查，异常一律不合格。白名单由 ModKit 固定维护。</summary>
    public static class Ranked
    {
        private static readonly HashSet<string> whitelist = new HashSet<string>(StringComparer.Ordinal)
        {
            ModKitPlugin.Guid,
            "plge0n.dungeonbrawl.damagemeter",
            "plge0n.dungeonbrawl.smartskill",
            "plge0n.dungeonbrawl.autopickup",
            "plge0n.dungeonbrawl.smartmove",
            "plge0n.dungeonbrawl.autoforge",
        };
        private static readonly HashSet<string> owners = new HashSet<string>(StringComparer.Ordinal)
        {
            ModKitPlugin.Guid, ModKitPlugin.Guid + ".cards",
        };
        internal const string LobbyKey = "dbmods.ranked";
        internal const string AllowedNames = "DBModKit、DamageMeter、SmartSkill、AutoPickup、SmartMove、AutoForge（尚未推出）";
        private static ConfigEntry<bool> mode;
        private static bool configured, startupLogged;
        private static string configError;
        private static float battleNoticeAt = -1f;

        /// <summary>本次启动是否使用打榜模式。修改配置要重启游戏后才改变。</summary>
        public static bool ModeActive { get; private set; }
        /// <summary>当前本机和大厅内可见的 ModKit 队友都合格。真正上传时会再次检查。</summary>
        public static bool Eligible => Check("接口").Eligible;
        /// <summary>当前不合格原因的只读快照；合格时为空。</summary>
        public static IReadOnlyList<string> Reasons => Check("接口").Reasons;

        internal sealed class Result
        {
            internal bool Eligible => Reasons.Count == 0;
            internal IReadOnlyList<string> Reasons { get; }
            internal string Summary => Eligible ? "能上传" : "不能上传：" + string.Join("；", Reasons);
            internal string Brief => string.Join("；", Reasons.Take(3)) + (Reasons.Count > 3 ? "等" : "");
            internal Result(List<string> reasons) => Reasons = Array.AsReadOnly(reasons.Distinct().ToArray());
        }

        internal static bool IsWhitelisted(string guid) => guid != null && whitelist.Contains(guid);

        internal static void Configure(ConfigFile config)
        {
            // ScriptEngine 可能先触发 ModKit 初始化。之后切回插件自己的 Config，保留第一次读到的运行模式。
            mode = config.Bind("General", "RankedMode", false,
                "打榜模式，重启游戏后生效。只启用 " + AllowedNames + "；Hextech、DBStudio、内容包等改玩法的 Mod 停用。关闭再重启恢复。其他插件和预加载补丁仍可能导致不能打榜。");
            if (configured) return;
            configured = true;
            ModeActive = mode.Value;
            Log.Info("打榜模式：" + (ModeActive ? "开启" : "关闭") + "（本次启动固定，切换后重启生效）");
        }

        internal static void EnsureConfigured()
        {
            if (configured) return;
            try { Configure(new ConfigFile(Path.Combine(Paths.ConfigPath, ModKitPlugin.Guid + ".cfg"), true)); }
            catch (Exception e)
            {
                configured = true;
                configError = "无法读取打榜模式配置";
                Log.Error(configError + "：" + e);
            }
        }

        internal static bool RequestedMode => mode != null && mode.Value;
        internal static void SetMode(bool value)
        {
            if (mode == null) return;
            mode.Value = value;
            mode.ConfigFile.Save();
            ScreenNotice.Show("打榜模式：重启游戏后生效", 6f);
        }

        internal static Result Check(string occasion, bool includeTeam = true)
        {
            var reasons = new List<string>();
            try
            {
                CheckLocal(reasons);
                if (includeTeam && ModKit.Compatible) CheckTeam(reasons);
            }
            catch (Exception e)
            {
                reasons.Add("资格检查异常，无法确认能否打榜");
                Log.Warning("打榜资格检查异常：" + e);
            }
            var result = new Result(reasons);
            RunLog.WriteCore(ModKitPlugin.Name, "打榜资格", result.Eligible ? RunLogLevel.Info : RunLogLevel.Warning,
                occasion, new (string, object)[] { ("合格", result.Eligible), ("原因", string.Join("；", result.Reasons)) }, mirror: false);
            return result;
        }

        private static void CheckLocal(List<string> reasons)
        {
            if (configError != null) reasons.Add(configError);
            if (!ModKit.Compatible) reasons.Add("ModKit 与游戏不兼容或排行榜保护不可用");
            var patchedAssemblies = new HashSet<Assembly>();
            foreach (var method in Harmony.GetAllPatchedMethods().ToArray())
            {
                var patches = Harmony.GetPatchInfo(method);
                if (patches == null) throw new InvalidOperationException("补丁列表发生变化");
                CheckPatches(method, patches.Prefixes, HarmonyPatchType.Prefix, patchedAssemblies, reasons);
                CheckPatches(method, patches.Postfixes, HarmonyPatchType.Postfix, patchedAssemblies, reasons);
                CheckPatches(method, patches.Transpilers, HarmonyPatchType.Transpiler, patchedAssemblies, reasons);
                CheckPatches(method, patches.Finalizers, HarmonyPatchType.Finalizer, patchedAssemblies, reasons);
            }
            var seen = new HashSet<BaseUnityPlugin>();
            foreach (var info in Chainloader.PluginInfos.Values.ToArray())
            {
                CheckPlugin(info.Metadata, info.Instance, info.Location, patchedAssemblies, reasons);
                if (!ReferenceEquals(info.Instance, null)) seen.Add(info.Instance);
            }
            // 包括 HideAndDontSave、未激活、未登记到 PluginInfos 的 ScriptEngine 插件。
            foreach (var plugin in Resources.FindObjectsOfTypeAll<BaseUnityPlugin>())
                if (seen.Add(plugin)) CheckPlugin(MetadataHelper.GetMetadata(plugin), plugin, plugin.Info?.Location, patchedAssemblies, reasons);
            // ScriptEngine r11.2 把程序集改名为 名字-时间戳，Assembly.Load(byte[]) 的 Location 为空；
            // 即使脚本对象已销毁、PluginInfos 被移除，仍不把本次进程判成干净。
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name == "ScriptEngine" || name.StartsWith("UnityExplorer", StringComparison.OrdinalIgnoreCase))
                    reasons.Add("加载了 " + name);
                if (assembly.IsDynamic) continue;
                if (UnderScripts(assembly.Location) || Regex.IsMatch(name, @"-\d{10,}$"))
                    foreach (var type in assembly.GetTypes())
                        if (typeof(BaseUnityPlugin).IsAssignableFrom(type) && MetadataHelper.GetMetadata(type) != null)
                            reasons.Add("加载了脚本插件 " + MetadataHelper.GetMetadata(type).Name);
            }
            string patchers = Path.Combine(Paths.BepInExRootPath, "patchers");
            if (Directory.Exists(patchers))
                foreach (string file in Directory.GetFiles(patchers, "*", SearchOption.AllDirectories).Where(f => string.Equals(Path.GetExtension(f), ".dll", StringComparison.OrdinalIgnoreCase)))
                    reasons.Add("装了预加载补丁 " + Path.GetFileName(file));
            foreach (var pack in ContentPacks.All.Where(p => p.Loaded)) reasons.Add("加载了内容包 " + pack.DisplayName);
            if (ActiveSkills.All.Count > 0) reasons.Add("注册了自定义主动技能");
            if (PassiveSkills.All.Count > 0) reasons.Add("注册了自定义被动技能");
            if (CustomRelics.All.Count > 0) reasons.Add("注册了自定义遗物");
            if (CustomEquipment.All.Count > 0 || CustomEquipEffects.All.Count > 0) reasons.Add("注册了自定义装备或装备特效");
            if (HeroVariants.All.Count > 0) reasons.Add("注册了自定义英雄或变体");
            // 按玩家决定，CardCollection 运行时开关和永久写入均不参与资格。
        }

        private static void CheckPatches(MethodBase target, IEnumerable<Patch> patches, HarmonyPatchType kind,
            HashSet<Assembly> patchedAssemblies, List<string> reasons)
        {
            foreach (var patch in patches)
            {
                var patchMethod = patch.PatchMethod;
                if (!owners.Contains(patch.owner) && !RankedBepInEx.IsExempt(patchMethod, target, kind))
                    reasons.Add("存在未获准的 Harmony 补丁：" + patch.owner + " → " + target.DeclaringType?.FullName + "." + target);
                if (patchMethod == null) throw new InvalidOperationException("无法确认补丁来源");
                patchedAssemblies.Add(patchMethod.Module.Assembly);
            }
        }

        private static void CheckPlugin(BepInPlugin meta, BaseUnityPlugin plugin, string location, HashSet<Assembly> patched, List<string> reasons)
        {
            if (meta == null) throw new InvalidOperationException("插件元数据缺失");
            string assemblyLocation = ReferenceEquals(plugin, null) ? null : plugin.GetType().Assembly.Location;
            if (UnderScripts(location) || UnderScripts(assemblyLocation)
                || (!ReferenceEquals(plugin, null) && Regex.IsMatch(plugin.GetType().Assembly.GetName().Name, @"-\d{10,}$")))
            {
                reasons.Add("加载了脚本插件 " + meta.Name);
                return;
            }
            if (IsWhitelisted(meta.GUID)) return;
            if (ModeActive && !ReferenceEquals(plugin, null)
                && ModContext.All.TryGetValue(meta.GUID, out var ctx) && ctx.RankedDisabled
                && ReferenceEquals(ctx.Plugin, plugin)
                && MetadataHelper.GetDependencies(plugin.GetType()).Any(d => d.DependencyGUID == ModKitPlugin.Guid)
                && !patched.Contains(plugin.GetType().Assembly)) return;
            reasons.Add("装了 " + meta.Name);
        }

        private static bool UnderScripts(string file)
        {
            if (string.IsNullOrEmpty(file)) return false;
            string root = Path.GetFullPath(Path.Combine(Paths.BepInExRootPath, "scripts")).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return Path.GetFullPath(file).StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        // 只公布本机，不能公布包含队友的结果，否则会互相引用，无法恢复合格。
        internal static void PublishLocal()
        {
            var result = Check("大厅公布（本机）", false);
            string reason = result.Eligible ? "" : result.Reasons[0];
            if (reason.Length > 160) reason = reason.Substring(0, 160);
            LobbySync.Publish(LobbyKey, "1|" + DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
                + "|" + (result.Eligible ? "1" : "0") + "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(reason)));
        }

        private static void CheckTeam(List<string> reasons)
        {
            ulong lobby = GameRaw.LobbyId();
            if (lobby == 0)
            {
                if (!Game.IsSolo) reasons.Add("联机没有 Steam 大厅，无法确认队友资格");
                return;
            }
            ulong me = GameRaw.MySteamId();
            if (me == 0) throw new InvalidOperationException("无法确认本机 Steam 身份");
            var members = GameRaw.LobbyMembers(lobby);
            if (!members.Contains(me)) throw new InvalidOperationException("无法确认大厅成员");
            foreach (ulong member in members.Where(m => m != me))
            {
                string handshake = GameRaw.LobbyMemberData(lobby, member, Multiplayer.HandshakeKey);
                string value = GameRaw.LobbyMemberData(lobby, member, LobbyKey);
                if (string.IsNullOrEmpty(handshake) && string.IsNullOrEmpty(value)) continue; // 看不到 ModKit 的原版队友
                string name = member.ToString(CultureInfo.InvariantCulture);
                var parts = (value ?? "").Split('|');
                if (parts.Length != 4 || parts[0] != "1" || (parts[2] != "0" && parts[2] != "1") || parts[3].Length > 1024
                    || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long timestamp)
                    || timestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10 || timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5)
                {
                    reasons.Add("队友 " + name + " 的 ModKit 未提供有效打榜资格（旧版或数据过期）");
                    continue;
                }
                string reason = Encoding.UTF8.GetString(Convert.FromBase64String(parts[3]));
                if (parts[2] == "1" && reason.Length == 0) continue;
                reasons.Add("队友 " + name + " 不能打榜" + (reason.Length == 0 ? "" : "：" + reason));
            }
        }

        internal static void BattleStarted() => battleNoticeAt = Time.unscaledTime + 5f;

        internal static void Tick()
        {
            if (!startupLogged && Time.unscaledTime >= 12f)
            {
                startupLogged = true;
                Log.Info("打榜资格（启动）：" + Check("启动").Summary);
            }
            if (battleNoticeAt < 0f || Time.unscaledTime < battleNoticeAt) return;
            battleNoticeAt = -1f;
            var result = Check("进入战斗");
            ScreenNotice.Show(result.Eligible ? "打榜：本局可上传，结算时再次检查" : "打榜：本局不能上传，" + result.Reasons[0], 8f);
        }

        internal static void BattleEnded() => battleNoticeAt = -1f;
    }
}
