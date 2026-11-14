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
        public static readonly string[] TestedVersions = { "7.19" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["nz"] = new[] { "bvno", "bfdx", "bfdz", "bffb", "bfes", "bfen", "bfff", "ggf", "ggy", "MaxSkillNum", "MaxEquipNum" },
            ["pr"] = new[] { "gwf", "bgkc", "bgkd" },
            ["ck"] = new[] { "bviv", "bvjc", "bvjd", "bvjf", "bvjh", "bvjk", "bvjn" },
            ["ff"] = new[] { "ekw", "ekx", "eln", "eil", "eof", "eoi", "emf", "elg", "eli", "eod", "eol", "emr", "eks", "ekp", "eku", "elo", "eop", "eni", "eom" },
            ["biv"] = new[] { "oiu" },
            ["qr"] = new[] { "bgpq", "bgps", "bgpt", "bgqd", "hhn" },
            ["bio"] = new[] { "bloc", "blod", "blof", "blob", "blog", "UpdateSkill", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["sd"] = new[] { "hms", "hmt" },
            ["kp"] = new[] { "benq", "benr", "bens", "bent", "benu", "Init", "Clear", "fke", "fku", "fkv" },
            ["ma"] = new string[0],
            ["te"] = new[] { "bgzt", "bgzv", "bgzz", "bhaa", "bhac", "hrd", "hre", "hrf", "hrh", "bgzq", "bgzr", "bgzu", "bhab", "hsm", "hsn" },
            ["hw"] = new[] { "becc" },
            ["kj"] = new[] { "bejb", "fik", "beiy", "bejm", "fgl", "fgt", "Init", "k9c40390c8126488da1f314b1038ec0f7", "beiz", "bekd", "kea53024f293c4524856315937baf32a1", "kf35d4832a7394c8d9ecbc59746c95b90", "k8d1b3b4943b449b4986395a3154b49eb" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["pb"] = new[] { "bged", "glg" },
            ["pc"] = new[] { "bgeg", "bgeh", "bgej", "bgek", "bgel", "bgem" },
            ["oi"] = new[] { "bfwr" },
            ["bgl"] = new[] { "bkxr" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["cfd"] = new[] { "tkf", "tkp" },
            ["cfb"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["zo"] = new[]
            {
                "hp", "maxHp", "authorityId", "jwc", "jwj", "localRoleBuffDic", "jzi", "jyh", "jvc", "bvvc", "bvvf", "bvuh", "bwxh", "roleName", "bvue", "bikr", "jvd", "bvvd", "roleType",
                "henShinSkillId", "timer", "jyc", "jyd", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bvve", "bvuv", "bvuu", "bvut", "bvug", "bvuq", "bvur", "bvup", "bvuw", "bvva", "bvud", "jwr", "jvz",
                "localActiveSkillCastEvent", "jqr", "jrn", "roleBuffManager", "jwc",
                "juu", "jut", "jxt", "jze", "jyg", "jye", "jvb", "jva", "jve", "overrideAnimSkillId",
            },
            ["beh"] = new[]
            {
                "heroType", "mof", "bwcl", "bkbu", "bkbv", "loj", "AttackUpdate", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "mff", "k0cacca12211e4c7a9a7ce24f98338480", "kf4752dd3ce344e09aac619153affdfcb",
                "mdk", "lxi", "CmdPickItem", "playerAttribute", "roleSkillList", "mge", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "mdl", "mdn", "CmdAddBuff", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "jpy", "jwd", "CmdCreateSkill", "mdo", "mdm",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "svt", "bwny" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["pt"] = new[] { "bgks", "gza" },
            ["pu"] = new[] { "bgkx", "bgky", "bgkz", "bglb" },
            ["bea"] = new[] { "lti", "bjwf", "bjwg", "bjwj", "ltr", "kc78d4235a0b14993b40c8fe280410a02", "k77f68316a9124bd4ad2f0a0965bc5ce0", "k3a5cf9879aca44758535444122a969b7" },
            ["AudioManager"] = new[] { "dlw" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["bvz"] = new[] { "qwi", "bdli" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["bhn"] = new[] { "oab" },
            ["cty"] = new[] { "btjh", "btja" },
            ["ctl"] = new[] { "bwxl", "btgj" },
            ["zb"] = new[] { "isBoss", "isElite" },
            ["pg"] = new[] { "bgfm", "bgfk", "bgfi", "bgfj", "gqv", "gqt", "gqd", "bgfl", "bgfz" },
            ["od"] = new[] { "bfol" },
            ["cy"] = new[] { "bdgj" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["dz"] = new[] { "eca", "bdlq" },
            ["cm"] = new[] { "dqd", "dqe", "bdcm" },
            // 自定义被动
            ["bdo"] = new[] { "lnn" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["ok"] = new[] { "bfxa" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["qo"] = new[] { "hea", "hee" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["o"] = new[] { "djn", "bcwq", "bcwr" },
            // 施法动作
            ["oe"] = new[] { "bfpi" },
            // 自定义主动技能
            ["bip"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "btik" },
            ["blh"] = new[] { "ooo" },
            // 嵌套类型 di.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["di"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["bva"] = new[] { "bole", "bwjt", "qqy", "qqs" },
            ["bvb"] = new[] { "bomv", "bomx", "bomu", "qth", "bomw" },
            ["blg"] = new[]
            {
                "bmgi", "bmhc", "bmgz", "bmgk", "bmgh", "bwhb", "k6b39e0cb281545478858ba9afb5e615b", "k5c301c768cb8448188afc09a7aa7d93e",
                "ke602b58eb75347ceb956cb64e9007c9a", "k8c53e4d939504d8fa48f67e1729bf46f", "k0d55525c89b44bdfa4f12b119f48dfb7",
                "k1d4b25d9f4df4120bb217adddb5b9f80", "k5e3b1b0371ab4b51ab13600d55234919", "bmgn", "bmgt", "bmgu", "bmhj",
                "k3f83f8d645c84b9b91d135b0306628f0", "hna", "ook", "bmgj",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["bid"] = new[] { "oel" },
            ["Buff无敌"] = new string[0],
            ["xi"] = new[] { "hwp", "hwr", "bvpq" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["bqd"] = new[] { "bdli" },
            ["bpw"] = new[] { "pox" },
            ["PlayerDamageFeedbackKind"] = new string[0],
            ["RoleModeBase"] = new[] { "addRange", "jbn", "myAnim" },
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
            ["cch"] = new[] { "sjm", "sje", "sjf", "siz", "six", "skt" },
            ["ccn"] = new[] { "slp" },
            ["ccs"] = new[] { "sni", "snj", "bwmu", "snd" },
            ["ccm"] = new[] { "bqdf" },
            ["cck"] = new[] { "sla" },
            ["bkg"] = new[] { "olw" },
            ["bex"] = new[] { "niw" },
            ["ccp"] = new[] { "smc" },
            // 皮肤同步：Steam 大厅成员数据
            ["qy"] = new[] { "bvpb" },
            ["ca"] = new[] { "dor" },
            ["cqu"] = new[] { "wyx", "wyw", "wyp", "wyq", "wzh" },
            ["crj"] = new[] { "xnd" },
            ["WorkshopItemStatus"] = new[] { "publishedFileId", "installed", "heroId", "installFolder" },
            ["LocalWorkshopManifest"] = new[] { "heroId", "title", "heroName", "heroIcon", "author", "contentType", "bundleRelativePath", "bundleAssetName" },
        };

        // 卡牌保护单独检查。它失败时仅停用卡牌，其他 Mod 继续使用原来的兼容检查。
        private static readonly (string type, string[] members)[] CardRequired =
        {
            ("di", new[] { "bdij", "bdim", "bvjz", "bvka", "bdjh", "dwj", "dwy", "dxg", "dxf", "dwh", "dwx" }),
            ("di+GameSaveData", new[] { "maxCapacity", "cardCapacityProgressionVersion", "equipCards", "equipCardPresets", "currentEquipCardPresetIndex", "haveCardDataDic" }),
            ("di+EquipCardPresetData", new[] { "presetName", "equipCards", "isSaved" }),
            ("bat", new[] { "kuy", "kvg", "kuz", "bjbx" }),
            ("bat+HaveCardData", new[] { "cardId", "haveNum", "curProgress" }),
            ("btr", new[] { "qju", "qjv", "qjo", "qjp", "qkb", "qja", "bdli" }),
            ("pg", new[] { "bgfq" }),
            ("CardData", new[] { "capacity", "progress" }),
        };

        // 独立检查失败仅停用自动锻造；反射签名由安装过程再次核验。
        public static List<string> FindForgingMissing()
        {
            var required = new (string type, string[] members)[] {
                ("qo", new[] { "hgc" }), ("qo+ForgingTier", new[] { "Normal", "Gold" }),
                ("bvz", new[] { "qwi", "qws", "boso", "bdli" }),
                ("RoguelikeUIData", new[] { "name", "quality", "data" }),
                ("ck", new[] { "bvjf" }), ("pg", new[] { "bgfo" }), ("nz", new[] { "bvno" }),
                ("ForgingData", new[] { "type", "isPercent" }),
            };
            var missing = new List<string>();
            foreach (var item in required) {
                var type = AccessTools.TypeByName(item.type);
                if (type == null) missing.Add(item.type);
                else foreach (string member in item.members) if (!HasMember(type, member)) missing.Add(item.type + "." + member);
            }
            return missing;
        }

        public static List<string> FindCardMissing()
        {
            var missing = new List<string>();
            foreach (var item in CardRequired)
            {
                var type = AccessTools.TypeByName(item.type);
                if (type == null) missing.Add(item.type);
                else foreach (string member in item.members)
                    if (!HasMember(type, member)) missing.Add(item.type + "." + member);
            }
            return missing;
        }

        public static string GameVersion => Application.version;

        public static bool IsTestedVersion => TestedVersions.Contains(GameVersion);

        // 这一版适配的游戏版本，给玩家看（游戏作者会放出历史版本，玩家可以把游戏固定在这个版本）
        public static string TargetVersion => string.Join("、", TestedVersions);

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
            var ez = AccessTools.TypeByName("ff");
            if (ez != null)
            {
                CheckSignature(ez, "ekw", new[] { "ActiveSkillEnum", "beh", "Int32" }, "Boolean", missing);
                CheckSignature(ez, "ekx", new[] { "beh", "ActiveSkillData" }, "zo", missing);
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
