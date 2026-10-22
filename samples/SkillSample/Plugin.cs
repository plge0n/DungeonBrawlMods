using BepInEx;
using DBModKit;
using UnityEngine;

namespace SkillSample
{
    // 示例：一个自定义被动（饮血之刃）、一个自定义主动技能（雷暴）、一个自定义遗物（铁壁徽章），
    // 以及装备：给游戏的 2 号装备挂一个特效（静电），再加一件新的神器（风暴法杖，带自己的特效）。
    // 和 templates/ContentPack 里的内容包是同一套技能，对照着看能知道 JSON 的每个字段对应什么。
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, "0.24.0")]
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

            // 遗物：出现在遗物三选一里（按品级），显示在遗物栏。{0} 由游戏换成数值（含等级成长）
            mod.RegisterRelic(new RelicDef
            {
                Key = "iron_badge",
                Name = "铁壁徽章",
                Description = "护甲 +{0}",
                Icon = "remain_armor",
                Quality = PassiveQuality.B,
                Values = new[] { 15f },
                LevelUp = new[] { 10f },
                Create = () => new IronBadge(),
            });

            // 装备特效：AttachTo 写游戏装备编号，商店买到这件装备时特效一起生效，说明里也会多一行
            mod.RegisterEquipEffect(new EquipEffectDef
            {
                Key = "static_edge",
                Name = "静电",
                Description = "命中时回复 {0} 点法力",
                Values = new[] { 3f },
                AttachTo = new[] { 2 },
                Create = () => new StaticEdge(),
            });

            // 新装备：属性键名用 CustomEquipment.AttributeNames 里的（游戏的 jy.bayh），特效可以是
            // 自己注册的，也可以写游戏已有的特效名（GameEffects，数值照抄游戏里第一件带它的装备）。
            // Artifact = true 是神器；新装备不进商店，用 CustomEquipment.Give 发给本地玩家
            var charge = mod.RegisterEquipEffect(new EquipEffectDef
            {
                Key = "storm_charge",
                Name = "风暴充能",
                Description = "击杀敌人回复 {0} 点生命",
                Values = new[] { 30f },
                Create = () => new StormCharge(),
            });
            mod.RegisterEquipment(new EquipmentDef
            {
                Key = "storm_staff",
                Name = "风暴法杖",
                Description = "雷霆骑士的旧武器",
                Icon = "equip_2",
                Attributes = { ["attack"] = 60, ["hp"] = 200 },
                Effects = { charge },
                Artifact = true,
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

    // 遗物：每获得一次一个实例，只在本地玩家身上运行。升级时补上差值
    internal sealed class IronBadge : RelicBehaviour
    {
        private int added;

        protected override void OnAdded() => SetArmor(Mathf.RoundToInt(Value(0)));

        protected override void OnLevelChanged(int oldLevel, int newLevel) => SetArmor(Mathf.RoundToInt(ValueAt(0, newLevel)));

        protected override void OnRemoved() => SetArmor(0);

        private void SetArmor(int armor)
        {
            Owner?.AddArmor(armor - added);
            added = armor;
        }
    }

    // 装备特效：每件装备一个实例，只在本地玩家身上运行。卖掉装备时 OnRemoved
    internal sealed class StaticEdge : EquipEffectBehaviour
    {
        private float next;

        protected override void OnHit(HitInfo hit)
        {
            if (Time.time < next) return;
            next = Time.time + 0.5f;
            Owner?.AddMana(Mathf.RoundToInt(Value(0)));
        }
    }

    internal sealed class StormCharge : EquipEffectBehaviour
    {
        protected override void OnGameEvent(GameEvent e)
        {
            if (e.Kind == GameEventKind.EnemyKilled) Owner?.Heal(Value(0));
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
