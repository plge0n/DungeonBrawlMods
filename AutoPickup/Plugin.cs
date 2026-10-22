using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using DBModKit;
using UnityEngine;

namespace AutoPickup
{
    // 战斗中自动拾取身边能捡的掉落物（自己的、公共的、队友设了共享的），等于替玩家按 C（范围不超过按 C 的 5 米），可以按类别过滤
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, "0.12.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "plge0n.dungeonbrawl.autopickup";
        public const string Name = "AutoPickup";
        // 发布时同步修改 CHANGELOG.md；pack.ps1 从这里读取版本号
        public const string Version = "0.1.0";

        // 发出请求后这么久物品还在地上，就再请求一次（联机延迟、服务器没处理）；请求这么多次还在就不再管它
        private const float RetryAfter = 1.5f;
        private const int MaxAttempts = 3;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<float> Range;
        internal static ConfigEntry<float> Interval;
        internal static ConfigEntry<bool> Sound;
        private static readonly Dictionary<LootKind, ConfigEntry<bool>> kinds = new Dictionary<LootKind, ConfigEntry<bool>>();

        // 已请求拾取的物品 → 最近一次请求的时间、请求次数
        private readonly Dictionary<uint, (float at, int attempts)> requested = new Dictionary<uint, (float, int)>();
        private readonly HashSet<uint> seen = new HashSet<uint>();
        private float nextScan;
        private bool bagFullNoticed;

        // 必须直接继承 BaseUnityPlugin（不能继承 ModKit 里的类），否则 ScriptEngine 热重载会出错
        private ModContext mod;

        private void Awake() => mod = ModContext.Start(this, Logger, OnLoad, OnUnload);

        private void OnDestroy() => mod?.Dispose();

        private void OnLoad(ModContext mod)
        {
            Enabled = Config.Bind("General", "Enabled", true, "战斗中自动拾取身边能捡的掉落物（和按 C 一样）");
            ToggleKey = Config.Bind("General", "ToggleKey", KeyboardShortcut.Empty, "开关自动拾取的快捷键（默认不设）");
            Range = Config.Bind("General", "Range", Loot.PickRange,
                new ConfigDescription("拾取范围（米），最大和按 C 一样是 5 米", new AcceptableValueRange<float>(1f, Loot.PickRange)));
            Interval = Config.Bind("General", "Interval", 0.25f,
                new ConfigDescription("每隔几秒检查一次", new AcceptableValueRange<float>(0.1f, 2f)));
            Sound = Config.Bind("General", "Sound", true, "捡到东西时播放游戏的拾取音效");

            kinds.Clear();
            BindKind(LootKind.Gold, true, "金币、宝石、金币袋");
            BindKind(LootKind.AttributeBook, true, "属性书");
            BindKind(LootKind.SkillBook, true, "技能书（占背包格子）");
            BindKind(LootKind.Relic, true, "遗物");
            BindKind(LootKind.Card, true, "卡牌");
            BindKind(LootKind.Potion, true, "药剂（占背包格子）");
            BindKind(LootKind.Material, true, "装备强化材料");
            BindKind(LootKind.Talisman, false,
                "神符。游戏设置里关掉\"护符需按键拾取\"时，神符走到身上就会自动捡，这里不用开；开着那个设置时说明你想自己挑时机用");
            BindKind(LootKind.Other, false, "其他（剧情道具、Boss 武器、钥匙等，有的会触发事件，默认不捡）");

            mod.OnBattleStarted(Reset);
            mod.OnBattleEnded(Reset);
            mod.OnUpdate(Tick);
        }

        private void BindKind(LootKind kind, bool on, string description)
            => kinds[kind] = Config.Bind("Kinds", kind.ToString(), on, description);

        private void OnUnload()
        {
        }

        private void Reset()
        {
            requested.Clear();
            bagFullNoticed = false;
        }

        private void Tick()
        {
            if (ToggleKey.Value.IsDown())
            {
                Enabled.Value = !Enabled.Value;
                ScreenNotice.Show(Enabled.Value ? "AutoPickup：自动拾取已开启" : "AutoPickup：自动拾取已关闭", 3f);
            }
            if (!Enabled.Value || Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + Interval.Value;

            var player = Game.LocalPlayer;
            if (player == null) return;

            var items = Loot.All();
            ForgetGone(items);

            // 背包格子要算上已请求、服务器还没确认的
            int bag = Loot.BagCount;
            foreach (var item in items)
                if (item.UsesBagSlot && requested.TryGetValue(item.Id, out var r) && r.attempts < MaxAttempts) bag++;

            bool picked = false, bagFull = false;
            float now = Time.unscaledTime;
            foreach (var item in items)
            {
                if (!item.Pickable || !Wanted(item) || player.DistanceTo(item.Position) > Range.Value) continue;
                bool retry = requested.TryGetValue(item.Id, out var last);
                if (retry && (last.attempts >= MaxAttempts || now - last.at < RetryAfter)) continue;
                if (item.UsesBagSlot && !retry && bag >= Loot.BagSize)
                {
                    bagFull = true;
                    continue;
                }
                var result = Loot.TryPick(item);
                if (result == PickResult.BagFull) bagFull = true;
                if (result != PickResult.Requested) continue;
                if (item.UsesBagSlot && !retry) bag++;
                requested[item.Id] = (now, retry ? last.attempts + 1 : 1);
                picked = true;
            }

            if (picked && Sound.Value) Loot.PlayPickSound();
            // 背包满了只提示一次，腾出格子后再满会重新提示
            if (bagFull && !bagFullNoticed) ScreenNotice.Show("AutoPickup：背包已满，占格子的物品先不捡", 3f);
            bagFullNoticed = bagFull;
        }

        private static bool Wanted(LootItem item)
        {
            if (!kinds.TryGetValue(item.Kind, out var entry) || !entry.Value) return false;
            // 护符：只有开了"护符需按键拾取"才需要我们捡，否则游戏自己会捡（经验之书例外，按 C 也会捡）
            return item.Kind != LootKind.Talisman || Loot.KeyPickTalisman || item.ItemType == 1406;
        }

        // 地上已经没有的物品不再记着
        private void ForgetGone(List<LootItem> items)
        {
            if (requested.Count == 0) return;
            seen.Clear();
            foreach (var item in items) seen.Add(item.Id);
            var gone = new List<uint>();
            foreach (var id in requested.Keys)
                if (!seen.Contains(id)) gone.Add(id);
            foreach (var id in gone) requested.Remove(id);
        }
    }
}
