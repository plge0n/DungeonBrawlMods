using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBModKit
{
    /// <summary>Mod 挂到设置界面（DBStudio 的 Mod 页）里的一块自定义设置。</summary>
    public sealed class SettingsSection
    {
        /// <summary>所属插件的 GUID，设置界面把它显示在这个插件下面。</summary>
        public string OwnerGuid { get; }
        public string Title { get; }
        /// <summary>
        /// 往 container 里建控件。container 已有竖向布局（宽度撑满、高度随内容），用 NativeUi 的 Row、Toggle 等往里加。
        /// 每次显示这块设置时都会重新调用。
        /// </summary>
        public Action<RectTransform> Build { get; }
        /// <summary>显示期间每秒调用几次，用来刷新状态文字；可以为 null。</summary>
        public Action Refresh { get; }

        internal SettingsSection(string ownerGuid, string title, Action<RectTransform> build, Action refresh)
        {
            OwnerGuid = ownerGuid;
            Title = title;
            Build = build;
            Refresh = refresh;
        }
    }

    /// <summary>
    /// 自定义设置的登记处：Mod 登记，设置界面（DBStudio）显示。两边都只依赖 ModKit，互不引用。
    /// 用 <see cref="ModContext.AddSettings"/> 登记可以在卸载时自动撤下。
    /// BepInEx 配置文件（.cfg）里的项不用登记，设置界面会自动列出。
    /// </summary>
    public static class ModSettings
    {
        private static readonly List<SettingsSection> sections = new List<SettingsSection>();

        public static IReadOnlyList<SettingsSection> Sections => sections;

        /// <summary>登记或撤下时加 1，设置界面据此判断要不要重建。</summary>
        public static int Version { get; private set; }

        public static SettingsSection Register(string ownerGuid, string title, Action<RectTransform> build, Action refresh = null)
        {
            var s = new SettingsSection(ownerGuid, title, build ?? throw new ArgumentNullException(nameof(build)), refresh);
            sections.Add(s);
            Version++;
            return s;
        }

        public static void Unregister(SettingsSection section)
        {
            if (sections.Remove(section)) Version++;
        }

        private static Action<string> opener;

        /// <summary>有设置界面（装了 DBStudio）。</summary>
        public static bool CanOpen => opener != null;

        /// <summary>打开设置界面并选中 guid 这个插件；guid 为 null 时只打开。没有设置界面时什么也不做。</summary>
        public static void Open(string guid) => opener?.Invoke(guid);

        /// <summary>设置界面登记自己的打开方法（主菜单的 Mod 列表用它跳过去），Dispose 时撤下。</summary>
        public static IDisposable SetOpener(Action<string> open)
        {
            opener = open ?? throw new ArgumentNullException(nameof(open));
            return new Disposable(() =>
            {
                if (opener == open) opener = null;
            });
        }
    }
}
