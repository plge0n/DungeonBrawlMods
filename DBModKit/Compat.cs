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
        public static readonly string[] TestedVersions = { "7.13" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["nt"] = new[] { "bryb", "bdgk", "bdgm", "bdho", "bdhf", "bdha", "bdhs", "fwz", "fxr", "MaxSkillNum", "MaxEquipNum" },
            ["pb"] = new[] { "gir", "beke", "bekf" },
            ["ck"] = new[] { "brty", "bruf", "brug", "brui", "bruk", "brun", "bruq" },
            ["ez"] = new[] { "ecc", "ecd", "ecp", "dzr", "efg", "efj", "edh", "ecj", "eck", "efe", "efm", "edt", "eby", "ebv", "eca", "ecq", "efq", "eej", "efn" },
            ["bgn"] = new[] { "nkc" },
            ["pv"] = new[] { "beoc", "beod", "beoe", "beoo", "gsk" },
            ["bgg"] = new[] { "bjbc", "bjbd", "bjbf", "bjbb", "bjbg", "krh", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["qy"] = new[] { "gwv", "gww" },
            ["kj"] = new[] { "bcqd", "bcqe", "bcqf", "bcqg", "bcqh", "Init", "Clear", "fay", "fbo", "fbp" },
            ["lu"] = new string[0],
            ["sc"] = new[] { "bexq", "bexs", "bexw", "bexx", "bexz", "hbg", "hbh", "hbi", "hbk", "bexn", "bexo", "bexr", "bexy", "hcp", "hcq" },
            ["hq"] = new[] { "bcep" },
            ["kd"] = new[] { "bclo", "eze", "bcll", "bclz", "exf", "exn", "Init", "k855040601ccd4004b658e50da9454994", "bclm", "bcmq", "kff7e9fdc37fd4ea58a588f0b690e4623", "kf1dc0eb1b8f64fe894ca43bb2518c267", "kc15c7ba2537446fc8359216d5cdbe313" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["os"] = new[] { "behc", "gcf" },
            ["ot"] = new[] { "behf", "behg", "behi", "behj", "behk", "behl" },
            ["oc"] = new[] { "beab" },
            ["bee"] = new[] { "bikr" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["bzy"] = new[] { "sdi", "sds" },
            ["bzw"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["yn"] = new[]
            {
                "hp", "maxHp", "authorityId", "jgi", "jgp", "localRoleBuffDic", "jjl", "jin", "jfh", "bsev", "bsey", "bsea", "btci", "roleName", "bsdx", "bgij", "jfi", "bsew", "roleType",
                "henShinSkillId", "timer", "jii", "jij", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bsex", "bseo", "bsen", "bsem", "bsdz", "bsej", "bsek", "bsei", "bsep", "bset", "bsdw", "jgx", "jgf",
                "localActiveSkillCastEvent", "jax", "jbt", "roleBuffManager", "jgi",
                "jez", "jey", "jhz", "jjj", "jim", "jik", "jfg", "jff", "jfj", "overrideAnimSkillId",
            },
            ["bca"] = new[]
            {
                "heroType", "lpj", "bskq", "bhov", "bhow", "lem", "lel", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "lgj", "k3c5f05d81410475b9f6c6314aded23f4", "k8b0b95bf0a114876b442ed99eb57eb0a",
                "leo", "kyn", "CmdPickItem", "playerAttribute", "roleSkillList", "lhi", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "lep", "ler", "CmdAddBuff", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "jae", "jgj", "CmdCreateSkill", "les", "leq",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "ros", "bsub" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["pc"] = new[] { "bekn", "gku" },
            ["pd"] = new[] { "beks", "bekt", "beku", "bekw" },
            ["bbt"] = new[] { "kup", "bhjj", "bhjk", "bhjm", "kuy", "kbfd6bdc3bdc14b7ab236e9d3500f9fb4", "kc6cf6f7872c24e719dea5e3fb2fba921", "k19dc666848a74e8ba2fc2e8ef7b815c9" },
            ["AudioManager"] = new[] { "deu" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["bsf"] = new[] { "ptt", "bbov" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["bfg"] = new[] { "nbg" },
            ["cmv"] = new[] { "bpul", "bpue" },
            ["cmh"] = new[] { "btcm", "bprn" },
            ["ya"] = new[] { "isBoss", "isElite" },
            ["ox"] = new[] { "beil", "beij", "beih", "beii", "ghr", "ghp", "ggz", "beik", "beiy" },
            ["nx"] = new[] { "bdrv" },
            ["cy"] = new[] { "bbks" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["dv"] = new[] { "dun", "bbpi" },
            ["cm"] = new[] { "djc", "djd", "bbgz" },
            // 自定义被动
            ["bbq"] = new[] { "kry" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["oe"] = new[] { "beak" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["pu"] = new[] { "gpc", "gpg" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["o"] = new[] { "dcj", "bbbd", "bbbe" },
            // 施法动作
            ["ny"] = new[] { "bdss" },
            // 自定义主动技能
            ["bgh"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "bpto" },
            ["biz"] = new[] { "nrc" },
            // 嵌套类型 df.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["df"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["brh"] = new[] { "blpf", "bsqv", "pnt", "pnn" },
            ["bri"] = new[] { "blqx", "blqz", "blqw", "pqi", "blqy" },
            ["biy"] = new[]
            {
                "bjsh", "bjtb", "bjsy", "bjsj", "bjsg", "bspf", "k2ed993c06c594d29aa2d6519e03e82b6", "k3cf043e2f77a4673b4805a7846500b73",
                "ke290a4fe71104531a7f8c865f67ffb50", "k9a3a2a6b88374971b87f1be77e951ac0", "k1e6e688e728f402bb29e4ccf2e11fb52",
                "kfda6d7b2dd6b408db0ca98687af0a0d1", "ke9c0f5a8b78f4993b879cbd30d51b215", "bjsm", "bjss", "bjst", "bjti",
                "k474e18bf33a3493e8a2d2b37d5a6693e", "gxd", "nqy", "bjsi",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["bfw"] = new[] { "nfq" },
            ["Buff无敌"] = new string[0],
            ["wk"] = new[] { "hgp", "hgr", "brzk" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["bmk"] = new[] { "bbov" },
            ["bmd"] = new[] { "okb" },
            ["PlayerDamageFeedbackKind"] = new string[0],
            ["RoleModeBase"] = new[] { "addRange", "ilp", "myAnim" },
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
            ["bxc"] = new[] { "rco", "rcg", "rch", "rcb", "rbz", "rdv" },
            ["bxi"] = new[] { "rer" },
            ["bxn"] = new[] { "rgk", "rgl", "bssx", "rgf" },
            ["bxh"] = new[] { "bnah" },
            ["bxf"] = new[] { "rec" },
            ["bhx"] = new[] { "nnv" },
            ["bcq"] = new[] { "mjz" },
            ["bxk"] = new[] { "rfe" },
            // 皮肤同步：Steam 大厅成员数据
            ["qa"] = new[] { "brza" },
            ["ca"] = new[] { "dhp" },
            ["cjp"] = new[] { "vda", "vcz", "vcs", "vct", "vdk" },
            ["cke"] = new[] { "vrh" },
            ["WorkshopItemStatus"] = new[] { "publishedFileId", "installed", "heroId", "installFolder" },
            ["LocalWorkshopManifest"] = new[] { "heroId", "title", "heroName", "heroIcon", "author", "contentType", "bundleRelativePath", "bundleAssetName" },
        };

        // 卡牌保护单独检查。它失败时仅停用卡牌，其他 Mod 继续使用原来的兼容检查。
        private static readonly (string type, string[] members)[] CardRequired =
        {
            ("df", new[] { "bbme", "bbmh", "bruy", "bruz", "bbmz", "dou", "dph", "dpp", "dpo", "dos", "dpg" }),
            ("df+GameSaveData", new[] { "maxCapacity", "cardCapacityProgressionVersion", "equipCards", "equipCardPresets", "currentEquipCardPresetIndex", "haveCardDataDic" }),
            ("df+EquipCardPresetData", new[] { "presetName", "equipCards", "isSaved" }),
            ("zt", new[] { "kfn", "kfv", "kfo", "bgzn" }),
            ("zt+HaveCardData", new[] { "cardId", "haveNum", "curProgress" }),
            ("bpy", new[] { "pgj", "pgk", "pgd", "pge", "pgq", "pfp", "bbov" }),
            ("ox", new[] { "beip" }),
            ("CardData", new[] { "capacity", "progress" }),
        };

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
            var ez = AccessTools.TypeByName("ez");
            if (ez != null)
            {
                CheckSignature(ez, "ecc", new[] { "ActiveSkillEnum", "bca", "Int32" }, "Boolean", missing);
                CheckSignature(ez, "ecd", new[] { "bca", "ActiveSkillData" }, "yn", missing);
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
