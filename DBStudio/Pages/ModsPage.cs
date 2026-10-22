using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace DBStudio.Pages
{
    // Mod：运行环境、ModKit 兼容状态、已加载的插件
    internal sealed class ModsPage : IStudioPage
    {
        private Text environment, plugins;

        public string Title => "Mod";

        public void Build(RectTransform body)
        {
            var columns = Studio.Columns(body);

            var left = NativeUi.Column(columns, 620f);
            NativeUi.Heading(left, "运行环境");
            environment = Studio.Block(left);

            var right = NativeUi.Column(columns);
            NativeUi.Heading(right, "已加载的插件");
            var content = NativeUi.ScrollView(right, out _);
            plugins = Studio.Block(content);
            plugins.text = PluginText();
        }

        public void Refresh() => environment.text = EnvironmentText();

        private static string EnvironmentText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"游戏版本  {ModKit.GameVersion}");
            sb.AppendLine($"DBModKit  {ModKit.Version}  " + (ModKit.Compatible ? Good("兼容") : Bad("不兼容，依赖它的 Mod 已停用")));
            if (ModKit.MissingMembers.Count > 0)
                sb.AppendLine(Dim("缺失：" + string.Join("，", ModKit.MissingMembers)));
            sb.AppendLine("自动释放钩子  " + (AutoCast.Available ? Good("可用") : Bad("不可用")));
            sb.AppendLine("排行榜  " + Dim("装了 Mod 不上传（游戏作者的规则）"));
            sb.AppendLine();
            string room = Game.IsSolo ? "单人"
                : Game.PlayerCount > 0 ? $"联机，你是房主，{Game.PlayerCount} 人"
                : "联机，在别人的房间里";
            sb.AppendLine($"房间  {room}");
            sb.Append(Dim("联机时测试操作会停用：游戏不检查 Mod，伤害由各自的客户端计算，改了会影响队友。"));
            return sb.ToString();
        }

        private static string PluginText()
        {
            // ScriptEngine 热重载的插件不在 Chainloader 里，从场景里找
            var list = new Dictionary<string, (string name, string version, bool hot)>();
            foreach (var info in Chainloader.PluginInfos.Values)
                list[info.Metadata.GUID] = (info.Metadata.Name, info.Metadata.Version.ToString(), false);
            foreach (var plugin in Object.FindObjectsOfType<BaseUnityPlugin>())
            {
                var meta = MetadataHelper.GetMetadata(plugin);
                if (meta != null && !list.ContainsKey(meta.GUID))
                    list[meta.GUID] = (meta.Name, meta.Version.ToString(), true);
            }

            var sb = new StringBuilder();
            foreach (var kv in list.OrderBy(kv => kv.Value.name))
            {
                string hot = kv.Value.hot ? "  <color=#d6b67d>[热重载]</color>" : "";
                sb.AppendLine($"{kv.Value.name}  {kv.Value.version}{hot}");
                sb.AppendLine(Dim("    " + kv.Key));
            }
            return sb.Length == 0 ? Dim("没有插件") : sb.ToString().TrimEnd();
        }

        private static string Good(string s) => $"<color=#9fd08a>{s}</color>";
        private static string Bad(string s) => $"<color=#e07a5f>{s}</color>";
        private static string Dim(string s) => $"<color=#a6a6a6>{s}</color>";
    }
}
