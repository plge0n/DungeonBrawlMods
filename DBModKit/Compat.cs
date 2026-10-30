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
        public static readonly string[] TestedVersions = { "7.11" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["nr"] = new[] { "bqtm", "bcqk", "bcqm", "bcro", "bcrf", "bcra", "bcrs", "fuy", "fvq", "MaxSkillNum", "MaxEquipNum" },
            ["oz"] = new[] { "ggq", "bduc", "bdud" },
            ["cj"] = new[] { "bqpl", "bqps", "bqpt", "bqpv", "bqpx", "bqqa", "bqqd" },
            ["ey"] = new[] { "eai", "eaj", "eav", "dxx", "edm", "edp", "ebn", "eap", "eaq", "edk", "eds", "ebz", "eae", "eab", "eag", "eaw", "edw", "ecp", "edt" },
            ["bgg"] = new[] { "ndk" },
            ["pt"] = new[] { "bdya", "bdyb", "bdyc", "bdym", "gqj" },
            ["bfz"] = new[] { "bifz", "biga", "bigc", "bify", "bigd", "koz", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["qw"] = new[] { "gur", "gus" },
            ["kh"] = new[] { "bcad", "bcae", "bcaf", "bcag", "bcah", "Init", "Clear", "eyx", "ezn", "ezo" },
            ["lq"] = new string[0],
            ["rv"] = new[] { "beho", "behq", "behu", "behv", "behx", "gzb", "gzc", "gzd", "gzf" },
            ["hn"] = new[] { "bbop" },
            ["kb"] = new[] { "bbvo", "exd", "bbvl", "bbvz", "eve", "evm", "Init", "k1eba8d5a73c94e5ca750525c90233eef" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["oq"] = new[] { "bdra", "gae" },
            ["or"] = new[] { "bdrd", "bdre", "bdrg", "bdrh", "bdri", "bdrj" },
            ["oa"] = new[] { "bdkb" },
            ["bdx"] = new[] { "bhpo" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["byv"] = new[] { "rqf", "rqp" },
            ["byt"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["yl"] = new[]
            {
                "hp", "maxHp", "authorityId", "jed", "jek", "localRoleBuffDic", "jhg", "jgi", "jdc", "braf", "brai", "bqzk", "brvu", "roleName", "bqzh", "bfse", "jdd", "brag", "roleType",
                "henShinSkillId", "timer", "jgd", "jge", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "brah", "bqzy", "bqzx", "bqzw", "bqzj", "bqzt", "bqzu", "bqzs", "bqzz", "brad", "bqzg", "jes", "jea",
                "localActiveSkillCastEvent", "iys", "izo", "roleBuffManager", "jed",
                "jcu", "jct", "jfu", "jhe", "jgh", "jgf", "jdb", "jda", "jde", "overrideAnimSkillId",
            },
            ["bbx"] = new[]
            {
                "heroType", "lmq", "brfz", "bgxu", "bgxv", "lbu", "lbt", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "ldr", "k6f41850941404079a07256c13d65099e", "kad2a3d5a59724b85bfc565c5f7d7246e",
                "lbw", "kvv", "CmdPickItem", "playerAttribute", "roleSkillList", "leq", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "lbx", "lbz", "CmdAddBuff", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "ixz", "jee", "CmdCreateSkill",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "rcs", "broa" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["pa"] = new[] { "bdul", "git" },
            ["pb"] = new[] { "bduq", "bdur", "bdus", "bduu" },
            ["bbq"] = new[] { "kry", "bgsi", "bgsj", "bgsl", "ksh", "kf96f68101a514b2b881d72b87bf2b1f2" },
            ["AudioManager"] = new[] { "dcy" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["brx"] = new[] { "pmi", "bayx" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["bez"] = new[] { "mun" },
            ["cla"] = new[] { "bopw", "bopp" },
            ["ckm"] = new[] { "brvy", "bomw" },
            ["xy"] = new[] { "isBoss", "isElite" },
            ["ov"] = new[] { "bdsj", "bdsh", "bdsf", "bdsg", "gfq", "gfo", "gey", "bdsi", "bdsw" },
            ["nv"] = new[] { "bdbv" },
            ["cx"] = new[] { "bauu" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["du"] = new[] { "dst", "bazk" },
            ["cl"] = new[] { "dhh", "dhi", "barb" },
            // 自定义被动
            ["bbn"] = new[] { "kpq" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["oc"] = new[] { "bdkk" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["pr"] = new[] { "gnb", "gnf" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["m"] = new[] { "dap", "bale", "balf" },
            // 施法动作
            ["nw"] = new[] { "bdcs" },
            // 自定义主动技能
            ["bga"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "booz" },
            ["bis"] = new[] { "nkj" },
            // 嵌套类型 de.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["de"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["bqz"] = new[] { "bksw", "brlk", "pgj" },
            ["bra"] = new[] { "bkuo", "bkuq", "bkun", "pix", "bkup" },
            ["bir"] = new[]
            {
                "bixe", "bixy", "bixv", "bixg", "bixd", "brjw", "k18d428a62c6a4a89a1c9e898b51c307d", "k3b58ed391382412ca53e330e0cb7d426",
                "kdcc53ba24803444e8ab688e8fc3b2bf0", "k3d8a1569d86242c188efa8b7ab5991f0", "k475dbd72b9da4163aacdb1a9171b584b",
                "kcb5414097f184c63aec73da2482ce6f4", "k3ff90762c3e740f1ba56b4c49d492645", "bixj", "bixp", "bixq", "biyf",
                "k1a61433033fa4ec8a46cdab5c5e5e6dc", "guz", "nkf",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["bfp"] = new[] { "myy" },
            ["Buff无敌"] = new string[0],
            ["wi"] = new[] { "hek", "hem", "bquu" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["bmc"] = new[] { "bayx" },
            ["blv"] = new[] { "ocr" },
            ["PlayerDamageFeedbackKind"] = new string[0],
            ["RoleModeBase"] = new[] { "addRange", "ijk", "myAnim" },
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
            ["bwg"] = new[] { "qrp", "qrh", "qri", "qrc", "qra", "qsw" },
            ["bwm"] = new[] { "qts" },
            ["bwr"] = new[] { "qvl", "qvm", "brmy", "qvg" },
            ["bwl"] = new[] { "blzt" },
            ["bwj"] = new[] { "qtd" },
            ["bhq"] = new[] { "nhd" },
            ["bcj"] = new[] { "mdg" },
            ["bwo"] = new[] { "quf" },
            // 皮肤同步：Steam 大厅成员数据
            ["py"] = new[] { "bqul" },
            ["bz"] = new[] { "dfv" },
            ["chv"] = new[] { "umx", "umw", "ump", "umq", "uni" },
            ["cik"] = new[] { "vbh" },
            ["WorkshopItemStatus"] = new[] { "publishedFileId", "installed", "heroId", "installFolder" },
            ["LocalWorkshopManifest"] = new[] { "heroId", "title", "heroName", "heroIcon", "author", "contentType", "bundleRelativePath", "bundleAssetName" },
        };

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
            var ez = AccessTools.TypeByName("ey");
            if (ez != null)
            {
                CheckSignature(ez, "eai", new[] { "ActiveSkillEnum", "bbx", "Int32" }, "Boolean", missing);
                CheckSignature(ez, "eaj", new[] { "bbx", "ActiveSkillData" }, "yl", missing);
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
