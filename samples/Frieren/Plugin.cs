using BepInEx;
using DBModKit;
using UnityEngine;

namespace Frieren
{
    // 示例：变体英雄"芙莉莲"。游戏里没有这个英雄，所以用变体做：底层是精灵魔法使（20），
    // 专属技能换成被动"无限魔力"，再加一个主动技能"杀人魔法"（直线光束，穿透路上的敌人）进技能书。
    // 玩家在 Mod 设置 → DBModKit → 英雄变体里给精灵魔法使选"芙莉莲"。
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, "0.21.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "example.dungeonbrawl.frieren";
        public const string Name = "Frieren";
        public const string Version = "1.0.0";

        // 精灵魔法使（HeroType 20）
        private const int ElfMage = 20;

        // https://steamcommunity.com/sharedfiles/filedetails/?id=3798456516
        private const ulong FrierenSkin = 3798456516;

        private ModContext mod;

        private void Awake() => mod = ModContext.Start(this, Logger, OnLoad);

        private void OnDestroy() => mod?.Dispose();

        private void OnLoad(ModContext mod)
        {
            var infiniteMana = mod.RegisterPassive(new PassiveSkillDef
            {
                Key = "infinite_mana",
                Name = "无限魔力",
                Description = "法力永远是满的",
                Quality = PassiveQuality.S,
                // 只给变体当专属技能，不进技能书
                InSkillBook = false,
                Create = () => new InfiniteMana(),
            });

            var zoltraak = mod.RegisterSkill(new ActiveSkillDef
            {
                Key = "zoltraak",
                Name = "杀人魔法",
                Description = "朝一个方向发射魔法光束，对直线上的所有敌人造成 {0} 点伤害",
                Quality = PassiveQuality.A,
                // Vector：朝一个方向放，Position 是施法者脚下，Direction 是朝向
                Indicator = SkillIndicator.Vector,
                Cost = 30,
                Cooldown = 3f,
                // 光束长度
                CastingRange = 10f,
                // 光束半宽（会乘上玩家的技能范围加成，见 Radius）
                Range = 0.8f,
                DamageBase = 150,
                DamageValue = 2f,
                DamageType = 0,
                Tags = new[] { SkillTags.Area },
                Create = () => new Zoltraak(),
            });

            mod.RegisterHeroVariant(new HeroVariantDef
            {
                Key = "frieren",
                Name = "芙莉莲",
                Description = "活了一千多年的精灵魔法使：法力无限，开局自带杀人魔法",
                BaseHero = ElfMage,
                PassiveSkill = infiniteMana,
                // 模型用创意工坊的"精灵魔法使-芙莉莲皮肤"（作者 CRIN，manifest 的 heroId 是 20）。
                // 要玩家自己订阅；没订阅时变体照样能用，只是显示原版模型。模型只在自己这边显示
                WorkshopModel = FrierenSkin,
            });

            // 开局把杀人魔法放进技能栏。变体的属性和专属技能只在 Multiplayer 允许时生效，这里跟着同一个结果走
            mod.OnBattleStarted(() =>
            {
                var v = HeroVariants.LocalVariant;
                if (v == null || v.Owner != Guid || !HeroVariants.LocalGameplayActive) return;
                bool given = ActiveSkills.Give(zoltraak);
                mod.RunLog.Info("芙莉莲", given ? "发放杀人魔法" : "技能栏满了，没有发放杀人魔法");
            });
        }
    }

    // 无限魔力：蓝少了就补满。只在放技能扣蓝之后才会补，所以运行记录里每次放技能一条"加蓝"
    internal sealed class InfiniteMana : PassiveSkill
    {
        protected override void OnUpdate()
        {
            var me = Owner;
            if (me != null && me.Mana < me.MaxMana) me.AddMana(me.MaxMana - me.Mana);
        }
    }

    // 杀人魔法：沿朝向一条长 CastingRange、半宽 Radius 的直线，打中线上所有敌人（穿透）
    internal sealed class Zoltraak : ActiveSkill
    {
        protected override void OnCast()
        {
            float length = Info.Def.CastingRange;
            Vector3 start = Position, dir = Direction;

            // 表现：沿光束放一串特效，每个客户端都能看到
            for (float d = 1f; d <= length; d += 1.5f)
                Effect("Effect/FireFlash", start + dir * d, 0.6f, 0.8f);

            // 结算只在施法者自己的客户端（IsMine）；Damage 在别人的客户端上本来也不起作用
            var me = IsMine ? Game.LocalPlayer : null;
            if (me == null) return;
            foreach (var enemy in me.EnemiesWithin(length + 2f))
            {
                Vector3 offset = enemy.Position - start;
                offset.y = 0f;
                float along = Vector3.Dot(offset, dir);
                if (along < 0f || along > length) continue;
                float side = (offset - dir * along).magnitude;
                if (side <= Radius + enemy.HitRadius) Damage(enemy);
            }
        }
    }
}
