using System;
using UnityEngine;

namespace DBModKit
{
    /// <summary>屏蔽游戏输入：打开自己的窗口时，键盘和点击不要漏到游戏里。</summary>
    public static class GameInput
    {
        /// <summary>
        /// 屏蔽移动、技能键、Tab（商店）、ESC（游戏菜单），直到返回值被 Dispose；可以嵌套。
        /// 点击穿透不用管：鼠标在带 Image 的 UI 上时游戏自己会忽略点击。
        /// 一定要释放（热重载时在 onUnload 里关窗口），否则游戏会一直不响应操作。
        /// </summary>
        public static IDisposable Block() => ModKit.Compatible ? GameRaw.BlockInput() : Disposable.Empty;
    }

    /// <summary>暂停游戏时间（Time.timeScale = 0）。联机时不要用：别人的游戏不会停。</summary>
    public static class GamePause
    {
        private static int count;
        private static float saved = 1f;

        public static bool IsPaused => count > 0;

        /// <summary>暂停，直到返回值被 Dispose；可以嵌套，全部释放后恢复原来的速度。</summary>
        public static IDisposable Acquire()
        {
            if (count++ == 0)
            {
                saved = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }
            return new Disposable(Release);
        }

        private static void Release()
        {
            if (count == 0 || --count > 0) return;
            if (Time.timeScale == 0f) Time.timeScale = saved;
        }

        // 暂停期间游戏自己改了速度（比如倍速按钮），记下新值并继续保持暂停
        internal static void Tick()
        {
            if (count == 0 || Time.timeScale == 0f) return;
            saved = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    /// <summary>只执行一次的 IDisposable。</summary>
    public sealed class Disposable : IDisposable
    {
        public static readonly IDisposable Empty = new Disposable(null);

        private Action action;

        public Disposable(Action action) => this.action = action;

        public void Dispose()
        {
            var a = action;
            action = null;
            a?.Invoke();
        }
    }
}
