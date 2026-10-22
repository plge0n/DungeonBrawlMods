using System.Linq;
using System.Text;
using BepInEx.Logging;

namespace DamageMeter
{
    internal static class Summary
    {
        public static void Log(ManualLogSource log, string label, Period period)
        {
            long total = period.Total;
            if (total <= 0) return;
            var sb = new StringBuilder();
            sb.Append($"[{label}] 总计 {Meter.Format(total)}，DPS {Meter.Format(period.Dps)}，用时 {Meter.FormatTime(period.Seconds)}，")
              .Append($"承受 {Meter.Format(period.Taken)}，击杀 {period.Kills}");
            foreach (var s in period.Sources)
                sb.Append($"\n  {s.Name}：{Meter.Format(s.Damage)}（{100.0 * s.Damage / total:0.0}%），{s.Hits} 次，")
                  .Append($"暴击 {(s.Hits > 0 ? 100f * s.Crits / s.Hits : 0f):0}%，最高 {Meter.Format(s.MaxHit)}");
            log.LogInfo(sb.ToString());
        }
    }
}
