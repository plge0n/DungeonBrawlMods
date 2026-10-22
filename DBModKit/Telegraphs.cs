using System.Collections.Generic;
using UnityEngine;

namespace DBModKit
{
    public enum TelegraphShape
    {
        /// <summary>圆形或扇形（Angle ≥ 360 为整圆）。</summary>
        Sector,
        /// <summary>从 Origin 沿 Direction 延伸 Length、宽 Width 的矩形。</summary>
        Rect,
    }

    /// <summary>
    /// 敌人技能、陷阱在地面上的红色预警。倒计时结束时在这个范围内的角色会被打中（判定还会加上角色体型半径）。
    /// </summary>
    public sealed class Telegraph
    {
        public TelegraphShape Shape { get; private set; }
        /// <summary>圆心，或矩形起点。</summary>
        public Vector3 Origin { get; private set; }
        /// <summary>扇形朝向，或矩形延伸方向（水平单位向量）。</summary>
        public Vector3 Direction { get; private set; }
        public float Radius { get; private set; }
        /// <summary>扇形全角（度）。</summary>
        public float Angle { get; private set; }
        public float Width { get; private set; }
        public float Length { get; private set; }
        /// <summary>距离生效还有多少秒。</summary>
        public float TimeLeft { get; private set; }

        internal static Telegraph Sector(Vector3 center, float radius, Vector3 dir, float angle, float timeLeft) => new Telegraph
        {
            Shape = TelegraphShape.Sector,
            Origin = Flat(center),
            Direction = Dir(dir),
            Radius = radius,
            Angle = angle,
            TimeLeft = timeLeft,
        };

        internal static Telegraph Rect(Vector3 origin, Vector3 dir, float width, float length, float timeLeft) => new Telegraph
        {
            Shape = TelegraphShape.Rect,
            Origin = Flat(origin),
            Direction = Dir(dir),
            Width = width,
            Length = length,
            TimeLeft = timeLeft,
        };

        /// <summary>点是否在预警范围内；margin 是向外扩的距离（自身体型半径 + 安全余量）。</summary>
        public bool Contains(Vector3 point, float margin)
        {
            Vector3 d = Flat(point) - Origin;
            if (Shape == TelegraphShape.Rect)
            {
                float along = Vector3.Dot(d, Direction);
                float side = Vector3.Dot(d, new Vector3(Direction.z, 0f, -Direction.x));
                return along >= -margin && along <= Length + margin && Mathf.Abs(side) <= Width / 2f + margin;
            }

            float dist = d.magnitude;
            if (dist > Radius + margin) return false;
            if (Angle >= 360f || dist <= margin) return true;
            // 扇形：按角度判定，边缘再放宽 margin 对应的角度
            float slack = Mathf.Atan2(margin, dist) * Mathf.Rad2Deg;
            return Vector3.Angle(d, Direction) <= Angle / 2f + slack;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static Vector3 Dir(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }
    }

    /// <summary>场上的地面预警和场地边界。</summary>
    public static class Arena
    {
        private static readonly List<Telegraph> buffer = new List<Telegraph>();

        /// <summary>当前所有地面预警（不区分来源：敌人技能、陷阱等都对玩家有害）。</summary>
        public static List<Telegraph> Telegraphs()
        {
            var result = new List<Telegraph>();
            if (!ModKit.Compatible) return result;
            GameRaw.ReadTelegraphs(buffer);
            result.AddRange(buffer);
            return result;
        }

        /// <summary>位置在场地边界内，且离边缘至少 margin。不检查场内障碍物（用 <see cref="FreeDistance"/> / <see cref="ObstacleClearance"/>）。</summary>
        public static bool IsInside(Vector3 position, float margin = 0f)
            => !ModKit.Compatible || GameRaw.InsideArena(position, margin);

        private static readonly RaycastHit[] castHits = new RaycastHit[32];
        private static readonly Collider[] overlapHits = new Collider[64];

        /// <summary>
        /// 玩家沿 direction 最多能走多远（≤ max）才会撞上场景碰撞体（墙、柱子、石头等）。
        /// 用玩家自己的碰撞体形状和碰撞层检测，所以和实际移动时挡不挡一致；角色、触发器、脚下的地面不算。
        /// </summary>
        public static float FreeDistance(Player player, Vector3 direction, float max)
        {
            if (!ModKit.Compatible || !Probe(player, out var cc, out int mask, out float feet)) return max;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return max;
            direction.Normalize();

            // 胶囊底部抬高到台阶高度以上，免得擦到地面
            float r = cc.radius * 0.9f;
            Vector3 center = cc.bounds.center;
            Vector3 bottom = new Vector3(center.x, feet + cc.stepOffset + r, center.z);
            Vector3 top = new Vector3(center.x, Mathf.Max(bottom.y, cc.bounds.max.y - r), center.z);

            int n = Physics.CapsuleCastNonAlloc(bottom, top, r, direction, castHits, max, mask, QueryTriggerInteraction.Ignore);
            float best = max;
            for (int i = 0; i < n; i++)
            {
                var hit = castHits[i];
                if (!IsObstacle(hit.collider, cc, feet)) continue;
                // 一开始就重叠的碰撞体 distance 为 0：已经贴着它了，只挡朝它走的方向
                if (hit.distance <= 0f)
                {
                    Vector3 toward = hit.collider.ClosestPoint(center) - center;
                    toward.y = 0f;
                    if (toward.sqrMagnitude > 1e-6f && Vector3.Dot(toward, direction) <= 0f) continue;
                }
                if (hit.distance < best) best = hit.distance;
            }
            return best;
        }

        /// <summary>位置离最近的场景障碍物（墙、柱子、石头等）的水平距离，最多算到 max。不含场地边界（用 <see cref="IsInside"/>）。</summary>
        public static float ObstacleClearance(Player player, Vector3 position, float max)
        {
            if (!ModKit.Compatible || !Probe(player, out var cc, out int mask, out float feet)) return max;
            float height = cc.bounds.center.y - player.Position.y;
            Vector3 center = new Vector3(position.x, position.y + height, position.z);

            int n = Physics.OverlapSphereNonAlloc(center, max, overlapHits, mask, QueryTriggerInteraction.Ignore);
            float best = max;
            for (int i = 0; i < n; i++)
            {
                var col = overlapHits[i];
                if (!IsObstacle(col, cc, feet)) continue;
                // 非凸网格不支持 ClosestPoint（会原样返回输入点），退而用包围盒
                Vector3 p = col is MeshCollider mesh && !mesh.convex ? col.ClosestPointOnBounds(center) : col.ClosestPoint(center);
                float d = new Vector2(p.x - center.x, p.z - center.z).magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        private static bool Probe(Player player, out CharacterController cc, out int mask, out float feet)
        {
            cc = player == null ? null : GameRaw.Controller(player.Native);
            mask = 0;
            feet = 0f;
            if (cc == null || !cc.enabled) return false;
            // 和玩家所在层发生碰撞的层，就是会挡住移动的层
            int layer = cc.gameObject.layer;
            for (int i = 0; i < 32; i++)
                if (!Physics.GetIgnoreLayerCollision(layer, i)) mask |= 1 << i;
            feet = cc.bounds.min.y;
            return true;
        }

        // 地面、矮于台阶高度的东西能直接走过去；角色之间的碰撞不算障碍物（敌人会动）
        private static bool IsObstacle(Collider col, CharacterController self, float feet)
        {
            if (col == null || col == self || col.isTrigger) return false;
            if (col.bounds.max.y <= feet + self.stepOffset) return false;
            return !GameRaw.IsRoleCollider(col);
        }
    }
}
