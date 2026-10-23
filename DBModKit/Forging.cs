using System;

namespace DBModKit
{
    /// <summary>属性锻造器的档次（商店里的属性锻造器 / 黄金属性锻造器 / 史诗属性锻造器）。</summary>
    public enum ForgeTier { Normal, Gold, Epic }

    /// <summary>
    /// 免费用一次属性锻造器：和在商店买一个完全一样（抽稀有度、弹"属性选择"三选一、选中后加属性，幸运和锻造器增幅照常生效），
    /// 只是不花钱、价格不涨。锻造器不是能捡的道具，所以没有"掉一个锻造器"，只能直接打开。
    /// 会弹三选一，最好在休息阶段用：战斗中弹出来会挡住操作，游戏也不会暂停。
    /// </summary>
    public static class Forging
    {
        /// <summary>现在能打开：在战斗里，并且三选一界面空闲。</summary>
        public static bool CanOpen => ModKit.Compatible && Game.InBattle && GameRaw.CanForge();

        /// <summary>打开一次锻造，界面忙时返回 false（这时什么都不做，稍后再试）。</summary>
        public static bool Open(ForgeTier tier = ForgeTier.Normal)
        {
            if (!CanOpen) return false;
            string caller = RunLog.CallerMod();
            try
            {
                GameRaw.Forge((int)tier);
            }
            catch (Exception e)
            {
                Safe.Report("锻造", e);
                return false;
            }
            RunLog.Info(caller, "锻造", "打开属性锻造", ("档次", tier));
            return true;
        }
    }
}
