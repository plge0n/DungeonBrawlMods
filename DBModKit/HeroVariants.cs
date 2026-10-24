using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DBModKit
{
    /// <summary>
    /// 变体英雄的定义，用 <see cref="ModContext.RegisterHeroVariant"/> 注册。变体底层仍是游戏已有的英雄（<see cref="BaseHero"/>），
    /// 出生时换掉属性、专属技能、模型和名字，所以联机、存档、图鉴、成就都按原英雄走，没装 Mod 的人看到的是原英雄。
    /// </summary>
    /// <remarks>
    /// 属性和专属技能会改变玩法，和 DBStudio 的玩法包一样只在 <see cref="Multiplayer.GameplayAllowed"/> 时生效
    /// （单人、房间里每个人都装了同样的 Mod，或房主允许）；否则只换名字和模型。玩家在 DBStudio 的 Mod 页（"英雄变体"）里给每个英雄选用哪个变体，Mod 也可以用 <see cref="HeroVariants.Select"/>。
    /// </remarks>
    public sealed class HeroVariantDef
    {
        /// <summary>在这个 Mod 里唯一的键（只用字母、数字、下划线）。</summary>
        public string Key = "";
        /// <summary>名字（血条上、设置里显示；翻译键 hero.&lt;Key&gt;.name）。</summary>
        public string Name = "";
        /// <summary>一句说明（设置里显示；翻译键 hero.&lt;Key&gt;.desc）。</summary>
        public string Description = "";
        /// <summary>基于哪个英雄（HeroType 的值，1~43；见 <see cref="GameData.Entries"/> 的英雄表）。</summary>
        public int BaseHero;

        // ---- 属性：为 null 时用原英雄的值 ----
        public int? Strength, Agility, Stamina;
        /// <summary>每级成长。</summary>
        public int? StrengthGrowth, AgilityGrowth, StaminaGrowth;
        public int? Attack, Armor, Hp, Mp, HpRecover, MpRecover;
        public float? MoveSpeed, AttackSpeed;

        // ---- 专属技能：都为 null 时用原英雄的 ----
        /// <summary>换成一个主动技能（游戏的或 <see cref="ModContext.RegisterSkill"/> 注册的）。</summary>
        public ActiveSkillInfo ActiveSkill;
        /// <summary>换成一个被动技能（<see cref="ModContext.RegisterPassive"/> 注册的）。</summary>
        public PassiveSkillInfo PassiveSkill;
        /// <summary>换成游戏自己的主动技能（ActiveSkillEnum 的值）或被动技能（被动表编号）。</summary>
        public int? GameActiveSkill, GamePassiveSkill;

        // ---- 模型（联机时也换，只在自己这边显示） ----
        /// <summary>
        /// 英雄模型文件夹（和创意工坊英雄模型同样的格式：manifest.json + AssetBundle，manifest 的 heroId 要等于 <see cref="BaseHero"/>），
        /// 相对路径从插件文件夹找（例如 "assets/skin"）。
        /// </summary>
        public string ModelFolder;
        /// <summary>或者用一个已订阅的创意工坊英雄模型（物品编号）。</summary>
        public ulong WorkshopModel;
    }

    /// <summary>已注册的变体英雄。</summary>
    public sealed class HeroVariantInfo
    {
        /// <summary>"Mod 的 GUID:Key"，选择时存的就是它。</summary>
        public string Id { get; }
        public string Owner { get; }
        public HeroVariantDef Def { get; }
        internal ModText Text { get; }
        /// <summary>模型文件夹的完整路径；没有时为 null。</summary>
        public string ModelFolder { get; }

        internal HeroVariantInfo(string owner, HeroVariantDef def, ModText text, string modelFolder)
        {
            Id = owner + ":" + def.Key;
            Owner = owner;
            Def = def;
            Text = text;
            ModelFolder = modelFolder;
        }

        public int BaseHero => Def.BaseHero;
        public string Name => Text != null ? Text[$"hero.{Def.Key}.name", Def.Name] : Def.Name;
        public string Description => Text != null ? Text[$"hero.{Def.Key}.desc", Def.Description] : Def.Description;

        /// <summary>改了属性或专属技能（这部分算改玩法的内容，见 <see cref="Multiplayer"/>）。</summary>
        public bool ChangesGameplay
        {
            get
            {
                var d = Def;
                return d.Strength.HasValue || d.Agility.HasValue || d.Stamina.HasValue || d.StrengthGrowth.HasValue || d.AgilityGrowth.HasValue
                       || d.StaminaGrowth.HasValue || d.Attack.HasValue || d.Armor.HasValue || d.Hp.HasValue || d.Mp.HasValue || d.HpRecover.HasValue
                       || d.MpRecover.HasValue || d.MoveSpeed.HasValue || d.AttackSpeed.HasValue || SkillOverride(out _, out _);
            }
        }

        // 专属技能换成什么：编号和是否主动
        internal bool SkillOverride(out int id, out bool active)
        {
            var d = Def;
            if (d.ActiveSkill != null) { id = d.ActiveSkill.Id; active = true; return true; }
            if (d.PassiveSkill != null) { id = d.PassiveSkill.Id; active = false; return true; }
            if (d.GameActiveSkill.HasValue) { id = d.GameActiveSkill.Value; active = true; return true; }
            if (d.GamePassiveSkill.HasValue) { id = d.GamePassiveSkill.Value; active = false; return true; }
            id = 0;
            active = false;
            return false;
        }
    }

    /// <summary>变体英雄的注册表和玩家的选择。</summary>
    public static class HeroVariants
    {
        private static readonly List<HeroVariantInfo> registered = new List<HeroVariantInfo>();
        private static JsonConfig<Dictionary<string, string>> choices;
        private static bool settingsRegistered;

        /// <summary>出生钩子装上了。</summary>
        public static bool Available { get; internal set; }

        public static IReadOnlyList<HeroVariantInfo> All => registered;

        /// <summary>本地玩家这一局用的变体；没用时为 null。</summary>
        public static HeroVariantInfo LocalVariant { get; internal set; }

        /// <summary>这一局变体的属性和专属技能生效了（出生时 <see cref="Multiplayer.GameplayAllowed"/>）。</summary>
        public static bool LocalGameplayActive { get; internal set; }

        /// <summary>选择变了。</summary>
        public static event Action Changed;

        internal static HeroVariantInfo Register(string owner, ModText text, string modFolder, HeroVariantDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (string.IsNullOrEmpty(def.Key) || !def.Key.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new ArgumentException($"变体的 Key 只能用字母、数字、下划线：\"{def.Key}\"");
            if (def.BaseHero <= 0) throw new ArgumentException($"变体 {def.Key} 没有设置 BaseHero");
            if (registered.Any(r => r.Owner == owner && r.Def.Key == def.Key)) throw new ArgumentException($"变体 {def.Key} 已经注册过");
            string folder = null;
            if (!string.IsNullOrEmpty(def.ModelFolder))
            {
                folder = Path.IsPathRooted(def.ModelFolder) || modFolder == null ? def.ModelFolder : Path.Combine(modFolder, def.ModelFolder);
                if (!Directory.Exists(folder)) throw new DirectoryNotFoundException($"变体 {def.Key} 的模型文件夹不存在：{folder}");
            }
            var info = new HeroVariantInfo(owner, def, text, folder);
            registered.Add(info);
            EnsureLoaded();
            if (!settingsRegistered)
            {
                settingsRegistered = true;
                VariantSettings.Register();
            }
            RaiseChanged();
            return info;
        }

        internal static void Unregister(HeroVariantInfo info)
        {
            if (info == null || !registered.Remove(info)) return;
            RaiseChanged();
        }

        private static void EnsureLoaded()
        {
            if (choices == null) choices = new JsonConfig<Dictionary<string, string>>("DBModKit.variants.json");
        }

        /// <summary>选了哪个变体（基础英雄 → 变体）；没选、或选的变体没注册（Mod 没装）时为 null。</summary>
        public static HeroVariantInfo Selected(int heroId)
        {
            if (registered.Count == 0) return null;
            EnsureLoaded();
            return choices.Data.TryGetValue(heroId.ToString(), out var id) ? registered.FirstOrDefault(r => r.Id == id && r.BaseHero == heroId) : null;
        }

        /// <summary>选这个英雄用哪个变体（null = 原版），下次出生时生效（模型立即换）。</summary>
        public static void Select(int heroId, HeroVariantInfo variant)
        {
            if (variant != null && variant.BaseHero != heroId) throw new ArgumentException($"变体 {variant.Name} 是给英雄 {variant.BaseHero} 的");
            EnsureLoaded();
            if (variant == null) choices.Data.Remove(heroId.ToString());
            else choices.Data[heroId.ToString()] = variant.Id;
            choices.MarkDirty();
            RaiseChanged();
            if (HeroSkins.Available && HeroSkins.LocalHeroId == heroId)
                Safe.Run("变体英雄：重新换模型", () => GameRaw.ReapplyLocalHeroModel(heroId));
        }

        // HeroSkins 查找本地玩家的模型时先问这里：选了带模型的变体就用变体的
        internal static bool TryModelFolder(int heroId, out string folder)
        {
            folder = null;
            var v = Selected(heroId);
            if (v == null) return false;
            if (v.ModelFolder != null) folder = v.ModelFolder;
            else if (v.Def.WorkshopModel != 0) folder = GameRaw.WorkshopFolder(v.Def.WorkshopModel);
            return folder != null && GameRaw.IsValidSkinFolder(folder, heroId);
        }

        // 血条上的名字（本地玩家用了变体时）
        internal static string LocalName(int heroId) => LocalVariant != null && LocalVariant.BaseHero == heroId ? LocalVariant.Name : null;

        private static void RaiseChanged()
        {
            var evt = Changed;
            if (evt != null) Safe.InvokeEach(evt, nameof(Changed));
        }

        // ---- 出生（由 Hooks 在 bbs.ljm 前后调用，只对本地玩家）----

        internal static object BeforeSpawn(int heroId)
        {
            LocalVariant = Selected(heroId);
            LocalGameplayActive = false;
            if (LocalVariant == null) return null;
            bool gameplay = LocalVariant.ChangesGameplay && Multiplayer.GameplayAllowed;
            LocalGameplayActive = gameplay;
            RunLog.WriteCore(ModKitPlugin.Name, "变体英雄", RunLogLevel.Info, "出生",
                new (string, object)[] { ("变体", LocalVariant.Id), ("英雄", heroId), ("属性和技能", gameplay ? "生效" : LocalVariant.ChangesGameplay ? "联机握手不允许，不生效" : "没改") }, mirror: false);
            // 不生效时的提示由 Multiplayer 在进入战斗时统一给
            return gameplay ? GameRaw.ApplyVariantTables(heroId, LocalVariant) : null;
        }

        internal static void AfterSpawn(object restore) => GameRaw.RestoreVariantTables(restore);
    }

    // DBStudio 的 Mod 页里"英雄变体"一栏：每个有变体的英雄一个下拉框
    internal static class VariantSettings
    {
        private static RectTransform list;
        private static bool dirty = true;

        public static void Register()
        {
            ModSettings.Register(ModKitPlugin.Guid, "英雄变体", Build, Refresh);
            HeroVariants.Changed += () => dirty = true;
        }

        private static void Build(RectTransform container)
        {
            NativeUi.Label(container, "选英雄时用哪个变体。属性和专属技能只在单人、或房间里每个人都装了同样的 Mod（或房主允许）时生效，否则只换名字和模型。", 16, -1f, NativeUi.DimText);
            list = NativeUi.Column(container, -1f, 6f);
            list.GetComponent<LayoutElement>().flexibleHeight = 0f;
            dirty = true;
            Refresh();
        }

        private static void Refresh()
        {
            if (list == null || !dirty) return;
            dirty = false;
            for (int i = list.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(list.GetChild(i).gameObject);
            var groups = HeroVariants.All.GroupBy(v => v.BaseHero).OrderBy(g => g.Key).ToList();
            if (groups.Count == 0)
            {
                NativeUi.Label(list, "没有 Mod 注册变体英雄", 18, -1f, NativeUi.DimText);
                return;
            }
            foreach (var g in groups)
            {
                int hero = g.Key;
                var variants = g.ToList();
                var row = NativeUi.Row(list, 48f, 12f);
                NativeUi.Label(row, Game.Localize("hero_" + hero, "英雄 " + hero), 22, 220f);
                var options = new List<string> { "原版" };
                options.AddRange(variants.Select(v => v.Name));
                var selected = HeroVariants.Selected(hero);
                var dropdown = NativeUi.Dropdown(row, options, 360f, i => HeroVariants.Select(hero, i <= 0 ? null : variants[i - 1]));
                dropdown.SetValueWithoutNotify(selected == null ? 0 : variants.IndexOf(selected) + 1);
                var desc = selected?.Description;
                if (!string.IsNullOrEmpty(desc)) NativeUi.Label(row, desc, 16, -1f, NativeUi.DimText);
            }
        }
    }
}
