using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DBModKit
{
    /// <summary>
    /// Mod 自己的 AssetBundle（Unity 编辑器打包），用 <see cref="ModContext.LoadBundle"/> 打开，Mod 卸载时自动关闭。
    /// </summary>
    public sealed class ModBundle
    {
        internal AssetBundle Bundle;

        public string File { get; }

        internal ModBundle(string file, AssetBundle bundle)
        {
            File = file;
            Bundle = bundle;
        }

        /// <summary>包里的资源名（打包时的路径，小写）。</summary>
        public string[] AssetNames => Bundle != null ? Bundle.GetAllAssetNames() : new string[0];

        /// <summary>按名字读资源（可以写完整路径，也可以只写文件名，不带扩展名也行）；没有时为 null。</summary>
        public T Load<T>(string name) where T : UnityEngine.Object
        {
            var asset = Bundle != null ? Bundle.LoadAsset<T>(name) : null;
            // 记下包里的预制体：注册特效时它们直接当模板，不能像场景里的对象那样挂到别的物体下
            if (asset is GameObject go) ModAssets.BundlePrefabs.Add(go);
            return asset;
        }

        public Sprite Sprite(string name)
        {
            var sprite = Load<Sprite>(name);
            if (sprite != null) return sprite;
            // 贴图没设成 Sprite 类型时自己包一个
            var texture = Load<Texture2D>(name);
            return texture != null ? ModAssets.ToSprite(texture) : null;
        }

        public GameObject Prefab(string name) => Load<GameObject>(name);

        public AnimationClip Clip(string name) => Load<AnimationClip>(name);
    }

    /// <summary>
    /// Mod 的图标和特效。注册后得到一个名字，用法和游戏自己的资源一样：
    /// 图标名填进 <see cref="PassiveSkillDef.Icon"/> / <see cref="ActiveSkillDef.Icon"/>，特效路径传给 <see cref="ActiveSkill.Effect"/>。
    /// </summary>
    /// <remarks>
    /// 游戏按 Resources 路径加载图标（Bundles/UI/Icon/Skill/ + 名字）和特效（Bundles/ + 路径，经对象池）。
    /// 我们的名字都以 "DBMod/" 开头，ModKit 在加载处把它们换成 Mod 注册的资源。
    /// </remarks>
    public static class ModAssets
    {
        internal const string Prefix = "DBMod/";

        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, GameObject> effects = new Dictionary<string, GameObject>();
        private static GameObject templateRoot;
        // 从 ModBundle 读出来的预制体（资源，不在任何场景里）
        internal static readonly HashSet<GameObject> BundlePrefabs = new HashSet<GameObject>();

        /// <summary>游戏各处的图标加载都能换成 Mod 的图标（Resources.Load 补丁装上了）。为 false 时只有技能栏按钮能显示。</summary>
        public static bool IconsAvailable { get; internal set; }

        /// <summary>特效补丁装上了。</summary>
        public static bool EffectsAvailable { get; internal set; }

        internal static string Name(string owner, string key)
        {
            if (string.IsNullOrEmpty(key) || key.IndexOfAny(new[] { '/', '\\' }) >= 0) throw new ArgumentException($"资源的键不能为空或包含 / \\：\"{key}\"");
            return Prefix + owner + "/" + key;
        }

        internal static string RegisterIcon(string owner, string key, Sprite sprite)
        {
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));
            string name = Name(owner, key);
            icons[name] = sprite;
            return name;
        }

        internal static void UnregisterIcon(string name) => icons.Remove(name);

        internal static string RegisterEffect(string owner, string key, GameObject template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            string name = Name(owner, key);
            // 代码搭的对象做成不显示、不随场景销毁的模板；包里的预制体本来就不在场景里，直接用。
            // 不能用 scene.IsValid() 判断：插件 Awake 时（游戏启动早期）新建的对象 scene 也无效，会被当成预制体留在启动场景里，切场景就没了
            if (!BundlePrefabs.Contains(template))
            {
                if (templateRoot == null)
                {
                    // 只用 DontDestroyOnLoad 不够：游戏切场景时会把这类对象也清掉（ModKit 的 Host 同理），
                    // 要加 HideAndDontSave。只设在根节点上，不动模板本身（免得生成的特效也带上这个标记、切场景不清理）
                    templateRoot = new GameObject("DBModKit.EffectTemplates") { hideFlags = HideFlags.HideAndDontSave };
                    templateRoot.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(templateRoot);
                }
                template.transform.SetParent(templateRoot.transform, false);
            }
            effects[name] = template;
            Safe.Run("清理特效对象池", () => GameRaw.ForgetPooledEffect(name));
            return name;
        }

        internal static void UnregisterEffect(string name)
        {
            if (!effects.TryGetValue(name, out var template)) return;
            effects.Remove(name);
            // 对象池里缓存了模板和已生成的实例，要一起清掉，否则下次会拿到已销毁的模板
            Safe.Run("清理特效对象池", () => GameRaw.ForgetPooledEffect(name));
            if (template != null && !BundlePrefabs.Contains(template)) UnityEngine.Object.Destroy(template);
        }

        internal static bool IsOurs(string path) => path != null && path.IndexOf(Prefix, StringComparison.Ordinal) >= 0;

        /// <summary>按游戏的加载路径找图标（路径里 "DBMod/" 起的部分是名字）。</summary>
        internal static Sprite IconFor(string path)
        {
            int i = path?.IndexOf(Prefix, StringComparison.Ordinal) ?? -1;
            return i >= 0 && icons.TryGetValue(path.Substring(i), out var sprite) ? sprite : null;
        }

        internal static GameObject EffectFor(string path)
        {
            int i = path?.IndexOf(Prefix, StringComparison.Ordinal) ?? -1;
            if (i < 0 || !effects.TryGetValue(path.Substring(i), out var template)) return null;
            if (template != null) return template;
            // 模板被销毁了（AssetBundle 被关掉、代码搭的对象被别人删了）：记一条说明原因的错误（ActiveSkill.Effect 会先检查，不会走到这里）
            Safe.Report("Mod 特效 " + path.Substring(i), new InvalidOperationException("特效模板已被销毁，请重新 RegisterEffect"));
            return null;
        }

        /// <summary>读 PNG/JPG 文件做成图标（Sprite）。</summary>
        public static Sprite LoadSprite(string file)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(file)))
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidDataException($"不是能读的图片：{file}");
            }
            texture.name = Path.GetFileNameWithoutExtension(file);
            return ToSprite(texture);
        }

        internal static Sprite ToSprite(Texture2D texture)
        {
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            return sprite;
        }
    }
}
