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
        // 落脚点离墙或障碍物小于这个距离时扣分：每近 1 米相当于多走 WallWeight 米
        private const float PreferredClearance = 4f;
        private const float ClearanceStep = 0.5f;
        private const float WallWeight = 1.2f;
        // 路线离障碍物至少留这么远
        private const float ObstacleMargin = 0.3f;
        // 走出预警后最多再往前多走多远找更好的落脚点
        private const float ExtraWalk = 3f;

        // 选定的落脚点：走出预警后继续走到这里，避免停在预警边缘（往往贴着墙）
        private const float GoalReached = 0.4f;
        // 预计走到落脚点的时间之外多给的时间，超时说明被卡住了
        private const float GoalSlack = 0.5f;

        private static Vector3? goal;
        private static float goalUntil;
        private static int lastFrame;
        private static string lastReason;

        public static void Reset() => goal = null;

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
            // 中间有帧没被询问，说明玩家在按方向键（或在放技能），之前的落脚点作废
            if (Time.frameCount - lastFrame > 1) goal = null;
            lastFrame = Time.frameCount;

            if (!Plugin.Enabled.Value || (!Game.AutoBattle && !Plugin.WithoutAutoBattle.Value))
            {
                goal = null;
                return MoveCommand.None;
            }

            var p = ctx.Player;
            var telegraphs = Arena.Telegraphs();
            float margin = p.HitRadius + Plugin.Margin.Value;

            Telegraph danger = null;
            foreach (var t in telegraphs)
                if (t.Contains(p.Position, margin) && (danger == null || t.TimeLeft < danger.TimeLeft))
                    danger = t;

            // 落脚点被新的预警盖住、超时（卡住）了就重新找
            if (goal is Vector3 g && (Time.time > goalUntil || InsideAny(telegraphs, g, margin))) goal = null;

            if (danger != null && goal == null)
            {
                goal = Escape(p, telegraphs, margin, danger.TimeLeft);
                if (goal is Vector3 found)
                    goalUntil = Time.time + Flat(found - p.Position).magnitude / Mathf.Max(1f, p.MoveSpeed) + GoalSlack;
            }
            if (goal is not Vector3 target) return MoveCommand.None;

            Vector3 to = Flat(target - p.Position);
            if (to.magnitude <= GoalReached)
            {
                goal = null;
                return MoveCommand.None;
            }
            // 攻击出手前躲也会打断攻击，但冷却会立刻转好，损失很小
            string reason = danger != null ? $"躲避预警（{danger.TimeLeft:F1}秒后生效）" : "走到离墙远的落脚点";
            return MoveCommand.Move(to, reason);
        }

        // 找走出所有预警的落脚点：路程短、落脚点离墙和障碍物远（贴墙、进墙角下一个预警就躲不开了）、不在敌人攻击范围内。
        // 路上撞到场地边缘或障碍物的方向不要；找不到返回 null
        private static Vector3? Escape(Player p, List<Telegraph> telegraphs, float margin, float timeLeft)
        {
            Vector3 origin = p.Position;
            var enemies = p.Enemies();
            float speed = Mathf.Max(1f, p.MoveSpeed);
            Vector3? best = null;
            float bestCost = float.MaxValue;
            for (int i = 0; i < Directions; i++)
            {
                Vector3 dir = Direction(i);
                // 这个方向走多远会撞上障碍物，留一点距离免得蹭着走
                float free = Arena.FreeDistance(p, dir, MaxProbe + Step) - ObstacleMargin;
                // 出了预警还可以多走几步换一个离墙更远的落脚点
                float firstSafe = -1f;
                for (float dist = Step; dist <= MaxProbe && dist <= free; dist += Step)
                {
                    Vector3 pos = origin + dir * dist;
                    if (!Arena.IsInside(pos, WallMargin)) break;
                    if (InsideAny(telegraphs, pos, margin))
                    {
                        if (firstSafe >= 0f) break;
                        continue;
                    }
                    if (firstSafe < 0f) firstSafe = dist;
                    else if (dist - firstSafe > ExtraWalk) break;

                    float cost = dist
                        + WallWeight * Mathf.Max(0f, PreferredClearance - Clearance(p, pos))
                        + 0.5f * CountReach(p, enemies, pos);
                    // 来不及走到的落脚点基本不考虑
                    if (dist / speed > timeLeft) cost += 10f;
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        best = pos;
                    }
                }
            }
            return best;
        }

        // 落脚点离场地边缘、场内障碍物的距离（边缘只精确到 ClearanceStep，最多算到 PreferredClearance）
        private static float Clearance(Player p, Vector3 pos)
        {
            float c = WallMargin;
            while (c < PreferredClearance && Arena.IsInside(pos, c + ClearanceStep)) c += ClearanceStep;
            return Mathf.Min(c, Arena.ObstacleClearance(p, pos, PreferredClearance));
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
