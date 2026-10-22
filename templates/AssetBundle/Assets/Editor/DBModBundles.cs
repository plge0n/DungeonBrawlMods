// 地下城竞技场 Mod 的 AssetBundle 打包脚本。放进 Unity 工程的 Assets/Editor/ 下。
// 工程要求：Unity 2022.3.62f1c1（游戏和工坊模型用的版本；国际版 2022.3.62f1 也行），URP 模板。
//
// 用法：
//   1. 把要打包的资源放进 Assets/DBMod/<包名>/，图标放 Icons/，动作 FBX 放 Animations/，特效预制体放任意位置
//   2. 菜单 DBMod → Build AssetBundles，产物在工程根目录的 Build/<包名>.bundle
//   3. 复制到 Mod 项目的 assets/ 下，代码里 mod.LoadBundle("assets/<包名>.bundle")
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DBModBundles
{
    private const string Root = "Assets/DBMod";
    private const string Output = "Build";

    [MenuItem("DBMod/Build AssetBundles")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Root))
        {
            EditorUtility.DisplayDialog("DBMod", $"没有 {Root} 文件夹：在里面给每个包建一个子文件夹", "好");
            return;
        }
        // 每个子文件夹一个包，包名就是文件夹名（小写）
        var builds = AssetDatabase.GetSubFolders(Root).Select(folder => new AssetBundleBuild
        {
            assetBundleName = Path.GetFileName(folder).ToLowerInvariant() + ".bundle",
            assetNames = AssetDatabase.FindAssets("", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .Distinct()
                .ToArray(),
        }).Where(b => b.assetNames.Length > 0).ToArray();
        if (builds.Length == 0)
        {
            EditorUtility.DisplayDialog("DBMod", $"{Root} 下没有资源", "好");
            return;
        }
        Directory.CreateDirectory(Output);
        // 游戏是 Windows 64 位；LZ4 压缩加载快
        BuildPipeline.BuildAssetBundles(Output, builds, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        EditorUtility.RevealInFinder(Output);
        Debug.Log($"DBMod：打包了 {builds.Length} 个 AssetBundle → {Path.GetFullPath(Output)}");
    }
}

// 导入设置：Icons/ 下的图片设成 Sprite，Animations/ 下的 FBX 设成 Humanoid（英雄模型都是 Humanoid，片段才能套到任何英雄身上）
public class DBModImport : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/DBMod/") || !assetPath.Contains("/Icons/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 256;
    }

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/DBMod/") || !assetPath.Contains("/Animations/")) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.importAnimation = true;
        importer.importMaterials = false;
    }
}
