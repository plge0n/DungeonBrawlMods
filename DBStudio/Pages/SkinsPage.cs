using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace DBStudio.Pages
{
    // 皮肤：给每个英雄从已订阅的工坊模型、本地皮肤文件夹里挑一套换上，或者强制用原版；
    // 左下角显示房间里也装了 Mod 的队友在用的皮肤
    internal sealed class SkinsPage : IStudioPage
    {
        // 订阅的物品可能在页面打开期间才下载完
        private const float ScanInterval = 5f;

        // 切页再回来时保持选择
        private static string selectedHero;

        private RectTransform heroList, options, others;
        private Image headIcon;
        private Text headTitle, hint, state;
        private bool listBuilt;
        private float nextScan;
        private int othersVersion = -1;
        private bool othersShown;
        private readonly Dictionary<string, Text> heroLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, Image> heroBackgrounds = new Dictionary<string, Image>();
        // 右边每个选项的背景，键是皮肤编号（null 表示按游戏设置）
        private readonly List<(string id, Image background)> optionBackgrounds = new List<(string, Image)>();

        public string Title => "皮肤";

        public void Build(RectTransform body)
        {
            listBuilt = false;
            nextScan = Time.unscaledTime + ScanInterval;
            Skins.Rescan();
            var columns = Studio.Columns(body);

            var left = NativeUi.Column(columns, 470f);
            NativeUi.Heading(left, "英雄");
            heroList = NativeUi.ScrollView(left, out _, 4f);
            NativeUi.Separator(left);
            NativeUi.Heading(left, "联机");
            var onlyLocal = NativeUi.Toggle(left, "只换自己", 300f, Skins.SetOnlyLocalPlayer);
            onlyLocal.isOn = Skins.Current.OnlyLocalPlayer;
            NativeUi.SetEnabled(onlyLocal, HeroSkins.OnlyLocalPlayerAvailable);
            var showOthers = NativeUi.Toggle(left, "显示队友的皮肤", 300f, Skins.SetShowOtherPlayers);
            showOthers.isOn = Skins.Current.ShowOtherPlayers;
            // 给队友换模型靠的是记下"正在出生的是谁"的钩子，和"只换自己"是同一组
            NativeUi.SetEnabled(showOthers, HeroSkins.Available && HeroSkins.OnlyLocalPlayerAvailable);
            others = FixedColumn(left);
            othersVersion = -1;

            var right = NativeUi.Column(columns);
            var head = NativeUi.Row(right, 64f, 14f);
            var icon = new GameObject("icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            icon.transform.SetParent(head, false);
            var iconLe = icon.GetComponent<LayoutElement>();
            iconLe.preferredWidth = iconLe.minWidth = 60f;
            headIcon = icon.GetComponent<Image>();
            headIcon.preserveAspect = true;
            headTitle = NativeUi.Label(head, "", 30, -1f, NativeUi.Gold);
            headTitle.fontStyle = FontStyle.Bold;
            hint = NativeUi.Label(right, "", 18, -1f, NativeUi.DimText);
            NativeUi.Separator(right);
            options = NativeUi.ScrollView(right, out _, 6f, boxed: false);

            NativeUi.Separator(right);
            var bottom = NativeUi.Row(right, 56f, 16f);
            state = NativeUi.Label(bottom, "", 18, -1f, NativeUi.DimText);
            NativeUi.Button(bottom, "刷新列表", false, 170f, () =>
            {
                Skins.Rescan();
                RebuildList();
                ShowHero();
            });
            NativeUi.Button(bottom, "打开皮肤文件夹", false, 230f, OpenLocalRoot);

            RebuildList();
            ShowHero();
        }

        public void Refresh()
        {
            // 主菜单之前打开时表还没加载
            bool changed = false;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + ScanInterval;
                changed = Skins.Rescan();
            }
            if ((!listBuilt || changed) && GameData.Ready)
            {
                RebuildList();
                ShowHero();
            }
            if (!HeroSkins.Available) state.text = "<color=#e07a5f>换模型的钩子没装上，选择不会生效</color>";
            else if (!HeroSkins.OnlyLocalPlayerAvailable) state.text = "<color=#e07a5f>“只换自己”的钩子没装上，同英雄的其他玩家也会换</color>";
            else state.text = Skins.Current.OnlyLocalPlayer ? "同英雄的其他玩家保持原样" : "同英雄的其他玩家也换成你选的模型";
            if (othersVersion != HeroSkins.OtherPlayersVersion || changed || othersShown != Skins.Current.ShowOtherPlayers)
            {
                othersVersion = HeroSkins.OtherPlayersVersion;
                othersShown = Skins.Current.ShowOtherPlayers;
                ShowOthers();
            }
        }

        // 房间里也装了 Mod 的其他玩家在用的皮肤；你没订阅的可以打开工坊页面自己订阅
        private void ShowOthers()
        {
            Clear(others);
            var list = HeroSkins.OtherPlayers;
            if (list.Count == 0)
            {
                Studio.Block(others, 18).text = "<color=#a6a6a6>Steam 联机时，房间里也装了 Mod 的队友在用的皮肤会显示在这里。局域网模式不同步。</color>";
                return;
            }
            foreach (var r in list)
            {
                var row = NativeUi.Row(others, 44f, 10f);
                string hero = GameData.Entries(DataTable.Hero).FirstOrDefault(e => e.Key == r.HeroId.ToString())?.Name ?? $"英雄 {r.HeroId}";
                string skin = !Skins.Current.ShowOtherPlayers ? "<color=#a6a6a6>（不显示队友的皮肤）</color>"
                    : r.SkinId == HeroSkins.Original ? "原版"
                    : r.Installed ? Skins.All.FirstOrDefault(x => x.Id == r.SkinId)?.Title ?? $"工坊 {r.PublishedFileId}"
                    : "<color=#d6b67d>没订阅，显示原版</color>";
                NativeUi.Label(row, $"{Escape(r.PlayerName)}  <color=#a6a6a6>{hero}</color>  {skin}", 18);
                if (!r.Installed)
                {
                    ulong id = r.PublishedFileId;
                    NativeUi.Button(row, "去订阅", false, 120f, () => HeroSkins.OpenWorkshopPage(id)).GetComponent<LayoutElement>().preferredHeight = 40f;
                }
            }
        }

        private void RebuildList()
        {
            Clear(heroList);
            heroLabels.Clear();
            heroBackgrounds.Clear();
            listBuilt = GameData.Ready;
            if (!listBuilt)
            {
                Studio.Block(heroList).text = "游戏还没加载完，进入主菜单后再看";
                return;
            }
            // 有皮肤可选的排在前面
            foreach (var e in GameData.Entries(DataTable.Hero).OrderByDescending(e => SkinsOf(e.Key).Count > 0))
            {
                var row = SelectableRow(heroList, e.Key, 52f, () =>
                {
                    selectedHero = e.Key;
                    UpdateHeroSelection();
                    ShowHero();
                }, out var bg);
                AddIcon(row, e.Icon, 44f);
                heroLabels[e.Key] = NativeUi.Label(row, HeroText(e), 22);
                heroBackgrounds[e.Key] = bg;
            }
            UpdateHeroSelection();
        }

        private string HeroText(DataEntry e)
        {
            int count = SkinsOf(e.Key).Count;
            string choice = HeroSkins.Get(HeroId(e.Key));
            string mark = choice == null ? ""
                : choice == HeroSkins.Original ? "  <color=#a6a6a6>原版</color>"
                : $"  <color=#d6b67d>● {Skins.All.FirstOrDefault(s => s.Id == choice)?.Title ?? "找不到"}</color>";
            return $"{e.Name}{(count > 0 ? $"  <color=#a6a6a6>{count} 套</color>" : "")}{mark}";
        }

        private void UpdateHeroSelection()
        {
            foreach (var kv in heroBackgrounds)
                kv.Value.color = kv.Key == selectedHero ? NativeUi.BoxColor : Color.clear;
        }

        // 右边：选中英雄的所有可选皮肤
        private void ShowHero()
        {
            Clear(options);
            optionBackgrounds.Clear();
            var hero = selectedHero == null ? null : GameData.Entries(DataTable.Hero).FirstOrDefault(e => e.Key == selectedHero);
            headIcon.enabled = hero?.Icon != null;
            headIcon.sprite = hero?.Icon;
            if (hero == null)
            {
                headTitle.text = "";
                hint.text = "皮肤只改显示，不影响玩法，联机时也能用。Steam 联机时，也装了 Mod 的队友能看到你的工坊皮肤（要他们也订阅了）。";
                Studio.Block(options).text = GameData.Ready ? "在左边选一个英雄" : "";
                return;
            }
            headTitle.text = hero.Name;
            hint.text = "选好立即换上，战斗中也可以换。工坊皮肤会同步给也装了 Mod 的队友；本地皮肤只有你自己能看到。";

            int heroId = HeroId(hero.Key);
            AddOption(heroId, null, hero.Icon, "游戏设置", "用主菜单“工坊 Mod”里给这个英雄启用的模型，没有就是原版");
            AddOption(heroId, HeroSkins.Original, hero.Icon, "原版", "不换模型");
            string choice = HeroSkins.Get(heroId);
            if (choice != null && choice != HeroSkins.Original && Skins.All.All(s => s.Id != choice))
                AddOption(heroId, choice, null, "<color=#e07a5f>找不到选过的皮肤</color>",
                    $"{choice}：可能已经取消订阅或删除了，现在按游戏设置显示");
            foreach (var skin in SkinsOf(hero.Key))
            {
                string from = skin.IsLocal ? $"本地 · {Path.GetFileName(skin.Folder)}" : "创意工坊";
                if (skin.Author != "") from += $" · 作者 {skin.Author}";
                AddOption(heroId, skin.Id, skin.Preview, skin.Title, from);
            }
            if (SkinsOf(hero.Key).Count == 0)
                Studio.Block(options, 18).text = "<color=#a6a6a6>这个英雄还没有皮肤。在 Steam 创意工坊订阅英雄模型，"
                    + "或者把模型文件夹（含 manifest.json）放进皮肤文件夹，然后点“刷新列表”。</color>";
            UpdateOptionSelection(heroId);
        }

        private void AddOption(int heroId, string id, Sprite picture, string title, string description)
        {
            var row = SelectableRow(options, id ?? "game", 104f, () =>
            {
                HeroSkins.Set(heroId, id);
                UpdateOptionSelection(heroId);
                if (heroLabels.TryGetValue(selectedHero, out var label))
                    label.text = HeroText(GameData.Entries(DataTable.Hero).First(e => e.Key == selectedHero));
                EventLog.Add($"皮肤：{headTitle.text} → {StripTags(title)}");
            }, out var bg);
            AddIcon(row, picture, 88f);
            var text = NativeUi.Column(row, -1f, 2f);
            NativeUi.Label(text, title, 24);
            NativeUi.Label(text, description, 18, -1f, NativeUi.DimText);
            optionBackgrounds.Add((id, bg));
        }

        private void UpdateOptionSelection(int heroId)
        {
            string choice = HeroSkins.Get(heroId);
            foreach (var (id, bg) in optionBackgrounds)
                bg.color = id == choice ? NativeUi.BoxColor : Color.clear;
        }

        private static List<HeroSkin> SkinsOf(string heroKey)
        {
            int heroId = HeroId(heroKey);
            return Skins.All.Where(s => s.HeroId == heroId).ToList();
        }

        private static int HeroId(string key) => int.TryParse(key, out int id) ? id : 0;

        // 整行可点的列表项（同数值页左边的列表）
        private static RectTransform SelectableRow(Transform parent, string name, float height, UnityEngine.Events.UnityAction onClick, out Image background)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(8, 8, 4, 4);
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            background = go.GetComponent<Image>();
            background.sprite = NativeUi.BoxSprite;
            background.type = Image.Type.Sliced;
            background.color = Color.clear;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = background;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(onClick);
            return (RectTransform)go.transform;
        }

        // 高度随内容变化、不抢剩余空间的竖向容器
        private static RectTransform FixedColumn(Transform parent)
        {
            var go = new GameObject("fixedColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return (RectTransform)go.transform;
        }

        // 没有图时留出同样的宽度，文字才对得齐
        private static void AddIcon(Transform parent, Sprite sprite, float size)
        {
            var icon = new GameObject("icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            icon.transform.SetParent(parent, false);
            var le = icon.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = size;
            var img = icon.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.sprite = sprite;
            img.color = sprite != null ? Color.white : Color.clear;
        }

        private static void OpenLocalRoot()
        {
            string root = HeroSkins.LocalRoot;
            Directory.CreateDirectory(root);
            Process.Start("explorer.exe", $"\"{root}\"");
        }

        private static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.Destroy(t.GetChild(i).gameObject);
        }

        // Steam 昵称里可能有 <size>、<color> 之类的标签，插一个零宽空格让它不被当成富文本
        private static string Escape(string s) => (s ?? "").Replace("<", "<\u200B");

        private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");
    }
}
