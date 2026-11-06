using System;
using System.Collections.Generic;
namespace DBModKit
{
    public enum ForgeAttribute { AddDamage, Agi, AllBreakShield, ArmedAdd, Armor, Attack, AttackSpeed, CoolDown, Critical, CriticalDamage, Doge, ExpAdd, ExtraDamage, HP, HPSec, HaloAdd, HenshinAdd, HpPercent, HpSecRate, Luck, MP, MPSec, MoveSpeed, NormalAdd, NormalBreak, ReduceInjury, SkillAdd, SkillBreak, SkillHit, Sta, Str, SummonAdd, Three, XiXue, XiXueRate }
    public sealed class ForgeStat
    {
        public ForgeAttribute Type { get; }
        public float Value { get; }
        public bool IsPercent { get; }
        public ForgeStat(ForgeAttribute type, float value, bool isPercent) { Type = type; Value = value; IsPercent = isPercent; }
    }
    public sealed class ForgeCandidate
    {
        public string Name { get; }
        public int Quality { get; }
        public IReadOnlyList<ForgeStat> Stats { get; }
        public ForgeCandidate(string name, int quality, IList<ForgeStat> stats) { Name = name; Quality = quality; Stats = new List<ForgeStat>(stats).AsReadOnly(); }
    }
    public sealed class ForgeOffer
    {
        public ForgeTier Tier { get; }
        public IReadOnlyList<ForgeCandidate> Candidates { get; }
        public ForgeOffer(ForgeTier tier, IList<ForgeCandidate> candidates) { Tier = tier; Candidates = new List<ForgeCandidate>(candidates).AsReadOnly(); }
    }
}
