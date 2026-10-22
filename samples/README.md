# 示例 Mod

完整、能编译的 Mod，演示 DBModKit 的用法。不在解决方案里，不打包，编译时默认不部署进游戏。

| 示例 | 演示什么 |
|---|---|
| [SkillSample](SkillSample) | 自定义被动（饮血之刃：加力量、命中回血）、主动技能（雷暴：范围伤害、持续伤害、特效、施法动作）、遗物（铁壁徽章：护甲，升级补差值）和装备（给 2 号装备挂特效"静电"、新神器"风暴法杖"）。和 [templates/ContentPack](../templates/ContentPack) 是同一套技能，可以对照 JSON 的写法 |
| [Frieren](Frieren) | 变体英雄"芙莉莲"：基于精灵魔法使，专属被动"无限魔力"，开局自带直线穿透技能"杀人魔法"，模型用创意工坊的芙莉莲皮肤（3798456516，要自己订阅） |
| [BundleSkill](BundleSkill) | 图标、特效预制体、施法动作都来自 Mod 自己的 AssetBundle（"陨石术"）。仓库不带 bundle 文件，做法见 [docs/AssetBundle.md](../docs/AssetBundle.md) |

```powershell
dotnet build samples/Frieren -c Release                         # 只编译
dotnet build samples/Frieren -c Release -p:DeployToGame=true    # 编译并部署进游戏试玩
```

部署前注意：几个工作目录共用一个游戏目录，部署前检查规则见根目录 README 的"部署检查"。
接口说明见 [docs/api](../docs/api/README.md)。
