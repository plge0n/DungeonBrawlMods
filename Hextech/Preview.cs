using System;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using DBModKit;

namespace Hextech
{
    // 生成预览：把所有英雄各品质会生成的卡写进 BepInEx/Hextech/preview.txt，调平衡用
    internal static class Preview
    {
        public static string Dump(int seed)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"海克斯生成预览（种子 {seed}，{DateTime.Now:yyyy-MM-dd HH:mm:ss}）");
            foreach (var entry in GameData.Entries(DataTable.Hero))
            {
                if (!int.TryParse(entry.Key, out int heroId)) continue;
                var hs = HeroSkill.Of(heroId);
                if (hs == null) continue;
                sb.AppendLine();
                if (!SkillData.Supported(hs.IsActive, hs.SkillId))
                {
                    sb.AppendLine($"=== {entry.Name}：{hs.Name}（{(hs.IsActive ? "主动" : "被动")} {hs.SkillId}）暂不支持");
                    continue;
                }
                var p = hs.IsActive
                    ? SkillData.Active(hs.SkillId, hs.Name, hs.Cooldown, hs.Cost, hs.Duration, hs.Charges)
                    : SkillData.Passive(hs.SkillId, hs.Name, hs.PassiveValues.ToArray());
                sb.AppendLine($"=== {entry.Name}：{hs.Name}（{(hs.IsActive ? "主动" : "被动")} {hs.SkillId}）积木：{string.Join(" ", Generator.Candidates(p).Select(b => b.Word))}");
                var rng = new Random(seed ^ heroId);
                foreach (Tier t in Enum.GetValues(typeof(Tier)))
                    foreach (var c in Generator.Offer(p, t, 3, rng))
                        sb.AppendLine($"  [{Engine.TierName(t)}] {c.Name}：{string.Join("；", c.Lines)}");
            }
            string dir = Path.Combine(Paths.BepInExRootPath, "Hextech");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "preview.txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            return path;
        }
    }
}
