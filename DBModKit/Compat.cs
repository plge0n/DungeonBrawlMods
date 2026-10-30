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
        public static readonly string[] TestedVersions = { "7.10" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["nr"] = new[] { "bqtl", "bcqj", "bcql", "bcrn", "bcre", "bcqz", "bcrr", "fuy", "fvq", "MaxSkillNum", "MaxEquipNum" },
            ["oz"] = new[] { "ggq", "bdub", "bduc" },
            ["cj"] = new[] { "bqpk", "bqpr", "bqps", "bqpu", "bqpw", "bqpz", "bqqc" },
            ["ey"] = new[] { "eai", "eaj", "eav", "dxx", "edm", "edp", "ebn", "eap", "eaq", "edk", "eds", "ebz", "eae", "eab", "eag", "eaw", "edw", "ecp", "edt" },
            ["bgg"] = new[] { "ndk" },
            ["pt"] = new[] { "bdxz", "bdya", "bdyb", "bdyl", "gqj" },
            ["bfz"] = new[] { "bify", "bifz", "bigb", "bifx", "bigc", "koz", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["qw"] = new[] { "gur", "gus" },
            ["kh"] = new[] { "bcac", "bcad", "bcae", "bcaf", "bcag", "Init", "Clear", "eyx", "ezn", "ezo" },
            ["lq"] = new string[0],
            ["rv"] = new[] { "behn", "behp", "beht", "behu", "behw", "gzb", "gzc", "gzd", "gzf" },
            ["hn"] = new[] { "bboo" },
            ["kb"] = new[] { "bbvn", "exd", "bbvk", "bbvy", "eve", "evm", "Init", "k364fd131f3d047c3bede3e47a20043d8" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["oq"] = new[] { "bdqz", "gae" },
            ["or"] = new[] { "bdrc", "bdrd", "bdrf", "bdrg", "bdrh", "bdri" },
            ["oa"] = new[] { "bdka" },
            ["bdx"] = new[] { "bhpn" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["byv"] = new[] { "rqf", "rqp" },
            ["byt"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["yl"] = new[]
            {
                "hp", "maxHp", "authorityId", "jed", "jek", "localRoleBuffDic", "jhg", "jgi", "jdc", "brae", "brah", "bqzj", "brvt", "roleName", "bqzg", "bfsd", "jdd", "braf", "roleType",
                "henShinSkillId", "timer", "jgd", "jge", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "brag", "bqzx", "bqzw", "bqzv", "bqzi", "bqzs", "bqzt", "bqzr", "bqzy", "brac", "bqzf", "jes", "jea",
                "localActiveSkillCastEvent", "iys", "izo", "roleBuffManager", "jed",
                "jcu", "jct", "jfu", "jhe", "jgh", "jgf", "jdb", "jda", "jde", "overrideAnimSkillId",
            },
            ["bbx"] = new[]
            {
                "heroType", "lmq", "brfy", "bgxt", "bgxu", "lbu", "lbt", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "ldr", "kcd1434482c4243539d912d2120fb1ecb", "kf31f08cac8024a2f8d1ba29c9ce932ab",
                "lbw", "kvv", "CmdPickItem", "playerAttribute", "roleSkillList", "leq", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "lbx", "lbz", "CmdAddBuff", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "ixz", "jee", "CmdCreateSkill",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "rcs", "brnz" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["pa"] = new[] { "bduk", "git" },
            ["pb"] = new[] { "bdup", "bduq", "bdur", "bdut" },
            ["bbq"] = new[] { "kry", "bgsh", "bgsi", "bgsk", "ksh", "kd7e673b0e9f14b83a57f059e77d69d55" },
            ["AudioManager"] = new[] { "dcy" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["brx"] = new[] { "pmi", "bayw" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["bez"] = new[] { "mun" },
            ["cla"] = new[] { "bopv", "bopo" },
            ["ckm"] = new[] { "brvx", "bomv" },
            ["xy"] = new[] { "isBoss", "isElite" },
            ["ov"] = new[] { "bdsi", "bdsg", "bdse", "bdsf", "gfq", "gfo", "gey", "bdsh", "bdsv" },
            ["nv"] = new[] { "bdbu" },
            ["cx"] = new[] { "baut" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["du"] = new[] { "dst", "bazj" },
            ["cl"] = new[] { "dhh", "dhi", "bara" },
            // 自定义被动
            ["bbn"] = new[] { "kpq" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["oc"] = new[] { "bdkj" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["pr"] = new[] { "gnb", "gnf" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["m"] = new[] { "dap", "bald", "bale" },
            // 施法动作
            ["nw"] = new[] { "bdcr" },
            // 自定义主动技能
            ["bga"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "booy" },
            ["bis"] = new[] { "nkj" },
            // 嵌套类型 de.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["de"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["bqz"] = new[] { "bksv", "brlj", "pgj" },
            ["bra"] = new[] { "bkun", "bkup", "bkum", "pix", "bkuo" },
            ["bir"] = new[]
            {
                "bixd", "bixx", "bixu", "bixf", "bixc", "brjv", "kc3df4b8421d940c2a169a70224dc3bc4", "k46e8465094a44a9a8b78e714ec584574",
                "ka54fe1e95a174ae8b02abb4afa9b2a8e", "ka1916cecf94642839bd343cea3cecec4", "k4df8b6c79c054a2c84b72dea10972ca6",
                "k9d7799ba870f427aa03e5c645f1f401b", "k7354c2c6716d49f0949b993ca4bef1d2", "bixi", "bixo", "bixp", "biye",
                "k664412dcf08a4debb919b10dc9792f15", "guz", "nkf",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["bfp"] = new[] { "myy" },
            ["Buff无敌"] = new string[0],
            ["wi"] = new[] { "hek", "hem", "bqut" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["bmc"] = new[] { "bayw" },
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
            ["bwr"] = new[] { "qvl", "qvm", "brmx", "qvg" },
            ["bwl"] = new[] { "blzs" },
            ["bwj"] = new[] { "qtd" },
            ["bhq"] = new[] { "nhd" },
            ["bcj"] = new[] { "mdg" },
            ["bwo"] = new[] { "quf" },
            // 皮肤同步：Steam 大厅成员数据
            ["py"] = new[] { "bquk" },
            ["bz"] = new[] { "dfv" },
            ["chv"] = new[] { "umw", "umv", "umo", "ump", "unh" },
            ["cik"] = new[] { "vbg" },
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
