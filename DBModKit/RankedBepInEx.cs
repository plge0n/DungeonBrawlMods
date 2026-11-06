using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Preloader;
using HarmonyLib;

namespace DBModKit
{
    /// <summary>BepInEx 5.4.23.5 原装兼容补丁；只读反射，可在游戏外直接测试。</summary>
    internal static class RankedBepInEx
    {
        internal static bool IsExempt(MethodInfo patchMethod, MethodBase target, HarmonyPatchType kind)
        {
            if (patchMethod?.DeclaringType == null || target == null) return false;
            var assembly = patchMethod.Module.Assembly;
            string name = assembly.GetName().Name;
            // 用已绑定的 BepInEx 类型锚定当前进程的程序集实例，不按名称搜索或另行加载 DLL。
            if (!(name == "BepInEx.Preloader" && ReferenceEquals(assembly, typeof(EnvVars).Assembly))
                && !(name == "BepInEx" && ReferenceEquals(assembly, typeof(Paths).Assembly))) return false;

            const string ns = "BepInEx.Preloader.RuntimeFixes.";
            switch (patchMethod.DeclaringType.FullName)
            {
                case ns + "HarmonyInteropFix":
                    return IsMethod(patchMethod, "OnAssemblyLoad") && kind == HarmonyPatchType.Prefix
                        && (Equals(target, typeof(Assembly).GetMethod("LoadFile", new[] { typeof(string) }))
                            || Equals(target, typeof(Assembly).GetMethod("LoadFrom", new[] { typeof(string) })));
                case ns + "UnityPatches":
                    return kind == HarmonyPatchType.Postfix
                        && ((IsMethod(patchMethod, "GetLocation") && Equals(target, typeof(Assembly).GetProperty("Location")?.GetGetMethod()))
                            || (IsMethod(patchMethod, "GetCodeBase") && Equals(target, typeof(Assembly).GetProperty("CodeBase")?.GetGetMethod())));
                case ns + "ConsoleSetOutFix":
                    return IsMethod(patchMethod, "OnSetOut") && kind == HarmonyPatchType.Prefix
                        && Equals(target, typeof(Console).GetMethod("SetOut", new[] { typeof(TextWriter) }));
                default:
                    return false;
            }
        }

        private static bool IsMethod(MethodInfo method, string name) => Equals(method,
            method.DeclaringType.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
    }
}
