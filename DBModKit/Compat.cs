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
        public static readonly string[] TestedVersions = { "7.15" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["nv"] = new[] { "bvaj", "bevp", "bevr", "bewt", "bewk", "bewf", "bewx", "geh", "gfa", "MaxSkillNum", "MaxEquipNum" },
            ["pn"] = new[] { "guc", "bgbq", "bgbr" },
            ["ck"] = new[] { "buvw", "buwd", "buwe", "buwg", "buwi", "buwl", "buwo" },
            ["fb"] = new[] { "eje", "ejf", "ejv", "egt", "emm", "emp", "ekn", "ejo", "ejq", "emk", "ems", "ekz", "eja", "eix", "ejc", "ejw", "emw", "elp", "emt" },
            ["bip"] = new[] { "ofl" },
            ["qm"] = new[] { "bggw", "bggy", "bggz", "bghj", "hfd" },
            ["bii"] = new[] { "blei", "blej", "blel", "bleh", "blem", "UpdateSkill", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["rs"] = new[] { "hki", "hkj" },
            ["kl"] = new[] { "befi", "befj", "befk", "befl", "befm", "Init", "Clear", "fih", "fix", "fiy" },
            ["lw"] = new string[0],
            ["sw"] = new[] { "bgqz", "bgrb", "bgrf", "bgrg", "bgri", "hot", "hou", "hov", "hox", "bgqw", "bgqx", "bgra", "bgrh", "hqc", "hqd" },
            ["hs"] = new[] { "bdts" },
            ["kf"] = new[] { "beas", "fgn", "beap", "bebe", "fen", "fev", "Init", "kc294c6cb65da421f9a021be2e2275ffc", "beaq", "bebv", "kaac4c22ee1ee42ed90d6e1f6b2e4d679", "kfd93594ee6f14333bc581712293b9afa", "kc371b8efa6a549259c61864e7347e9ac" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["ox"] = new[] { "bfvu", "gji" },
            ["oy"] = new[] { "bfvx", "bfvy", "bfwa", "bfwb", "bfwc", "bfwd" },
            ["oe"] = new[] { "bfoj" },
            ["bgf"] = new[] { "bknx" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["cee"] = new[] { "tcw", "tdg" },
            ["cec"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["zj"] = new[]
            {
                "hp", "maxHp", "authorityId", "jts", "jtz", "localRoleBuffDic", "jwx", "jvx", "jss", "bvht", "bvhw", "bvgy", "bwjl", "roleName", "bvgv", "bibx", "jst", "bvhu", "roleType",
                "henShinSkillId", "timer", "jvs", "jvt", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bvhv", "bvhm", "bvhl", "bvhk", "bvgx", "bvhh", "bvhi", "bvhg", "bvhn", "bvhr", "bvgu", "juh", "jtp",
                "localActiveSkillCastEvent", "joh", "jpd", "roleBuffManager", "jts",
                "jsk", "jsj", "jvj", "jwt", "jvw", "jvu", "jsr", "jsq", "jsu", "overrideAnimSkillId",
            },
            ["beb"] = new[]
            {
                "heroType", "mkx", "bvoz", "bjsa", "bjsb", "llg", "AttackUpdate", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "mbx", "k1b4a3713338f4d979993ea273ad91c17", "k35b1d6301126412e913613132edd38c3",
                "lzy", "lua", "CmdPickItem", "playerAttribute", "roleSkillList", "mcw", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "lzz", "mab", "CmdAddBuff", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "jnn", "jtt", "CmdCreateSkill", "mad", "maa",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "soj", "bwac" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["po"] = new[] { "bgcg", "gwv" },
            ["pp"] = new[] { "bgcl", "bgcm", "bgcn", "bgcp" },
            ["bdu"] = new[] { "lqa", "bjml", "bjmm", "bjmp", "lqj", "k100bc9e4d0384dcab8834e7ff8b38594", "k605866ecec4d4fb9a22bbb4e0335cb03", "kbdfb84d759544852a9647be95ace3081" },
            ["AudioManager"] = new[] { "dkx" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["bvb"] = new[] { "qpf", "bddi" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["bhh"] = new[] { "nwt" },
            ["csz"] = new[] { "bswj", "bswc" },
            ["csm"] = new[] { "bwjp", "bstl" },
            ["yw"] = new[] { "isBoss", "isElite" },
            ["pc"] = new[] { "bfxd", "bfxb", "bfwz", "bfxa", "gox", "gov", "gof", "bfxc", "bfxq" },
            ["nz"] = new[] { "bfgd" },
            ["cy"] = new[] { "bcyz" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["dw"] = new[] { "eao", "bddq" },
            ["cm"] = new[] { "dpe", "dpf", "bcvf" },
            // 自定义被动
            ["bdi"] = new[] { "lkl" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["og"] = new[] { "bfos" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["qj"] = new[] { "hbq", "hbu" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["o"] = new[] { "dio", "bcpj", "bcpk" },
            // 施法动作
            ["oa"] = new[] { "bfha" },
            // 自定义主动技能
            ["bij"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "bsvm" },
            ["blb"] = new[] { "ole" },
            // 嵌套类型 df.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["df"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["buc"] = new[] { "bnyy", "bvvx", "qjv", "qjp" },
            ["bud"] = new[] { "boap", "boar", "boao", "qme", "boaq" },
            ["bla"] = new[]
            {
                "blwo", "blxi", "blxf", "blwq", "blwn", "bvto", "k3a7deff2c0e046039249740bede184ca", "k78264ccc7c994bc1891d6951139a9d8f",
                "kc804bd0966a845cd923831d4c53527ef", "k6686bf02585b4cd8af9a6ed7198695e8", "k0c96cb0fe4604269ba75a1f774a7899f",
                "k7d0bfbe74bca4b06a60232aed5801182", "kffe36c09f0bd46e481e0806c1dba36c5", "blwt", "blwz", "blxa", "blxp",
                "kc6cd39f4e4eb49bcac2db46bc1b92697", "hkq", "ola", "blwp",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["bhx"] = new[] { "obc" },
            ["Buff无敌"] = new string[0],
            ["xd"] = new[] { "huf", "huh", "bvch" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["bpf"] = new[] { "bddi" },
            ["boy"] = new[] { "phv" },
            ["PlayerDamageFeedbackKind"] = new string[0],
            ["RoleModeBase"] = new[] { "addRange", "izc", "myAnim" },
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
            ["cbi"] = new[] { "scd", "sbv", "sbw", "sbq", "sbo", "sdk" },
            ["cbo"] = new[] { "seg" },
            ["cbt"] = new[] { "sga", "sgb", "bvyy", "sfv" },
            ["cbn"] = new[] { "bpqk" },
            ["cbl"] = new[] { "sdr" },
            ["bka"] = new[] { "oin" },
            ["ber"] = new[] { "nfo" },
            ["cbq"] = new[] { "seu" },
            // 皮肤同步：Steam 大厅成员数据
            ["qt"] = new[] { "bvbs" },
            ["ca"] = new[] { "dns" },
            ["cpv"] = new[] { "eys", "wrq", "wrj", "wrk", "wsb" },
            ["cqk"] = new[] { "xfx" },
            ["WorkshopItemStatus"] = new[] { "publishedFileId", "installed", "heroId", "installFolder" },
            ["LocalWorkshopManifest"] = new[] { "heroId", "title", "heroName", "heroIcon", "author", "contentType", "bundleRelativePath", "bundleAssetName" },
        };

        // 卡牌保护单独检查。它失败时仅停用卡牌，其他 Mod 继续使用原来的兼容检查。
        private static readonly (string type, string[] members)[] CardRequired =
        {
            ("df", new[] { "bdam", "bdap", "buww", "buwx", "bdbh", "duz", "dvm", "dvu", "dvt", "dux", "dvl" }),
            ("df+GameSaveData", new[] { "maxCapacity", "cardCapacityProgressionVersion", "equipCards", "equipCardPresets", "currentEquipCardPresetIndex", "haveCardDataDic" }),
            ("df+EquipCardPresetData", new[] { "presetName", "equipCards", "isSaved" }),
            ("bao", new[] { "kso", "ksw", "ksp", "bitd" }),
            ("bao+HaveCardData", new[] { "cardId", "haveNum", "curProgress" }),
            ("bss", new[] { "qcr", "qcs", "qcl", "qcm", "qcy", "qbx", "bddi" }),
            ("pc", new[] { "bfxh" }),
            ("CardData", new[] { "capacity", "progress" }),
        };

        // 独立检查失败仅停用自动锻造；反射签名由安装过程再次核验。
        public static List<string> FindForgingMissing()
        {
            var required = new (string type, string[] members)[] {
                ("qj", new[] { "hds" }), ("qj+ForgingTier", new[] { "Normal", "Gold" }),
                ("bvb", new[] { "qpf", "qpp", "bogg", "bddi" }),
                ("RoguelikeUIData", new[] { "name", "quality", "data" }),
                ("ck", new[] { "buwg" }), ("pc", new[] { "bfxf" }), ("nv", new[] { "bvaj" }),
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
            var ez = AccessTools.TypeByName("fb");
            if (ez != null)
            {
                CheckSignature(ez, "eje", new[] { "ActiveSkillEnum", "beb", "Int32" }, "Boolean", missing);
                CheckSignature(ez, "ejf", new[] { "beb", "ActiveSkillData" }, "zj", missing);
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
