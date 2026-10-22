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
            ["nn"] = new[] { "bpbu" },
            ["cj"] = new[] { "boyb", "boyi", "boyl" },
            ["ez"] = new[] { "dxx", "dxy" },
            ["bwu"] = new[] { "quj", "qut" },
            ["bws"] = new string[0],
            ["LeaderboardUploadFailure"] = new[] { "InvalidData" },
            ["LeaderboardUploadStage"] = new string[0],
            ["yb"] = new[] { "hp", "maxHp", "izk", "iyn", "ivj", "bphx", "bpia" },
            ["bbe"] = new[] { "bfru", "bfrv" },
            ["xo"] = new[] { "isBoss", "isElite" },
            ["oo"] = new[] { "bcrw" },
            ["du"] = new[] { "dqr" },
            ["cl"] = new[] { "dff" },
            ["bpe"] = new[] { "bjes" },
            ["bpf"] = new[] { "bjgk", "bjgm" },
            ["bht"] = new[] { "bhne", "bhny", "bpqi", "k896ff3548b31472b8b2cf8f649c25e65", "kacb7d091b5904139a602f04ed9552242" },
            ["RoleModeBase"] = new[] { "addRange" },
            ["ActiveSkillData"] = new[] { "tags", "indicator", "castingRange", "range", "cd", "canAuto" },
            ["ActiveSkillEnum"] = new[] { "None" },
            ["SkillTag"] = new[] { "Heal", "Revive", "Shield", "Defense", "Buff", "Summon", "Aura", "Area", "Move", "Control" },
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
