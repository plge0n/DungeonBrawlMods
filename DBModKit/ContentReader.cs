using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DBModKit
{
    // 读内容包的 JSON：技能、被动、变体英雄。错误带上文件和位置（例如 skills.json activeSkills[0].onCast[1]），
    // 写错的条目跳过，其他条目照常加载
    internal sealed class ParsedContent
    {
        public readonly List<(ActiveSkillDef def, ActiveSpec spec)> Actives = new List<(ActiveSkillDef, ActiveSpec)>();
        public readonly List<(PassiveSkillDef def, PassiveSpec spec)> Passives = new List<(PassiveSkillDef, PassiveSpec)>();
        // 变体的专属技能可以写同一个包里的键，注册完技能后再接上
        public readonly List<(HeroVariantDef def, string active, string passive, string where)> Variants = new List<(HeroVariantDef, string, string, string)>();
        public readonly List<string> Errors = new List<string>();
    }

    internal static class ContentReader
    {
        // 图标：值是包里的图片（带扩展名）时由 loadIcon 读成图标名，否则当游戏图标名
        public static void ReadFile(string path, string label, Func<string, string, string> loadIcon, ParsedContent output)
        {
            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(path));
            }
            catch (Exception e) when (e is JsonException || e is IOException)
            {
                output.Errors.Add($"{label}：读不了（{e.Message}）");
                return;
            }
            var top = new Obj(root, label, output.Errors);
            ReadList(top, "activeSkills", (o, where) => ReadActive(o, loadIcon), output.Actives.Add);
            ReadList(top, "passiveSkills", (o, where) => ReadPassive(o, loadIcon), output.Passives.Add);
            ReadList(top, "heroVariants", (o, where) =>
            {
                var v = ReadVariant(o, out string active, out string passive);
                return (v, active, passive, where);
            }, output.Variants.Add);
            top.Done();
        }

        // 每个条目单独检查：有任何错误就整条跳过（半对的技能比没有更难查），其他条目照常加载
        private static void ReadList<T>(Obj parent, string key, Func<Obj, string, T> read, Action<T> add)
        {
            var token = parent.Take(key);
            if (token == null) return;
            if (!(token is JArray array))
            {
                parent.Error(key, "应该是列表 [ … ]");
                return;
            }
            for (int i = 0; i < array.Count; i++)
            {
                string where = $"{parent.Where}.{key}[{i}]";
                if (!(array[i] is JObject jo))
                {
                    parent.Errors.Add($"{where}：应该是 {{ … }}");
                    continue;
                }
                int before = parent.Errors.Count;
                var o = new Obj(jo, where, parent.Errors);
                try
                {
                    var item = read(o, where);
                    o.Done();
                    if (parent.Errors.Count == before) add(item);
                }
                catch (ContentError)
                {
                    // 已经记在 Errors 里
                }
            }
        }

        // ---- 主动技能 ----

        private static (ActiveSkillDef, ActiveSpec) ReadActive(Obj o, Func<string, string, string> loadIcon)
        {
            var d = new ActiveSkillDef
            {
                Key = o.Key(),
                Name = o.Str("name", ""),
                Description = o.Str("description", ""),
                Quality = o.Enum("quality", PassiveQuality.C),
                Indicator = o.Enum("indicator", SkillIndicator.Range),
                Cost = o.Int("cost", 20),
                Cooldown = o.Num("cooldown", 8f),
                CastingRange = o.Num("castingRange", 8f),
                Range = o.Num("range", 3f),
                Duration = o.Num("duration", 0f),
                Interval = o.Num("interval", 0f),
                DamageBase = o.Int("damageBase", 0),
                DamageValue = o.Num("damageValue", 0f),
                DamageType = o.Int("damageType", 0),
                Charges = o.Int("charges", 0),
                ChargeTime = o.Num("chargeTime", 0f),
                Values = o.Nums("values"),
                CanAuto = o.Bool("canAuto", true),
                InSkillBook = o.Bool("inSkillBook", true),
                Tags = o.Strs("tags") ?? new[] { "Area" },
            };
            string icon = o.Str("icon", null);
            if (icon != null) d.Icon = o.Guard("icon", () => loadIcon(d.Key, icon));
            if (d.Name == "") o.Error("name", "技能要有名字");

            var spec = new ActiveSpec
            {
                OnCast = Actions(o, "onCast", true),
                OnEnd = Actions(o, "onEnd", true),
            };
            var tick = o.Take("onTick");
            if (tick != null)
            {
                var t = o.Child(tick, "onTick");
                spec.TickEvery = t.Num("every", 0f);
                if (spec.TickEvery <= 0f) t.Error("every", "要写大于 0 的间隔秒数");
                spec.OnTick = Actions(t, "do", true);
                t.Done();
                if (d.Duration <= 0f) o.Error("onTick", "一次性技能（duration 为 0）不会有 onTick，给技能设置 duration");
            }
            var anim = o.Take("animation");
            if (anim != null)
            {
                var a = o.Child(anim, "animation");
                spec.Animation = a.Str("game", null);
                if (spec.Animation == null) a.Error("game", "写游戏动作的名字，比如 Frank_RPG_2Hand_Skill01_WhirlWind");
                spec.AnimationSpeed = a.Num("speed", 1f);
                if (a.Has("duration")) spec.AnimationDuration = a.Num("duration", 0f);
                a.Done();
            }
            d.Create = () => new ContentActiveSkill(spec);
            return (d, spec);
        }

        // ---- 被动 ----

        private static readonly Dictionary<string, PassiveTrigger> PassiveTriggers = new Dictionary<string, PassiveTrigger>
        {
            ["onAdded"] = PassiveTrigger.OnAdded,
            ["onRemoved"] = PassiveTrigger.OnRemoved,
            ["onHit"] = PassiveTrigger.OnHit,
            ["onKill"] = PassiveTrigger.OnKill,
            ["onCrit"] = PassiveTrigger.OnCrit,
            ["onHurt"] = PassiveTrigger.OnHurt,
            ["onWaveStart"] = PassiveTrigger.OnWaveStart,
            ["onCast"] = PassiveTrigger.OnCast,
        };

        private static (PassiveSkillDef, PassiveSpec) ReadPassive(Obj o, Func<string, string, string> loadIcon)
        {
            var d = new PassiveSkillDef
            {
                Key = o.Key(),
                Name = o.Str("name", ""),
                Description = o.Str("description", ""),
                Quality = o.Enum("quality", PassiveQuality.C),
                Values = o.Nums("values"),
                LevelUp = o.Nums("levelUp"),
                InSkillBook = o.Bool("inSkillBook", true),
                Repeatable = o.Bool("repeatable", false),
                Tags = o.Strs("tags") ?? new[] { "Buff" },
            };
            string icon = o.Str("icon", null);
            if (icon != null) d.Icon = o.Guard("icon", () => loadIcon(d.Key, icon));
            if (d.Name == "") o.Error("name", "被动要有名字");

            var spec = new PassiveSpec();
            foreach (var kv in PassiveTriggers)
            {
                var list = Actions(o, kv.Key, false);
                if (list.Count > 0) spec.On[kv.Value] = list;
            }
            var tick = o.Take("onTick");
            if (tick != null)
            {
                var t = o.Child(tick, "onTick");
                spec.TickEvery = t.Num("every", 0f);
                if (spec.TickEvery <= 0f) t.Error("every", "要写大于 0 的间隔秒数");
                spec.On[PassiveTrigger.OnTick] = Actions(t, "do", false);
                t.Done();
            }
            d.Create = () => new ContentPassive(spec);
            return (d, spec);
        }

        // ---- 变体英雄 ----

        private static HeroVariantDef ReadVariant(Obj o, out string active, out string passive)
        {
            var d = new HeroVariantDef
            {
                Key = o.Key(),
                Name = o.Str("name", ""),
                Description = o.Str("description", ""),
                BaseHero = o.Int("baseHero", 0),
                Strength = o.OptInt("strength"),
                Agility = o.OptInt("agility"),
                Stamina = o.OptInt("stamina"),
                StrengthGrowth = o.OptInt("strengthGrowth"),
                AgilityGrowth = o.OptInt("agilityGrowth"),
                StaminaGrowth = o.OptInt("staminaGrowth"),
                Attack = o.OptInt("attack"),
                Armor = o.OptInt("armor"),
                Hp = o.OptInt("hp"),
                Mp = o.OptInt("mp"),
                HpRecover = o.OptInt("hpRecover"),
                MpRecover = o.OptInt("mpRecover"),
                MoveSpeed = o.Has("moveSpeed") ? o.Num("moveSpeed", 0f) : (float?)null,
                AttackSpeed = o.Has("attackSpeed") ? o.Num("attackSpeed", 0f) : (float?)null,
                GameActiveSkill = o.OptInt("gameActiveSkill"),
                GamePassiveSkill = o.OptInt("gamePassiveSkill"),
                ModelFolder = o.Str("modelFolder", null),
                WorkshopModel = (ulong)Math.Max(0L, o.Long("workshopModel", 0)),
            };
            if (d.Name == "") o.Error("name", "变体要有名字");
            if (d.BaseHero < 1 || d.BaseHero > 43) o.Error("baseHero", "写原英雄的编号（1~43）");
            active = o.Str("activeSkill", null);
            passive = o.Str("passiveSkill", null);
            int skills = (active != null ? 1 : 0) + (passive != null ? 1 : 0) + (d.GameActiveSkill.HasValue ? 1 : 0) + (d.GamePassiveSkill.HasValue ? 1 : 0);
            if (skills > 1) o.Error("activeSkill", "activeSkill、passiveSkill、gameActiveSkill、gamePassiveSkill 只能写一个");
            return d;
        }

        // ---- 效果 ----

        // 效果名 → 允许的附加字段
        private static readonly Dictionary<string, string[]> Kinds = new Dictionary<string, string[]>
        {
            ["damage"] = new[] { "scale", "amount", "radius" },
            ["heal"] = new string[0],
            ["mana"] = new string[0],
            ["gold"] = new string[0],
            ["addStats"] = new[] { "seconds" },
            ["addAttackSpeed"] = new[] { "seconds" },
            ["addArmor"] = new[] { "seconds" },
            ["effect"] = new[] { "duration", "scale", "at" },
            ["animation"] = new[] { "speed", "duration" },
            ["notice"] = new[] { "seconds" },
        };

        private static readonly string[] Conditions = { "chance", "hpBelow", "isBoss", "cooldown" };

        private static List<EffectAction> Actions(Obj parent, string key, bool inSkill)
        {
            var result = new List<EffectAction>();
            var token = parent.Take(key);
            if (token == null) return result;
            if (!(token is JArray array))
            {
                parent.Error(key, "应该是效果的列表 [ { … }, … ]");
                return result;
            }
            for (int i = 0; i < array.Count; i++)
            {
                string where = $"{parent.Where}.{key}[{i}]";
                if (!(array[i] is JObject jo))
                {
                    parent.Errors.Add($"{where}：效果应该是 {{ \"heal\": 100 }} 这样的对象");
                    continue;
                }
                var o = new Obj(jo, where, parent.Errors);
                try
                {
                    var a = Action(o, inSkill);
                    o.Done();
                    if (a != null) result.Add(a);
                }
                catch (ContentError)
                {
                }
            }
            return result;
        }

        private static EffectAction Action(Obj o, bool inSkill)
        {
            var kinds = Kinds.Keys.Where(o.Has).ToList();
            if (kinds.Count != 1)
            {
                // 整个效果作废，字段都算看过了，不再逐个报"不认识"
                foreach (var k in o.Keys.ToList()) o.Take(k);
                o.Errors.Add(kinds.Count == 0
                    ? $"{o.Where}：没有效果（可以用：{string.Join("、", Kinds.Keys)}）；多余的字段：{string.Join("、", o.Keys)}"
                    : $"{o.Where}：一个效果只能写一种，这里有 {string.Join("、", kinds)}，拆成几个");
                return null;
            }
            string kind = kinds[0];
            foreach (var k in o.Keys.Where(k => k != kind && !Conditions.Contains(k) && !Kinds[kind].Contains(k)).ToList())
            {
                o.Take(k);
                o.Error(k, $"{kind} 不认识这个字段（可以用：{string.Join("、", Kinds[kind].Concat(Conditions))}）");
            }

            var a = new EffectAction { Where = o.Where };
            if (o.Has("chance")) a.Chance = o.Expr("chance");
            if (o.Has("hpBelow")) a.HpBelow = o.Expr("hpBelow");
            if (o.Has("isBoss")) a.IsBoss = o.Bool("isBoss", false);
            a.Cooldown = o.Num("cooldown", 0f);
            var seconds = o.Has("seconds") ? o.Expr("seconds") : null;

            switch (kind)
            {
                case "damage":
                {
                    if (!inSkill) o.Error(kind, "被动里暂时不能造成伤害");
                    string mode = o.Str(kind, "area");
                    var scale = o.Has("scale") ? o.Expr("scale") : null;
                    var amount = o.Has("amount") ? o.Expr("amount") : null;
                    var radius = o.Has("radius") ? o.Expr("radius") : null;
                    long Amount(EffectScope s) => (long)Math.Max(0, amount != null ? amount(s) : s.Host.SkillDamage * (scale?.Invoke(s) ?? 1));
                    if (mode == "area")
                        a.Run = s => s.Host.DamageArea(s.Position, radius != null ? (float)radius(s) : s.Host.Radius, Amount(s));
                    else if (mode == "target")
                    {
                        if (radius != null) o.Error("radius", "\"target\" 用不到 radius");
                        a.Run = s =>
                        {
                            if (s.Target != null) s.Host.DamageTarget(s.Target, Amount(s));
                        };
                    }
                    else o.Error(kind, $"写 \"area\"（范围）或 \"target\"（锁定的目标），不认识 \"{mode}\"");
                    break;
                }
                case "heal":
                {
                    var v = o.Expr(kind);
                    a.Run = s => s.Me?.Heal(v(s));
                    break;
                }
                case "mana":
                {
                    var v = o.Expr(kind);
                    a.Run = s => s.Me?.AddMana((int)Math.Round(v(s)));
                    break;
                }
                case "gold":
                {
                    var v = o.Expr(kind);
                    // 金币只在单人时给：联机时会让队伍经济失衡
                    a.Run = s =>
                    {
                        if (s.Me != null && Game.IsSolo) s.Me.AddGold((int)Math.Round(v(s)));
                    };
                    break;
                }
                case "addStats":
                {
                    var stats = o.Child(o.Take(kind), kind);
                    var str = stats.Has("str") ? stats.Expr("str") : null;
                    var agi = stats.Has("agi") ? stats.Expr("agi") : null;
                    var sta = stats.Has("sta") ? stats.Expr("sta") : null;
                    var atk = stats.Has("attack") ? stats.Expr("attack") : null;
                    stats.Done();
                    a.Run = s => Apply(s, seconds, me =>
                    {
                        int x = Round(str, s), y = Round(agi, s), z = Round(sta, s);
                        long w = Round(atk, s);
                        me.AddStats(x, y, z, w);
                        return () => me.AddStats(-x, -y, -z, -w);
                    });
                    break;
                }
                case "addAttackSpeed":
                {
                    var v = o.Expr(kind);
                    a.Run = s => Apply(s, seconds, me =>
                    {
                        float r = (float)v(s);
                        me.AddAttackSpeed(r);
                        return () => me.AddAttackSpeed(-r);
                    });
                    break;
                }
                case "addArmor":
                {
                    var v = o.Expr(kind);
                    a.Run = s => Apply(s, seconds, me =>
                    {
                        int n = Round(v, s);
                        me.AddArmor(n);
                        return () => me.AddArmor(-n);
                    });
                    break;
                }
                case "effect":
                {
                    string path = o.Str(kind, "");
                    if (path == "") o.Error(kind, "写游戏特效的路径，比如 Effect/SpellThunder");
                    var duration = o.Has("duration") ? o.Expr("duration") : ContentExprParser.Constant(1);
                    var scale = o.Has("scale") ? o.Expr("scale") : ContentExprParser.Constant(1);
                    string at = o.Str("at", "position");
                    if (at != "position" && at != "self" && at != "target") o.Error("at", "写 position（技能位置 / 事件目标）、self（自己）或 target（目标）");
                    a.Run = s =>
                    {
                        var pos = at == "self" ? s.Host.SelfPosition : at == "target" ? s.Target?.Position : s.Position;
                        if (pos.HasValue) s.Host.Effect(path, pos.Value, (float)duration(s), (float)scale(s));
                    };
                    break;
                }
                case "animation":
                {
                    if (!inSkill) o.Error(kind, "只有主动技能能换动作");
                    string clip = o.Str(kind, "");
                    float speed = o.Num("speed", 1f);
                    float? duration = o.Has("duration") ? o.Num("duration", 0f) : (float?)null;
                    a.Run = s => s.Host.Animate(clip, speed, duration);
                    break;
                }
                case "notice":
                {
                    string text = o.Str(kind, "");
                    a.Run = s =>
                    {
                        if (s.Me == null) return;
                        string shown = System.Text.RegularExpressions.Regex.Replace(text, @"\{(\d+)\}",
                            m => s.Host.Value(int.Parse(m.Groups[1].Value)).ToString("0.##", CultureInfo.InvariantCulture));
                        ScreenNotice.Show(shown, seconds != null ? (float)seconds(s) : 4f);
                    };
                    break;
                }
            }
            return a;
        }

        private static int Round(ContentExpr e, EffectScope s) => e == null ? 0 : (int)Math.Round(e(s));

        // 加属性一类：有 seconds 时到时间撤销，否则跟着技能走（主动技能结束 / 被动失去时撤销）
        private static void Apply(EffectScope s, ContentExpr seconds, Func<Player, Action> apply)
        {
            var me = s.Me;
            if (me == null) return;
            var revert = apply(me);
            Action safe = () =>
            {
                if (me.IsLocal) revert();
            };
            if (seconds != null) ContentTimers.After((float)seconds(s), safe);
            else s.Host.Hold(safe);
        }

        // ---- JSON 读取辅助：类型不对、多余字段都记成错误 ----

        internal sealed class ContentError : Exception
        {
        }

        private sealed class Obj
        {
            private readonly JObject o;
            private readonly HashSet<string> used = new HashSet<string>();
            public readonly string Where;
            public readonly List<string> Errors;

            public Obj(JObject o, string where, List<string> errors)
            {
                this.o = o;
                Where = where;
                Errors = errors;
            }

            public IEnumerable<string> Keys => o.Properties().Select(p => p.Name);

            public bool Has(string key) => o.Property(key) != null;

            public JToken Take(string key)
            {
                used.Add(key);
                return o.Property(key)?.Value;
            }

            public Obj Child(JToken token, string key)
            {
                if (token is JObject jo) return new Obj(jo, $"{Where}.{key}", Errors);
                Error(key, "应该是 { … }");
                throw new ContentError();
            }

            public void Error(string key, string message) => Errors.Add($"{Where}.{key}：{message}");

            public void Done()
            {
                foreach (var key in Keys.Where(k => !used.Contains(k)))
                    Errors.Add($"{Where}：不认识的字段 \"{key}\"（大小写也要一致）");
            }

            public T Guard<T>(string key, Func<T> read)
            {
                try
                {
                    return read();
                }
                catch (Exception e)
                {
                    Error(key, e.Message);
                    throw new ContentError();
                }
            }

            public string Key()
            {
                string key = Str("key", "");
                if (key == "" || !key.All(c => c < 128 && (char.IsLetterOrDigit(c) || c == '_')))
                {
                    Error("key", $"只能用英文字母、数字、下划线：\"{key}\"");
                    throw new ContentError();
                }
                return key;
            }

            public string Str(string key, string fallback)
            {
                var t = Take(key);
                if (t == null) return fallback;
                if (t.Type == JTokenType.String) return (string)t;
                Error(key, "应该是文字（加引号）");
                return fallback;
            }

            public float Num(string key, float fallback)
            {
                var t = Take(key);
                if (t == null) return fallback;
                if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) return (float)t;
                Error(key, "应该是数字");
                return fallback;
            }

            public long Long(string key, long fallback)
            {
                var t = Take(key);
                if (t == null) return fallback;
                if (t.Type == JTokenType.Integer) return (long)t;
                Error(key, "应该是整数");
                return fallback;
            }

            public int Int(string key, int fallback) => (int)Long(key, fallback);

            public int? OptInt(string key) => Has(key) ? Int(key, 0) : (int?)null;

            public bool Bool(string key, bool fallback)
            {
                var t = Take(key);
                if (t == null) return fallback;
                if (t.Type == JTokenType.Boolean) return (bool)t;
                Error(key, "应该是 true 或 false");
                return fallback;
            }

            public T Enum<T>(string key, T fallback) where T : struct
            {
                string s = Str(key, null);
                if (s == null) return fallback;
                if (System.Enum.TryParse(s, false, out T v) && System.Enum.IsDefined(typeof(T), v)) return v;
                Error(key, $"可以写：{string.Join("、", System.Enum.GetNames(typeof(T)))}");
                return fallback;
            }

            public float[] Nums(string key)
            {
                var t = Take(key);
                if (t == null) return new float[0];
                if (t is JArray a && a.All(x => x.Type == JTokenType.Integer || x.Type == JTokenType.Float))
                    return a.Select(x => (float)x).ToArray();
                Error(key, "应该是数字列表，比如 [20, 5]");
                return new float[0];
            }

            public string[] Strs(string key)
            {
                var t = Take(key);
                if (t == null) return null;
                if (t is JArray a && a.All(x => x.Type == JTokenType.String)) return a.Select(x => (string)x).ToArray();
                Error(key, "应该是文字列表，比如 [\"Area\", \"Buff\"]");
                return null;
            }

            public ContentExpr Expr(string key)
            {
                var t = Take(key);
                if (t == null) return ContentExprParser.Constant(0);
                if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) return ContentExprParser.Constant((double)t);
                if (t.Type == JTokenType.String)
                {
                    try
                    {
                        return ContentExprParser.Parse((string)t);
                    }
                    catch (FormatException e)
                    {
                        Error(key, e.Message);
                        return ContentExprParser.Constant(0);
                    }
                }
                Error(key, "应该是数字或表达式（比如 \"damage * {1} / 100\"）");
                return ContentExprParser.Constant(0);
            }
        }
    }
}
