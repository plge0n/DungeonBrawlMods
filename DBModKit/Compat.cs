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
        public static readonly string[] TestedVersions = { "7.16" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["ny"] = new[] { "bvbx", "bewu", "beww", "bexy", "bexp", "bexk", "beyc", "geo", "gfg", "MaxSkillNum", "MaxEquipNum" },
            ["pq"] = new[] { "gui", "bgcv", "bgcw" },
            ["ck"] = new[] { "buxk", "buxr", "buxs", "buxu", "buxw", "buxz", "buyc" },
            ["fe"] = new[] { "ejk", "ejl", "ekb", "egz", "ems", "emv", "ekt", "eju", "ejw", "emq", "emy", "elf", "ejg", "ejd", "eji", "ekc", "enc", "elv", "emz" },
            ["bis"] = new[] { "ofz" },
            ["qp"] = new[] { "bgib", "bgid", "bgie", "bgio", "hfk" },
            ["bil"] = new[] { "blfr", "blfs", "blfu", "blfq", "blfv", "UpdateSkill", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["sa"] = new[] { "hkp", "hkq" },
            ["ko"] = new[] { "begn", "bego", "begp", "begq", "begr", "Init", "Clear", "fin", "fjd", "fje" },
            ["lz"] = new string[0],
            ["tc"] = new[] { "bgse", "bgsg", "bgsk", "bgsl", "bgsn", "hpa", "hpb", "hpc", "hpe", "bgsb", "bgsc", "bgsf", "bgsm", "hqj", "hqk" },
            ["hv"] = new[] { "bdux" },
            ["ki"] = new[] { "beby", "fgt", "bebv", "becj", "fet", "ffb", "Init", "k148c3e972dd8406aa5b2dfa1cacf7c19", "bebw", "beda", "k09e358dc81864381a5a602f848135be7", "k6b35f03d129f4e1c8e875f635d67d7f9", "k5aa166efd16a45c780dee0a2fff44424" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["pa"] = new[] { "bfwz", "gjo" },
            ["pb"] = new[] { "bfxc", "bfxd", "bfxf", "bfxg", "bfxh", "bfxi" },
            ["oh"] = new[] { "bfpo" },
            ["bgi"] = new[] { "bkpg" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["ceh"] = new[] { "tdl", "tdv" },
            ["cef"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["zm"] = new[]
            {
                "hp", "maxHp", "authorityId", "jtz", "jug", "localRoleBuffDic", "jxf", "jwe", "jsz", "bvji", "bvjl", "bvin", "bwlb", "roleName", "bvik", "bidc", "jta", "bvjj", "roleType",
                "henShinSkillId", "timer", "jvz", "jwa", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bvjk", "bvjb", "bvja", "bviz", "bvim", "bviw", "bvix", "bviv", "bvjc", "bvjg", "bvij", "juo", "jtw",
                "localActiveSkillCastEvent", "joo", "jpk", "roleBuffManager", "jtz",
                "jsr", "jsq", "jvq", "jxb", "jwd", "jwb", "jsy", "jsx", "jtb", "overrideAnimSkillId",
            },
            ["bee"] = new[]
            {
                "heroType", "mll", "bvqp", "bjtj", "bjtk", "llo", "AttackUpdate", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "mcl", "k032d846994fd4b83a295bcc788fef6a7", "k63525c684c3d4f97ab79ae1e2fdb7fd9",
                "man", "luo", "CmdPickItem", "playerAttribute", "roleSkillList", "mdk", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "mao", "mar", "CmdAddBuff", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "jnu", "jua", "CmdCreateSkill", "mas", "maq",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "sox", "bwbs" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["pr"] = new[] { "bgdl", "gxb" },
            ["pt"] = new[] { "bgdq", "bgdr", "bgds", "bgdu" },
            ["bdx"] = new[] { "lqo", "bjnu", "bjnv", "bjny", "lqx", "kba1142604f33441f81beb687e734fd2b", "ke4141bc2d642410fb4335c88f19763fe", "k253c4e0e935f4db38ecfdb0e3dae06f8" },
            ["AudioManager"] = new[] { "dla" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["bve"] = new[] { "qpt", "bden" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["bhk"] = new[] { "nxh" },
            ["ctc"] = new[] { "bsxw", "bsxp" },
            ["csp"] = new[] { "bwlf", "bsuy" },
            ["yz"] = new[] { "isBoss", "isElite" },
            ["pf"] = new[] { "bfyi", "bfyg", "bfye", "bfyf", "gpd", "gpb", "gol", "bfyh", "bfyv" },
            ["oc"] = new[] { "bfhi" },
            ["cy"] = new[] { "bczo" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["dz"] = new[] { "eau", "bdev" },
            ["cm"] = new[] { "dph", "dpi", "bcvu" },
            // 自定义被动
            ["bdl"] = new[] { "lkt" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["oj"] = new[] { "bfpx" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["qm"] = new[] { "hbx", "hcb" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["o"] = new[] { "dir", "bcpy", "bcpz" },
            // 施法动作
            ["od"] = new[] { "bfif" },
            // 自定义主动技能
            ["bim"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "bswz" },
            ["ble"] = new[] { "ols" },
            // 嵌套类型 di.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["di"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["buf"] = new[] { "boai", "bvxn", "qkj", "qkd" },
            ["bug"] = new[] { "bobz", "bocb", "boby", "qms", "boca" },
            ["bld"] = new[]
            {
                "blxx", "blyr", "blyo", "blxz", "blxw", "bvve", "k908597744de547508939478b9332df01", "ka2990ecd2da449f28852b0b2427727d4",
                "kce7fe872c5304c87a5328ba62f6d7a09", "k7eb6364eb60840fca3d3415afada06f8", "kc097d2d01dbd4a7da63a23d8433d2aef",
                "kd7e2c71a7bec4784ad2a9d00f87724ca", "k05a20010fba7441f9259c04cf9d1dd49", "blyc", "blyi", "blyj", "blyy",
                "kc28e07a836fc4a5cbd817cd67cd41a7f", "hkx", "olo", "blxy",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["bia"] = new[] { "obr" },
            ["Buff无敌"] = new string[0],
            ["xg"] = new[] { "hum", "huo", "bvdw" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["bpi"] = new[] { "bden" },
            ["bpb"] = new[] { "pij" },
            ["PlayerDamageFeedbackKind"] = new string[0],
            ["RoleModeBase"] = new[] { "addRange", "izj", "myAnim" },
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
            ["cbl"] = new[] { "scr", "scj", "sck", "sce", "scc", "sdy" },
            ["cbr"] = new[] { "sev" },
            ["cbw"] = new[] { "sgo", "sgp", "bwao", "sgj" },
            ["cbq"] = new[] { "bpru" },
            ["cbo"] = new[] { "sef" },
            ["bkd"] = new[] { "ojb" },
            ["beu"] = new[] { "ngc" },
            ["cbt"] = new[] { "sfi" },
            // 皮肤同步：Steam 大厅成员数据
            ["qw"] = new[] { "bvdh" },
            ["ca"] = new[] { "dnv" },
            ["cpy"] = new[] { "ekh", "wse", "wrx", "wry", "wsp" },
            ["cqn"] = new[] { "xgl" },
            ["WorkshopItemStatus"] = new[] { "publishedFileId", "installed", "heroId", "installFolder" },
            ["LocalWorkshopManifest"] = new[] { "heroId", "title", "heroName", "heroIcon", "author", "contentType", "bundleRelativePath", "bundleAssetName" },
        };

        // 卡牌保护单独检查。它失败时仅停用卡牌，其他 Mod 继续使用原来的兼容检查。
        private static readonly (string type, string[] members)[] CardRequired =
        {
            ("di", new[] { "bdbo", "bdbr", "buyk", "buyl", "bdcm", "dvd", "dvs", "dwa", "dvz", "dvb", "dvr" }),
            ("di+GameSaveData", new[] { "maxCapacity", "cardCapacityProgressionVersion", "equipCards", "equipCardPresets", "currentEquipCardPresetIndex", "haveCardDataDic" }),
            ("di+EquipCardPresetData", new[] { "presetName", "equipCards", "isSaved" }),
            ("bar", new[] { "ksw", "kte", "ksx", "biui" }),
            ("bar+HaveCardData", new[] { "cardId", "haveNum", "curProgress" }),
            ("bsv", new[] { "qdf", "qdg", "qcz", "qda", "qdm", "qcl", "bden" }),
            ("pf", new[] { "bfym" }),
            ("CardData", new[] { "capacity", "progress" }),
        };

        // 独立检查失败仅停用自动锻造；反射签名由安装过程再次核验。
        public static List<string> FindForgingMissing()
        {
            var required = new (string type, string[] members)[] {
                ("qm", new[] { "hdz" }), ("qm+ForgingTier", new[] { "Normal", "Gold" }),
                ("bve", new[] { "qpt", "qqd", "bohq", "bden" }),
                ("RoguelikeUIData", new[] { "name", "quality", "data" }),
                ("ck", new[] { "buxu" }), ("pf", new[] { "bfyk" }), ("ny", new[] { "bvbx" }),
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
            var ez = AccessTools.TypeByName("fe");
            if (ez != null)
            {
                CheckSignature(ez, "ejk", new[] { "ActiveSkillEnum", "bee", "Int32" }, "Boolean", missing);
                CheckSignature(ez, "ejl", new[] { "bee", "ActiveSkillData" }, "zm", missing);
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
