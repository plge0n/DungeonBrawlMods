using System.Collections.Generic;
using System.Linq;
using System.Text;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace DBStudio.Pages
{
    // 调试：实时查看玩家、技能栏、附近敌人和事件日志；单人时可以用测试操作
    internal sealed class DebugPage : IStudioPage
    {
        private const int MaxEnemies = 8;

        private Text player, skills, enemies, log, cheatHint;
        private ScrollRect logScroll;
        private readonly List<Button> cheats = new List<Button>();
        private int logVersion = -1;

        public string Title => "调试";

        public void Build(RectTransform body)
        {
            cheats.Clear();
            logVersion = -1;
            var columns = Studio.Columns(body);

            var left = NativeUi.Column(columns, 440f);
            NativeUi.Heading(left, "玩家");
            player = Studio.Block(left);
            NativeUi.Separator(left);
            NativeUi.Heading(left, "测试操作");
            var row = NativeUi.Row(left, 56f, 12f);
            cheats.Add(NativeUi.Button(row, "满血", false, 130f, () => Cheat("满血", p => p.Heal(p.MaxHp - p.Hp))));
            cheats.Add(NativeUi.Button(row, "满蓝", false, 130f, () => Cheat("满蓝", p => p.AddMana(p.MaxMana - p.Mana))));
            cheats.Add(NativeUi.Button(row, "金币 +1000", false, 150f, () => Cheat("金币 +1000", p => p.AddGold(1000))));
            cheatHint = NativeUi.Label(left, "", 18, -1f, NativeUi.DimText);
            Spacer(left);

            var middle = NativeUi.Column(columns);
            NativeUi.Heading(middle, "技能栏");
            skills = Studio.Block(middle);
            NativeUi.Separator(middle);
            NativeUi.Heading(middle, "附近敌人");
            enemies = Studio.Block(middle);
            Spacer(middle);

            var right = NativeUi.Column(columns);
            var head = NativeUi.Row(right, 40f);
            NativeUi.Heading(head, "事件日志");
            SmallButton(head, "标记", 90f, () => RunLog.Mark());
            SmallButton(head, "导出", 90f, Export);
            SmallButton(head, "清空", 90f, EventLog.Clear);
            var tools = NativeUi.Row(right, 36f);
            NativeUi.Toggle(tools, "详细（含每个战斗事件）", 330f, on => EventLog.Verbose = on).isOn = EventLog.Verbose;
            NativeUi.Label(tools, $"{RunLog.FileLevel} 级以上写入 trace.log", 16, -1f, NativeUi.DimText);
            var content = NativeUi.ScrollView(right, out logScroll);
            log = Studio.Block(content, 18);
        }

        public void Refresh()
        {
            var p = Game.LocalPlayer;
            player.text = p == null ? Dim("不在战斗中") : PlayerText(p);
            skills.text = p == null ? Dim("—") : SkillText(p);
            enemies.text = p == null ? Dim("—") : EnemyText(p);

            bool canCheat = p != null && Game.IsSolo;
            foreach (var b in cheats) NativeUi.SetEnabled(b, canCheat);
            cheatHint.text = p == null ? "进入战斗后可用" : Game.IsSolo ? "只影响自己，排行榜照常拦截" : "联机时停用：会影响其他玩家";

            if (logVersion != EventLog.Version)
            {
                logVersion = EventLog.Version;
                // 原本停在底部就继续跟随最新的日志
                bool follow = logScroll.verticalNormalizedPosition <= 0.01f || logScroll.content.rect.height <= logScroll.viewport.rect.height;
                var lines = EventLog.Lines();
                log.text = lines.Count == 0 ? Dim("暂无事件") : string.Join("\n", lines);
                if (follow)
                {
                    Canvas.ForceUpdateCanvases();
                    logScroll.verticalNormalizedPosition = 0f;
                }
            }
        }

        private static string PlayerText(Player p)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"生命  {p.Hp} / {p.MaxHp}  ({p.HpPercent:P0})");
            sb.AppendLine($"蓝量  {p.Mana} / {p.MaxMana}");
            sb.AppendLine($"金币  {p.Gold}");
            sb.AppendLine($"攻击  {p.Attack}    攻速  {p.AttackSpeed:F2} 次/秒");
            sb.AppendLine($"力量  {p.Strength}    敏捷  {p.Agility}    体力  {p.Stamina}");
            sb.AppendLine($"暴击  {p.CriticalRate:P0}    暴伤  {p.CriticalDamage:F2}");
            sb.AppendLine($"护甲  {p.Armor}    闪避  {p.Dodge}    吸血  {p.LifeSteal:F2}");
            sb.Append($"移速  {p.MoveSpeed:F1}");
            return sb.ToString();
        }

        private static string SkillText(Player p)
        {
            var lines = new List<string>();
            foreach (var b in SkillBar.Buttons())
            {
                var slot = b.Slot;
                var info = slot?.Info;
                if (info == null) continue;
                string cd = slot.CooldownRemaining > 0f ? $"冷却 {slot.CooldownRemaining:F1}s" : "<color=#9fd08a>就绪</color>";
                string cost = slot.CostsGold ? "花金币" : $"耗蓝 {slot.CostFor(p)}";
                string auto = slot.IsAutoCastOn ? "  <color=#d6b67d>[自动]</color>" : "";
                lines.Add($"{info.Name}  {Dim($"{cd} · {cost} · 范围 {info.CastRange:F1}")}{auto}");
            }
            return lines.Count == 0 ? Dim("技能栏是空的") : string.Join("\n", lines);
        }

        private static string EnemyText(Player p)
        {
            var all = p.Enemies();
            if (all.Count == 0) return Dim("场上没有敌人");
            int bosses = all.Count(e => e.IsBoss), elites = all.Count(e => e.IsElite);
            var sb = new StringBuilder();
            sb.AppendLine($"共 {all.Count} 个" + (bosses > 0 ? $"，Boss {bosses}" : "") + (elites > 0 ? $"，精英 {elites}" : ""));
            foreach (var e in all.OrderBy(e => p.DistanceTo(e)).Take(MaxEnemies))
            {
                string kind = e.IsBoss ? "<color=#e07a5f>Boss</color>" : e.IsElite ? "<color=#d6b67d>精英</color>" : "普通";
                sb.AppendLine($"{kind}  {Dim($"距离 {p.DistanceTo(e):F1} · 生命 {e.Hp} ({e.HpPercent:P0})")}");
            }
            if (all.Count > MaxEnemies) sb.Append(Dim($"……还有 {all.Count - MaxEnemies} 个"));
            return sb.ToString().TrimEnd();
        }

        private static void Cheat(string name, System.Action<Player> action)
        {
            var p = Game.LocalPlayer;
            // 按钮状态每 0.25 秒刷新一次，点击时再确认一遍
            if (p == null || !Game.IsSolo) return;
            EventLog.Add($"测试操作：{name}");
            action(p);
        }

        private static void SmallButton(Transform parent, string text, float width, UnityEngine.Events.UnityAction onClick)
            => NativeUi.Button(parent, text, false, width, onClick).GetComponent<LayoutElement>().preferredHeight = 40f;

        // 诊断报告：版本、兼容检查、当前状态和全部运行记录，反馈问题时附上
        private static void Export()
        {
            try
            {
                string path = RunLog.ExportReport();
                ScreenNotice.Show($"诊断报告已导出：{path}", 6f);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"导出诊断报告失败：{e}");
                ScreenNotice.Show("导出诊断报告失败，详情见 BepInEx/LogOutput.log", 4f);
            }
        }

        // 把栏里的内容顶到上面
        private static void Spacer(Transform parent)
        {
            var go = new GameObject("spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().flexibleHeight = 1f;
        }

        private static string Dim(string s) => $"<color=#a6a6a6>{s}</color>";
    }
}
