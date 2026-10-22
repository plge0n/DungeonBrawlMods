using System.Collections.Generic;
using DBModKit;
using UnityEngine;

namespace SmartMove
{
    // 每帧（玩家没按方向键时）检查是否站在地面预警里，是就走出去
    internal static class Planner
    {
        // 候选方向数、每个方向最远试探多远
        private const int Directions = 16;
        private const float Step = 0.5f;
        private const float MaxProbe = 8f;
        // 离场地边缘至少留这么远，避免贴墙卡住
        private const float WallMargin = 0.6f;

        private static string lastReason;

        public static MoveCommand Decide(MoveContext ctx)
        {
            var cmd = Plan(ctx);
            if (Plugin.VerboseLog.Value)
            {
                string reason = cmd.Override ? cmd.Reason : null;
                if (reason != lastReason) Plugin.Log.LogInfo(reason ?? "交还原版");
                lastReason = reason;
            }
            return cmd;
        }

        private static MoveCommand Plan(MoveContext ctx)
        {
            if (!Plugin.Enabled.Value) return MoveCommand.None;
            if (!Game.AutoBattle && !Plugin.WithoutAutoBattle.Value) return MoveCommand.None;

            var p = ctx.Player;
            var telegraphs = Arena.Telegraphs();
            float margin = p.HitRadius + Plugin.Margin.Value;

            Telegraph danger = null;
            foreach (var t in telegraphs)
                if (t.Contains(p.Position, margin) && (danger == null || t.TimeLeft < danger.TimeLeft))
                    danger = t;
            if (danger == null) return MoveCommand.None;

            // 攻击出手前躲也会打断攻击，但冷却会立刻转好，损失很小
            var dir = Escape(p, telegraphs, margin);
            return dir is Vector3 d ? MoveCommand.Move(d, $"躲避预警（{danger.TimeLeft:F1}秒后生效）") : MoveCommand.None;
        }

        // 找最快走出所有预警的方向；路上撞到场地边缘的方向不要。找不到返回 null
        private static Vector3? Escape(Player p, List<Telegraph> telegraphs, float margin)
        {
            Vector3 origin = p.Position;
            var enemies = p.Enemies();
            Vector3? best = null;
            float bestCost = float.MaxValue;
            for (int i = 0; i < Directions; i++)
            {
                Vector3 dir = Direction(i);
                for (float dist = Step; dist <= MaxProbe; dist += Step)
                {
                    Vector3 pos = origin + dir * dist;
                    if (!Arena.IsInside(pos, WallMargin)) break;
                    if (InsideAny(telegraphs, pos, margin)) continue;
                    // 走出去的距离为主；落脚点在敌人攻击范围内的略微扣分
                    float cost = dist + 0.5f * CountReach(p, enemies, pos);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        best = dir;
                    }
                    break;
                }
            }
            return best;
        }

        private static bool InsideAny(List<Telegraph> telegraphs, Vector3 pos, float margin)
        {
            foreach (var t in telegraphs)
                if (t.Contains(pos, margin)) return true;
            return false;
        }

        private static int CountReach(Player p, List<Role> enemies, Vector3 pos)
        {
            int n = 0;
            foreach (var e in enemies)
                if (Flat(pos - e.Position).magnitude <= e.AttackRange + p.HitRadius) n++;
            return n;
        }

        private static Vector3 Direction(int i)
        {
            float a = i * Mathf.PI * 2f / Directions;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
