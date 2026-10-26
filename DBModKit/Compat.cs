using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DBModKit
{
    // 游戏每次更新都可能重新混淆，混淆名全部改变。启动时先用字符串+反射确认 GameRaw / Hooks 用到的成员都在，
    // 对不上就整体停用，避免补丁打到错误的方法上。这里不能直接引用游戏类型，否则类型改名时 JIT 阶段就会崩。
    internal static class Compat
    {
        // 测试过的游戏版本（Application.version）
        public static readonly string[] TestedVersions = { "7.06" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["ns"] = new[] { "bqjh", "bclj", "bcll", "bcmn", "bcme", "bclz", "bcmr", "fug", "fuy", "MaxSkillNum", "MaxEquipNum" },
            ["pa"] = new[] { "gfv", "bdpa", "bdpb" },
            ["cj"] = new[] { "bqfi", "bqfp", "bqfq", "bqfs", "bqfu", "bqfx", "bqga" },
            ["ez"] = new[] { "dzv", "dzw", "eai", "dxk", "ecz", "edc", "eba", "eac", "ead", "ecx", "edf", "ebm", "dzr", "dzo", "dzt", "eaj", "edj", "ecc", "edg" },
            ["bgg"] = new[] { "mzq" },
            ["pu"] = new[] { "bdsy", "bdsz", "bdta", "bdtk", "gpo" },
            ["bfz"] = new[] { "bhxn", "bhxo", "bhxq", "bhxm", "bhxr", "kny", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["qx"] = new[] { "gtw", "gtx" },
            ["ki"] = new[] { "bbvc", "bbvd", "bbve", "bbvf", "bbvg", "Init", "Clear", "eyf", "eyv", "eyw" },
            ["ls"] = new string[0],
            ["sa"] = new[] { "bech", "becj", "becn", "beco", "becq", "gyd", "gye", "gyf", "gyh" },
            ["ho"] = new[] { "bbjo" },
            ["kc"] = new[] { "bbqn", "ewl", "bbqk", "bbqy", "eum", "euu", "Init", "kd30f5b3c3ab244cd88213faa2597f817" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["or"] = new[] { "bdly", "fzk" },
            ["os"] = new[] { "bdmb", "bdmc", "bdme", "bdmf", "bdmg", "bdmh" },
            ["ob"] = new[] { "bdfa" },
            ["bdx"] = new[] { "bhhc" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["byr"] = new[] { "rmb", "rml" },
            ["byp"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["ym"] = new[]
            {
                "hp", "maxHp", "authorityId", "jdf", "jdm", "localRoleBuffDic", "jgg", "jfi", "jce", "bqqa", "bqqd", "bqpf", "brla", "roleName", "bqpc", "bfmx", "jcf", "bqqb", "roleType",
                "henShinSkillId", "timer", "jfd", "jfe", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bqqc", "bqpt", "bqps", "bqpr", "bqpe", "bqpo", "bqpp", "bqpn", "bqpu", "bqpy", "bqpb", "jdu", "jdc",
                "localActiveSkillCastEvent", "ixu", "iyq", "roleBuffManager", "jdf",
                "jbw", "jbv", "jeu", "jge", "jfh", "jff", "jcd", "jcc", "jcg", "overrideAnimSkillId",
            },
            ["bby"] = new[]
            {
                "heroType", "lll", "bqvu", "bgsn", "bgso", "lat", "las", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "lcq", "k439a7df0628d41a58137326b09c996a1", "k33509bd61ca34e39a3b5106e4c4843bd",
                "lav", "kuu", "CmdPickItem", "playerAttribute", "roleSkillList", "ldp", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "law", "lay", "CmdAddBuff", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "ixb", "jdg", "CmdCreateSkill",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "qyr", "brdg" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["pb"] = new[] { "bdpj", "ghy" },
            ["pc"] = new[] { "bdpo", "bdpp", "bdpq", "bdps" },
            ["bbr"] = new[] { "kqx", "bgnb", "bgnc", "bgne", "krg", "kaefc39a55c154ac48b9e1ad427755ad0" },
            ["AudioManager"] = new[] { "dct" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["bru"] = new[] { "pij", "bauh" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["bez"] = new[] { "mqq" },
            ["ckv"] = new[] { "bofr", "bofk" },
            ["ckh"] = new[] { "brle", "bocs" },
            ["xz"] = new[] { "isBoss", "isElite" },
            ["ow"] = new[] { "bdnh", "bdnf", "bdnd", "bdne", "gev", "get", "gec", "bdng", "bdnu" },
            ["nw"] = new[] { "bcwu" },
            ["cx"] = new[] { "baqe" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["du"] = new[] { "dso", "bauu" },
            ["cl"] = new[] { "dhc", "dhd", "bamk" },
            // 自定义被动
            ["bbo"] = new[] { "kop" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["od"] = new[] { "bdfj" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["pt"] = new[] { "gmg", "gmk" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["m"] = new[] { "dak", "bago", "bagp" },
            // 施法动作
            ["nx"] = new[] { "bcxr" },
            // 自定义主动技能
            ["bga"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "boeu" },
            ["biq"] = new[] { "ngm" },
            // 嵌套类型 de.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["de"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["bqw"] = new[] { "bkjz", "braq", "pck" },
            ["bqx"] = new[] { "bklr", "bklt", "bklq", "pey", "bkls" },
            ["bip"] = new[]
            {
                "biol", "bipf", "bipc", "bion", "biok", "bqzc", "kb53ba154d6ba48c195044736e0c28b22", "k0294f73bde8347108c24b89307064702",
                "k607574168ca94c468449c7d79299b775", "k5ec29f1d06d24a12a8cc3bba3ebe5622", "kcf972aa658b8450b91e741042d238597",
                "keed1a8dbb9a94cc986cc199fc704a0fc", "k2fbdf1b966004215bb256771c5f0dcdd", "bioq", "biow", "biox", "bipm",
                "kbeb3e4beb92e40468f1efbc75a4871e4", "gue", "ngi",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["bfp"] = new[] { "mvd" },
            ["Buff无敌"] = new string[0],
            ["wj"] = new[] { "hdm", "hdo", "bqkp" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["blz"] = new[] { "bauh" },
            ["bls"] = new[] { "nyr" },
            ["PlayerDamageFeedbackKind"] = new string[0],
            ["RoleModeBase"] = new[] { "addRange", "iim", "myAnim" },
            ["ActiveSkillData"] = new[]
            {
                "tags", "indicator", "castingRange", "range", "cd", "canAuto", "icon",
                "damageBase", "damageValue", "cost", "duration", "interval", "chargingNum", "chargingCd",
            },
            ["ActiveSkillEnum"] = new[]
            {
                "None", "D_Execution", "S_Execution", "D_Alchemy", "S_Alchemy", "Bite", "ChickenDance", "RealChickenDance",
                "C_SpellThunder", "B_SwordMove", "A_SpellThunder",
            },
            ["SkillTag"] = new[] { "Heal", "Revive", "Shield", "Defense", "Buff", "Summon", "Aura", "Area", "Move", "Control", "Henshin", "Duration" },
            // 英雄皮肤
            ["bwd"] = new[] { "qno", "qng", "qnh", "qnb", "qmz", "qov" },
            ["bwj"] = new[] { "qpr" },
            ["bwo"] = new[] { "qrk", "qrl", "brce", "qrf" },
            ["bwi"] = new[] { "blqu" },
            ["bwg"] = new[] { "qpc" },
            ["bho"] = new[] { "ndg" },
            ["bck"] = new[] { "mak" },
            ["bwl"] = new[] { "qqe" },
            // 皮肤同步：Steam 大厅成员数据
            ["pz"] = new[] { "bqkg" },
            ["bz"] = new[] { "dfq" },
            ["chq"] = new[] { "uih", "uig", "uhz", "uia", "uir" },
            ["cif"] = new[] { "uwr" },
            ["WorkshopItemStatus"] = new[] { "publishedFileId", "installed", "heroId", "installFolder" },
            ["LocalWorkshopManifest"] = new[] { "heroId", "title", "heroName", "heroIcon", "author", "contentType", "bundleRelativePath", "bundleAssetName" },
        };

        public static string GameVersion => Application.version;

        public static bool IsTestedVersion => TestedVersions.Contains(GameVersion);

        // 返回缺失项；为空表示兼容
        public static List<string> FindMissing()
        {
            var missing = new List<string>();
            foreach (var kv in Required)
            {
                var type = AccessTools.TypeByName(kv.Key);
                if (type == null)
                {
                    missing.Add(kv.Key);
                    continue;
                }
                foreach (var member in kv.Value)
                    if (!HasMember(type, member))
                        missing.Add($"{kv.Key}.{member}");
            }

            // 方法签名也要对上：dxx(ActiveSkillEnum, bbe, int) / dxy(bbe, ActiveSkillData)
            var ez = AccessTools.TypeByName("ez");
            if (ez != null)
            {
                CheckSignature(ez, "dzv", new[] { "ActiveSkillEnum", "bby", "Int32" }, "Boolean", missing);
                CheckSignature(ez, "dzw", new[] { "bby", "ActiveSkillData" }, "ym", missing);
            }
            return missing;
        }

        private static bool HasMember(Type type, string name)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            for (var t = type; t != null; t = t.BaseType)
                if (t.GetMember(name, all).Length > 0)
                    return true;
            return false;
        }

        private static void CheckSignature(Type type, string name, string[] parameters, string returns, List<string> missing)
        {
            var ok = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Any(m => m.Name == name
                    && m.ReturnType.Name == returns
                    && m.GetParameters().Select(p => p.ParameterType.Name).SequenceEqual(parameters));
            if (!ok) missing.Add($"ez.{name}({string.Join(", ", parameters)}) 签名");
        }
    }
}
