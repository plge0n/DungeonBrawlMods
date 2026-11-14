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
        public static readonly string[] TestedVersions = { "7.18" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["nz"] = new[] { "bvnm", "bfdv", "bfdx", "bfez", "bfeq", "bfel", "bffd", "ggf", "ggy", "MaxSkillNum", "MaxEquipNum" },
            ["pr"] = new[] { "gwf", "bgka", "bgkb" },
            ["ck"] = new[] { "bvit", "bvja", "bvjb", "bvjd", "bvjf", "bvji", "bvjl" },
            ["ff"] = new[] { "ekw", "ekx", "eln", "eil", "eof", "eoi", "emf", "elg", "eli", "eod", "eol", "emr", "eks", "ekp", "eku", "elo", "eop", "eni", "eom" },
            ["biv"] = new[] { "ois" },
            ["qr"] = new[] { "bgpo", "bgpq", "bgpr", "bgqb", "hhm" },
            ["bio"] = new[] { "bloa", "blob", "blod", "blnz", "bloe", "UpdateSkill", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["sd"] = new[] { "hmr", "hms" },
            ["kp"] = new[] { "beno", "benp", "benq", "benr", "bens", "Init", "Clear", "fke", "fku", "fkv" },
            ["ma"] = new string[0],
            ["te"] = new[] { "bgzr", "bgzt", "bgzx", "bgzy", "bhaa", "hrc", "hrd", "hre", "hrg", "bgzo", "bgzp", "bgzs", "bgzz", "hsl", "hsm" },
            ["hw"] = new[] { "beca" },
            ["kj"] = new[] { "beiz", "fik", "beiw", "bejk", "fgl", "fgt", "Init", "k33fabab9ec034f6cb59cf8f57dae4f08", "beix", "bekb", "kc8c3004fb9d04e739947343a21944c0e", "k1e5f877a09d64947988f71099ca4b4c3", "kb3977c7bc6e646ba89146119a7bde9e0" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["pb"] = new[] { "bgeb", "glg" },
            ["pc"] = new[] { "bgee", "bgef", "bgeh", "bgei", "bgej", "bgek" },
            ["oi"] = new[] { "bfwp" },
            ["bgl"] = new[] { "bkxp" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["cfd"] = new[] { "tkd", "tkn" },
            ["cfb"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["zo"] = new[]
            {
                "hp", "maxHp", "authorityId", "jwb", "jwi", "localRoleBuffDic", "jzh", "jyg", "jvb", "bvva", "bvvd", "bvuf", "bwxe", "roleName", "bvuc", "bikp", "jvc", "bvvb", "roleType",
                "henShinSkillId", "timer", "jyb", "jyc", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bvvc", "bvut", "bvus", "bvur", "bvue", "bvuo", "bvup", "bvun", "bvuu", "bvuy", "bvub", "jwq", "jvy",
                "localActiveSkillCastEvent", "jqq", "jrm", "roleBuffManager", "jwb",
                "jut", "jus", "jxs", "jzd", "jyf", "jyd", "jva", "juz", "jvd", "overrideAnimSkillId",
            },
            ["beh"] = new[]
            {
                "heroType", "moe", "bwcj", "bkbs", "bkbt", "loi", "AttackUpdate", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "mfe", "k3cf7752c477a4ae78f2aa2186381781a", "kd28d966c38f5408886e60fb5854d5ab0",
                "mdj", "lxh", "CmdPickItem", "playerAttribute", "roleSkillList", "mgd", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "mdk", "mdm", "CmdAddBuff", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "jpx", "jwc", "CmdCreateSkill", "mdn", "mdl",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "svr", "bwnv" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["pt"] = new[] { "bgkq", "gza" },
            ["pu"] = new[] { "bgkv", "bgkw", "bgkx", "bgkz" },
            ["bea"] = new[] { "lth", "bjwd", "bjwe", "bjwh", "ltq", "k41d691fbf5b74981a355269b358d4b39", "k065ed57ba5594f758e75946883df7466", "k1aab9d5f76d64defb7ae7c3ba3681b77" },
            ["AudioManager"] = new[] { "dlw" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["bvz"] = new[] { "qwg", "bdlg" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["bhn"] = new[] { "oaa" },
            ["cty"] = new[] { "btjf", "btiy" },
            ["ctl"] = new[] { "bwxi", "btgh" },
            ["zb"] = new[] { "isBoss", "isElite" },
            ["pg"] = new[] { "bgfk", "bgfi", "bgfg", "bgfh", "gqv", "gqt", "gqd", "bgfj", "bgfx" },
            ["od"] = new[] { "bfoj" },
            ["cy"] = new[] { "bdgh" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["dz"] = new[] { "eca", "bdlo" },
            ["cm"] = new[] { "dqd", "dqe", "bdck" },
            // 自定义被动
            ["bdo"] = new[] { "lnm" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["ok"] = new[] { "bfwy" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["qo"] = new[] { "hdz", "hed" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["o"] = new[] { "djn", "bcwo", "bcwp" },
            // 施法动作
            ["oe"] = new[] { "bfpg" },
            // 自定义主动技能
            ["bip"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "btii" },
            ["blh"] = new[] { "oom" },
            // 嵌套类型 di.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["di"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["bva"] = new[] { "bolc", "bwjq", "qqw", "qqq" },
            ["bvb"] = new[] { "bomt", "bomv", "boms", "qtf", "bomu" },
            ["blg"] = new[]
            {
                "bmgg", "bmha", "bmgx", "bmgi", "bmgf", "bwgy", "k84fc3b52982243f2a512bdd7e2b4481e", "k0bbef4416aec42b29ddc7a9904664f0a",
                "k7113a5209d394aa38266a873fea0e585", "k5c07e3bd58c54e82b78c483d02fe6335", "kc7b47610f29641cb971b7f4213d59a64",
                "kbb2e0dec4fc84f31aee00d129e11be30", "ke7de4679f9de4af68e34a7242f5e4b6f", "bmgl", "bmgr", "bmgs", "bmhh",
                "kf75e48a90f6f4f3b9ebe2691f0e5788d", "hmz", "ooi", "bmgh",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["bid"] = new[] { "oek" },
            ["Buff无敌"] = new string[0],
            ["xi"] = new[] { "hwo", "hwq", "bvpo" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["bqd"] = new[] { "bdlg" },
            ["bpw"] = new[] { "pov" },
            ["PlayerDamageFeedbackKind"] = new string[0],
            ["RoleModeBase"] = new[] { "addRange", "jbm", "myAnim" },
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
            ["cch"] = new[] { "sjk", "sjc", "sjd", "six", "siv", "skr" },
            ["ccn"] = new[] { "sln" },
            ["ccs"] = new[] { "sng", "snh", "bwmr", "snb" },
            ["ccm"] = new[] { "bqdd" },
            ["cck"] = new[] { "sky" },
            ["bkg"] = new[] { "olu" },
            ["bex"] = new[] { "niv" },
            ["ccp"] = new[] { "sma" },
            // 皮肤同步：Steam 大厅成员数据
            ["qy"] = new[] { "bvoz" },
            ["ca"] = new[] { "dor" },
            ["cqu"] = new[] { "wyv", "wyu", "wyn", "wyo", "wzf" },
            ["crj"] = new[] { "xnb" },
            ["WorkshopItemStatus"] = new[] { "publishedFileId", "installed", "heroId", "installFolder" },
            ["LocalWorkshopManifest"] = new[] { "heroId", "title", "heroName", "heroIcon", "author", "contentType", "bundleRelativePath", "bundleAssetName" },
        };

        // 卡牌保护单独检查。它失败时仅停用卡牌，其他 Mod 继续使用原来的兼容检查。
        private static readonly (string type, string[] members)[] CardRequired =
        {
            ("di", new[] { "bdih", "bdik", "bvjx", "bvjy", "bdjf", "dwj", "dwy", "dxg", "dxf", "dwh", "dwx" }),
            ("di+GameSaveData", new[] { "maxCapacity", "cardCapacityProgressionVersion", "equipCards", "equipCardPresets", "currentEquipCardPresetIndex", "haveCardDataDic" }),
            ("di+EquipCardPresetData", new[] { "presetName", "equipCards", "isSaved" }),
            ("bat", new[] { "kux", "kvf", "kuy", "bjbv" }),
            ("bat+HaveCardData", new[] { "cardId", "haveNum", "curProgress" }),
            ("btr", new[] { "qjs", "qjt", "qjm", "qjn", "qjz", "qiy", "bdlg" }),
            ("pg", new[] { "bgfo" }),
            ("CardData", new[] { "capacity", "progress" }),
        };

        // 独立检查失败仅停用自动锻造；反射签名由安装过程再次核验。
        public static List<string> FindForgingMissing()
        {
            var required = new (string type, string[] members)[] {
                ("qo", new[] { "hgb" }), ("qo+ForgingTier", new[] { "Normal", "Gold" }),
                ("bvz", new[] { "qwg", "qwq", "bosm", "bdlg" }),
                ("RoguelikeUIData", new[] { "name", "quality", "data" }),
                ("ck", new[] { "bvjd" }), ("pg", new[] { "bgfm" }), ("nz", new[] { "bvnm" }),
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
