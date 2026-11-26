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
        public static readonly string[] TestedVersions = { "7.20" };

        // 类型名 → Mod 用到的成员（字段/属性/方法，含继承自基类的）
        private static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            ["vg"] = new[] { "bznz", "bjhv", "bjhx", "bjiz", "bjiq", "bjil", "bjjd", "khw", "kip", "MaxSkillNum", "MaxEquipNum" },
            ["ww"] = new[] { "kyb", "bkob", "bkoc" },
            ["cl"] = new[] { "bzaf", "bzam", "bzan", "bzap", "bzar", "bzau", "bzax" },
            ["fh"] = new[] { "eqw", "eqx", "ern", "eol", "eue", "euh", "esf", "erg", "eri", "euc", "euk", "esr", "eqs", "eqp", "equ", "ero", "eup", "eth", "eul" },
            ["bps"] = new[] { "qtk" },
            ["xy"] = new[] { "bkuc", "bkue", "bkuf", "bkup", "ljp" },
            ["fx"] = new[] { "bfwq" },
            ["bpl"] = new[] { "cacp", "bppa", "bppc", "bpox", "bppd", "UpdateSkill", "Clear" },
            // 伤害来自哪个被动 / 装备 / 遗物 / Buff
            ["ze"] = new[] { "loy", "Exit" },
            ["ri"] = new[] { "birg", "birh", "biri", "birj", "birk", "Init", "Clear", "jlr", "jmh", "jmi" },
            ["tc"] = new string[0],
            ["bad"] = new[] { "blfh", "blfj", "blfn", "blfo", "blfq", "irz", "Exit", "lux", "luz", "blfe", "blff", "blfi", "blfp", "lwd", "lwe" },
            ["oo"] = new[] { "bifp" },
            ["rc"] = new[] { "bimr", "jjx", "biml", "binc", "jhy", "jig", "Init", "k384ecdc917984358a194f3af1209d5a4", "bimp", "bint", "k9a844fca9bdd4cbbba7fa481f2600c7f", "k48ef58e0be174fa98cbd9d2c63c29c9a", "kfe8392ba23334d98b27ca6240ad40435" },
            ["AttackType"] = new[] { "Normal", "Skill", "Buff", "AttackEffect", "TrueDamage" },
            ["DamageSourceKind"] = new[] { "Default", "ActiveSkill", "Aura", "Status", "Reflect", "PassiveProc", "IllusoryWeapon" },
            ["wi"] = new[] { "bkib", "kmx" },
            ["wj"] = new[] { "bkie", "bkif", "bkih", "bkii", "bkij", "bkik" },
            ["vp"] = new[] { "bkap" },
            ["bni"] = new[] { "boyl", "qco" },
            ["ProgressCombatFact"] = new[] { "Amount", "ShieldDamage", "SkillId", "Active", "Critical", "Normal" },
            ["RoleState"] = new[] { "Attack", "Dead" },
            ["RoleType"] = new[] { "King", "Summon" },
            ["cky"] = new[] { "vjp", "vjz" },
            ["ckw"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["ga"] = new[]
            {
                "hp", "maxHp", "authorityId", "fgp", "fgw", "localRoleBuffDic", "fkd", "fja", "ffk", "bzeu", "bzex", "bzdz", "cary", "roleName", "bzdw", "bfzm", "ffl", "bzev", "roleType",
                "henShinSkillId", "timer", "fiv", "fiw", "isCheckAttack",
                "enemyDefeatedEvent", "activeSkillCastEvent", "criticalEvent", "dogeEvent",
                "bzew", "bzen", "bzem", "bzel", "bzdy", "bzei", "bzej", "bzeh", "bzeo", "bzes", "bzdv", "fhe", "fgm",
                "localActiveSkillCastEvent", "fat", "fbp", "roleBuffManager", "fgp",
                "ffa", "fey", "fij", "fjz", "fiz", "fix", "ffj", "ffi", "ffm", "overrideAnimSkillId",
            },
            ["ix"] = new[]
            {
                "heroType", "hck", "bzjd", "bgtw", "bgtx", "gqk", "AttackUpdate", "addDamagePercent", "addBossEnemy", "addEliteEnemy", "addNormalEnemy",
                "gsr", "k086c96c3900942ca92eb423bbcd4ed05", "kf97a6a32e2324165add9a2ccac885fe8",
                "gqn", "gkn", "CmdPickItem", "playerAttribute", "roleSkillList", "gtq", "onPlayerLevelUp", "onPlayerRelife", "addBagItemEvent",
                "gqp", "gqs", "grx", "CmdAddNearbyHumanityShield", "onTeammateLevelUp", "CmdCreateItemByPosWithNum",
                "dieEvent", "faa", "fgq", "CmdCreateSkill", "gqt", "gqq",
            },
            ["EnemyDefeatContext"] = new[] { "IsBoss", "IsElite" },
            ["MyServerNetworkManager"] = new[] { "hvi", "bzks" },
            ["ClientNetMessage"] = new[] { "clientNetOperation" },
            ["ClientNetOperation"] = new[] { "OnStartGame", "OnStartRest", "EnemyEnterTip", "OnGameOver" },
            ["RemainsData"] = new[] { "grade", "conditions", "isUnique", "isTeam", "runAcquireLimit", "mapTypes" },
            // 掉落物拾取
            ["wx"] = new[] { "bkor", "law" },
            ["wy"] = new[] { "bkox", "bkoy", "bkoz", "bkpb" },
            ["bla"] = new[] { "pfc", "bnzo", "bnzp", "bnzt", "pfp", "k57f806ca95414585baf602b50479519d", "kbef2ac46a59c4e24b4db3c25d86dc1f7", "k13a4bd9a0793417a9795a531acac0a86" },
            ["AudioManager"] = new[] { "drt" },
            ["ItemType"] = new[] { "Gold", "Gem", "STRBook", "Active_Book_D", "Passsive_Book_D", "GoldCoinBag_LV1", "Talisman_Roar", "Card_0" },
            ["ccx"] = new[] { "ths", "bfpn" },
            ["RoguelikeUIData"] = new[] { "name", "dec", "icon", "quality", "data" },
            ["GoldIncomeSource"] = new[] { "Gameplay" },
            ["LocalBuffType"] = new[] { "SacredEmberBlessing", "SkillHaste", "IronArmor" },
            ["bol"] = new[] { "qkn" },
            ["czv"] = new[] { "bxar", "bxak" },
            ["czi"] = new[] { "casc", "bwxt" },
            ["bfy"] = new[] { "isBoss", "isElite" },
            ["wn"] = new[] { "bkjl", "bkjj", "bkjh", "bkji", "ksr", "ksp", "krz", "bkjk", "bkjy" },
            ["vk"] = new[] { "bjsj" },
            ["cz"] = new[] { "bfko" },
            ["HeroType"] = new string[0],
            ["EnemyType"] = new string[0],
            ["EnemySpawnKind"] = new string[0],
            ["RoleAttribute"] = new[]
            {
                "hp", "mp", "attackPower", "armor", "STR", "STRadd", "AGI", "AGIadd", "STA", "STAadd",
                "attackSpeed", "moveSpeed", "hpRecover", "mpRecover", "shiled",
            },
            ["ea"] = new[] { "ehy", "bfpv" },
            ["cn"] = new[] { "dwb", "dwc", "bfgr" },
            // 自定义被动
            ["bkm"] = new[] { "ozf" },
            ["MirrorBossPlayerSnapshot"] = new[] { "skills", "player", "relics" },
            // 自定义遗物
            ["MirrorBossRelicSource"] = new[] { "relic" },
            ["ItemData"] = new[] { "name", "quality", "model" },
            ["EntryConditions"] = new[] { "None" },
            ["RelicValueType"] = new[] { "Normal", "Percent", "PercentConst", "InversePercent" },
            ["vr"] = new[] { "bkay" },
            ["MirrorBossSkillSource"] = new[] { "activeSkill", "skillName" },
            ["PasssiveSkillEnum"] = new string[0],
            // 自定义装备（jy、ke、bba、oo、ez、de、cl、bpe、nn 在别的条目里）
            ["xt"] = new[] { "lfx", "lgb" },
            ["EquipSkillType"] = new[] { "None" },
            // Mod 资源
            ["u"] = new[] { "dpj", "bfav", "bfaw" },
            // 施法动作
            ["vl"] = new[] { "bjtg" },
            // 自定义主动技能
            ["bpm"] = new string[0],
            ["Mirror.NetworkManager"] = new[] { "bwzu" },
            ["bse"] = new[] { "qze" },
            // 嵌套类型 dj.PlayerKingData / PlayerKingSkillData 的名字没混淆，按成员检查（映射重建工具不认 "de+X" 这种写法）
            ["dj"] = new[] { "PlayerKingData", "PlayerKingSkillData", "PlayerKingRelicData", "PlayerKingEquipData" },
            ["cby"] = new[] { "bsme", "cafp", "tcd", "tbx" },
            ["cbz"] = new[] { "bsnz", "bsob", "bsny", "ter", "bsoa" },
            ["bsd"] = new[]
            {
                "bqhf", "bqhz", "bqhw", "bqhh", "bqhe", "cacw", "k1f3c659b204e4fb0b28f58c32735dde2", "k6a1f5c00f61e4d76bef7937f76b20f91",
                "kfe7b937f25de4b41a84fd1a00e379d5a", "k22ed8c083d094dc3bb551d1207914338", "k18d8aa6ca6314cafa81a01dddcc8d2a6",
                "k3a1552bf0f2148cc86f7e51efe78ced0", "kf54fad8c4863494988d45d2f2530a6b7", "bqhk", "bqhq", "bqhr", "bqig",
                "kb0f1e4ff12f84085b889d231237da08f", "lpf", "qza", "bqhg",
            },
            // 英雄专属技能：buff、重试波次、波次配置、开场界面（被动基类 qp、bff、AttackType、DamageSourceKind 在上面，
            // 字典初始化器里重复的键会让后一条覆盖前一条，这里不能再写一遍）
            ["bpb"] = new[] { "qow" },
            ["Buff无敌"] = new string[0],
            ["bef"] = new[] { "mai", "mak", "bzqg" },
            ["SOSpawnConfig"] = new[] { "enemySpawnData" },
            ["RoguelikeEventType"] = new[] { "Roguelike" },
            ["bxb"] = new[] { "bfpn" },
            ["bwu"] = new[] { "rzu" },
            ["PlayerDamageFeedbackKind"] = new string[0],
            ["RoleModeBase"] = new[] { "addRange", "nfd", "myAnim" },
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
            ["cjh"] = new[] { "uwe", "uvw", "uvx", "uvq", "uvo", "uxl" },
            ["cjn"] = new[] { "uyh" },
            ["cjs"] = new[] { "vaa", "vab", "caiq", "uzv" },
            ["cjm"] = new[] { "bufd" },
            ["cjk"] = new[] { "uxs" },
            ["brd"] = new[] { "qwm" },
            ["blq"] = new[] { "psk" },
            ["cjp"] = new[] { "uyu" },
            // 皮肤同步：Steam 大厅成员数据
            ["yf"] = new[] { "bzpm" },
            ["cb"] = new[] { "dup" },
            ["cwr"] = new[] { "zdc", "zdb", "zcu", "zcv", "zdm" },
            ["cxg"] = new[] { "zri" },
            ["WorkshopItemStatus"] = new[] { "publishedFileId", "installed", "heroId", "installFolder" },
            ["LocalWorkshopManifest"] = new[] { "heroId", "title", "heroName", "heroIcon", "author", "contentType", "bundleRelativePath", "bundleAssetName" },
        };

        // 卡牌保护单独检查。它失败时仅停用卡牌，其他 Mod 继续使用原来的兼容检查。
        private static readonly (string type, string[] members)[] CardRequired =
        {
            ("dj", new[] { "bfmo", "bfmr", "bzbj", "bzbk", "bfnm", "ech", "ecw", "ede", "edd", "ecf", "ecv" }),
            ("dj+GameSaveData", new[] { "maxCapacity", "cardCapacityProgressionVersion", "equipCards", "equipCardPresets", "currentEquipCardPresetIndex", "haveCardDataDic" }),
            ("dj+EquipCardPresetData", new[] { "presetName", "equipCards", "isSaved" }),
            ("bhp", new[] { "ogk", "ogs", "ogl", "bneg" }),
            ("bhp+HaveCardData", new[] { "cardId", "haveNum", "curProgress" }),
            ("caq", new[] { "sux", "suy", "sur", "sus", "sve", "sud", "bfpn" }),
            ("wn", new[] { "bkjp" }),
            ("CardData", new[] { "capacity", "progress" }),
        };

        // 独立检查失败仅停用自动锻造；反射签名由安装过程再次核验。
        public static List<string> FindForgingMissing()
        {
            var required = new (string type, string[] members)[] {
                ("xt", new[] { "lhz", "lia" }), ("xt+ForgingTier", new[] { "Normal", "Gold" }),
                ("ccx", new[] { "ths", "tic", "bstp", "bfpn" }),
                ("RoguelikeUIData", new[] { "name", "quality", "data" }),
                ("cl", new[] { "bzap" }), ("wn", new[] { "bkjn" }), ("vg", new[] { "bznz" }),
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
            var ez = AccessTools.TypeByName("fh");
            if (ez != null)
            {
                CheckSignature(ez, "eqw", new[] { "ActiveSkillEnum", "ix", "Int32" }, "Boolean", missing);
                CheckSignature(ez, "eqx", new[] { "ix", "ActiveSkillData" }, "ga", missing);
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
