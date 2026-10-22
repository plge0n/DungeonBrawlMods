using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DBModKit
{
    // Harmony 补丁统一在这里按签名查找并安装；找不到或安装失败只停用对应功能，不影响其他部分
    internal static class Hooks
    {
        /// <summary>安装补丁。排行榜拦截装不上时返回 false，ModKit 整体停用。</summary>
        public static bool Install(Harmony harmony)
        {
            if (!TryPatch(harmony, typeof(LeaderboardHooks), "排行榜上传拦截",
                    AccessTools.Method(typeof(bwu), "quj", new[]
                    {
                        typeof(int), typeof(int), typeof(de.TeamBuildData), typeof(Action<bws>),
                        typeof(Action<LeaderboardUploadStage>), typeof(string),
                    }),
                    prefix: nameof(LeaderboardHooks.UploadPrefix)))
                return false;

            AutoCast.Available =
                TryPatch(harmony, typeof(AutoCastHooks), "自动释放决策",
                    AccessTools.Method(typeof(ez), "dxx", new[] { typeof(ActiveSkillEnum), typeof(bbe), typeof(int) }),
                    prefix: nameof(AutoCastHooks.DecidePrefix), postfix: nameof(AutoCastHooks.DecidePostfix))
                && TryPatch(harmony, typeof(AutoCastHooks), "自动释放选目标",
                    AccessTools.Method(typeof(ez), "dxy", new[] { typeof(bbe), typeof(ActiveSkillData) }),
                    prefix: nameof(AutoCastHooks.TargetPrefix));

            GameEvents.WaveEventsAvailable = TryPatch(harmony, typeof(WaveHooks), "波次事件",
                AccessTools.Method(typeof(MyServerNetworkManager), "qhu", new[] { typeof(ClientNetMessage) }),
                postfix: nameof(WaveHooks.MessagePostfix));

            // 分开打：gad 失败时 ModKit 在进入战斗时补写倍率，gaf 失败时出场覆盖的数值不乘倍率
            GameData.Available = TryPatch(harmony, typeof(GameDataHooks), "怪物倍率（出场覆盖）",
                AccessTools.Method(typeof(oo), "gaf", new[] { typeof(EnemyType), typeof(EnemySpawnKind), typeof(int) }),
                postfix: nameof(GameDataHooks.EnemyAttributePostfix));
            TryPatch(harmony, typeof(GameDataHooks), "怪物倍率（表加载）",
                AccessTools.Method(typeof(oo), "gad", Type.EmptyTypes),
                postfix: nameof(GameDataHooks.EnemyTablePostfix));

            HeroSkins.Available = TryPatch(harmony, typeof(HeroSkinHooks), "英雄皮肤（查找模型）",
                AccessTools.Method(typeof(buj), "pwu", new[] { typeof(int), typeof(string).MakeByRefType(), typeof(LocalWorkshopManifest).MakeByRefType() }),
                prefix: nameof(HeroSkinHooks.ResolvePrefix));
            // 出生、变身期间记下是哪个角色：pwu 据此对其他玩家返回原版（模型和名字都按原版）
            HeroSkins.OnlyLocalPlayerAvailable =
                TryPatch(harmony, typeof(HeroSkinHooks), "英雄皮肤（出生）",
                    AccessTools.Method(typeof(bbe), "kzz", new[] { typeof(Vector3), typeof(uint), typeof(HeroType), typeof(string) }),
                    prefix: nameof(HeroSkinHooks.SpawnPrefix), finalizer: nameof(HeroSkinHooks.RestoreOwner))
                & TryPatch(harmony, typeof(HeroSkinHooks), "英雄皮肤（变身技能）",
                    AccessTools.Method(typeof(bgt), "mqp", new[] { typeof(string) }),
                    prefix: nameof(HeroSkinHooks.SkillTransformPrefix), finalizer: nameof(HeroSkinHooks.RestoreOwner))
                & TryPatch(harmony, typeof(HeroSkinHooks), "英雄皮肤（变身恢复）",
                    AccessTools.Method(typeof(bbp), "lob", new[] { typeof(bbe) }),
                    prefix: nameof(HeroSkinHooks.CheckpointTransformPrefix), finalizer: nameof(HeroSkinHooks.RestoreOwner));

            // 开关技能开着时，自动释放直接把它关掉，不经过 dxx
            if (AutoCast.Available)
                TryPatch(harmony, typeof(AutoCastHooks), "开关技能自动关闭",
                    AccessTools.Method(typeof(bht), "kcac92c2266be41d3979b385715ce318e", new Type[0]),
                    prefix: nameof(AutoCastHooks.TogglePrefix), postfix: nameof(AutoCastHooks.TogglePostfix));

            MoveControl.Available =
                TryPatch(harmony, typeof(MoveHooks), "走位（待机/移动）",
                    AccessTools.Method(typeof(bbe), "kqi", new[] { typeof(bool) }),
                    prefix: nameof(MoveHooks.Prefix), postfix: nameof(MoveHooks.Postfix))
                && TryPatch(harmony, typeof(MoveHooks), "走位（攻击中）",
                    AccessTools.Method(typeof(bbe), "kqh", new Type[0]),
                    prefix: nameof(MoveHooks.AttackPrefix), postfix: nameof(MoveHooks.Postfix));

            // 伤害上下文：没装上时命中事件照常，只是没有类型、来源和技能实例
            Combat.ContextAvailable =
                TryPatch(harmony, typeof(DamageContextHooks), "伤害上下文（iwk）",
                    AccessTools.Method(typeof(yb), "iwk", new[]
                    {
                        typeof(yb), typeof(double), typeof(float), typeof(AttackType), typeof(bool), typeof(float?),
                        typeof(ActiveSkillEnum), typeof(DamageSourceKind), typeof(bfe), typeof(PlayerDamageFeedbackKind),
                    }),
                    prefix: nameof(DamageContextHooks.HitPrefix), finalizer: nameof(DamageContextHooks.Pop))
                & TryPatch(harmony, typeof(DamageContextHooks), "伤害上下文（iwr）",
                    AccessTools.Method(typeof(yb), "iwr", new[] { typeof(AttackType), typeof(double), typeof(bool), typeof(yb), typeof(bool) }),
                    prefix: nameof(DamageContextHooks.DotPrefix), finalizer: nameof(DamageContextHooks.Pop));

            SkillTuning.Available =
                TryPatch(harmony, typeof(TuningHooks), "技能调整（创建技能）",
                    AccessTools.Method(typeof(ez), "dzc", new[]
                    {
                        typeof(uint), typeof(ActiveSkillEnum), typeof(Vector3), typeof(yb), typeof(int), typeof(float), typeof(int), typeof(int), typeof(bff),
                    }),
                    prefix: nameof(TuningHooks.FactoryPrefix), finalizer: nameof(TuningHooks.Restore))
                && TryPatch(harmony, typeof(TuningHooks), "技能调整（按键检查）",
                    AccessTools.Method(typeof(ez), "dye", new[] { typeof(ActiveSkillEnum), typeof(bbe), typeof(int) }),
                    prefix: nameof(TuningHooks.CheckPrefix), finalizer: nameof(TuningHooks.Restore))
                && TryPatch(harmony, typeof(TuningHooks), "技能调整（释放）",
                    AccessTools.Method(typeof(ez), "dyf", new[]
                    {
                        typeof(bbe), typeof(ActiveSkillEnum), typeof(Vector3), typeof(ActiveSkillData), typeof(float), typeof(int), typeof(int), typeof(bool),
                    }),
                    prefix: nameof(TuningHooks.CastPrefix), finalizer: nameof(TuningHooks.Restore))
                && TryPatch(harmony, typeof(TuningHooks), "技能调整（伤害）",
                    AccessTools.Method(typeof(yb), "iwk", new[]
                    {
                        typeof(yb), typeof(double), typeof(float), typeof(AttackType), typeof(bool), typeof(float?), typeof(ActiveSkillEnum),
                        typeof(DamageSourceKind), typeof(bfe), typeof(PlayerDamageFeedbackKind),
                    }),
                    prefix: nameof(TuningHooks.DamagePrefix));

            // 两个都装上才通知：只有一半时还原会出错
            CheckpointHooks.Available =
                TryPatch(harmony, typeof(CheckpointHooks), "重试波次（快照）",
                    AccessTools.Method(typeof(wb), "gyk", Type.EmptyTypes),
                    postfix: nameof(CheckpointHooks.SavedPostfix))
                && TryPatch(harmony, typeof(CheckpointHooks), "重试波次（还原）",
                    AccessTools.Method(typeof(wb), "gym", Type.EmptyTypes),
                    postfix: nameof(CheckpointHooks.RestoredPostfix));

            // 先装构筑数据的过滤（保证自定义被动不会传给没装 Mod 的人），都装上了才接管被动的创建
            PassiveSkills.Available =
                TryPatch(harmony, typeof(PassiveHooks), "自定义被动（构筑数据过滤）",
                    AccessTools.Method(typeof(ez), "ebf", Type.EmptyTypes),
                    postfix: nameof(PassiveHooks.KingSkillsPostfix))
                && TryPatch(harmony, typeof(PassiveHooks), "自定义被动（镜像 Boss 过滤）",
                    AccessTools.Method(typeof(bax), "ker", Type.EmptyTypes),
                    postfix: nameof(PassiveHooks.MirrorSnapshotPostfix))
                && TryPatch(harmony, typeof(PassiveHooks), "自定义被动（文本）",
                    AccessTools.Method(typeof(cl), "dfg", new[] { typeof(string), typeof(string), typeof(bool) }),
                    prefix: nameof(PassiveHooks.TextPrefix))
                && TryPatch(harmony, typeof(PassiveHooks), "自定义被动（创建）",
                    AccessTools.Method(typeof(ez), "eax", new[] { typeof(string) }),
                    postfix: nameof(PassiveHooks.CreatePostfix));
            // 主动技能和被动共用文本、构筑过滤；另外接管技能实例的创建（和上面的技能调整挂在同一个 ez.dzc 上，互不影响）
            ActiveSkills.Available = PassiveSkills.Available
                && TryPatch(harmony, typeof(PassiveHooks), "自定义主动技能（创建）",
                    AccessTools.Method(typeof(ez), "dzc", new[]
                    {
                        typeof(uint), typeof(ActiveSkillEnum), typeof(Vector3), typeof(yb), typeof(int), typeof(float), typeof(int), typeof(int), typeof(bff),
                    }),
                    postfix: nameof(PassiveHooks.SkillCreatedPostfix));

            // Mod 的特效：m.cyn(路径) 是对象池第一次用到某个路径时加载模板的地方（m.cyp 等取对象的方法都经过它）
            ModAssets.EffectsAvailable = TryPatch(harmony, typeof(AssetHooks), "Mod 特效",
                AccessTools.Method(typeof(m), "cyn", new[] { typeof(string) }),
                postfix: nameof(AssetHooks.PrefabPostfix));
            // Mod 的图标：游戏到处用 Resources.Load<Sprite>("Bundles/UI/Icon/Skill/" + 名字)（技能栏、三选一、图鉴、构筑面板…），
            // 泛型 Load<T> 内部调用 Load(string, Type)。它是 Unity 的 extern 方法，HarmonyX 用原生跳转打补丁；装不上时技能栏单独补
            ModAssets.IconsAvailable = TryPatch(harmony, typeof(AssetHooks), "Mod 图标",
                AccessTools.Method(typeof(Resources), nameof(Resources.Load), new[] { typeof(string), typeof(Type) }),
                prefix: nameof(AssetHooks.ResourcesPrefix));
            TryPatch(harmony, typeof(AssetHooks), "Mod 图标（技能栏）",
                AccessTools.Method(typeof(bpf), "opi"),
                postfix: nameof(AssetHooks.SkillButtonPostfix));

            // 变体英雄：出生时临时换属性表和专属技能，结束后换回；名字在出生后改
            HeroVariants.Available = TryPatch(harmony, typeof(VariantHooks), "变体英雄（出生）",
                AccessTools.Method(typeof(bbe), "kzz", new[] { typeof(Vector3), typeof(uint), typeof(HeroType), typeof(string) }),
                prefix: nameof(VariantHooks.SpawnPrefix), postfix: nameof(VariantHooks.SpawnPostfix), finalizer: nameof(VariantHooks.SpawnFinalizer));

            try
            {
                Combat.Install();
            }
            catch (Exception e)
            {
                Log.Error($"命中事件：订阅失败，该功能停用：{e}");
            }
            return true;
        }

        private static bool TryPatch(Harmony harmony, Type hooks, string feature, MethodBase target,
            string prefix = null, string postfix = null, string finalizer = null)
        {
            if (target == null)
            {
                Log.Error($"{feature}：找不到目标方法，该功能停用");
                return false;
            }
            try
            {
                harmony.Patch(target,
                    prefix: prefix == null ? null : new HarmonyMethod(hooks, prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(hooks, postfix),
                    finalizer: finalizer == null ? null : new HarmonyMethod(hooks, finalizer));
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"{feature}：补丁安装失败，该功能停用：{e}");
                return false;
            }
        }
    }

    // 游戏作者的规则：装了 Mod 不能上传排行榜（违规可能被拉进按 Steam 账号判定的黑名单，连累整队）。
    // bwu.quj 是排行榜上传入口（由主机 MyServerNetworkManager 调用；bwu.nlq 是同签名的诱饵）。
    // 强制拦截，不提供开关；按游戏自己的失败流程回调，结算界面会显示上传失败。
    internal static class LeaderboardHooks
    {
        private static readonly MethodInfo makeFailure =
            AccessTools.Method(typeof(bwu), "qut", new[] { typeof(LeaderboardUploadFailure), typeof(string) });

        internal static bool UploadPrefix(Action<bws> __3)
        {
            Log.Warning("已安装 Mod：按游戏作者的规则，本局成绩不上传排行榜");
            ScreenNotice.Show("已安装 Mod：按游戏作者的规则，本局成绩不上传排行榜。", 8f);
            if (__3 != null)
                Safe.Run("排行榜拦截回调", () =>
                    __3((bws)makeFailure.Invoke(null, new object[] { LeaderboardUploadFailure.InvalidData, "Mods installed: leaderboard upload disabled." })));
            return false;
        }
    }

    // ez.dxx(ActiveSkillEnum, bbe, int bookId)：游戏的自动释放入口，技能冷却好、蓝够时每帧调用
    // ez.dxy(bbe, ActiveSkillData)：dxx 里选目标，原版找施法范围内最近的敌人
    internal static class AutoCastHooks
    {
        // dxx 执行期间的决策，dxy 据此选目标
        [ThreadStatic] private static AutoCastVerdict? current;

        internal struct State
        {
            public SkillSlot Slot;
            public AutoCastVerdict Verdict;
        }

        internal static bool DecidePrefix(ActiveSkillEnum __0, bbe __1, ref bool __result, out State? __state)
        {
            __state = null;
            var handler = AutoCast.Handler;
            if (handler == null || __1 == null || __1 != GameRaw.LocalPlayer()) return true;

            var slot = SkillBar.Find((int)__0);
            var player = Game.LocalPlayer;
            if (slot == null || player == null) return true;

            AutoCastVerdict verdict;
            try
            {
                verdict = handler(slot, player);
            }
            catch (Exception e)
            {
                Safe.Report("AutoCast 处理器", e);
                return true;
            }
            __state = new State { Slot = slot, Verdict = verdict };

            switch (verdict.Action)
            {
                case AutoCastAction.Skip:
                    __result = false;
                    return false;
                case AutoCastAction.Vanilla:
                    return true;
                default:
                    current = verdict;
                    return true;
            }
        }

        internal static void DecidePostfix(bool __result, State? __state)
        {
            current = null;
            if (__state is State s) AutoCast.RaiseResolved(new AutoCastResult(s.Slot, s.Verdict, __result));
        }

        internal static bool TargetPrefix(bbe __0, ActiveSkillData __1, ref yb __result)
        {
            if (!(current is AutoCastVerdict v) || __0 == null) return true;
            var player = Game.LocalPlayer;
            if (player == null || player.Native != __0) return true;

            if (v.Action == AutoCastAction.CastOnSelf && (__1.indicator == "None" || __1.indicator == "Switch"))
            {
                // 无需瞄准的技能，dxx 拿到非空目标后直接释放，目标本身不会被使用
                __result = __0;
                return false;
            }
            if (v.TargetScore == null) return true;

            var target = player.PickEnemy(__1.castingRange, v.TargetScore);
            __result = (yb)target?.Native;
            return false;
        }

        // bht.kcac92…()：bpf.opk 每帧在冷却好时调用的自动释放入口。开关技能开着（bhnv）时它会直接关掉技能，
        // 这里先问处理器：Skip = 保持开着，其余 = 照原版关掉
        internal static bool TogglePrefix(bht __instance, ref bool __result, out State? __state)
        {
            __state = null;
            var handler = AutoCast.Handler;
            var native = __instance.bhng?.bjgk;
            if (handler == null || native == null || !native.bhnv || __instance.bhnd == null || __instance.bhnd != GameRaw.LocalPlayer())
                return true;

            var slot = SkillSlot.Wrap(native);
            var player = Game.LocalPlayer;
            if (slot == null || player == null) return true;

            AutoCastVerdict verdict;
            try
            {
                verdict = handler(slot, player);
            }
            catch (Exception e)
            {
                Safe.Report("AutoCast 处理器（开关技能）", e);
                return true;
            }
            __state = new State { Slot = slot, Verdict = verdict };
            if (verdict.Action != AutoCastAction.Skip) return true;
            __result = false;
            return false;
        }

        internal static void TogglePostfix(bool __result, State? __state)
        {
            if (__state is State s) AutoCast.RaiseResolved(new AutoCastResult(s.Slot, s.Verdict, __result));
        }
    }

    // 自定义被动、主动技能（见 PassiveSkills、ActiveSkills）：
    // ez.dzc(实例编号, 技能, 位置, 施法者, _, 朝向, 目标, 技能书, 工厂)：每个客户端收到技能释放后创建技能实例（ez 里另有一个同签名、没有调用者的诱饵副本）
    // ez.eax(类名)：被动表 class 列 → qp 实例的大 switch，未知类名返回 null。学技能（yb.ivb / iuz）、镜像 Boss（zr.jzy 兜底）都经过它
    // cl.dfg(键, 默认值, 找不到时返回键)：dfe / dff 的真正实现（mee 是同签名的诱饵）
    // ez.ebf()：构筑里的技能列表（"p_" / "a_" + 编号），ez.ebj 用它生成同步给队友、上传国王对决的数据
    // bax.ker()：镜像 Boss 用的本地玩家快照（jwm 是没有调用者的副本）
    internal static class PassiveHooks
    {
        internal static void CreatePostfix(string __0, ref qp __result)
        {
            if (__result != null) return;
            try
            {
                __result = GameRaw.CreateModPassive(__0);
            }
            catch (Exception e)
            {
                Safe.Report("自定义被动（创建）", e);
            }
        }

        internal static void SkillCreatedPostfix(uint __0, ActiveSkillEnum __1, Vector3 __2, yb __3, float __5, int __6)
        {
            try
            {
                GameRaw.CreateModSkill(__0, __1, __2, __3, __5, __6);
            }
            catch (Exception e)
            {
                Safe.Report("自定义主动技能（创建）", e);
            }
        }

        internal static bool TextPrefix(string __0, ref string __result)
        {
            string text = GameRaw.SkillText(__0);
            if (text == null) return true;
            __result = text;
            return false;
        }

        internal static void KingSkillsPostfix(ref de.PlayerKingSkillData[] __result)
        {
            try
            {
                __result = GameRaw.FilterKingSkills(__result);
            }
            catch (Exception e)
            {
                Safe.Report("自定义被动（构筑数据过滤）", e);
            }
        }

        internal static void MirrorSnapshotPostfix(MirrorBossPlayerSnapshot __result)
        {
            try
            {
                GameRaw.FilterMirrorSnapshot(__result);
            }
            catch (Exception e)
            {
                Safe.Report("自定义被动（镜像 Boss 过滤）", e);
            }
        }
    }

    // Mod 的图标和特效（见 ModAssets）：名字都以 "DBMod/" 开头，原版的路径一律放行
    internal static class AssetHooks
    {
        internal static void PrefabPostfix(string __0, ref GameObject __result)
        {
            if (__result == null && ModAssets.IsOurs(__0)) __result = ModAssets.EffectFor(__0);
        }

        internal static bool ResourcesPrefix(string __0, Type __1, ref UnityEngine.Object __result)
        {
            if (!ModAssets.IsOurs(__0)) return true;
            var sprite = ModAssets.IconFor(__0);
            if (sprite == null) return true;
            if (__1 == null || __1.IsAssignableFrom(typeof(Sprite))) __result = sprite;
            else if (__1.IsAssignableFrom(typeof(Texture2D))) __result = sprite.texture;
            else return true;
            return false;
        }

        internal static void SkillButtonPostfix(object __instance, object __0)
        {
            try
            {
                GameRaw.FixSkillButtonIcon(__instance, __0);
            }
            catch (Exception e)
            {
                Safe.Report("Mod 图标（技能栏）", e);
            }
        }
    }

    // 变体英雄（见 HeroVariants）：bbe.kzz(位置, 编号, 英雄, Steam 名)，每个客户端对每个玩家都会调用，只处理本地玩家
    internal static class VariantHooks
    {
        internal static void SpawnPrefix(bbe __instance, HeroType __2, out object __state)
        {
            __state = null;
            if (!GameRaw.IsLocalSpawn(__instance)) return;
            try
            {
                __state = HeroVariants.BeforeSpawn((int)__2);
            }
            catch (Exception e)
            {
                Safe.Report("变体英雄（出生前）", e);
            }
        }

        internal static void SpawnPostfix(bbe __instance)
        {
            if (!GameRaw.IsLocalSpawn(__instance)) return;
            try
            {
                GameRaw.ApplyVariantName(__instance);
            }
            catch (Exception e)
            {
                Safe.Report("变体英雄（名字）", e);
            }
        }

        // 原方法抛异常时也要把表换回去
        internal static void SpawnFinalizer(object __state)
        {
            if (__state == null) return;
            try
            {
                HeroVariants.AfterSpawn(__state);
            }
            catch (Exception e)
            {
                Safe.Report("变体英雄（还原表）", e);
            }
        }
    }

    // bbe.kqi(bool)：待机/移动状态每帧的控制逻辑（含官方自动战斗）；bbe.kqh()：攻击状态每帧，玩家有移动输入时会打断攻击。
    // 两者都从 os.bctl / os.bctm 读移动输入。处理器要移动时，在调用期间把方向写进输入、结束后还原，
    // 游戏就会按"玩家按住方向键"处理：转向、移速、碰撞、打断攻击都走原版逻辑。
    internal static class MoveHooks
    {
        internal struct Saved
        {
            public float X, Z;
        }

        internal static void Prefix(bbe __instance, bool __0, out Saved? __state)
        {
            __state = __0 ? Inject(__instance, false) : null;
        }

        internal static void AttackPrefix(bbe __instance, out Saved? __state)
        {
            __state = Inject(__instance, true);
        }

        internal static void Postfix(Saved? __state)
        {
            if (__state is Saved s)
            {
                os.bctl = s.X;
                os.bctm = s.Z;
            }
        }

        private static Saved? Inject(bbe player, bool inAttack)
        {
            var handler = MoveControl.Handler;
            if (handler == null || player == null || player != GameRaw.LocalPlayer()) return null;
            if (player.roleType == RoleType.King || nn.bbtm) return null;
            // 玩家自己在操作
            if (!Mathf.Approximately(os.bctl, 0f) || !Mathf.Approximately(os.bctm, 0f)) return null;

            var p = Game.LocalPlayer;
            if (p == null) return null;
            MoveCommand cmd;
            try
            {
                cmd = handler(new MoveContext(p, inAttack));
            }
            catch (Exception e)
            {
                Safe.Report("移动处理器", e);
                return null;
            }
            if (!cmd.Override) return null;

            MoveControl.Last = cmd;
            var saved = new Saved { X = os.bctl, Z = os.bctm };
            os.bctl = cmd.Direction.x;
            os.bctm = cmd.Direction.z;
            return saved;
        }
    }

    // 只改本地玩家技能的补丁（见 SkillTuning）：
    // ez.dzc(…, 技能, …, 施法者, …)：所有技能实例的工厂，范围、持续时间、damageExValue 等都在这里从表里读出来交给技能实例；
    // ez.dye(技能, 玩家, 技能书)：按键后检查蓝量、选瞄准方式；ez.dyf(玩家, 技能, …, 技能数据, …)：真正放出，扣蓝时 ez.dyg 重新查表。
    // 施法者是本地玩家时，调用期间把 bcrw[技能] 换成调过的副本，结束后（finalizer，原方法抛异常也会执行）换回原版。
    // yb.iwk(攻击者, 伤害, …, 技能 g, 来源, 技能实例 i, …)：在受害者身上调用的真正受伤方法，伤害只在攻击方客户端算
    internal static class TuningHooks
    {
        internal static void FactoryPrefix(ActiveSkillEnum __1, yb __3, out bool __state) => __state = GameRaw.BeginSwap(__3, (int)__1);

        internal static void CheckPrefix(ActiveSkillEnum __0, bbe __1, out bool __state) => __state = GameRaw.BeginSwap(__1, (int)__0);

        internal static void CastPrefix(bbe __0, ActiveSkillEnum __1, ref ActiveSkillData __3, out bool __state)
        {
            __state = GameRaw.BeginSwap(__0, (int)__1);
            if (__state) __3 = GameRaw.CurrentRow(__1);
        }

        internal static void Restore(bool __state)
        {
            if (__state) GameRaw.EndSwap();
        }

        // 技能实例 i 的 bgwi 是技能编号；有的技能只传了 g（例如撕咬、雷之一闪两者都有，闪烁、炸弹只有 i）
        internal static void DamagePrefix(yb __0, ref double __1, ActiveSkillEnum __6, bfe __8)
        {
            if (__0 == null || __0 != GameRaw.LocalPlayer()) return;
            var skill = __8 != null ? __8.bgwi : __6;
            if (skill == ActiveSkillEnum.None) return;
            float m = SkillTuning.Get((int)skill).Damage;
            if (m != 1f) __1 *= m;
        }
    }

    // 重试波次（wb）：wb.gyk 在 OnStartRest 之后的下一次网络循环里拍快照（wb.bpcw 为真表示快照有效）；
    // wb.gym 是点"重试"后的还原入口，同步执行完还原，返回是否成功
    internal static class CheckpointHooks
    {
        public static bool Available { get; internal set; }

        internal static void SavedPostfix()
        {
            if (!GameRaw.CheckpointValid()) return;
            RunLog.Info(ModKitPlugin.Name, "重试波次", "快照", ("波次", Game.Wave));
            SkillTuning.SaveCheckpoint();
            ModEvents.RaiseCheckpointSaved();
        }

        internal static void RestoredPostfix(bool __result)
        {
            if (!__result) return;
            RunLog.Info(ModKitPlugin.Name, "重试波次", "还原", ("波次", Game.Wave));
            SkillTuning.RestoreCheckpoint();
            ModEvents.RaiseCheckpointRestored();
        }
    }

    // oo.gad()：每局开始把 enemy 表里还没有的怪物加进 bcru（已有的不覆盖），加完后补上倍率
    // oo.gaf(EnemyType, EnemySpawnKind, int 出场次数)：刷怪时取属性。bcru 里的值已乘过倍率，
    // 但命中 enemyOccurrence（bcrv）时会用表里的值直接覆盖生命、攻击、护盾，这几项要再乘一次（gii、qy 是同签名的诱饵）
    internal static class GameDataHooks
    {
        internal static void EnemyTablePostfix() => Safe.Run("怪物倍率（表加载）", GameData.OnEnemyTableLoaded);

        internal static void EnemyAttributePostfix(oo __instance, EnemyType __0, EnemySpawnKind __1, int __2, ref RoleAttribute __result)
        {
            string key = __0.ToString();
            if (!GameData.HasEnemyScale(key) || !OccurrenceOverrides(__instance, __0, __1, __2)) return;
            __result.hp = ScaleInt(__result.hp, GameData.EnemyScale(key, "hp"));
            __result.attackPower = ScaleInt(__result.attackPower, GameData.EnemyScale(key, "attackPower"));
            __result.shiled *= (float)GameData.EnemyScale(key, "shiled");
        }

        // 与 gaf 的判断一致：出场类型不是 None、出场次数 ≥ 0，且 bcrv 里有出场次数不超过它的条目
        private static bool OccurrenceOverrides(oo table, EnemyType enemy, EnemySpawnKind kind, int occurrence)
        {
            if (kind == EnemySpawnKind.None || occurrence < 0 || table?.bcrv == null) return false;
            // 键的格式同 oo.gag：(int)怪物 + "_" + (int)出场类型
            if (!table.bcrv.TryGetValue((int)enemy + "_" + (int)kind, out var list) || list == null) return false;
            foreach (var n in list)
                if (n != null && n.bcec <= occurrence) return true;
            return false;
        }

        private static int ScaleInt(int v, double scale) => (int)Math.Round(Math.Min(int.MaxValue, v * scale));
    }

    // MyServerNetworkManager.qhu(ClientNetMessage)：客户端（含主机自己）收到服务器消息后的分发（在 cik.vig 里注册，是真方法）。
    // 用后缀：游戏先处理完（比如更新波次），再通知 Mod
    internal static class WaveHooks
    {
        internal static void MessagePostfix(ClientNetMessage __0)
        {
            switch (__0.clientNetOperation)
            {
                case ClientNetOperation.OnStartGame:
                    GameEvents.Raise(new GameEvent(GameEventKind.WaveStarted));
                    break;
                case ClientNetOperation.OnStartRest:
                    GameEvents.Raise(new GameEvent(GameEventKind.WaveEnded));
                    break;
                case ClientNetOperation.EnemyEnterTip:
                    GameEvents.Raise(new GameEvent(GameEventKind.BossAppeared, isBoss: true));
                    break;
                case ClientNetOperation.OnGameOver:
                    GameEvents.Raise(new GameEvent(GameEventKind.GameOver));
                    break;
            }
        }
    }

    // buj.pwu(英雄编号, out 文件夹, out manifest)：查找英雄模型文件夹的唯一入口，出生、选英雄预览、变身、英雄名和头像覆盖都经过它
    //   （wy 是同签名的诱饵）。原版顺序：PlayerPrefs Workshop.LocalHeroModel.<编号> → 设置里的本地文件 Mod → 工坊绑定。
    //   只按英雄查本机设置，所以原版在你这边会把同英雄的其他玩家也换掉，名字（roleName）也换成模型里的 heroName
    // bbe.kzz：出生（ClientRpcBornPos），每个客户端对每个玩家都会调用；里面 yb.iuh → ez.dvm 算名字、buj.pwh 换模型
    // bgt.mqp（变身技能）、bbp.lob（变身状态恢复）：通过 buj.pwj → pwl 给新模型换上 additionalModels
    internal static class HeroSkinHooks
    {
        // 正在出生或变身的角色；不在这些方法里时为 null（例如选英雄界面的预览）
        private static yb owner;

        internal static bool ResolvePrefix(int __0, ref string __1, ref LocalWorkshopManifest __2, ref bool __result)
        {
            if (GameRaw.ModsDisabled()) return true;
            string folder;
            try
            {
                if (!HeroSkins.TryResolveFor(owner, __0, out folder)) return true;
            }
            catch (Exception e)
            {
                Safe.Report("英雄皮肤（查找模型）", e);
                return true;
            }
            LocalWorkshopManifest manifest = null;
            if (folder != null && !bup.pyx(folder, out manifest)) return true;
            __1 = folder ?? string.Empty;
            __2 = manifest;
            __result = folder != null;
            return false;
        }

        internal static void SpawnPrefix(bbe __instance, out yb __state)
        {
            __state = owner;
            owner = __instance;
        }

        internal static void SkillTransformPrefix(bgt __instance, out yb __state)
        {
            __state = owner;
            owner = __instance.bgwf;
        }

        internal static void CheckpointTransformPrefix(bbe __0, out yb __state)
        {
            __state = owner;
            owner = __0;
        }

        // 用 finalizer 而不是 postfix：原方法抛异常时也要恢复
        internal static void RestoreOwner(yb __state) => owner = __state;

        // Mod 自己给某个角色重新换模型时用（游戏里同样的操作只会对本地玩家做）
        internal static void WithOwner(yb role, Action action)
        {
            var previous = owner;
            owner = role;
            try
            {
                action();
            }
            finally
            {
                owner = previous;
            }
        }
    }

    // yb.iwk(攻击者, 伤害, 角度, AttackType, 暴击, …, ActiveSkillEnum, DamageSourceKind, 技能实例 bfe, …)：目标受到伤害的入口
    //   （doi、hon 是同签名的诱饵）；yb.iwr(AttackType, 伤害, 暴击, 攻击者, …)：Buff 持续伤害、真实伤害，直接调 iwl。
    // 两者里面都会走到 bdc.lvz 触发命中事件 bgfu，这里在调用期间记下伤害的类型、来源和技能实例。可能嵌套（命中触发别的伤害），用栈
    internal static class DamageContextHooks
    {
        internal struct Context
        {
            public yb Target, Attacker;
            public AttackType Type;
            public DamageSourceKind Source;
            public bfe Skill;
            // 没有技能实例时，调用栈里发起这次伤害的被动 / 装备特效 / 遗物 / Buff 的类型
            public Type Caller;
        }

        // 往上最多看这么多层（调用者 → ez.dxt 之类的中转 → iwk）
        private const int MaxFrames = 10;

        private static readonly List<Context> stack = new List<Context>();

        internal static void HitPrefix(yb __instance, yb __0, AttackType __3, DamageSourceKind __7, bfe __8, out bool __state)
        {
            var ctx = new Context { Target = __instance, Attacker = __0, Type = __3, Source = __7, Skill = __8 };
            if (__8 == null && __3 != AttackType.Normal && ShouldTrace(__0)) ctx.Caller = FindCaller();
            stack.Add(ctx);
            __state = true;
        }

        internal static void DotPrefix(yb __instance, AttackType __0, yb __3, out bool __state)
        {
            var ctx = new Context { Target = __instance, Attacker = __3, Type = __0, Source = DamageSourceKind.Default };
            if (ShouldTrace(__3)) ctx.Caller = FindCaller();
            stack.Add(ctx);
            __state = true;
        }

        // 取调用栈有开销：只在有人订阅命中事件、而且是本地玩家打出的伤害时取
        private static bool ShouldTrace(yb attacker)
            => attacker != null && attacker == nn.bpbu && Combat.HasSubscribers;

        private static Type FindCaller()
        {
            try
            {
                var trace = new System.Diagnostics.StackTrace(2, false);
                int n = Math.Min(trace.FrameCount, MaxFrames);
                for (int i = 0; i < n; i++)
                {
                    // 协程、闭包是编译器生成的嵌套类，要看外层的类
                    for (var t = trace.GetFrame(i)?.GetMethod()?.DeclaringType; t != null; t = t.DeclaringType)
                        if (GameRaw.IsEffectType(t)) return t;
                }
            }
            catch (Exception)
            {
                // 取不到就当识别不出
            }
            return null;
        }

        // 用 finalizer：原方法抛异常时也要出栈
        internal static void Pop(bool __state)
        {
            if (__state && stack.Count > 0) stack.RemoveAt(stack.Count - 1);
        }

        // 最内层的调用对得上这次命中的目标和攻击者才算
        internal static bool TryGet(yb target, yb attacker, out Context ctx)
        {
            ctx = default;
            if (stack.Count == 0) return false;
            ctx = stack[stack.Count - 1];
            return ctx.Target == target && ctx.Attacker == attacker;
        }
    }
}
