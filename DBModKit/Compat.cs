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
            ["nn"] = new[] { "bpbu", "bbsr", "bbtv", "bbtm", "bbth", "bbtz", "fqr", "frk" },
            ["os"] = new[] { "gba", "bctl", "bctm" },
            ["cj"] = new[] { "boyb", "boyi", "boyj", "boyl", "boyn", "boyq", "boyt" },
            ["ez"] = new[] { "dxx", "dxy", "dyk", "dvm", "eaz", "ebc" },
            ["bfl"] = new[] { "mna" },
            ["pj"] = new[] { "bcxi" },
            ["bfe"] = new[] { "bgwf", "bgwg", "bgwi" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["oj"] = new[] { "bcqp" },
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
                "hp", "maxHp", "authorityId", "iwk", "iwr", "izk", "iyn", "ivj", "bphx", "bpia", "bphc", "bqbx", "roleName", "bpgz", "beqd", "ivk", "bphy", "roleType",
                "henShinSkillId", "timer", "iyi", "iyj", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bphz", "bphq", "bphp", "bpho", "bphb", "bphl", "bphm", "bphk", "bphr", "bphv", "bpgy", "iwz", "iwh",
            },
            ["bbe"] = new[]
            {
                "heroType", "kzz", "bpnc", "bfru", "bfrv", "kqi", "kqh", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "ksd", "k4a25de8c63fe4a7192613346ae51af4e", "kfd63a19d353a42878846361e6af4f637",
                "kqk", "kkl", "CmdPickItem", "playerAttribute", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "qhu" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade" },
            // 掉落物拾取
            ["ot"] = new[] { "bctu", "gdb" },
            ["ou"] = new[] { "bcty", "bctz", "bcua", "bcuc" },
            ["bba"] = new[] { "kgw" },
            ["AudioManager"] = new[] { "daw" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["bqa"] = new[] { "osj", "bacf" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["ciy"] = new[] { "bmyj", "bmyc" },
            ["cik"] = new[] { "bqcb" },
            ["xo"] = new[] { "isBoss", "isElite" },
            ["oo"] = new[] { "bcry", "bcrw", "bcru", "bcrv", "gaf", "gad" },
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
            ["cl"] = new[] { "dff" },
            ["bpe"] = new[] { "bjes", "bprr" },
            ["bpf"] = new[] { "bjgk", "bjgm" },
            ["bht"] = new[]
            {
                "bhne", "bhny", "bhnv", "bhng", "bhnd", "bpqi", "k896ff3548b31472b8b2cf8f649c25e65", "kacb7d091b5904139a602f04ed9552242",
                "k4b2ca10ddec34cdb90bf84a3f664f727", "kbb80bea445ac47778d36b05d19be7f37", "kcac92c2266be41d3979b385715ce318e",
            },
            ["RoleModeBase"] = new[] { "addRange", "ico" },
            ["ActiveSkillData"] = new[]
            {
                "tags", "indicator", "castingRange", "range", "cd", "canAuto", "icon",
                "damageBase", "damageValue", "cost", "duration", "interval", "chargingNum", "chargingCd",
            },
            ["ActiveSkillEnum"] = new[] { "None", "D_Execution", "S_Execution", "D_Alchemy", "S_Alchemy", "Bite" },
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
            ["cft"] = new[] { "tqj", "tqi", "tqb", "tqc" },
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
