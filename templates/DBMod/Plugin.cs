using BepInEx;
using BepInEx.Configuration;
using DBModKit;
//#if (skills)
using UnityEngine;
//#endif

namespace DBModTemplate
{
    // MOD_DESCRIPTION
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, ModKit.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "plge0n.dungeonbrawl.MOD_GUID_LOWER";
        public const string Name = "DBModTemplate";
        // 发布时同步修改 CHANGELOG.md；pack.ps1 从这里读取版本号
        public const string Version = "0.1.0";

        internal static ConfigEntry<bool> Enabled;

        // 必须直接继承 BaseUnityPlugin（不能继承 ModKit 里的类），否则 ScriptEngine 热重载会出错
        private ModContext mod;

        private void Awake() => mod = ModContext.Start(this, Logger, OnLoad, OnUnload);

        private void OnDestroy() => mod?.Dispose();

        // 通过 mod.OnUpdate / OnBattleStarted / UseAutoCast / LoadJson 等注册的内容，卸载（热重载）时会自动清理
        private void OnLoad(ModContext mod)
        {
            Enabled = Config.Bind("General", "Enabled", true, "总开关");

//#if (skills)
            // 自定义技能：注册后进技能书三选一，卸载时自动注销。写法详见 docs/api/自定义技能.md 和 samples/
            mod.RegisterPassive(new PassiveSkillDef
            {
                Key = "example_passive",
                Name = "示例被动",
                Description = "力量 +{0}",
                Values = new[] { 20f },
                Create = () => new ExamplePassive(),
            });
            mod.RegisterSkill(new ActiveSkillDef
            {
                Key = "example_skill",
                Name = "示例技能",
                Description = "对范围内敌人造成 {0} 点伤害",
                Indicator = SkillIndicator.Range,
                Cost = 20,
                Cooldown = 8f,
                Range = 3f,
                DamageBase = 100,
                DamageValue = 1.5f,
                Create = () => new ExampleSkill(),
            });

//#endif
            mod.OnBattleStarted(() => Logger.LogInfo("进入战斗"));
            mod.OnUpdate(() =>
            {
                if (!Enabled.Value) return;
                var player = Game.LocalPlayer;
                if (player == null) return;
                // TODO
            });
        }

        // 自己创建、挂在游戏对象上的东西在这里销毁
        private void OnUnload()
        {
        }
    }
//#if (skills)

    // 被动：只在拥有者的客户端上运行，加的属性在失去时撤销
    internal sealed class ExamplePassive : PassiveSkill
    {
        private int added;

        protected override void OnAdded()
        {
            added = Mathf.RoundToInt(Value(0));
            Owner?.AddStats(added, 0, 0, 0);
        }

        protected override void OnRemoved() => Owner?.AddStats(-added, 0, 0, 0);
    }

    // 主动技能：每次释放一个实例；特效每个客户端都放，伤害只在施法者那边结算
    internal sealed class ExampleSkill : ActiveSkill
    {
        protected override void OnCast()
        {
            Effect("Effect/SpellThunder", Position, 1.5f);
            DamageArea(Position, Radius);
        }
    }
//#endif
}
