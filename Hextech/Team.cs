using System;
using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;

namespace Hextech
{
    // 队友卡（参照斗魂竞技场）：读队友的位置、血量，用游戏原版的治疗、范围护盾、圣焰祝福影响队友，队友没装 Mod 也生效。
    // 联机时没有重试波次，限时加成（复仇）在波次结束时撤销。
    // 每个玩家有自己的怪、打别人的怪收益归动手的人：在队友身边的范围伤害（音爆、晾衣绳）跳过队友的怪，不抢收益
    internal static class Team
    {
        // ---- 数值 ----
        public const float Range = 15f, LowHp = 0.3f;
        public const float SacrificeMana = 0.3f, SacrificeHeal = 0.2f, SacrificeCooldown = 20f;
        public const int VengeanceDamage = 15, VengeanceLifesteal = 8;
        public const int JuiceItem = Augments.FruitItem;
        public const float ManeuverRadius = 10f, ManeuverShield = 0.2f, ManeuverHeal = 0.1f, ManeuverCooldown = 30f;
        public const float LinkRatio = 0.2f, LinkBudget = 0.03f;
        public const float BannerDamage = 0.2f, BannerSeconds = 6f;
        public const float BoomRadius = 4f, BoomDamage = 3f, BoomCooldown = 2f;
        public const float DivineInterval = 20f, DivineRadius = 12f, DivineShield = 0.15f, DivineInvulnerable = 1.5f;
        public const float LineRange = 25f, LineInterval = 0.5f, LineDamage = 0.4f, LineWidth = 1.5f;
        // 让经济
        public const float PatronPay = 0.2f, PatronRate = 1.5f;
        public const int BacklineStats = 3;
        public const float OathRange = 15f, OathDamage = 0.15f;
        public const float AllInPay = 0.4f, AllInRate = 2f, AllInRange = 25f, AllInDamage = 0.25f;
        // 祝福每隔这么久续一次，持续时间比间隔长一点，不会断
        private const float BlessEvery = 4f, BlessSeconds = 5f;

        // 队友列表每秒刷新一次（Game.Teammates 会遍历场景）
        private static List<Player> mates = new List<Player>();
        private static float matesAt;
        // 上一次看到的队友血量和存活（灵魂连接、复仇用）
        private static readonly Dictionary<Player, long> lastHp = new Dictionary<Player, long>();
        private static readonly Dictionary<Player, bool> lastAlive = new Dictionary<Player, bool>();
        private static readonly Dictionary<Player, float> sacrificeAt = new Dictionary<Player, float>();
        private static readonly Dictionary<Player, float> boomAt = new Dictionary<Player, float>();
        private static float maneuverAt, divineAt, lineAt, lineEffectAt, linkBudgetAt;
        private static double linkBudget;
        private static bool vengeanceOn;
        // 供养的队友；要（重新）指定时 carryPending
        private static Player carry;
        private static bool carryPending;
        private static float blessAt;

        public static Player Carry => carry;

        public static bool HasTeammates => mates.Count > 0;

        private static bool Has(string id) => Engine.Has(id);

        public static void Reset()
        {
            mates.Clear();
            matesAt = 0f;
            lastHp.Clear();
            lastAlive.Clear();
            sacrificeAt.Clear();
            boomAt.Clear();
            maneuverAt = lineAt = lineEffectAt = linkBudgetAt = 0f;
            divineAt = Time.time + DivineInterval;
            linkBudget = 0;
            vengeanceOn = false;
            carry = null;
            carryPending = false;
            blessAt = 0f;
        }

        // 不管选没选队友卡都要刷新：抽卡时要知道有没有队友
        public static void RefreshMates()
        {
            if (Time.unscaledTime < matesAt) return;
            matesAt = Time.unscaledTime + 1f;
            mates = Game.Teammates();
        }

        public static void Tick(Player me, bool inWave)
        {
            RefreshMates();
            if (!me.IsAlive) return;
            float now = Time.time;
            foreach (var m in mates)
            {
                bool alive = m.IsAlive;
                long hp = alive ? m.Hp : 0;
                // 第一次看到的队友不算"刚倒下"（可能本来就躺着）
                bool wasAlive = lastAlive.TryGetValue(m, out var a) && a;
                if (Has("vengeance") && wasAlive && !alive && inWave) StartVengeance(me, m);
                if (alive && Has("spiritlink") && lastHp.TryGetValue(m, out var before) && hp < before && m.DistanceTo(me) <= Range)
                    SpiritLink(me, m, before - hp);
                if (alive && Has("sacrifice") && m.HpPercent < LowHp && m.DistanceTo(me) <= Range)
                    Sacrifice(me, m, now);
                lastHp[m] = hp;
                lastAlive[m] = alive;
            }
            if (Has("maneuvers") && me.HpPercent < LowHp && now >= maneuverAt)
            {
                maneuverAt = now + ManeuverCooldown;
                me.ShieldNearbyPlayers(ManeuverRadius, ManeuverShield);
                foreach (var m in Near(me, ManeuverRadius))
                {
                    me.HealAlly(m, m.MaxHp * ManeuverHeal);
                    Boom(me, m);
                }
            }
            if (Has("divine") && now >= divineAt)
            {
                divineAt = now + DivineInterval;
                me.ShieldNearbyPlayers(DivineRadius, DivineShield);
                me.GiveInvulnerable(DivineInvulnerable);
                foreach (var m in Near(me, DivineRadius)) Boom(me, m);
            }
            if (Has("clothesline") && inWave && now >= lineAt)
            {
                lineAt = now + LineInterval;
                Clothesline(me, now);
            }
            TickCarry(me, inWave, now);
        }

        // ---- 供养 ----
        private static bool AnySupport => Augments.All.Any(a => a.Support && Has(a.Id));

        // 选中供养卡时调用：还没有供养对象（或者他走了）就去指定
        public static void OnSupportPicked()
        {
            RefreshMates();
            if (carry == null || !mates.Contains(carry)) carryPending = true;
        }

        private static void TickCarry(Player me, bool inWave, float now)
        {
            if (!AnySupport) return;
            // 供养的队友离开了房间：重新指定
            if (carry != null && !mates.Contains(carry))
            {
                carry = null;
                carryPending = true;
            }
            if (carryPending && mates.Count > 0) ChooseCarry(inWave);
            if (carry == null || !carry.IsAlive || now < blessAt) return;
            blessAt = now + BlessEvery;
            float bonus = 0f;
            if (Has("oath") && carry.DistanceTo(me) <= OathRange) bonus = Math.Max(bonus, OathDamage);
            if (Has("allin") && carry.DistanceTo(me) <= AllInRange) bonus = Math.Max(bonus, AllInDamage);
            if (bonus > 0f) me.BlessPlayer(carry, bonus, BlessSeconds);
        }

        private static void ChooseCarry(bool inWave)
        {
            if (mates.Count == 1)
            {
                SetCarry(mates[0]);
                return;
            }
            // 有好几个队友时弹一个选择；不在波次中弹（联机不能暂停），三选一界面被占用就等下一帧
            if (inWave || ChoicePanel.IsBusy) return;
            var list = mates.Take(ChoicePanel.MaxOptions).ToList();
            var options = list.Select(m => new ChoiceOption
            {
                Name = m.Name,
                Description = "你的供养卡（慷慨供养、守护之誓、举国之力）都给他",
                IconPath = "Bundles/UI/Icon/Shop/forging_epic",
            }).ToList();
            if (ChoicePanel.TryShow("海克斯 · 选择你要供养的队友", options, i => SetCarry(list[Mathf.Clamp(i, 0, list.Count - 1)])))
                carryPending = false;
        }

        private static void SetCarry(Player m)
        {
            carry = m;
            carryPending = false;
            blessAt = 0f;
            mod.RunLog.Info("供养", "指定供养的队友", ("队友", m.Name));
            ScreenNotice.Show($"你正在供养 {m.Name}", 3f);
        }

        // 休息开始：交出一部分金币，在供养的队友脚下掉更多
        private static void GiveGold(Player me)
        {
            if (carry == null || !carry.IsAlive) return;
            float pay = (Has("patron") ? PatronPay : 0f) + (Has("allin") ? AllInPay : 0f);
            if (pay <= 0f) return;
            float rate = Has("allin") ? AllInRate : PatronRate;
            int gold = Mathf.FloorToInt(me.Gold * Mathf.Min(1f, pay));
            if (gold <= 0) return;
            int paid = me.GiveGold(carry, gold, Mathf.FloorToInt(gold * rate));
            if (paid > 0) ScreenNotice.Show($"供养：交出 {paid} 金币，{carry.Name} 脚下掉了 {Mathf.FloorToInt(paid * rate)} 金币", 3f);
        }

        // 后排成长：队友升级时加属性
        public static void OnTeammateLevelUp(Player me)
        {
            if (Has("backline")) me.AddStats(BacklineStats, BacklineStats, BacklineStats, 0);
        }

        private static IEnumerable<Player> Near(Player me, float radius) => mates.Where(m => m.IsAlive && m.DistanceTo(me) <= radius);

        private static void Sacrifice(Player me, Player m, float now)
        {
            if (sacrificeAt.TryGetValue(m, out var at) && now < at) return;
            int cost = Mathf.CeilToInt(me.Mana * SacrificeMana);
            sacrificeAt[m] = now + SacrificeCooldown;
            if (cost > 0) me.AddMana(-cost);
            me.HealAlly(m, me.MaxHp * SacrificeHeal);
            Boom(me, m);
            mod.RunLog.Debug("甘愿牺牲", "治疗队友", ("队友", m.Name), ("法力", cost));
        }

        private static void SpiritLink(Player me, Player m, long lost)
        {
            float now = Time.time;
            if (now >= linkBudgetAt)
            {
                linkBudgetAt = now + 1f;
                linkBudget = me.MaxHp * LinkBudget;
            }
            double heal = Math.Min(lost * LinkRatio, linkBudget);
            if (heal < 1) return;
            linkBudget -= heal;
            me.HealAlly(m, heal);
            Boom(me, m);
        }

        private static void StartVengeance(Player me, Player fallen)
        {
            ScreenNotice.Show($"复仇：{fallen.Name} 倒下了！这一波伤害 +{VengeanceDamage}%、吸血 +{VengeanceLifesteal}%", 3f);
            if (vengeanceOn) return;
            vengeanceOn = true;
            me.AddAttribute("AddDamage", VengeanceDamage);
            me.AddAttribute("XiXueRate", VengeanceLifesteal);
        }

        private static void StopVengeance(Player me)
        {
            if (!vengeanceOn) return;
            vengeanceOn = false;
            me.AddAttribute("AddDamage", -VengeanceDamage);
            me.AddAttribute("XiXueRate", -VengeanceLifesteal);
        }

        // 号令之旗：放主动技能（不算免费释放）时祝福最近的队友
        public static void OnCast(Player me)
        {
            if (!Has("banner")) return;
            var m = Near(me, Range).OrderBy(x => x.DistanceTo(me)).FirstOrDefault();
            if (m == null) return;
            me.BlessPlayer(m, BannerDamage, BannerSeconds);
            Boom(me, m);
        }

        // 音爆：被本 Mod 治疗、护盾、祝福的队友周围爆一圈
        private static void Boom(Player me, Player m)
        {
            if (!Has("sonicboom") || !m.IsAlive) return;
            float now = Time.time;
            if (boomAt.TryGetValue(m, out var at) && now < at) return;
            boomAt[m] = now + BoomCooldown;
            me.DamageArea((float)Math.Min(float.MaxValue / 4, me.Attack * BoomDamage), m.Position, BoomRadius, "Effect/DeathExplosion", 1f, spareTeammates: true);
        }

        // 晾衣绳：沿两人连线每隔一个宽度判定一次（不播特效）；每秒在线的中点播一次小特效，让人知道线在哪
        private static void Clothesline(Player me, float now)
        {
            bool effect = now >= lineEffectAt;
            if (effect) lineEffectAt = now + 1f;
            float damage = (float)Math.Min(float.MaxValue / 4, me.Attack * LineDamage);
            foreach (var m in Near(me, LineRange))
            {
                Vector3 a = me.Position, b = m.Position;
                float length = Vector3.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / (LineWidth * 2f)));
                // 两端各留一个宽度，不打到自己脚下（那里本来就有普攻）
                for (int i = 1; i < steps; i++)
                    me.DamageArea(damage, Vector3.Lerp(a, b, (float)i / steps), LineWidth, null, 1f, spareTeammates: true);
                if (effect) me.PlayEffect("Effect/DeathExplosion", Vector3.Lerp(a, b, 0.5f), 0.4f);
            }
        }

        // 休息阶段：复仇结束（之后的重试快照里不能带着它）；供养交金币；果汁盒给每个人脚下掉痊愈神符
        public static void OnWaveEnded(Player me)
        {
            StopVengeance(me);
            GiveGold(me);
            if (!Has("juicebox")) return;
            RefreshMates();
            me.DropItem(JuiceItem, me.Position);
            foreach (var m in mates.Where(x => x.IsAlive)) me.DropItem(JuiceItem, m.Position);
        }

        public static void Revert(Player me)
        {
            if (me != null) StopVengeance(me);
        }

        private static ModContext mod;

        public static void Attach(ModContext ctx) => mod = ctx;
    }
}
