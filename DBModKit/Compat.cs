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
        public static readonly string[] TestedVersions = { "7.21" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["ve"] = new[] { "bzoh", "bjhv", "bjhx", "bjiz", "bjiq", "bjil", "bjjd", "kht", "kim", "MaxSkillNum", "MaxEquipNum" },
            ["wu"] = new[] { "kxy", "bkob", "bkoc" },
            ["cl"] = new[] { "bzan", "bzau", "bzav", "bzax", "bzaz", "bzbc", "bzbf" },
            ["fh"] = new[] { "eqw", "eqx", "ern", "eol", "eue", "euh", "esf", "erg", "eri", "euc", "euk", "esr", "eqs", "eqp", "equ", "ero", "eup", "eth", "eul" },
            ["bpq"] = new[] { "qtl" },
            ["xw"] = new[] { "bkuc", "bkue", "bkuf", "bkup", "ljm" },
            ["fx"] = new[] { "bfwt" },
            ["bpj"] = new[] { "cacx", "bppc", "bppe", "bpoz", "bppf", "UpdateSkill", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["zc"] = new[] { "lov", "Exit" },
            ["rg"] = new[] { "birg", "birh", "biri", "birj", "birk", "Init", "Clear", "jlo", "jme", "jmf" },
            ["ta"] = new string[0],
            ["bab"] = new[] { "blfh", "blfj", "blfn", "blfo", "blfq", "irw", "Exit", "luw", "luy", "blfe", "blff", "blfi", "blfp", "lwc", "lwd" },
            ["om"] = new[] { "bifp" },
            ["qz"] = new[] { "bimr", "jju", "biml", "binc", "jhv", "jid", "Init", "k5d321335cb734cac8da38d573c3cea8d", "bimp", "bint", "k91409a041abe4331af274f3515e7dae3", "k3688fb37eb4148deafe1e753c2782f64", "k5dcdafbd117f4a769f1362c8706f252b" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["wg"] = new[] { "bkib", "kmu" },
            ["wh"] = new[] { "bkie", "bkif", "bkih", "bkii", "bkij", "bkik" },
            ["vn"] = new[] { "bkap" },
            ["bng"] = new[] { "boyn", "qcp" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["ckx"] = new[] { "vjs", "vkc" },
            ["ckv"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["ga"] = new[]
            {
                "hp", "maxHp", "authorityId", "fgp", "fgw", "localRoleBuffDic", "fkd", "fja", "ffk", "bzfc", "bzff", "bzeh", "cash", "roleName", "bzee", "bfzp", "ffl", "bzfd", "roleType",
                "henShinSkillId", "timer", "fiv", "fiw", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bzfe", "bzev", "bzeu", "bzet", "bzeg", "bzeq", "bzer", "bzep", "bzew", "bzfa", "bzed", "fhe", "fgm",
                "localActiveSkillCastEvent", "fat", "fbp", "roleBuffManager", "fgp",
                "ffa", "fey", "fij", "fjz", "fiz", "fix", "ffj", "ffi", "ffm", "overrideAnimSkillId",
            },
            ["ix"] = new[]
            {
                "heroType", "hck", "bzjl", "bgtz", "bgua", "gqk", "AttackUpdate", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "gsr", "kf14b304114354994a4be119d9aa0e9b9", "kd0fac5219197445bb4299b8eeb3f7506",
                "gqn", "gkn", "CmdPickItem", "playerAttribute", "roleSkillList", "gtq", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "gqp", "gqs", "grx", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "faa", "fgq", "CmdCreateSkill", "gqt", "gqq",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "hvf", "bzla" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["wv"] = new[] { "bkor", "lat" },
            ["ww"] = new[] { "bkox", "bkoy", "bkoz", "bkpb" },
            ["bky"] = new[] { "pfb", "bnzp", "bnzq", "bnzu", "pfo", "kcbee28a83e564866a41bb1c70d0a4c3a", "kdb3cda14277f45b8a23fc6deddf83889", "k21bb3b0a9b4b4132940717b24e0d7104" },
            ["AudioManager"] = new[] { "drt" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["ccw"] = new[] { "thw", "bfpq" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["boj"] = new[] { "qko" },
            ["czu"] = new[] { "bxaz", "bxas" },
            ["czh"] = new[] { "casl", "bwyb" },
            ["bfw"] = new[] { "isBoss", "isElite" },
            ["wl"] = new[] { "bkjl", "bkjj", "bkjh", "bkji", "kso", "ksm", "krw", "bkjk", "bkjy" },
            ["vi"] = new[] { "bjsj" },
            ["cz"] = new[] { "bfkr" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["ea"] = new[] { "ehy", "bfpy" },
            ["cn"] = new[] { "dwb", "dwc", "bfgu" },
            // 自定义被动
            ["bkk"] = new[] { "oze" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["vp"] = new[] { "bkay" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["xr"] = new[] { "lfu", "lfy" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["u"] = new[] { "dpi", "bfay", "bfaz" },
            // 施法动作
            ["vj"] = new[] { "bjtg" },
            // 自定义主动技能
            ["bpk"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "bxac" },
            ["bsc"] = new[] { "qzf" },
            // 嵌套类型 dj.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["dj"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["cbx"] = new[] { "bsmm", "cafx", "tch", "tcb" },
            ["cby"] = new[] { "bsoh", "bsoj", "bsog", "tev", "bsoi" },
            ["bsb"] = new[]
            {
                "bqhh", "bqib", "bqhy", "bqhj", "bqhg", "cade", "ke8047c95741f44188f28c161ac7711f2", "k08e247265bcb4240a3c442c1c17e6020",
                "k16072c38393744e7a171f382ae77e129", "kc3d1e204c8dc45c28177ddbf52a2aeef", "k130b5255edd24527ad64312c1ab1f02b",
                "ke0bf2acb0f814915b4f3627825dba326", "kaa5ccab3d95d4bb8bc78f00783b9f5d4", "bqhm", "bqhs", "bqht", "bqii",
                "kb6da93adfd584b46bf9f2c41b032fd8d", "lpc", "qzb", "bqhi",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["boz"] = new[] { "qox" },
            ["Buff无敌"] = new string[0],
            ["bed"] = new[] { "mah", "maj", "bzqo" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["bxa"] = new[] { "bfpq" },
            ["bwt"] = new[] { "rzy" },
            ["PlayerDamageFeedbackKind"] = new string[0],
            ["RoleModeBase"] = new[] { "addRange", "nfc", "myAnim" },
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
            ["cjg"] = new[] { "uwh", "uvz", "uwa", "uvu", "uvr", "uxo" },
            ["cjm"] = new[] { "uyk" },
            ["cjr"] = new[] { "vad", "vae", "caiz", "uzy" },
            ["cjl"] = new[] { "bufl" },
            ["cjj"] = new[] { "uxv" },
            ["brb"] = new[] { "qwn" },
            ["blo"] = new[] { "psl" },
            ["cjo"] = new[] { "uyx" },
            // 皮肤同步：Steam 大厅成员数据
            ["yd"] = new[] { "bzpu" },
            ["cb"] = new[] { "dup" },
            ["cwq"] = new[] { "zdf", "zde", "zcx", "zcy", "zdp" },
            ["cxf"] = new[] { "zrl" },
            ["WorkshopItemStatus"] = new[] { "publishedFileId", "installed", "heroId", "installFolder" },
            ["LocalWorkshopManifest"] = new[] { "heroId", "title", "heroName", "heroIcon", "author", "contentType", "bundleRelativePath", "bundleAssetName" },
        };

        // 卡牌保护单独检查。它失败时仅停用卡牌，其他 Mod 继续使用原来的兼容检查。
        private static readonly (string type, string[] members)[] CardRequired =
        {
            ("dj", new[] { "bfmr", "bfmu", "bzbr", "bzbs", "bfnp", "ech", "ecw", "ede", "edd", "ecf", "ecv" }),
            ("dj+GameSaveData", new[] { "maxCapacity", "cardCapacityProgressionVersion", "equipCards", "equipCardPresets", "currentEquipCardPresetIndex", "haveCardDataDic" }),
            ("dj+EquipCardPresetData", new[] { "presetName", "equipCards", "isSaved" }),
            ("bhn", new[] { "ogj", "ogr", "ogk", "bneg" }),
            ("bhn+HaveCardData", new[] { "cardId", "haveNum", "curProgress" }),
            ("cao", new[] { "svb", "svc", "suv", "suw", "svi", "suh", "bfpq" }),
            ("wl", new[] { "bkjp" }),
            ("CardData", new[] { "capacity", "progress" }),
        };

        // 独立检查失败仅停用自动锻造；反射签名由安装过程再次核验。
        public static List<string> FindForgingMissing()
        {
            var required = new (string type, string[] members)[] {
                ("xr", new[] { "lhw", "lhx" }), ("xr+ForgingTier", new[] { "Normal", "Gold" }),
                ("ccw", new[] { "thw", "tig", "bstx", "bfpq" }),
                ("RoguelikeUIData", new[] { "name", "quality", "data" }),
                ("cl", new[] { "bzax" }), ("wl", new[] { "bkjn" }), ("ve", new[] { "bzoh" }),
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

            // 自动施法和选目标还要核对完整参数与返回类型。
            var skillUtility = AccessTools.TypeByName("fh");
            if (skillUtility != null)
            {
                CheckSignature(skillUtility, "eqw", new[] { "ActiveSkillEnum", "ix", "Int32" }, "Boolean", missing);
                CheckSignature(skillUtility, "eqx", new[] { "ix", "ActiveSkillData" }, "ga", missing);
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
            if (!ok) missing.Add($"{type.FullName}.{name}({string.Join(", ", parameters)}) 签名");
        }
    }
}
