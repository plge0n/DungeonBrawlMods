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
        public static readonly string[] TestedVersions = { "7.03" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["nr"] = new[] { "bqcj", "bchg", "bchi", "bcik", "bcib", "bchw", "bcio", "ftr", "fuj", "MaxSkillNum", "MaxEquipNum" },
            ["oz"] = new[] { "gff", "bdku", "bdkv" },
            ["cj"] = new[] { "bpyk", "bpyr", "bpys", "bpyu", "bpyw", "bpyz", "bpzc" },
            ["ez"] = new[] { "dzj", "dzk", "dzw", "dwy", "ecn", "ecq", "eao", "dzq", "dzr", "ecl", "ect", "eba", "dzf", "dzc", "dzh", "dzx", "ecx", "ebq", "ecu" },
            ["bga"] = new[] { "mxf" },
            ["pt"] = new[] { "bdor", "bdos", "bdot", "bdpd", "gow" },
            ["bft"] = new[] { "bhsg", "bhsh", "bhsj", "bhsf", "bhsk", "kms", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["qw"] = new[] { "gte", "gtf" },
            ["kh"] = new[] { "bbqz", "bbra", "bbrb", "bbrc", "bbrd", "Init", "Clear", "exq", "eyg", "eyh" },
            ["lq"] = new string[0],
            ["rv"] = new[] { "bdxy", "bdya", "bdye", "bdyf", "bdyh", "gxl", "gxm", "gxn", "gxp" },
            ["ho"] = new[] { "bbfm" },
            ["kb"] = new[] { "bbmk", "evw", "bbmh", "bbmv", "etx", "euf", "Init", "k39bb44975a7c4604a6775b7b28f87925" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["oq"] = new[] { "bdhs", "fyv" },
            ["or"] = new[] { "bdhv", "bdhw", "bdhy", "bdhz", "bdia", "bdib" },
            ["oa"] = new[] { "bdau" },
            ["bdr"] = new[] { "bhbv" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["byf"] = new[] { "ria", "rik" },
            ["byd"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["yi"] = new[]
            {
                "hp", "maxHp", "authorityId", "jcb", "jci", "localRoleBuffDic", "jfc", "jee", "jba", "bqjc", "bqjf", "bqih", "brdu", "roleName", "bqie", "bfij", "jbb", "bqjd", "roleType",
                "henShinSkillId", "timer", "jdz", "jea", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bqje", "bqiv", "bqiu", "bqit", "bqig", "bqiq", "bqir", "bqip", "bqiw", "bqja", "bqid", "jcq", "jby",
                "localActiveSkillCastEvent", "iwq", "ixm", "roleBuffManager", "jcb",
                "jas", "jar", "jdq", "jfa", "jed", "jeb", "jaz", "jay", "jbc", "overrideAnimSkillId",
            },
            ["bbs"] = new[]
            {
                "heroType", "ljm", "bqos", "bgnn", "bgno", "kzf", "kze", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "lba", "k234ec3ea02364731a43b3e9a91b92631", "k9920daae7c484b21bc3c60afd0889cb2",
                "kzh", "ktg", "CmdPickItem", "playerAttribute", "roleSkillList", "lby", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "kzi", "kzk", "CmdAddBuff", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "ivx", "jcc", "CmdCreateSkill",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "qva", "bqwa" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["pa"] = new[] { "bdld", "ghg" },
            ["pb"] = new[] { "bdlh", "bdli", "bdlj", "bdll" },
            ["bbm"] = new[] { "kpn", "bgij", "bgik", "bgim", "kpw", "ke35090bd99034a36bfe5671e5b916a5b" },
            ["AudioManager"] = new[] { "dch" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["brk"] = new[] { "pes", "baqf" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["bet"] = new[] { "mof" },
            ["ckj"] = new[] { "bnys", "bnyl" },
            ["cjv"] = new[] { "brdy", "bnvu" },
            ["xv"] = new[] { "isBoss", "isElite" },
            ["ov"] = new[] { "bdjb", "bdiz", "bdix", "bdiy", "gef", "ged", "gdn", "bdja", "bdjo" },
            ["nv"] = new[] { "bcsr" },
            ["cx"] = new[] { "bamb" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["du"] = new[] { "dsc", "baqs" },
            ["cl"] = new[] { "dgq", "dgr", "baii" },
            // 自定义被动
            ["bbj"] = new[] { "knj" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["oc"] = new[] { "bdbd" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["pr"] = new[] { "glo", "gls" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["m"] = new[] { "czy", "bacm", "bacn" },
            // 施法动作
            ["nw"] = new[] { "bcto" },
            // 自定义主动技能
            ["bfu"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "bnxv" },
            ["bik"] = new[] { "neb" },
            // 嵌套类型 de.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["de"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["bqm"] = new[] { "bkdn", "bqtk", "oyt" },
            ["bqn"] = new[] { "bkff", "bkfh", "bkfe", "pbh", "bkfg" },
            ["bij"] = new[]
            {
                "bije", "bijy", "bijv", "bijg", "bijd", "bqry", "kdfb5f2f52501482d81fe1c346b77db8b", "k4c26e2de47124ac6a98c9c271958a0ae",
                "k3637fdbcd15f41218bab00a5a9ed26d7", "k26d6dfcc45ef4b10954cb8159b2ea005", "kfb561741f63f48ec9abff30bf115fc6c",
                "k2ef49c3285ec4ff78ed8a0f5629e1332", "k818080e29b624a3f887496ddd9b03270", "bijj", "bijp", "bijq", "bikf",
                "kce2b7db28c9f4236973348d6d911582c", "gtm", "ndx",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["bfj"] = new[] { "msq" },
            ["Buff无敌"] = new string[0],
            ["wi"] = new[] { "hcu", "hcw", "bqdr" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["blp"] = new[] { "baqf" },
            ["bli"] = new[] { "nva" },
            ["PlayerDamageFeedbackKind"] = new string[0],
            ["RoleModeBase"] = new[] { "addRange", "ihi", "myAnim" },
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
            ["bvt"] = new[] { "qjx", "qjp", "qjq", "qjk", "qji", "qle" },
            ["bvz"] = new[] { "qma" },
            ["bwe"] = new[] { "qnt", "qnu", "bquy", "qno" },
            ["bvy"] = new[] { "blki" },
            ["bvw"] = new[] { "qll" },
            ["bhi"] = new[] { "nav" },
            ["bce"] = new[] { "lye" },
            ["bwb"] = new[] { "qmn" },
            // 皮肤同步：Steam 大厅成员数据
            ["py"] = new[] { "bqdi" },
            ["bz"] = new[] { "dfe" },
            ["che"] = new[] { "uef", "uee", "udx", "udy", "uep" },
            ["cht"] = new[] { "uso" },
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
                CheckSignature(ez, "dzj", new[] { "ActiveSkillEnum", "bbs", "Int32" }, "Boolean", missing);
                CheckSignature(ez, "dzk", new[] { "bbs", "ActiveSkillData" }, "yi", missing);
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
