using System.IO;
using BepInEx;
using DBModKit;
using UnityEngine;

namespace BundleSkill
{
    // 示例：图标、特效、施法动作都来自 Mod 自己的 AssetBundle 的主动技能"陨石术"。
    // AssetBundle 用 Unity 2022.3.62f1 打包，做法见 docs/AssetBundle.md 和 templates/AssetBundle/。
    // 这个示例要求 assets/meteor.bundle 里有：
    //   meteor_icon（Sprite 或 Texture2D，技能图标）
    //   meteor_fx  （预制体，落地特效）
    //   meteor_cast（Humanoid 动作片段，施法动作）
    // 仓库里不带这个文件；没有时只记一条警告，不注册技能。
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, "0.21.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "example.dungeonbrawl.bundleskill";
        public const string Name = "BundleSkill";
        public const string Version = "1.0.0";

        private ModContext mod;

        private void Awake() => mod = ModContext.Start(this, Logger, OnLoad);

        private void OnDestroy() => mod?.Dispose();

        private void OnLoad(ModContext mod)
        {
            // 项目里的 assets/ 编译时跟着 dll 部署，mod.Folder 就是部署后的插件文件夹
            if (mod.Folder == null || !File.Exists(Path.Combine(mod.Folder, "assets/meteor.bundle")))
            {
                mod.RunLog.Warning("资源", "没有 assets/meteor.bundle，陨石术不注册（打包方法见 docs/AssetBundle.md）");
                return;
            }

            // 卸载（热重载）时自动关包；注册的图标、特效先注销
            var bundle = mod.LoadBundle("assets/meteor.bundle");
            Meteor.EffectPath = mod.RegisterEffect("meteor_fx", bundle.Prefab("meteor_fx"));
            Meteor.Clip = bundle.Clip("meteor_cast");

            mod.RegisterSkill(new ActiveSkillDef
            {
                Key = "meteor",
                Name = "陨石术",
                Description = "召唤陨石砸向地面，对范围内敌人造成 {0} 点伤害",
                Icon = mod.RegisterIcon("meteor", bundle.Sprite("meteor_icon")),
                Quality = PassiveQuality.A,
                Indicator = SkillIndicator.Range,
                Cost = 40,
                Cooldown = 10f,
                CastingRange = 9f,
                Range = 3.5f,
                Duration = 1.2f,
                DamageBase = 300,
                DamageValue = 3f,
                Tags = new[] { SkillTags.Area },
                Create = () => new Meteor(),
            });
        }
    }

    internal sealed class Meteor : ActiveSkill
    {
        internal static string EffectPath;
        internal static AnimationClip Clip;

        private bool landed;

        protected override void OnCast()
        {
            // 片段是 Humanoid 的话所有英雄模型都能用；duration 不写时按片段长度延长技能
            if (Clip != null) PlayAnimation(Clip, 1f, Info.Def.Duration);
        }

        // 动作做到一半时陨石落地
        protected override void OnUpdate(float deltaTime)
        {
            if (landed || Remaining > Info.Def.Duration / 2f) return;
            landed = true;
            Effect(EffectPath, Position, 2f, Radius / 3.5f);
            DamageArea(Position, Radius);
        }
    }
}
