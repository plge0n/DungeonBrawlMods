using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DBModKit
{
    /// <summary>可以改数值的配置表。</summary>
    public enum DataTable
    {
        /// <summary>主动技能（键：技能枚举名）。</summary>
        Skill,
        /// <summary>英雄（键：英雄编号）。</summary>
        Hero,
        /// <summary>怪物（键：怪物枚举名）。数值按倍率调整，见 <see cref="DataField.IsScale"/>。</summary>
        Enemy,
    }

    /// <summary>表里一个可改的数值。</summary>
    public sealed class DataField
    {
        /// <summary>游戏数据结构里的字段名，也是存档里的键。</summary>
        public string Key { get; }
        public string Label { get; }
        /// <summary>给玩家看的一句说明。</summary>
        public string Hint { get; }
        /// <summary>只能是整数。</summary>
        public bool Whole { get; }
        /// <summary>
        /// 值是倍率（1 = 原版）而不是具体数值。怪物在不同难度、不同出场次数下数值不一样，只能按倍率调。
        /// </summary>
        public bool IsScale { get; }

        internal DataField(string key, string label, string hint, bool whole = false, bool isScale = false)
        {
            Key = key;
            Label = label;
            Hint = hint;
            Whole = whole;
            IsScale = isScale;
        }
    }

    /// <summary>表里的一行。</summary>
    public sealed class DataEntry
    {
        public DataTable Table { get; }
        public string Key { get; }
        /// <summary>本地化后的名字。</summary>
        public string Name { get; }
        /// <summary>图标的 Resources 路径；没有时为 null。</summary>
        public string IconPath { get; }
        private Sprite icon;
        private bool iconLoaded;

        internal DataEntry(DataTable table, string key, string name, string iconPath)
        {
            Table = table;
            Key = key;
            Name = name;
            IconPath = iconPath;
        }

        /// <summary>图标；没有时为 null。</summary>
        public Sprite Icon
        {
            get
            {
                if (!iconLoaded)
                {
                    iconLoaded = true;
                    if (!string.IsNullOrEmpty(IconPath)) icon = Resources.Load<Sprite>(IconPath);
                }
                return icon;
            }
        }
    }

    /// <summary>
    /// 改游戏配置表里的数值。改动先记在内存里，<see cref="Active"/> 为 true 时写进游戏的表，为 false 时表恢复原版。
    /// 生效时机：技能伤害、范围下次释放就生效；技能冷却和耗蓝在获得技能时拷贝，已有的技能要重新获得；
    /// 英雄数值在英雄生成（开局）时拷贝；怪物数值在怪物生成时拷贝。
    /// 只改主机/单人有意义：怪物在主机上生成，联机时各客户端的表互不同步。
    /// </summary>
    public static class GameData
    {
        public static readonly IReadOnlyList<DataField> SkillFields = new[]
        {
            new DataField("damageBase", "基础伤害", "伤害 = 基础伤害 + 属性 × 伤害系数", whole: true),
            new DataField("damageValue", "伤害系数", "乘在攻击、力量等属性上（看技能说明用的是哪种属性）"),
            new DataField("cost", "耗蓝", "放一次消耗的蓝量", whole: true),
            new DataField("cd", "冷却（秒）", "重新获得技能后生效"),
            new DataField("castingRange", "施法距离", "离目标多远能放"),
            new DataField("range", "作用范围", "技能效果的范围"),
            new DataField("duration", "持续时间（秒）", "持续类技能的时长"),
            new DataField("interval", "伤害间隔（秒）", "持续伤害每隔多久跳一次"),
            new DataField("chargingNum", "充能次数", "可以连续放几次，0 表示不充能", whole: true),
            new DataField("chargingCd", "充能时间（秒）", "恢复一次充能要多久"),
        };

        public static readonly IReadOnlyList<DataField> HeroFields = new[]
        {
            new DataField("hp", "生命", "初始生命", whole: true),
            new DataField("mp", "蓝量", "初始蓝量上限", whole: true),
            new DataField("attackPower", "攻击", "初始攻击", whole: true),
            new DataField("armor", "护甲", "初始护甲", whole: true),
            new DataField("STR", "力量", "初始力量", whole: true),
            new DataField("STRadd", "力量成长", "每升一级加多少力量", whole: true),
            new DataField("AGI", "敏捷", "初始敏捷", whole: true),
            new DataField("AGIadd", "敏捷成长", "每升一级加多少敏捷", whole: true),
            new DataField("STA", "体力", "初始体力（每点 +10 生命上限）", whole: true),
            new DataField("STAadd", "体力成长", "每升一级加多少体力", whole: true),
            new DataField("attackSpeed", "攻速", "基础攻速"),
            new DataField("moveSpeed", "移速", "移动速度"),
            new DataField("hpRecover", "回血", "每秒回复的生命", whole: true),
            new DataField("mpRecover", "回蓝", "每秒回复的蓝量", whole: true),
        };

        public static readonly IReadOnlyList<DataField> EnemyFields = new[]
        {
            new DataField("hp", "生命", "按难度和出场次数算出的生命再乘这个倍率", isScale: true),
            new DataField("attackPower", "攻击", "按难度算出的攻击再乘这个倍率", isScale: true),
            new DataField("shiled", "护盾", "护盾值的倍率", isScale: true),
            new DataField("armor", "护甲", "护甲的倍率", isScale: true),
            new DataField("attackSpeed", "攻速", "攻速的倍率", isScale: true),
            new DataField("moveSpeed", "移速", "移速的倍率", isScale: true),
        };

        // 表 → 键 → 字段 → 值（怪物是倍率）
        private static readonly Dictionary<DataTable, Dictionary<string, Dictionary<string, double>>> overrides =
            new Dictionary<DataTable, Dictionary<string, Dictionary<string, double>>>
            {
                [DataTable.Skill] = new Dictionary<string, Dictionary<string, double>>(),
                [DataTable.Hero] = new Dictionary<string, Dictionary<string, double>>(),
                [DataTable.Enemy] = new Dictionary<string, Dictionary<string, double>>(),
            };

        private static readonly Dictionary<DataTable, List<DataEntry>> entries = new Dictionary<DataTable, List<DataEntry>>();
        private static bool active;

        /// <summary>改数值的钩子已安装（怪物倍率需要）。为 false 时只能改技能和英雄。</summary>
        public static bool Available { get; internal set; }

        /// <summary>游戏的配置表已加载（游戏启动完成之前为 false，这时读不到数据，也不能激活）。</summary>
        public static bool Ready => ModKit.Compatible && GameRaw.TablesReady();

        /// <summary>改动写进游戏的表；设为 false 时所有表恢复原版（改动保留，可以再打开）。表还没加载时设为 true 无效。</summary>
        public static bool Active
        {
            get => active;
            set
            {
                if (active == value || (value && !Ready)) return;
                active = value;
                RunLog.Info(RunLog.CallerMod(), "数值", value ? "改动生效" : "改动停用，表已恢复原版", ("改动行数", overrides.Sum(t => t.Value.Count)));
                foreach (var table in overrides.Keys)
                    foreach (var key in overrides[table].Keys.ToList())
                        Sync(table, key);
                RaiseChanged();
            }
        }

        /// <summary>
        /// 当前负责这些改动的对象（一般是编辑器插件实例）。热重载时新实例会先于旧实例的清理启动，
        /// 旧实例清理前检查自己还是不是 Owner，不是就别清空，否则会把新实例刚加载的改动清掉。
        /// </summary>
        public static object Owner { get; set; }

        /// <summary>改动在 Active 变化、或任何值被修改后触发。</summary>
        public static event Action Changed;

        // 每项改动是哪个 Mod 写的；不同的 Mod 改了同一项、或载入时清掉了别人的改动，记下来给 ModConflicts
        private static readonly Dictionary<(DataTable table, string key, string field), string> writers = new Dictionary<(DataTable, string, string), string>();
        internal static readonly List<(string what, string first, string second, bool cleared)> Overlaps = new List<(string, string, string, bool)>();

        private static void NoteOverlap(DataTable table, string key, string field, string first, string second, bool cleared)
        {
            string what = $"{table}/{key}/{field}";
            if (Overlaps.Any(o => o.what == what && o.first == first && o.second == second && o.cleared == cleared)) return;
            Overlaps.Add((what, first, second, cleared));
            RunLog.WriteCore(ModKitPlugin.Name, "冲突", RunLogLevel.Warning, cleared ? "载入数值时清掉了别的 Mod 的改动" : "两个 Mod 改了同一项数值",
                new (string, object)[] { ("项", what), ("先", first), ("后", second) }, mirror: false);
            ModConflicts.Touch();
        }

        public static IReadOnlyList<DataField> Fields(DataTable table)
        {
            switch (table)
            {
                case DataTable.Skill: return SkillFields;
                case DataTable.Hero: return HeroFields;
                default: return EnemyFields;
            }
        }

        public static DataField Field(DataTable table, string key) => Fields(table).FirstOrDefault(f => f.Key == key);

        /// <summary>表里的所有行（按游戏里的顺序）。</summary>
        public static IReadOnlyList<DataEntry> Entries(DataTable table)
        {
            if (!Ready) return new DataEntry[0];
            if (!entries.TryGetValue(table, out var list))
            {
                list = GameRaw.ReadEntries(table);
                entries[table] = list;
            }
            return list;
        }

        /// <summary>原版的值；怪物字段固定返回 1。读不到时返回 NaN。</summary>
        public static double Original(DataTable table, string key, string field)
        {
            if (Field(table, field)?.IsScale == true) return 1.0;
            return Ready ? GameRaw.ReadOriginal(table, key, field) : double.NaN;
        }

        /// <summary>现在设定的值（没改过时等于原版）。</summary>
        public static double Get(DataTable table, string key, string field)
            => overrides[table].TryGetValue(key, out var fields) && fields.TryGetValue(field, out var v) ? v : Original(table, key, field);

        public static bool IsModified(DataTable table, string key, string field = null)
        {
            if (!overrides[table].TryGetValue(key, out var fields)) return false;
            return field == null ? fields.Count > 0 : fields.ContainsKey(field);
        }

        /// <summary>所有改动（表 → 键 → 字段 → 值），用于保存。</summary>
        public static Dictionary<DataTable, Dictionary<string, Dictionary<string, double>>> Snapshot()
            => overrides.ToDictionary(t => t.Key, t => t.Value.ToDictionary(k => k.Key, k => new Dictionary<string, double>(k.Value)));

        /// <summary>设定一个值。等于原版时视为恢复。整数字段会四舍五入，负数会改成 0。</summary>
        public static void Set(DataTable table, string key, string field, double value)
        {
            var f = Field(table, field) ?? throw new ArgumentException($"{table} 表没有字段 {field}");
            if (double.IsNaN(value) || double.IsInfinity(value) || !Ready) return;
            value = Math.Max(0.0, f.Whole ? Math.Round(value) : value);
            if (Math.Abs(value - Original(table, key, field)) < 1e-6)
            {
                Reset(table, key, field);
                return;
            }
            if (!overrides[table].TryGetValue(key, out var fields)) overrides[table][key] = fields = new Dictionary<string, double>();
            string caller = RunLog.CallerMod();
            if (writers.TryGetValue((table, key, field), out var writer) && writer != caller) NoteOverlap(table, key, field, writer, caller, false);
            writers[(table, key, field)] = caller;
            RunLog.Info(caller, "数值", "修改", ("表", table.ToString()), ("行", key), ("字段", field),
                ("值", $"{Get(table, key, field)}→{value}"), ("原版", Original(table, key, field)), ("生效", active));
            fields[field] = value;
            Sync(table, key);
            RaiseChanged();
        }

        /// <summary>恢复一个值；field 为 null 时恢复整行。</summary>
        public static void Reset(DataTable table, string key, string field = null)
        {
            if (!overrides[table].TryGetValue(key, out var fields)) return;
            foreach (var w in writers.Keys.Where(w => w.table == table && w.key == key && (field == null || w.field == field)).ToList()) writers.Remove(w);
            RunLog.Info(RunLog.CallerMod(), "数值", "恢复原版", ("表", table.ToString()), ("行", key), ("字段", field ?? "整行"));
            if (field == null) fields.Clear();
            else fields.Remove(field);
            if (fields.Count == 0) overrides[table].Remove(key);
            Sync(table, key);
            RaiseChanged();
        }

        /// <summary>清空全部改动并换成 data（加载存档时用）；data 为 null 时只清空。</summary>
        public static void Load(Dictionary<DataTable, Dictionary<string, Dictionary<string, double>>> data)
        {
            var touched = overrides.SelectMany(t => t.Value.Keys.Select(k => (t.Key, k))).ToList();
            string caller = RunLog.CallerMod();
            foreach (var w in writers.Where(w => w.Value != caller).ToList()) NoteOverlap(w.Key.table, w.Key.key, w.Key.field, w.Value, caller, true);
            writers.Clear();
            foreach (var t in overrides.Values) t.Clear();
            if (data != null)
                foreach (var t in data)
                    foreach (var k in t.Value)
                        if (k.Value != null && k.Value.Count > 0)
                            overrides[t.Key][k.Key] = k.Value.Where(f => Field(t.Key, f.Key) != null)
                                .ToDictionary(f => f.Key, f => f.Value);
            foreach (var t in overrides)
                foreach (var k in t.Value)
                    foreach (var f in k.Value.Keys)
                        writers[(t.Key, k.Key, f)] = caller;
            RunLog.Info(caller, "数值", data == null ? "清空全部改动" : "载入改动", ("改动行数", overrides.Sum(t => t.Value.Count)));
            foreach (var (table, key) in touched.Concat(overrides.SelectMany(t => t.Value.Keys.Select(k => (t.Key, k)))).Distinct().ToList())
                Sync(table, key);
            RaiseChanged();
        }

        // 把一行写成"原版 + 改动"（未激活时写回原版）
        private static void Sync(DataTable table, string key)
        {
            // 表还没加载时不可能写过，等激活时再统一写
            if (!Ready) return;
            Dictionary<string, double> fields = null;
            if (active) overrides[table].TryGetValue(key, out fields);
            Safe.Run($"GameData 写入 {table}.{key}", () => GameRaw.WriteRow(table, key, fields));
            if (table == DataTable.Skill && GameRaw.SkillIdOf(key) is int id) SkillInfo.Invalidate(id);
        }

        // 怪物表每局开始会补充新的行，补完后把倍率写上
        internal static void OnEnemyTableLoaded()
        {
            if (!active) return;
            foreach (var key in overrides[DataTable.Enemy].Keys.ToList())
                Sync(DataTable.Enemy, key);
        }

        // 某些出场次数会用另一张表直接覆盖生命、攻击、护盾，这时倍率要重新乘上
        internal static double EnemyScale(string key, string field)
            => active && overrides[DataTable.Enemy].TryGetValue(key, out var f) && f.TryGetValue(field, out var v) ? v : 1.0;

        internal static bool HasEnemyScale(string key) => active && overrides[DataTable.Enemy].ContainsKey(key);

        private static void RaiseChanged()
        {
            var evt = Changed;
            if (evt != null) Safe.InvokeEach(evt, nameof(Changed));
        }
    }
}
