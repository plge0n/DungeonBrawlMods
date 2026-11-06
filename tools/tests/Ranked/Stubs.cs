using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace BepInEx {
    public class BepInPlugin : Attribute { public string GUID, Name; public BepInPlugin(string guid,string name) { GUID=guid; Name=name; } }
    public class BepInDependency : Attribute { public string DependencyGUID; public BepInDependency(string guid) { DependencyGUID=guid; } }
    public class BaseUnityPlugin { public PluginInfo Info = new PluginInfo(); }
    public class PluginInfo { public BepInPlugin Metadata; public BaseUnityPlugin Instance; public string Location; }
    public static class Paths { public static string BepInExRootPath, ConfigPath; }
    public static class MetadataHelper {
        public static BepInPlugin GetMetadata(BaseUnityPlugin p)=>p.Info.Metadata;
        public static BepInPlugin GetMetadata(Type t)=>t.GetCustomAttribute<BepInPlugin>();
        public static IEnumerable<BepInDependency> GetDependencies(Type t)=>t.GetCustomAttributes<BepInDependency>();
    }
}
namespace BepInEx.Bootstrap { public static class Chainloader { public static Dictionary<string,BepInEx.PluginInfo> PluginInfos=new(); } }
namespace BepInEx.Configuration {
    public class ConfigFile {
        public bool Requested; public ConfigFile(string p,bool save){} public void Save(){}
        public ConfigEntry<T> Bind<T>(string a,string b,T value,string d)=>new ConfigEntry<T>{Value=(T)(object)Requested,ConfigFile=this};
    }
    public class ConfigEntry<T> { public T Value; public ConfigFile ConfigFile; }
}
namespace HarmonyLib {
    public enum HarmonyPatchType { Prefix,Postfix,Transpiler,Finalizer }
    public class Patch { public string owner; public MethodInfo PatchMethod; }
    public class Patches { public List<Patch> Prefixes=new(),Postfixes=new(),Transpilers=new(),Finalizers=new(); }
    public class Harmony {
        public static Dictionary<MethodBase,Patches> Methods=new(); public static bool Throw; public static int Scans;
        public static IEnumerable<MethodBase> GetAllPatchedMethods() { Scans++; if(Throw) throw new Exception("fixture"); return Methods.Keys; }
        public static Patches GetPatchInfo(MethodBase m)=>Methods[m];
    }
}
namespace UnityEngine {
    public static class Resources { public static List<BepInEx.BaseUnityPlugin> Plugins=new(); public static T[] FindObjectsOfTypeAll<T>()=>Plugins.Cast<T>().ToArray(); }
    public static class Time { public static float unscaledTime; }
}
namespace DBModKit {
    public static class ModKitPlugin { public const string Guid="plge0n.dungeonbrawl.modkit",Name="DBModKit"; }
    public static class ModKit { public static bool Compatible=true; }
    public static class Log { public static void Info(string s){} public static void Warning(string s){} public static void Error(string s){} }
    public enum RunLogLevel { Info,Warning }
    public static class RunLog { public static List<string> Entries=new(); public static void WriteCore(string a,string b,RunLogLevel c,string d,(string,object)[] e,bool mirror){ Entries.Add(d); } }
    public static class ScreenNotice { public static void Show(string s,float f){} }
    public sealed class ModContext { public static Dictionary<string,ModContext> All=new(); public bool RankedDisabled; public BepInEx.BaseUnityPlugin Plugin; }
    public class ContentPack { public bool Loaded; public string DisplayName; }
    public static class RankedBepInEx { public static bool IsExempt(MethodInfo m,MethodBase t,HarmonyLib.HarmonyPatchType k)=>false; }
    public static class ContentPacks { public static int Version; public static List<ContentPack> All=new(); }
    public static class ActiveSkills { public static List<object> All=new(); }
    public static class PassiveSkills { public static List<object> All=new(); }
    public static class CustomRelics { public static List<object> All=new(); }
    public static class CustomEquipment { public static List<object> All=new(); }
    public static class CustomEquipEffects { public static List<object> All=new(); }
    public static class HeroVariants { public static List<object> All=new(); }
    public static class Multiplayer { public const string HandshakeKey="dbmods.mods"; }
    public static class LobbySync { public static string Published; public static void Publish(string k,string v)=>Published=v; }
    public static class Game { public static bool IsSolo=true; }
    public static class GameRaw {
        public static ulong Lobby,Me=1; public static bool Throw;
        public static Dictionary<(ulong,string),string> Data=new();
        public static ulong LobbyId() { if(Throw) throw new Exception("fixture"); return Lobby; }
        public static ulong MySteamId()=>Me;
        public static List<ulong> LobbyMembers(ulong l)=>new(){1,2};
        public static string LobbyMemberData(ulong l,ulong m,string k)=>Data.GetValueOrDefault((m,k));
    }
}
