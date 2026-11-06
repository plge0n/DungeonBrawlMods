using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using DBModKit;
using HarmonyLib;

class Program
{
    [BepInDependency(ModKitPlugin.Guid)] class Dependent : BaseUnityPlugin { }
    class Plain : BaseUnityPlugin { }
    static int count;
    static readonly string root=Path.Combine(Path.GetTempPath(),"ranked-checks-"+Guid.NewGuid().ToString("N"));
    public static void Target() { }
    static void Reset(bool mode=false) {
        Chainloader.PluginInfos.Clear(); ModContext.All.Clear(); UnityEngine.Resources.Plugins.Clear(); Harmony.Methods.Clear(); Harmony.Throw=false;
        ActiveSkills.All.Clear(); PassiveSkills.All.Clear(); CustomRelics.All.Clear(); CustomEquipment.All.Clear(); CustomEquipEffects.All.Clear(); HeroVariants.All.Clear(); ContentPacks.All.Clear();
        GameRaw.Data.Clear(); GameRaw.Lobby=0; GameRaw.Me=1; GameRaw.Throw=false; Game.IsSolo=true; ModKit.Compatible=true;
        typeof(Ranked).GetField("configured",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,false);
        UnityEngine.Time.unscaledTime=0; ContentPacks.Version=0; RunLog.Entries.Clear(); Harmony.Scans=0;
        typeof(Ranked).GetField("nextPublish",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,0f);
        typeof(Ranked).GetField("lastPublishedResult",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);
        Ranked.Configure(new ConfigFile("unused",false){Requested=mode});
    }
    static T Add<T>(string guid,string name,string location=null) where T:BaseUnityPlugin,new() {
        var p=new T(); p.Info=new PluginInfo{Metadata=new BepInPlugin(guid,name),Instance=p,Location=location};
        Chainloader.PluginInfos[guid]=p.Info; return p;
    }
    static void Assert(string name,bool expected) {
        var result=Ranked.Check("test "+name); if(result.Eligible!=expected) throw new Exception(name+": "+result.Summary);
        Console.WriteLine("PASS "+name); count++;
    }
    static void Verify(string name,bool ok) { if(!ok) throw new Exception(name); Console.WriteLine("PASS "+name); count++; }
    static void Patch(string owner) { var m=typeof(Program).GetMethod(nameof(Target)); var p=new Patches(); p.Prefixes.Add(new Patch{owner=owner,PatchMethod=m}); Harmony.Methods[m]=p; }
    static void Team(string value,string handshake="1|old|0|") { GameRaw.Lobby=7; Game.IsSolo=false; GameRaw.Data[(2,Multiplayer.HandshakeKey)]=handshake; GameRaw.Data[(2,Ranked.LobbyKey)]=value; }
    static string Peer(string status,string reason="")=>"2|"+status+"|"+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(reason));
    static void Main() {
        Directory.CreateDirectory(root); Paths.BepInExRootPath=root; Paths.ConfigPath=root;
        Reset(); Add<Plain>(ModKitPlugin.Guid,"kit"); Add<Plain>("plge0n.dungeonbrawl.smartskill","SmartSkill"); Assert("whitelist without ranked mode",true);
        Reset(); Add<Plain>("third.party","third party"); Assert("unknown plugin",false);
        Reset(); var hidden=new Plain(); hidden.Info.Metadata=new BepInPlugin("hidden","hidden"); UnityEngine.Resources.Plugins.Add(hidden); Assert("hidden plugin absent from PluginInfos",false);
        Reset(); Add<Plain>("plge0n.dungeonbrawl.smartskill","SmartSkill",Path.Combine(root,"scripts","SmartSkill.dll")); Assert("whitelisted script plugin blocked",false);
        Reset(); Add<Plain>("plge0n.dungeonbrawl.smartskill","SmartSkill",Path.Combine(root,"scripts-other","SmartSkill.dll")); Assert("scripts sibling path not mistaken",true);
        Reset(); Patch("third.party"); Assert("unknown Harmony owner on non-game method",false);
        Reset(); Patch(ModKitPlugin.Guid); Assert("ModKit owner",true);
        Reset(); Patch(ModKitPlugin.Guid+".cards"); Assert("card protection owner",true);
        Reset(true); var disabled=Add<Dependent>("disabled","disabled"); ModContext.All["disabled"]=new ModContext{Plugin=disabled,RankedDisabled=true}; Assert("disabled dependent plugin",true);
        Patch(ModKitPlugin.Guid); Assert("disabled plugin assembly has patches even with approved owner",false);
        Reset(true); var plain=Add<Plain>("disabled","disabled"); ModContext.All["disabled"]=new ModContext{Plugin=plain,RankedDisabled=true}; Assert("non-dependent cannot be exempted",false);
        Reset(true); disabled=Add<Dependent>("disabled","disabled"); ModContext.All["disabled"]=new ModContext{Plugin=new Dependent(),RankedDisabled=true}; Assert("disabled GUID cannot exempt a different instance",false);
        Reset(); ContentPacks.All.Add(new ContentPack{Loaded=false,DisplayName="off"}); Assert("unloaded content",true);
        ContentPacks.All[0].Loaded=true; Assert("loaded content",false);
        var registries=new List<object>[] {ActiveSkills.All,PassiveSkills.All,CustomRelics.All,CustomEquipment.All,CustomEquipEffects.All,HeroVariants.All};
        for(int i=0;i<registries.Length;i++){ Reset(); registries[i].Add(new object()); Assert("custom content registry "+i,false); }
        Reset(); var nested=Path.Combine(root,"patchers","nested"); Directory.CreateDirectory(nested); var dll=Path.Combine(nested,"patch.DLL"); File.WriteAllText(dll,""); Assert("recursive uppercase patcher DLL",false); File.Delete(dll); File.WriteAllText(Path.Combine(nested,"note.txt"),""); Assert("patcher directory without DLL",true);
        Reset(); Team(null,null); Assert("vanilla peer",true);
        Reset(); Team(null); Assert("old ModKit peer",false);
        Reset(); Team(Peer("1")); Assert("eligible ModKit peer",true);
        Reset(); Team(Peer("0","装了 Hextech")); Assert("ineligible ModKit peer",false);
        Reset(); Team("1|1|1|"); Assert("old timestamp format rejected",false);
        Reset(); Team("2|1"); Assert("missing reason field rejected",false);
        Reset(); Team(Peer("2")); Assert("unknown eligibility flag",false);
        Reset(); Team("2|1|invalid-base64"); Assert("malformed base64 fails closed",false);
        Reset(); Team(Peer("1","contradictory reason")); Assert("success flag with reason fails closed",false);
        Reset(); Game.IsSolo=false; Assert("LAN cannot confirm team",false);
        Reset(); Harmony.Throw=true; Assert("patch enumeration exception fails closed",false);
        Reset(); GameRaw.Throw=true; Assert("lobby exception fails closed",false);
        Reset(); ModKit.Compatible=false; Assert("incompatible kit",false);
        Reset(); Team(Peer("0","装了 Hextech")); Ranked.PublishLocal(); if(LobbySync.Published.Split('|')[1]!="1") throw new Exception("peer result leaked into local advertisement"); Console.WriteLine("PASS local advertisement has no peer recursion"); count++;
        Reset(); Ranked.SetMode(true); if(Ranked.ModeActive || !Ranked.RequestedMode) throw new Exception("mode changed without restart"); Console.WriteLine("PASS mode changes require restart"); count++;
        Reset(); Add<Plain>("third.party","unknown"); var reasons=Ranked.Reasons; bool threw=false; try { ((IList<string>)reasons).Add("tamper"); } catch(NotSupportedException) {threw=true;} if(!threw)throw new Exception("mutable reasons"); Console.WriteLine("PASS reasons are read-only snapshots"); count++;
        Reset(); Ranked.PublishLocal(); int scans=Harmony.Scans; int logs=RunLog.Entries.Count;
        UnityEngine.Time.unscaledTime=4.9f; Ranked.PublishLocal(); Verify("publication throttled before five seconds",Harmony.Scans==scans);
        UnityEngine.Time.unscaledTime=5f; Ranked.PublishLocal(); Verify("publication rechecks after five seconds without duplicate logs",Harmony.Scans==scans+1 && RunLog.Entries.Count==logs);
        GameRaw.Lobby=8; Ranked.PublishLocal(); Verify("lobby entry immediately rechecks",Harmony.Scans==scans+2);
        ContentPacks.Version++; Ranked.PublishLocal(); Verify("content version immediately rechecks",Harmony.Scans==scans+3);
        Ranked.BattleStarted(); Verify("battle entry immediately rechecks",Harmony.Scans==scans+4);
        Add<Plain>("changed","changed"); UnityEngine.Time.unscaledTime=10f; Ranked.PublishLocal(); Verify("changed result logged",RunLog.Entries.Count==logs+1);
        UnityEngine.Time.unscaledTime=15f; Ranked.PublishLocal(); Verify("unchanged rejection not logged again",RunLog.Entries.Count==logs+1);
        var firstValue=LobbySync.Published; UnityEngine.Time.unscaledTime=20f; Ranked.PublishLocal(); Verify("advertisement has no changing timestamp",LobbySync.Published==firstValue);
        Ranked.Check("上传成绩"); Ranked.Check("国王快照"); var eligible=Ranked.Eligible; var snapshot=Ranked.Reasons;
        Verify("upload snapshot and public interfaces always scan and log",Harmony.Scans==scans+11 && RunLog.Entries.Count==logs+5);
        Reset(); Team("2|1||"); Assert("extra fields rejected",false);
        Console.WriteLine($"{count} safety checks passed. No game process or game assemblies used.");
    }
}
