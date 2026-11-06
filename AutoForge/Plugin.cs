using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using DBModKit;
using UnityEngine;
namespace AutoForge
{
    public sealed class Settings
    {
        public bool Enabled = true;
        public SelectionMode Mode = SelectionMode.Weighted;
        public string ToggleKey = "F10";
        public Dictionary<ForgeAttribute, int> Weights = Selection.Defaults();
    }
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, "0.40.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "plge0n.dungeonbrawl.autoforge";
        public const string Name = "AutoForge";
        public const string Version = "0.40.0";
        private ModContext mod;
        private JsonConfig<Settings> config;
        private Dictionary<ForgeAttribute, float> totals = new Dictionary<ForgeAttribute, float>();
        private Dictionary<ForgeAttribute, float> checkpoint = new Dictionary<ForgeAttribute, float>();
        private int? pending;
        private ForgeOffer pendingOffer;
        private string notice;
        private float noticeAt;
        private int noticeCount;
        private KeyboardShortcut hotkey;
        private void Awake() => mod = ModContext.Start(this, Logger, Load);
        private void OnDestroy() => mod?.Dispose();
        private void Load(ModContext context)
        {
            mod = context;
            config = mod.LoadJson<Settings>("AutoForge.json");
            config.Data.Weights ??= Selection.Defaults();
            foreach (var pair in Selection.Defaults())
                if (!config.Data.Weights.ContainsKey(pair.Key)) config.Data.Weights[pair.Key] = pair.Value;
            ParseHotkey(config.Data.ToggleKey);
            mod.UseForgingChoice(Choose);
            mod.OnForgingChosen((offer, index) => {
                Selection.Accumulate(totals, offer.Candidates[index]);
                if (ReferenceEquals(offer, pendingOffer) && pending == index) {
                    if (Time.unscaledTime - noticeAt > 2.5f) noticeCount = 0;
                    noticeCount++; noticeAt = Time.unscaledTime;
                    notice = Describe(offer.Candidates[index]);
                    ScreenNotice.Show("自动锻体：" + notice + (noticeCount > 1 ? $"（连续 {noticeCount} 次）" : ""), 2.5f);
                }
                pending = null; pendingOffer = null;
            });
            mod.OnBattleStarted(Reset);
            mod.OnBattleEnded(Reset);
            mod.OnCheckpointSaved(() => checkpoint = new Dictionary<ForgeAttribute, float>(totals));
            mod.OnCheckpointRestored(() => { totals = new Dictionary<ForgeAttribute, float>(checkpoint); pending = null; pendingOffer = null; });
            mod.OnUpdate(() => {
                // IsDown 对部分组合允许额外修饰键；显式避免抢 RunLog 的 Ctrl+F10。
                if (!NativeUi.IsTyping && hotkey.IsDown() && !(hotkey.MainKey == KeyCode.F10 &&
                    (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))) {
                    config.Data.Enabled = !config.Data.Enabled; config.MarkDirty();
                    ScreenNotice.Show("自动锻体：" + (config.Data.Enabled ? "已开启" : "已关闭"), 2.5f);
                }
            });
            mod.AddSettings("自动锻体", BuildSettings);
            if (!Forging.ChoiceAvailable) mod.RunLog.Warning("锻造", "锻造接口不可用，保留手选");
        }
        private void Reset() { totals.Clear(); checkpoint.Clear(); pending = null; pendingOffer = null; noticeCount = 0; }
        private int? Choose(ForgeOffer offer)
        {
            pending = null; pendingOffer = null;
            if (!config.Data.Enabled) return null;
            int? choice = Selection.Choose(offer, config.Data.Mode, config.Data.Weights, totals);
            mod.RunLog.Info("锻造", "候选决策", ("档次", offer.Tier), ("模式", config.Data.Mode),
                ("候选和得分", string.Join(" | ", offer.Candidates.Select((c, i) => $"{i}:品质{c.Quality} {Describe(c)} score={Selection.Score(c, config.Data.Weights):0.####}"))),
                ("选中", choice.HasValue ? choice.Value.ToString() : "手选"));
            pending = choice; pendingOffer = offer;
            return choice;
        }
        private static string AttributeName(ForgeAttribute type) => Game.Localize("forging_" + type, type.ToString());
        private static string Describe(ForgeCandidate c) => string.Join("、", c.Stats.Select(s => $"{AttributeName(s.Type)} +{s.Value:0.##}{(s.IsPercent ? "%" : "")}"));
        private void ParseHotkey(string text)
        {
            try { hotkey = KeyboardShortcut.Deserialize(text); }
            catch { hotkey = new KeyboardShortcut(KeyCode.F10); }
        }
        private void BuildSettings(RectTransform parent)
        {
            var on = NativeUi.Toggle(parent, "启用自动锻体", 500, v => { config.Data.Enabled = v; config.MarkDirty(); });
            on.SetIsOnWithoutNotify(config.Data.Enabled);
            var mode = NativeUi.Dropdown(parent, new[] { "权重", "均衡" }, 300, v => { config.Data.Mode = (SelectionMode)v; config.MarkDirty(); });
            mode.SetValueWithoutNotify((int)config.Data.Mode);
            NativeUi.Label(parent, "热键（例如 F10；支持 BepInEx 组合键格式）", 18);
            var key = NativeUi.Input(parent, "F10", 400, v => { ParseHotkey(v); config.Data.ToggleKey = hotkey.Serialize(); config.MarkDirty(); });
            key.SetTextWithoutNotify(config.Data.ToggleKey);
            var sliders = new Dictionary<ForgeAttribute, UnityEngine.UI.Slider>();
            var labels = new Dictionary<ForgeAttribute, UnityEngine.UI.Text>();
            NativeUi.Button(parent, "恢复默认权重", false, 300, () => {
                config.Data.Weights = Selection.Defaults(); config.MarkDirty();
                foreach (var pair in sliders) { pair.Value.SetValueWithoutNotify(config.Data.Weights[pair.Key]); labels[pair.Key].text = AttributeName(pair.Key) + "：" + config.Data.Weights[pair.Key]; }
            });
            foreach (ForgeAttribute type in Enum.GetValues(typeof(ForgeAttribute))) {
                var row = NativeUi.Row(parent);
                var label = NativeUi.Label(row, AttributeName(type) + "：" + Selection.Weight(config.Data.Weights, type), 20, 300);
                labels[type] = label;
                var slider = NativeUi.Slider(row, 0, 10, true, v => { config.Data.Weights[type] = (int)v; label.text = AttributeName(type) + "：" + (int)v; config.MarkDirty(); });
                slider.SetValueWithoutNotify(Selection.Weight(config.Data.Weights, type)); sliders[type] = slider;
            }
        }
    }
}
