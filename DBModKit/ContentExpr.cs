using System;
using System.Collections.Generic;
using System.Globalization;

namespace DBModKit
{
    // 内容包里的数值表达式：数字、{n}（技能的第 n 个数值）、变量（damage、maxHp、hp、mana、maxMana、str、agi、sta、attack、level、wave）、
    // + - * / 和括号。读包时编译成委托，写错在读包时就报出来；不引入脚本引擎
    internal delegate double ContentExpr(EffectScope scope);

    internal static class ContentExprParser
    {
        private static readonly Dictionary<string, ContentExpr> Variables = new Dictionary<string, ContentExpr>(StringComparer.OrdinalIgnoreCase)
        {
            ["damage"] = s => s.Damage,
            ["maxHp"] = s => s.Me?.MaxHp ?? 0,
            ["hp"] = s => s.Me?.Hp ?? 0,
            ["mana"] = s => s.Me?.Mana ?? 0,
            ["maxMana"] = s => s.Me?.MaxMana ?? 0,
            ["str"] = s => s.Me?.Strength ?? 0,
            ["agi"] = s => s.Me?.Agility ?? 0,
            ["sta"] = s => s.Me?.Stamina ?? 0,
            ["attack"] = s => s.Me?.Attack ?? 0,
            ["level"] = s => s.Host.Level,
            ["wave"] = s => Game.Wave,
        };

        public static string VariableNames => string.Join("、", Variables.Keys);

        /// <summary>编译；写错时抛 FormatException，消息给内容作者看。</summary>
        public static ContentExpr Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("表达式是空的");
            var p = new Parser(text);
            var e = p.Sum();
            p.SkipSpace();
            if (!p.End) throw p.Error("多出来的内容");
            return e;
        }

        public static ContentExpr Constant(double v) => _ => v;

        private sealed class Parser
        {
            private readonly string s;
            private int i;

            public Parser(string text) => s = text;

            public bool End => i >= s.Length;

            public FormatException Error(string what) => new FormatException($"表达式 \"{s}\" 第 {i + 1} 个字符：{what}");

            public void SkipSpace()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            private bool Eat(char c)
            {
                SkipSpace();
                if (i < s.Length && s[i] == c)
                {
                    i++;
                    return true;
                }
                return false;
            }

            public ContentExpr Sum()
            {
                var left = Product();
                while (true)
                {
                    if (Eat('+'))
                    {
                        var a = left;
                        var b = Product();
                        left = x => a(x) + b(x);
                    }
                    else if (Eat('-'))
                    {
                        var a = left;
                        var b = Product();
                        left = x => a(x) - b(x);
                    }
                    else return left;
                }
            }

            private ContentExpr Product()
            {
                var left = Unary();
                while (true)
                {
                    if (Eat('*'))
                    {
                        var a = left;
                        var b = Unary();
                        left = x => a(x) * b(x);
                    }
                    else if (Eat('/'))
                    {
                        var a = left;
                        var b = Unary();
                        // 除以 0 得 0，不让 NaN、无穷大流进游戏
                        left = x =>
                        {
                            double d = b(x);
                            return d == 0 ? 0 : a(x) / d;
                        };
                    }
                    else return left;
                }
            }

            private ContentExpr Unary()
            {
                if (Eat('-'))
                {
                    var a = Unary();
                    return x => -a(x);
                }
                if (Eat('+')) return Unary();
                return Atom();
            }

            private ContentExpr Atom()
            {
                SkipSpace();
                if (End) throw Error("表达式不完整");
                char c = s[i];
                if (c == '(')
                {
                    i++;
                    var e = Sum();
                    if (!Eat(')')) throw Error("缺少 )");
                    return e;
                }
                if (c == '{')
                {
                    int start = ++i;
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                    if (i == start || i >= s.Length || s[i] != '}') throw Error("{n} 里要写数值的序号，比如 {0}");
                    int n = int.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
                    i++;
                    return x => x.Host.Value(n);
                }
                if (char.IsDigit(c) || c == '.')
                {
                    int start = i;
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                    if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                        throw Error("数字写错了");
                    return Constant(v);
                }
                if (char.IsLetter(c))
                {
                    int start = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                    string name = s.Substring(start, i - start);
                    if (Variables.TryGetValue(name, out var v)) return v;
                    i = start;
                    throw Error($"不认识的变量 \"{name}\"（可以用：{VariableNames}）");
                }
                throw Error($"不认识的字符 '{c}'");
            }
        }
    }
}
