# 用 AssetBundle 做技能的图标、特效和动作

只要图标的话不需要 AssetBundle：`mod.LoadIcon("key", "assets/icon.png")` 直接读图片。
特效预制体和动作片段要从 Unity 工程打成 AssetBundle。完整的代码示例是 [samples/BundleSkill](../samples/BundleSkill)。

## 准备 Unity 工程

- **版本**：Unity **2022.3.62f1c1**（创意工坊的英雄模型 manifest 里写的就是这个，游戏用的是同一个 2022.3.62）。
  国际版 2022.3.62f1 也可以，但不要用其他小版本：AssetBundle 跨版本加载容易失败。游戏里可以用 `Application.unityVersion` 确认。
- **模板**：选 **3D (URP)**。游戏用的是 URP（Universal Render Pipeline），材质要用 URP 的 Shader
  （粒子用 `Universal Render Pipeline/Particles/Unlit` 或 `Lit`），用内置管线的 Shader 在游戏里会变成粉色。
- **打包脚本**：把 [templates/AssetBundle/Assets/Editor/DBModBundles.cs](../templates/AssetBundle/Assets/Editor/DBModBundles.cs) 复制到工程的 `Assets/Editor/`。

## 放资源

```text
Assets/
├── Editor/DBModBundles.cs
└── DBMod/
    └── meteor/                 # 一个子文件夹打一个包：meteor.bundle
        ├── Icons/
        │   └── meteor_icon.png # 自动设成 Sprite、不生成 Mipmap，最大 256
        ├── Animations/
        │   └── meteor_cast.fbx # 自动设成 Humanoid
        └── meteor_fx.prefab    # 特效预制体
```

- **图标**：正方形，128×128 左右，透明背景。代码里 `bundle.Sprite("meteor_icon")`，再 `mod.RegisterIcon(...)`。
- **动作**：FBX 的 Rig 必须是 **Humanoid**（脚本会自动设置），英雄模型都是 Humanoid，这样一个片段所有英雄都能用。
  片段名用 `bundle.Clip("meteor_cast")` 读，在技能里 `PlayAnimation(clip, 速度, 时长)`。片段不用设 Loop，持续时间由技能决定。
- **特效**：预制体根节点放在原点，粒子系统的 `Stop Action` 不用设，游戏的对象池到时间会回收。
  代码里 `mod.RegisterEffect("meteor_fx", bundle.Prefab("meteor_fx"))` 得到一个路径，`Effect(路径, 位置, 秒数, 缩放)` 生成。
  脚本不能放进预制体（AssetBundle 不带代码），只用 Unity 自带的组件（粒子、灯光、Animator 等）。

## 打包

菜单 **DBMod → Build AssetBundles**，产物在工程根目录 `Build/meteor.bundle`。复制到 Mod 项目的 `assets/` 下，
编译时会跟着 dll 部署（热重载时在 `BepInEx/scripts/<程序集名>/assets/`），`pack.ps1` 也会打进发布包和工坊条目。

```csharp
var bundle = mod.LoadBundle("assets/meteor.bundle");   // 卸载时自动关包
foreach (var name in bundle.AssetNames) mod.RunLog.Debug("资源", name);   // 不知道名字时先列出来
```

## 常见问题

| 现象 | 原因 |
|---|---|
| `LoadBundle` 报"打不开 AssetBundle" | Unity 版本不对、BuildTarget 不是 StandaloneWindows64，或文件路径不对 |
| 特效是粉色的 | 材质用了内置管线的 Shader，换成 URP 的 |
| 动作不动或姿势扭曲 | FBX 不是 Humanoid，或 Avatar 没配好（Rig → Configure 检查骨骼映射） |
| 图标是白块 | 图片没设成 Sprite（放在 Icons/ 下会自动设置），或 `bundle.Sprite` 的名字写错 |
| `Prefab` / `Clip` 返回 null | 名字写错（可以只写文件名、不带扩展名），用 `AssetNames` 列出来看 |

## 英雄模型

整套英雄模型（皮肤）不走这里，用游戏自带的工坊模型格式（`manifest.json` + 模型包），
变体英雄用 `HeroVariantDef.ModelFolder` / `WorkshopModel` 引用，例子见 [samples/Frieren](../samples/Frieren)。
