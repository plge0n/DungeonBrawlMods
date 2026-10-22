using BepInEx;
using DBModKit;
using UnityEngine;

namespace SkillSample
{
    // 示例：一个自定义被动（饮血之刃）和一个自定义主动技能（雷暴）。
    // 和 templates/ContentPack 里的内容包是同一套技能，对照着看能知道 JSON 的每个字段对应什么。
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, "0.21.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "example.dungeonbrawl.skillsample";
        public const string Name = "SkillSample";
        public const string Version = "1.0.0";

        private ModContext mod;

        private void Awake() => mod = ModContext.Start(this, Logger, OnLoad);

        private void OnDestroy() => mod?.Dispose();

        private void OnLoad(ModContext mod)
        {
            // 注册后会出现在技能书三选一里。编号由 GUID 和 Key 算出，每次启动都一样；
            // 卸载（热重载）时自动注销，本地玩家身上的也会被移除
            mod.RegisterPassive(new PassiveSkillDef
            {
                Key = "blade",
                Name = "饮血之刃",
                Description = "力量 +{0}；命中时回复伤害 {1}% 的生命",
                Quality = PassiveQuality.C,
                Values = new[] { 20f, 5f },
                LevelUp = new[] { 10f, 1f },
                Tags = new[] { SkillTags.Buff, SkillTags.Heal },
                Create = () => new BloodBlade(),
            });

            mod.RegisterSkill(new ActiveSkillDef
            {
                Key = "nova",
                Name = "雷暴",
                // 没有 Values 时 {0} 是游戏按伤害公式生成的文字（"100+攻击力×1.5"）
                Description = "对范围内敌人造成 {0} 点伤害，之后 2 秒内每 0.5 秒再造成一次",
                Quality = PassiveQuality.B,
                Indicator = SkillIndicator.Range,
                Cost = 20,
                Cooldown = 8f,
                CastingRange = 8f,
                Range = 3f,
                Duration = 2f,
                Interval = 0.5f,
                DamageBase = 100,
                DamageValue = 1.5f,
                Tags = new[] { SkillTags.Area },
                Create = () => new ThunderNova(),
            });
        }
    }

    // 被动：每个技能格一个实例，只在拥有者（本地玩家）的客户端上运行
    internal sealed class BloodBlade : PassiveSkill
    {
        private int addedStrength;
        private float nextHeal;

        protected override void OnAdded()
        {
            // 加属性走游戏自己的方法，失去时要自己撤销（和游戏的被动一样）
            addedStrength = Mathf.RoundToInt(Value(0));
            Owner?.AddStats(addedStrength, 0, 0, 0);
        }

        protected override void OnRemoved()
        {
            Owner?.AddStats(-addedStrength, 0, 0, 0);
            addedStrength = 0;
        }

        protected override void OnHit(HitInfo hit)
        {
            // 命中很频繁，加一点内部冷却；回血会同步给服务器
            if (hit.Damage <= 0 || Time.time < nextHeal) return;
            nextHeal = Time.time + 0.2f;
            Owner?.Heal(hit.Damage * Value(1) / 100f);
        }
    }

    // 主动技能：每次释放一个实例。联机时每个装了这个 Mod 的客户端都会创建（大家都能看到特效），
    // 但伤害只在施法者的客户端结算（DamageArea 在别人的客户端上什么都不做）
    internal sealed class ThunderNova : ActiveSkill
    {
        private float tick;

        protected override void OnCast()
        {
            Effect("Effect/SpellThunder", Position, 1.5f);
            PlayAnimation("Frank_RPG_2Hand_Skill01_WhirlWind", 1f, 2f);
            DamageArea(Position, Radius);
        }

        protected override void OnUpdate(float deltaTime)
        {
            tick += deltaTime;
            if (tick < 0.5f) return;
            tick -= 0.5f;
            DamageArea(Position, Radius, FormulaDamage / 4);
        }
    }
}
