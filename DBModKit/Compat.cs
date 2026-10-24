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
        public static readonly string[] TestedVersions = { "7.01" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["nr"] = new[] { "bpwv", "bcen", "bcep", "bcfr", "bcfi", "bcfd", "bcfv", "fsw", "fto", "MaxSkillNum", "MaxEquipNum" },
            ["oy"] = new[] { "gei", "bdhv", "bdhw" },
            ["cj"] = new[] { "bpsx", "bpte", "bptf", "bpth", "bptj", "bptm", "bptp" },
            ["ez"] = new[] { "dyp", "dyq", "dzc", "dwe", "ebt", "ebw", "dzu", "dyw", "dyx", "ebr", "ebz", "eag", "dyl", "dyi", "dyn", "dzd", "ecd", "eaw", "eca" },
            ["bfz"] = new[] { "mwh" },
            ["pr"] = new[] { "bdls", "bdlt", "bdlu", "bdme", "goa" },
            ["bfs"] = new[] { "bhph", "bhpi", "bhpk", "bhpg", "bhpl", "klv", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["qv"] = new[] { "gsi", "gsj" },
            ["kh"] = new[] { "bbog", "bboh", "bboi", "bboj", "bbok", "Init", "Clear", "ewv", "exl", "exm" },
            ["lq"] = new string[0],
            ["rt"] = new[] { "bduz", "bdvb", "bdvf", "bdvg", "bdvi", "gwp", "gwq", "gwr", "gwt" },
            ["ho"] = new[] { "bbct" },
            ["kb"] = new[] { "bbjr", "evb", "bbjo", "bbkc", "etc", "etk", "Init", "k58cbd18331e34716863f13584ff59167" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["op"] = new[] { "bdet", "fxz" },
            ["oq"] = new[] { "bdew", "bdex", "bdez", "bdfa", "bdfb", "bdfc" },
            ["oa"] = new[] { "bcyb" },
            ["bdq"] = new[] { "bgyw" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["bxl"] = new[] { "rfh", "rfr" },
            ["bxj"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["yh"] = new[]
            {
                "hp", "maxHp", "authorityId", "jbf", "jbm", "localRoleBuffDic", "jeg", "jdi", "jae", "bqdo", "bqdr", "bqct", "bqyc", "roleName", "bqcq", "bffk", "jaf", "bqdp", "roleType",
                "henShinSkillId", "timer", "jdd", "jde", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bqdq", "bqdh", "bqdg", "bqdf", "bqcs", "bqdc", "bqdd", "bqdb", "bqdi", "bqdm", "bqcp", "jbu", "jbc",
                "localActiveSkillCastEvent", "ivu", "iwq", "roleBuffManager", "jbf",
                "izw", "izv", "jcu", "jee", "jdh", "jdf", "jad", "jac", "jag", "overrideAnimSkillId",
            },
            ["bbr"] = new[]
            {
                "heroType", "lio", "bqje", "bgko", "bgkp", "kyh", "kyg", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "lac", "kce6d0aea2c124d1fb21977b6443b14bc", "k98b329accfd04307bcf31c49e30a82fe",
                "kyj", "ksj", "CmdPickItem", "playerAttribute", "roleSkillList", "lba", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "kyk", "kym", "CmdAddBuff", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "ivb", "jbg", "CmdCreateSkill",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "qsi", "bqqj" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["oz"] = new[] { "bdie", "ggk" },
            ["pa"] = new[] { "bdii", "bdij", "bdik", "bdim" },
            ["bbl"] = new[] { "koq", "bgfk", "bgfl", "bgfn", "koz", "ka54fd8703e094fc095f862c08dbcd563" },
            ["AudioManager"] = new[] { "dbn" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["bqq"] = new[] { "pca", "banm" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["bes"] = new[] { "mnh" },
            ["cjp"] = new[] { "bntf", "bnsy" },
            ["cjb"] = new[] { "bqyg", "bnqh" },
            ["xu"] = new[] { "isBoss", "isElite" },
            ["ou"] = new[] { "bdgc", "bdga", "bdfy", "bdfz", "gdj", "gdh", "gcr", "bdgb", "bdgp" },
            ["nv"] = new[] { "bcpy" },
            ["cx"] = new[] { "baji" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["du"] = new[] { "dri", "banz" },
            ["cl"] = new[] { "dfw", "dfx", "bafp" },
            // 自定义被动
            ["bbi"] = new[] { "kmm" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["oc"] = new[] { "bcyk" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["pq"] = new[] { "gks", "gkw" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["m"] = new[] { "cze", "zzs", "zzt" },
            // 施法动作
            ["nw"] = new[] { "bcqv" },
            // 自定义主动技能
            ["bft"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "bnsi" },
            ["bij"] = new[] { "ndc" },
            // 嵌套类型 de.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["de"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["bps"] = new[] { "bjyc", "bqnt", "owb" },
            ["bpt"] = new[] { "bjzu", "bjzw", "bjzt", "oyp", "bjzv" },
            ["bii"] = new[]
            {
                "bigg", "biha", "bigx", "bigi", "bigf", "bqmk", "kcded58bae4384e80be7e3c7692b07553", "k7e56a69102964080a19da99e5918efbd",
                "kbe15cb76268e4fbb89ea8ce8d729300d", "k09a3cc1b9c344ba6b25d3feceefd8b57", "ke036b94f6c9b405d82a4ead3e0f3f1b4",
                "k2da5916a8da840dba685b192dc6a61a7", "k164c5ded09214170a636d8dad1df6c46", "bigl", "bigr", "bigs", "bihh",
                "k5062536e79224451ae57d334924d2f81", "gsq", "ncy",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["bfi"] = new[] { "mrr" },
            ["Buff无敌"] = new string[0],
            ["wh"] = new[] { "hby", "hca", "bpyd" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["bkv"] = new[] { "banm" },
            ["bko"] = new[] { "nsh" },
            ["PlayerDamageFeedbackKind"] = new string[0],
            ["RoleModeBase"] = new[] { "addRange", "igm", "myAnim" },
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
            ["buz"] = new[] { "qhf", "qgx", "qgy", "qgs", "qgq", "qim" },
            ["bvf"] = new[] { "qji" },
            ["bvk"] = new[] { "qlb", "qlc", "bqph", "qkw" },
            ["bve"] = new[] { "blew" },
            ["bvc"] = new[] { "qit" },
            ["bhh"] = new[] { "mzw" },
            ["bcd"] = new[] { "lxg" },
            ["bvh"] = new[] { "qjv" },
            // 皮肤同步：Steam 大厅成员数据
            ["px"] = new[] { "bpxu" },
            ["bz"] = new[] { "dej" },
            ["cgk"] = new[] { "ubm", "ubl", "ube", "ubf", "ubw" },
            ["cgz"] = new[] { "upt" },
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
                CheckSignature(ez, "dyp", new[] { "ActiveSkillEnum", "bbr", "Int32" }, "Boolean", missing);
                CheckSignature(ez, "dyq", new[] { "bbr", "ActiveSkillData" }, "yh", missing);
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
