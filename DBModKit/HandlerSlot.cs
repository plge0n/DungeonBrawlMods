using System.Collections.Generic;
using System.Linq;

namespace DBModKit
{
    // 同一时间只能有一个 Mod 接管的功能（自动释放、走位）：
    // 先注册的优先；玩家在"依赖和冲突"里指定了谁，就用谁。后注册的排队，生效的那个注销后自动轮到下一个。
    // 同一个 Mod 重新注册（热重载）时保留原来的位置
    internal sealed class HandlerSlot<T> where T : class
    {
        private static JsonConfig<Dictionary<string, string>> preferences;

        private readonly string kind;
        private readonly List<(string owner, T handler)> entries = new List<(string, T)>();

        public HandlerSlot(string kind, string label)
        {
            this.kind = kind;
            Label = label;
        }

        /// <summary>给玩家看的名字，比如"自动释放"。</summary>
        public string Label { get; }

        public T Active { get; private set; }

        /// <summary>生效的 Mod 的 GUID；没有时为 null。</summary>
        public string Owner { get; private set; }

        /// <summary>注册了的 Mod（按注册顺序）。</summary>
        public IReadOnlyList<string> Owners => entries.Select(e => e.owner).ToList();

        /// <summary>玩家指定的 Mod；没指定时为 null。</summary>
        public string Preferred
        {
            get => Preferences.Data.TryGetValue(kind, out var guid) ? guid : null;
            set
            {
                if (value == Preferred) return;
                if (value == null) Preferences.Data.Remove(kind);
                else Preferences.Data[kind] = value;
                Preferences.MarkDirty();
                Recompute();
            }
        }

        private static JsonConfig<Dictionary<string, string>> Preferences
            => preferences ??= new JsonConfig<Dictionary<string, string>>("DBModKit.handlers.json");

        public void Set(string owner, T handler)
        {
            int i = entries.FindIndex(e => e.owner == owner);
            if (i >= 0) entries[i] = (owner, handler);
            else entries.Add((owner, handler));
            if (entries.Count > 1 && i < 0)
                Log.Warning($"{Multiplayer.ModName(owner)} 也想接管{Label}，现在由 {Multiplayer.ModName(Pick().owner)} 处理（可以在 Mod 设置 → DBModKit → 依赖和冲突里换）");
            Recompute();
        }

        public void Clear(string owner)
        {
            if (entries.RemoveAll(e => e.owner == owner) > 0) Recompute();
        }

        private (string owner, T handler) Pick()
        {
            string preferred = Preferred;
            int i = preferred == null ? -1 : entries.FindIndex(e => e.owner == preferred);
            return i >= 0 ? entries[i] : entries.Count > 0 ? entries[0] : (null, null);
        }

        private void Recompute()
        {
            var (owner, handler) = Pick();
            if (owner != Owner)
                RunLog.WriteCore(ModKitPlugin.Name, "冲突", RunLogLevel.Info, $"{Label}由谁处理",
                    new (string, object)[] { ("现在", owner == null ? "没有" : Multiplayer.ModName(owner)), ("原来", Owner == null ? "没有" : Multiplayer.ModName(Owner)) }, mirror: false);
            Owner = owner;
            Active = handler;
            ModConflicts.Touch();
        }
    }
}
