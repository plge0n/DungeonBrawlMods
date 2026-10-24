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
        public static readonly string[] TestedVersions = { "7.02" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["nr"] = new[] { "bpwy", "bcep", "bcer", "bcft", "bcfk", "bcff", "bcfx", "fsw", "fto", "MaxSkillNum", "MaxEquipNum" },
            ["oy"] = new[] { "gei", "bdhx", "bdhy" },
            ["cj"] = new[] { "bpta", "bpth", "bpti", "bptk", "bptm", "bptp", "bpts" },
            ["ez"] = new[] { "dyp", "dyq", "dzc", "dwe", "ebt", "ebw", "dzu", "dyw", "dyx", "ebr", "ebz", "eag", "dyl", "dyi", "dyn", "dzd", "ecd", "eaw", "eca" },
            ["bfz"] = new[] { "mwh" },
            ["pr"] = new[] { "bdlu", "bdlv", "bdlw", "bdmg", "goa" },
            ["bfs"] = new[] { "bhpj", "bhpk", "bhpm", "bhpi", "bhpn", "klv", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["qv"] = new[] { "gsi", "gsj" },
            ["kh"] = new[] { "bboi", "bboj", "bbok", "bbol", "bbom", "Init", "Clear", "ewv", "exl", "exm" },
            ["lq"] = new string[0],
            ["rt"] = new[] { "bdvb", "bdvd", "bdvh", "bdvi", "bdvk", "gwp", "gwq", "gwr", "gwt" },
            ["ho"] = new[] { "bbcv" },
            ["kb"] = new[] { "bbjt", "evb", "bbjq", "bbke", "etc", "etk", "Init", "keb1c74cb4d1e42a6ad62f334036b5380" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["op"] = new[] { "bdev", "fxz" },
            ["oq"] = new[] { "bdey", "bdez", "bdfb", "bdfc", "bdfd", "bdfe" },
            ["oa"] = new[] { "bcyd" },
            ["bdq"] = new[] { "bgyy" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["bxl"] = new[] { "rfj", "rft" },
            ["bxj"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["yh"] = new[]
            {
                "hp", "maxHp", "authorityId", "jbf", "jbm", "localRoleBuffDic", "jeg", "jdi", "jae", "bqdr", "bqdu", "bqcw", "bqyf", "roleName", "bqct", "bffm", "jaf", "bqds", "roleType",
                "henShinSkillId", "timer", "jdd", "jde", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bqdt", "bqdk", "bqdj", "bqdi", "bqcv", "bqdf", "bqdg", "bqde", "bqdl", "bqdp", "bqcs", "jbu", "jbc",
                "localActiveSkillCastEvent", "ivu", "iwq", "roleBuffManager", "jbf",
                "izw", "izv", "jcu", "jee", "jdh", "jdf", "jad", "jac", "jag", "overrideAnimSkillId",
            },
            ["bbr"] = new[]
            {
                "heroType", "lio", "bqjh", "bgkq", "bgkr", "kyh", "kyg", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "lac", "kc3544e0533d74eb099526ef9f18e96ac", "ka840cd1e920444e3b6ab4a286d305e16",
                "kyj", "ksj", "CmdPickItem", "playerAttribute", "roleSkillList", "lba", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "kyk", "kym", "CmdAddBuff", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "ivb", "jbg", "CmdCreateSkill",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "qsj", "bqqm" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["oz"] = new[] { "bdig", "ggk" },
            ["pa"] = new[] { "bdik", "bdil", "bdim", "bdio" },
            ["bbl"] = new[] { "koq", "bgfm", "bgfn", "bgfp", "koz", "k29787416a8ee4df1803796f14d43d212" },
            ["AudioManager"] = new[] { "dbn" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["bqq"] = new[] { "pcb", "bano" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["bes"] = new[] { "mnh" },
            ["cjp"] = new[] { "bnti", "bntb" },
            ["cjb"] = new[] { "bqyj", "bnqk" },
            ["xu"] = new[] { "isBoss", "isElite" },
            ["ou"] = new[] { "bdge", "bdgc", "bdga", "bdgb", "gdj", "gdh", "gcr", "bdgd", "bdgr" },
            ["nv"] = new[] { "bcqa" },
            ["cx"] = new[] { "bajk" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["du"] = new[] { "dri", "baob" },
            ["cl"] = new[] { "dfw", "dfx", "bafr" },
            // 自定义被动
            ["bbi"] = new[] { "kmm" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["oc"] = new[] { "bcym" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["pq"] = new[] { "gks", "gkw" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["m"] = new[] { "cze", "zzu", "zzv" },
            // 施法动作
            ["nw"] = new[] { "bcqx" },
            // 自定义主动技能
            ["bft"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "bnsl" },
            ["bij"] = new[] { "ndc" },
            // 嵌套类型 de.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["de"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["bps"] = new[] { "bjye", "bqnw", "owc" },
            ["bpt"] = new[] { "bjzw", "bjzy", "bjzv", "oyq", "bjzx" },
            ["bii"] = new[]
            {
                "bigi", "bihc", "bigz", "bigk", "bigh", "bqmn", "kf027782612bc462da70312d68cecc52e", "k736f9d25b9e14a858ca4e799ff482ed9",
                "k4fa7bfc3bee94949a1e3fafe49f6a90b", "ke4ed491c02044a32beafe70b37115652", "kaa9e7d5c7e8c45fba32a2a2dddfc87a1",
                "kc70826deba7d4bcba786b95dfec058fc", "k7d9fe565baad4f10ba49ca4558bf2990", "bign", "bigt", "bigu", "bihj",
                "k9f648b740d1345f6b38d3f2d2842fe87", "gsq", "ncy",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["bfi"] = new[] { "mrr" },
            ["Buff无敌"] = new string[0],
            ["wh"] = new[] { "hby", "hca", "bpyg" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["bkv"] = new[] { "bano" },
            ["bko"] = new[] { "nsi" },
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
            ["buz"] = new[] { "qhg", "qgy", "qgz", "qgt", "qgr", "qin" },
            ["bvf"] = new[] { "qjj" },
            ["bvk"] = new[] { "qlc", "qld", "bqpk", "qkx" },
            ["bve"] = new[] { "bley" },
            ["bvc"] = new[] { "qiu" },
            ["bhh"] = new[] { "mzw" },
            ["bcd"] = new[] { "lxg" },
            ["bvh"] = new[] { "qjw" },
            // 皮肤同步：Steam 大厅成员数据
            ["px"] = new[] { "bpxx" },
            ["bz"] = new[] { "dej" },
            ["cgk"] = new[] { "ubo", "ubn", "ubg", "ubh", "uby" },
            ["cgz"] = new[] { "upv" },
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
