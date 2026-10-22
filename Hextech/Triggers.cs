using System;
using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;

namespace Hextech
{
    // 触发效果积木的运行：释放专属技能时（或每第 3 次）、技能击杀时
    internal static class Triggers
    {
        private sealed class Instance
        {
            public CardPart Part;
            public int Casts;
            public float ReadyAt;
        }

        // 限时加成：到时间用相反数撤销
        private sealed class Timed
        {
            public string Kind;
            public float Amount;
            public float Until;
        }

        private static readonly List<Instance> instances = new List<Instance>();
        private static readonly List<Timed> timed = new List<Timed>();
        private static readonly List<(float at, SkillCast cast)> echoes = new List<(float, SkillCast)>();
        private static SkillProfile profile;
        // 收割：每次释放最多触发一次
        private static bool harvestUsed = true;

        public static void Rebuild(SkillProfile p, IEnumerable<Card> picks)
        {
            profile = p;
            instances.Clear();
            if (p == null || !p.Active) return;
            foreach (var part in picks.SelectMany(c => c.Parts))
                if (part.Block.Kind == BlockKind.Trigger) instances.Add(new Instance { Part = part });
        }

        // revert = true：把还在生效的限时加成撤掉；false：游戏已经替我们还原了（重试波次、离开战斗），只丢掉记录
        public static void Clear(bool revert)
        {
            if (revert)
            {
                var player = Game.LocalPlayer;
                if (player != null)
                    foreach (var t in timed) Undo(player, t);
            }
            timed.Clear();
            echoes.Clear();
            foreach (var i in instances)
            {
                i.Casts = 0;
                i.ReadyAt = 0f;
            }
        }

        private static bool IsHeroSkill(int skillId) => Engine.SkillIds().Contains(skillId);

        public static void OnCast(SkillCast cast)
        {
            // 回响放出来的那一次不算（否则会无限连锁），也不计入"每第 3 次"
            if (cast.IsFree || !Engine.Active || profile == null || !IsHeroSkill(cast.SkillId)) return;
            var player = Game.LocalPlayer;
            if (player == null) return;
            harvestUsed = false;
            float now = Time.time;
            foreach (var i in instances)
            {
                if (i.Part.Block.Id == "harvest") continue;
                i.Casts++;
                if (i.Part.EveryThird && i.Casts % 3 != 0) continue;
                if (now < i.ReadyAt) continue;
                i.ReadyAt = now + Blocks.InternalCooldown(profile);
                Fire(player, i.Part, cast);
            }
        }

        public static void OnHit(HitInfo hit)
        {
            if (harvestUsed || !hit.Lethal || hit.SkillId == null || !Engine.Active || profile == null || !IsHeroSkill(hit.SkillId.Value)) return;
            var part = instances.FirstOrDefault(i => i.Part.Block.Id == "harvest")?.Part;
            if (part == null) return;
            var player = Game.LocalPlayer;
            if (player == null || hit.Source?.Raw != player.Raw) return;
            harvestUsed = true;
            var slot = player.HeroSkillSlot;
            if (slot == null) return;
            slot.ReduceCooldown(slot.CooldownTotal * Blocks.HarvestRatio * part.Mult);
        }

        private static void Fire(Player player, CardPart part, SkillCast cast)
        {
            float m = part.Mult * (part.EveryThird ? Blocks.EveryThirdFactor : 1f);
            float seconds = Blocks.BuffSeconds(profile);
            switch (part.Block.Id)
            {
                case "fury":
                    Add(player, "attackSpeed", Blocks.FuryAttackSpeed * m, seconds);
                    break;
                case "bulwark":
                    Add(player, "armor", Mathf.Round(Blocks.BulwarkArmor * m), seconds);
                    break;
                case "invuln":
                    player.GiveInvulnerable(Mathf.Min(Blocks.MaxInvulnerable, Blocks.InvulnerableSeconds * m));
                    break;
                case "reflux":
                    // 按调整后的蓝耗返还
                    float cost = profile.Cost * SkillTuning.Get(cast.SkillId).Cost;
                    int mana = Mathf.RoundToInt(cost * Mathf.Min(Blocks.MaxReflux, Blocks.RefluxRatio * m));
                    if (mana > 0) player.AddMana(mana);
                    break;
                case "drain":
                    player.Heal(player.MaxHp * Blocks.DrainRatio * m);
                    break;
                case "echo":
                    echoes.Add((Time.time + Blocks.EchoDelay, cast));
                    break;
            }
        }

        private static void Add(Player player, string kind, float amount, float seconds)
        {
            var t = new Timed { Kind = kind, Amount = amount, Until = Time.time + seconds };
            if (kind == "attackSpeed") player.AddAttackSpeed(amount);
            else player.AddArmor((int)amount);
            timed.Add(t);
        }

        private static void Undo(Player player, Timed t)
        {
            if (t.Kind == "attackSpeed") player.AddAttackSpeed(-t.Amount);
            else player.AddArmor(-(int)t.Amount);
        }

        public static void Tick(Player player)
        {
            float now = Time.time;
            for (int i = timed.Count - 1; i >= 0; i--)
            {
                if (now < timed[i].Until) continue;
                Undo(player, timed[i]);
                timed.RemoveAt(i);
            }
            for (int i = echoes.Count - 1; i >= 0; i--)
            {
                if (now < echoes[i].at) continue;
                var cast = echoes[i].cast;
                echoes.RemoveAt(i);
                if (Engine.Active && player.IsAlive) player.FreeCast(cast);
            }
        }
    }
}
