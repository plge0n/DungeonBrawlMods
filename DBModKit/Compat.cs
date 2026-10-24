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
        public static readonly string[] TestedVersions = { "6.45" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["nn"] = new[] { "bpbu", "bbsr", "bbst", "bbtv", "bbtm", "bbth", "bbtz", "fqr", "frk", "MaxSkillNum", "MaxEquipNum" },
            ["os"] = new[] { "gba", "bctl", "bctm" },
            ["cj"] = new[] { "boyb", "boyi", "boyj", "boyl", "boyn", "boyq", "boyt" },
            ["ez"] = new[] { "dxx", "dxy", "dyk", "dvm", "eaz", "ebc", "dzc", "dye", "dyf", "eax", "ebf", "dzo", "dxt", "dxq", "dxv", "dyl", "ebj", "eae", "ebg" },
            ["bfl"] = new[] { "mna" },
            ["pj"] = new[] { "bcxi", "bcxj", "bcxk", "bcxu", "gko" },
            ["bfe"] = new[] { "bgwf", "bgwg", "bgwi", "bgwe", "bgwj", "kea", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["qp"] = new[] { "gou", "gov" },
            ["ke"] = new[] { "bbcl", "bbcm", "bbcn", "bbco", "bbcp", "Init", "Clear", "euv", "evl", "evm" },
            ["lm"] = new string[0],
            ["rn"] = new[] { "bdgp", "bdgr", "bdgv", "bdgw", "bdgy", "gtb", "gtc", "gtd", "gtf" },
            ["hm"] = new[] { "baqz" },
            ["jy"] = new[] { "baxw", "etb", "baxt", "bayh", "erc", "erk", "Init", "k9326fabeadf343ea92814a2f3df04aac" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["oj"] = new[] { "bcqp", "fuv" },
            ["ok"] = new[] { "bcqs", "bcqt", "bcqv", "bcqw", "bcqx", "bcqy" },
            ["nw"] = new[] { "bcmf" },
            ["bdc"] = new[] { "bgfu" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["bwu"] = new[] { "quj", "qut" },
            ["bws"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["yb"] = new[]
            {
                "hp", "maxHp", "authorityId", "iwk", "iwr", "localRoleBuffDic", "izk", "iyn", "ivj", "bphx", "bpia", "bphc", "bqbx", "roleName", "bpgz", "beqd", "ivk", "bphy", "roleType",
                "henShinSkillId", "timer", "iyi", "iyj", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bphz", "bphq", "bphp", "bpho", "bphb", "bphl", "bphm", "bphk", "bphr", "bphv", "bpgy", "iwz", "iwh",
                "localActiveSkillCastEvent", "iqz", "irv", "roleBuffManager", "iwk",
                "ivb", "iva", "ixz", "izi", "iym", "iyk", "ivi", "ivh", "ivl", "overrideAnimSkillId",
            },
            ["bbe"] = new[]
            {
                "heroType", "kzz", "bpnc", "bfru", "bfrv", "kqi", "kqh", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "ksd", "k4a25de8c63fe4a7192613346ae51af4e", "kfd63a19d353a42878846361e6af4f637",
                "kqk", "kkl", "CmdPickItem", "playerAttribute", "roleSkillList", "ktb", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "kql", "kqn", "CmdAddBuff", "CmdAddNearbyHumanityShield",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "qhu", "bpue" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["ot"] = new[] { "bctu", "gdb" },
            ["ou"] = new[] { "bcty", "bctz", "bcua", "bcuc" },
            ["bba"] = new[] { "kgw", "bfmz", "bfna", "bfnc", "khf", "kb163cadd39ce47f38866dd8c2caf5f3c" },
            ["AudioManager"] = new[] { "daw" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["bqa"] = new[] { "osj", "bacf" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing" },
            ["bee"] = new[] { "mec" },
            ["ciy"] = new[] { "bmyj", "bmyc" },
            ["cik"] = new[] { "bqcb", "bmvl" },
            ["xo"] = new[] { "isBoss", "isElite" },
            ["oo"] = new[] { "bcry", "bcrw", "bcru", "bcrv", "gaf", "gad", "fzn", "bcrx", "bcsl" },
            ["nr"] = new[] { "bcec" },
            ["cx"] = new[] { "zyc" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["du"] = new[] { "dqr", "bact" },
            ["cl"] = new[] { "dff", "dfg", "zuj" },
            // 自定义被动
            ["bax"] = new[] { "ker" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["ny"] = new[] { "bcmo" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["pi"] = new[] { "ghk", "gho" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["m"] = new[] { "cyn", "zop", "zoq" },
            // 施法动作
            ["ns"] = new[] { "bcez" },
            // 自定义主动技能
            ["bff"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "bmxm" },
            ["bhu"] = new[] { "mty" },
            // 嵌套类型 de.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["de"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["bpe"] = new[] { "bjes", "bprr", "omt" },
            ["bpf"] = new[] { "bjgk", "bjgm", "bjgj", "opi" },
            ["bht"] = new[]
            {
                "bhne", "bhny", "bhnv", "bhng", "bhnd", "bpqi", "k896ff3548b31472b8b2cf8f649c25e65", "kacb7d091b5904139a602f04ed9552242",
                "k4b2ca10ddec34cdb90bf84a3f664f727", "kbb80bea445ac47778d36b05d19be7f37", "kcac92c2266be41d3979b385715ce318e",
                "kb7f88a1b0a6e4124b9b27921f2778742", "k443b7b692a1648099b5e5890ad35ed6d", "bhnj", "bhnp", "bhnq", "bhof",
                "kb03f1a3bfdcd4ea8aecbd90f39216246", "gpc",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["beu"] = new[] { "mim" },
            ["Buff无敌"] = new string[0],
            ["wb"] = new[] { "gyk", "gym", "bpcw" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["bkh"] = new[] { "bacf" },
            ["bka"] = new[] { "njb" },
            ["PlayerDamageFeedbackKind"] = new string[0],
            ["RoleModeBase"] = new[] { "addRange", "ico", "myAnim" },
            ["ActiveSkillData"] = new[]
            {
                "tags", "indicator", "castingRange", "range", "cd", "canAuto", "icon",
                "damageBase", "damageValue", "cost", "duration", "interval", "chargingNum", "chargingCd",
            },
            ["ActiveSkillEnum"] = new[] { "None", "D_Execution", "S_Execution", "D_Alchemy", "S_Alchemy", "Bite", "ChickenDance", "RealChickenDance" },
            ["SkillTag"] = new[] { "Heal", "Revive", "Shield", "Defense", "Buff", "Summon", "Aura", "Area", "Move", "Control", "Henshin", "Duration" },
            // 英雄皮肤
            ["buj"] = new[] { "pwu", "pwm", "pwn", "pwh", "pwf", "pyb" },
            ["bup"] = new[] { "pyx" },
            ["buu"] = new[] { "qaq", "qar", "bptc", "qal" },
            ["buo"] = new[] { "bkkn" },
            ["bum"] = new[] { "pyi" },
            ["bgt"] = new[] { "mqp" },
            ["bbp"] = new[] { "lob" },
            ["bur"] = new[] { "pzk" },
            // 皮肤同步：Steam 大厅成员数据
            ["pq"] = new[] { "bpcn" },
            ["bz"] = new[] { "ddr" },
            ["cft"] = new[] { "tqj", "tqi", "tqb", "tqc", "tqt" },
            ["cgi"] = new[] { "uep" },
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
                CheckSignature(ez, "dxx", new[] { "ActiveSkillEnum", "bbe", "Int32" }, "Boolean", missing);
                CheckSignature(ez, "dxy", new[] { "bbe", "ActiveSkillData" }, "yb", missing);
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
