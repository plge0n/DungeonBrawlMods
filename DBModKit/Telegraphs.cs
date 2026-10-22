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

        /// <summary>位置在场地边界内，且离边缘至少 margin。不检查场内障碍物。</summary>
        public static bool IsInside(Vector3 position, float margin = 0f)
            => !ModKit.Compatible || GameRaw.InsideArena(position, margin);
    }
}
