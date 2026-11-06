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
        /// <summary>安装补丁。排行榜拦截（上传、国王快照）装不上时返回 false，ModKit 整体停用。</summary>
        public static bool Install(Harmony harmony)
        {
            if (!TryPatch(harmony, typeof(LeaderboardHooks), "排行榜上传拦截",
                    AccessTools.Method(typeof(bzy), "sdi", new[]
                    {
                        typeof(int), typeof(int), typeof(df.TeamBuildData), typeof(Action<bzw>),
                        typeof(Action<LeaderboardUploadStage>), typeof(string),
                    }),
                    prefix: nameof(LeaderboardHooks.UploadPrefix)))
                return false;
            // 在别人的房间里：不把自己的构筑快照发给房主（房主没装 Mod 时会替我们上传）
            if (!TryPatch(harmony, typeof(LeaderboardHooks), "国王快照拦截",
                    AccessTools.Method(typeof(bca), "lhi", new[] { typeof(int), typeof(int) }),
                    prefix: nameof(LeaderboardHooks.KingSnapshotPrefix)))
                return false;

            InstallCardCollection();

            AutoCast.Available =
                TryPatch(harmony, typeof(AutoCastHooks), "自动释放决策",
                    AccessTools.Method(typeof(ez), "ecc", new[] { typeof(ActiveSkillEnum), typeof(bca), typeof(int) }),
                    prefix: nameof(AutoCastHooks.DecidePrefix), postfix: nameof(AutoCastHooks.DecidePostfix))
                && TryPatch(harmony, typeof(AutoCastHooks), "自动释放选目标",
                    AccessTools.Method(typeof(ez), "ecd", new[] { typeof(bca), typeof(ActiveSkillData) }),
                    prefix: nameof(AutoCastHooks.TargetPrefix));

            GameEvents.WaveEventsAvailable = TryPatch(harmony, typeof(WaveHooks), "波次事件",
                AccessTools.Method(typeof(MyServerNetworkManager), "ros", new[] { typeof(ClientNetMessage) }),
                postfix: nameof(WaveHooks.MessagePostfix));

            // 分开打：gad 失败时 ModKit 在进入战斗时补写倍率，gaf 失败时出场覆盖的数值不乘倍率
            GameData.Available = TryPatch(harmony, typeof(GameDataHooks), "怪物倍率（出场覆盖）",
                AccessTools.Method(typeof(ox), "ghr", new[] { typeof(EnemyType), typeof(EnemySpawnKind), typeof(int) }),
                postfix: nameof(GameDataHooks.EnemyAttributePostfix));
            TryPatch(harmony, typeof(GameDataHooks), "怪物倍率（表加载）",
                AccessTools.Method(typeof(ox), "ghp", Type.EmptyTypes),
                postfix: nameof(GameDataHooks.EnemyTablePostfix));

            TryPatch(harmony, typeof(GameDataHooks), "怪物倍率（受伤）",
                AccessTools.Method(typeof(yn), "jgi", new[]
                {
                    typeof(yn), typeof(double), typeof(float), typeof(AttackType), typeof(bool), typeof(float?), typeof(ActiveSkillEnum),
                    typeof(DamageSourceKind), typeof(bgg), typeof(PlayerDamageFeedbackKind),
                }),
                prefix: nameof(GameDataHooks.EnemyDamagePrefix));

            // 三选一卡片 brz.psk 按品质给图标染色（原版的深色技能图标染了才有色调），Mod 注册的图标保留原色
            TryPatch(harmony, typeof(ChoiceIconHooks), "三选一图标保留原色",
                AccessTools.Method(typeof(brz), "psk", new[] { typeof(RoguelikeUIData), typeof(int), typeof(bool), typeof(int) }),
                postfix: nameof(ChoiceIconHooks.CardPostfix));

            // 男枪弹药 bcc：lpz 开始装填、lqb 推进装填、lqc 冲锋、lpx 补满（同签名的其他方法是诱饵）；
            // PlayerShotgunMode.idh 每帧（补战术装填）、llk 同步；bkl.LateUpdate 弹药条。前 4 个都装上才算可用
            Shotgun.Available =
                TryPatch(harmony, typeof(ShotgunHooks), "男枪弹药（开始装填）",
                    AccessTools.Method(typeof(bdd), "mmb", Type.EmptyTypes), prefix: nameof(ShotgunHooks.StartReloadPrefix))
                && TryPatch(harmony, typeof(ShotgunHooks), "男枪弹药（装填进度）",
                    AccessTools.Method(typeof(bdd), "mmd", new[] { typeof(float), typeof(float), typeof(float) }), prefix: nameof(ShotgunHooks.AdvanceReloadPrefix))
                && TryPatch(harmony, typeof(ShotgunHooks), "男枪弹药（冲锋）",
                    AccessTools.Method(typeof(bdd), "mme", Type.EmptyTypes), prefix: nameof(ShotgunHooks.DashPrefix))
                && TryPatch(harmony, typeof(ShotgunHooks), "男枪弹药（补满）",
                    AccessTools.Method(typeof(bdd), "mlz", Type.EmptyTypes), prefix: nameof(ShotgunHooks.FillPrefix));
            if (Shotgun.Available)
            {
                TryPatch(harmony, typeof(ShotgunHooks), "男枪弹药（战术装填）",
                    AccessTools.Method(typeof(PlayerShotgunMode), "idh", Type.EmptyTypes), postfix: nameof(ShotgunHooks.TickPostfix));
                TryPatch(harmony, typeof(ShotgunHooks), "男枪弹药（同步）",
                    AccessTools.Method(typeof(PlayerShotgunMode), "mff", Type.EmptyTypes), prefix: nameof(ShotgunHooks.SyncPrefix));
                TryPatch(harmony, typeof(ShotgunHooks), "男枪弹药条",
                    AccessTools.Method(typeof(bkl), "LateUpdate", Type.EmptyTypes), prefix: nameof(ShotgunHooks.UiPrefix));
            }

            // 沙漠死神噬魂一击的溅射：lkt 是强化普攻结算完、清除强化的方法（镜像 Boss 的子类在 GameRaw 里排除）
            // 致命伤害拦截：yn.jgj 是所有伤害最后结算的地方（怪物打本机、bca.lvd 远端伤害都走到这里）
            Combat.LethalGuardAvailable = TryPatch(harmony, typeof(LethalHooks), "致命伤害拦截",
                AccessTools.Method(typeof(yn), "jgj", new[]
                {
                    typeof(double), typeof(AttackType), typeof(yn), typeof(float), typeof(bool), typeof(bool), typeof(RoleHitFeedbackType),
                    typeof(ActiveSkillEnum), typeof(bee.Origin).MakeByRefType(), typeof(PlayerDamageFeedbackKind),
                }),
                prefix: nameof(LethalHooks.SettlePrefix));

            // 撕咬击杀加生命上限：服务器上的 bca.kxw（只在主机执行）
            SkillForms.BiteAvailable = TryPatch(harmony, typeof(SkillFormHooks), "撕咬成长",
                AccessTools.Method(typeof(bca), "kxw", new[] { typeof(ya) }),
                prefix: nameof(SkillFormHooks.BitePrefix), postfix: nameof(SkillFormHooks.BitePostfix));
            // 炸弹的十字范围：bip.npp 初始化时算 bivg（ffq、fuw 是诱饵副本）
            SkillForms.BombAvailable = TryPatch(harmony, typeof(SkillFormHooks), "炸弹范围",
                AccessTools.Method(typeof(bip), "npp", new[] { typeof(ActiveSkillEnum), typeof(yn), typeof(Vector3), typeof(ActiveSkillData), typeof(int), typeof(float), typeof(float) }),
                postfix: nameof(SkillFormHooks.BombPostfix));

            SoulDevourer.Available = TryPatch(harmony, typeof(SoulDevourerHooks), "噬魂一击溅射",
                AccessTools.Method(typeof(PlayerKoboldMode), "mbg", Type.EmptyTypes),
                prefix: nameof(SoulDevourerHooks.StrikeEndPrefix));

            HeroSkins.Available = TryPatch(harmony, typeof(HeroSkinHooks), "英雄皮肤（查找模型）",
                AccessTools.Method(typeof(bxc), "rco", new[] { typeof(int), typeof(string).MakeByRefType(), typeof(LocalWorkshopManifest).MakeByRefType() }),
                prefix: nameof(HeroSkinHooks.ResolvePrefix));
            // 出生、变身期间记下是哪个角色：pwu 据此对其他玩家返回原版（模型和名字都按原版）
            HeroSkins.OnlyLocalPlayerAvailable =
                TryPatch(harmony, typeof(HeroSkinHooks), "英雄皮肤（出生）",
                    AccessTools.Method(typeof(bca), "lpj", new[] { typeof(Vector3), typeof(uint), typeof(HeroType), typeof(string) }),
                    prefix: nameof(HeroSkinHooks.SpawnPrefix), finalizer: nameof(HeroSkinHooks.RestoreOwner))
                & TryPatch(harmony, typeof(HeroSkinHooks), "英雄皮肤（变身技能）",
                    AccessTools.Method(typeof(bhx), "nnv", new[] { typeof(string) }),
                    prefix: nameof(HeroSkinHooks.SkillTransformPrefix), finalizer: nameof(HeroSkinHooks.RestoreOwner))
                & TryPatch(harmony, typeof(HeroSkinHooks), "英雄皮肤（变身恢复）",
                    AccessTools.Method(typeof(bcq), "mjz", new[] { typeof(bca) }),
                    prefix: nameof(HeroSkinHooks.CheckpointTransformPrefix), finalizer: nameof(HeroSkinHooks.RestoreOwner));

            // 开关技能开着时，自动释放直接把它关掉，不经过 dxx
            if (AutoCast.Available)
                TryPatch(harmony, typeof(AutoCastHooks), "开关技能自动关闭",
                    AccessTools.Method(typeof(biy), "k1e6e688e728f402bb29e4ccf2e11fb52", new Type[0]),
                    prefix: nameof(AutoCastHooks.TogglePrefix), postfix: nameof(AutoCastHooks.TogglePostfix));

            MoveControl.Available =
                TryPatch(harmony, typeof(MoveHooks), "走位（待机/移动）",
                    AccessTools.Method(typeof(bca), "lem", new[] { typeof(bool) }),
                    prefix: nameof(MoveHooks.Prefix), postfix: nameof(MoveHooks.Postfix))
                && TryPatch(harmony, typeof(MoveHooks), "走位（攻击中）",
                    AccessTools.Method(typeof(bca), "lel", new Type[0]),
                    prefix: nameof(MoveHooks.AttackPrefix), postfix: nameof(MoveHooks.Postfix));

            // 伤害上下文：没装上时命中事件照常，只是没有类型、来源和技能实例
            Combat.ContextAvailable =
                TryPatch(harmony, typeof(DamageContextHooks), "伤害上下文（iwk）",
                    AccessTools.Method(typeof(yn), "jgi", new[]
                    {
                        typeof(yn), typeof(double), typeof(float), typeof(AttackType), typeof(bool), typeof(float?),
                        typeof(ActiveSkillEnum), typeof(DamageSourceKind), typeof(bgg), typeof(PlayerDamageFeedbackKind),
                    }),
                    prefix: nameof(DamageContextHooks.HitPrefix), finalizer: nameof(DamageContextHooks.Pop))
                & TryPatch(harmony, typeof(DamageContextHooks), "伤害上下文（iwr）",
                    AccessTools.Method(typeof(yn), "jgp", new[] { typeof(AttackType), typeof(double), typeof(bool), typeof(yn), typeof(bool) }),
                    prefix: nameof(DamageContextHooks.DotPrefix), finalizer: nameof(DamageContextHooks.Pop));

            // 原始伤害：没装上时 HitInfo.RawDamage 等于 Damage
            TryPatch(harmony, typeof(RawDamageHooks), "原始伤害（lvz）",
                AccessTools.Method(typeof(bee), "mtf", new[]
                {
                    typeof(yn), typeof(yn), typeof(long), typeof(AttackType), typeof(long), typeof(bee.Origin).MakeByRefType(),
                }),
                prefix: nameof(RawDamageHooks.Prefix), finalizer: nameof(RawDamageHooks.Pop));

            SkillTuning.Available =
                TryPatch(harmony, typeof(TuningHooks), "技能调整（创建技能）",
                    AccessTools.Method(typeof(ez), "edh", new[]
                    {
                        typeof(uint), typeof(ActiveSkillEnum), typeof(Vector3), typeof(yn), typeof(int), typeof(float), typeof(int), typeof(int), typeof(bgh), typeof(Quaternion?),
                    }),
                    prefix: nameof(TuningHooks.FactoryPrefix), finalizer: nameof(TuningHooks.Restore))
                && TryPatch(harmony, typeof(TuningHooks), "技能调整（按键检查）",
                    AccessTools.Method(typeof(ez), "ecj", new[] { typeof(ActiveSkillEnum), typeof(bca), typeof(int) }),
                    prefix: nameof(TuningHooks.CheckPrefix), finalizer: nameof(TuningHooks.Restore))
                && TryPatch(harmony, typeof(TuningHooks), "技能调整（释放）",
                    AccessTools.Method(typeof(ez), "eck", new[]
                    {
                        typeof(bca), typeof(ActiveSkillEnum), typeof(Vector3), typeof(ActiveSkillData), typeof(float), typeof(int), typeof(int), typeof(bool),
                    }),
                    prefix: nameof(TuningHooks.CastPrefix), finalizer: nameof(TuningHooks.Restore))
                && TryPatch(harmony, typeof(TuningHooks), "技能调整（伤害）",
                    AccessTools.Method(typeof(yn), "jgi", new[]
                    {
                        typeof(yn), typeof(double), typeof(float), typeof(AttackType), typeof(bool), typeof(float?), typeof(ActiveSkillEnum),
                        typeof(DamageSourceKind), typeof(bgg), typeof(PlayerDamageFeedbackKind),
                    }),
                    prefix: nameof(TuningHooks.DamagePrefix));

            // 两个都装上才通知：只有一半时还原会出错
            CheckpointHooks.Available =
                TryPatch(harmony, typeof(CheckpointHooks), "重试波次（快照）",
                    AccessTools.Method(typeof(wk), "hgp", Type.EmptyTypes),
                    postfix: nameof(CheckpointHooks.SavedPostfix))
                && TryPatch(harmony, typeof(CheckpointHooks), "重试波次（还原）",
                    AccessTools.Method(typeof(wk), "hgr", Type.EmptyTypes),
                    postfix: nameof(CheckpointHooks.RestoredPostfix));

            // 先装构筑数据的过滤（保证自定义被动不会传给没装 Mod 的人），都装上了才接管被动的创建
            PassiveSkills.Available =
                TryPatch(harmony, typeof(PassiveHooks), "自定义被动（构筑数据过滤）",
                    AccessTools.Method(typeof(ez), "efm", Type.EmptyTypes),
                    postfix: nameof(PassiveHooks.KingSkillsPostfix))
                && TryPatch(harmony, typeof(PassiveHooks), "自定义被动（镜像 Boss 过滤）",
                    AccessTools.Method(typeof(bbq), "kry", Type.EmptyTypes),
                    postfix: nameof(PassiveHooks.MirrorSnapshotPostfix))
                && TryPatch(harmony, typeof(PassiveHooks), "自定义被动（文本）",
                    AccessTools.Method(typeof(cm), "djd", new[] { typeof(string), typeof(string), typeof(bool) }),
                    prefix: nameof(PassiveHooks.TextPrefix))
                && TryPatch(harmony, typeof(PassiveHooks), "自定义被动（创建）",
                    AccessTools.Method(typeof(ez), "efe", new[] { typeof(string) }),
                    postfix: nameof(PassiveHooks.CreatePostfix));
            // 主动技能和被动共用文本、构筑过滤；另外接管技能实例的创建（和上面的技能调整挂在同一个 ez.edh 上，互不影响）
            ActiveSkills.Available = PassiveSkills.Available
                && TryPatch(harmony, typeof(PassiveHooks), "自定义主动技能（创建）",
                    AccessTools.Method(typeof(ez), "edh", new[]
                    {
                        typeof(uint), typeof(ActiveSkillEnum), typeof(Vector3), typeof(yn), typeof(int), typeof(float), typeof(int), typeof(int), typeof(bgh), typeof(Quaternion?),
                    }),
                    postfix: nameof(PassiveHooks.SkillCreatedPostfix));
            // 自定义遗物：和被动共用文本、镜像 Boss 过滤；先装构筑数据和候选池的过滤，都装上了才接管遗物的创建
            CustomRelics.Available = PassiveSkills.Available
                && TryPatch(harmony, typeof(RelicHooks), "自定义遗物（构筑数据过滤）",
                    AccessTools.Method(typeof(ez), "efq", new[] { typeof(bool), typeof(bool) }),
                    postfix: nameof(RelicHooks.KingDataPostfix))
                && TryPatch(harmony, typeof(RelicHooks), "自定义遗物（候选池）",
                    AccessTools.Method(typeof(ez), "eej", new[] { typeof(Dictionary<ItemType, RemainsData>), typeof(HashSet<ItemType>), typeof(HashSet<ItemType>) }),
                    postfix: nameof(RelicHooks.PoolPostfix))
                && TryPatch(harmony, typeof(RelicHooks), "自定义遗物（创建）",
                    AccessTools.Method(typeof(bca), "lep", new[] { typeof(string) }),
                    postfix: nameof(RelicHooks.CreatePostfix));
            // 自定义装备和特效：和被动共用文本；先装构筑数据和神器候选的过滤，都装上了才追加特效、接管特效的创建
            CustomEquipEffects.Available = PassiveSkills.Available
                && TryPatch(harmony, typeof(EquipmentHooks), "自定义装备（构筑数据过滤）",
                    AccessTools.Method(typeof(ez), "efn", Type.EmptyTypes),
                    postfix: nameof(EquipmentHooks.KingEquipPostfix))
                && TryPatch(harmony, typeof(EquipmentHooks), "自定义装备（神器候选）",
                    AccessTools.Method(typeof(pu), "gpg", new[] { typeof(int), typeof(object) }),
                    postfix: nameof(EquipmentHooks.ArtifactPostfix))
                && TryPatch(harmony, typeof(EquipmentHooks), "自定义装备特效（追加）",
                    AccessTools.Method(typeof(kd), "Init", new[] { typeof(RoleType) }),
                    postfix: nameof(EquipmentHooks.InitPostfix))
                && TryPatch(harmony, typeof(EquipmentHooks), "自定义装备特效（创建）",
                    AccessTools.Method(typeof(bbt), "kuy", new[] { typeof(EquipSkillType), typeof(lu).MakeByRefType(), typeof(kj).MakeByRefType() }),
                    postfix: nameof(EquipmentHooks.CreatePostfix));
            CustomEquipment.Available = CustomEquipEffects.Available;

            // Mod 的特效：o.dcj(路径) 是对象池第一次用到某个路径时加载模板的地方（o.dcl 等取对象的方法都经过它）
            ModAssets.EffectsAvailable = TryPatch(harmony, typeof(AssetHooks), "Mod 特效",
                AccessTools.Method(typeof(o), "dcj", new[] { typeof(string) }),
                postfix: nameof(AssetHooks.PrefabPostfix));
            // Mod 的图标：游戏到处用 Resources.Load<Sprite>("Bundles/UI/Icon/Skill/" + 名字)（技能栏、三选一、图鉴、构筑面板…），
            // 泛型 Load<T> 内部调用 Load(string, Type)。它是 Unity 的 extern 方法，HarmonyX 用原生跳转打补丁；装不上时技能栏单独补
            ModAssets.IconsAvailable = TryPatch(harmony, typeof(AssetHooks), "Mod 图标",
                AccessTools.Method(typeof(Resources), nameof(Resources.Load), new[] { typeof(string), typeof(Type) }),
                prefix: nameof(AssetHooks.ResourcesPrefix));
            TryPatch(harmony, typeof(AssetHooks), "Mod 图标（技能栏）",
                AccessTools.Method(typeof(bri), "pqi"),
                postfix: nameof(AssetHooks.SkillButtonPostfix));

            // 变体英雄：出生时临时换属性表和专属技能，结束后换回；名字在出生后改
            HeroVariants.Available = TryPatch(harmony, typeof(VariantHooks), "变体英雄（出生）",
                AccessTools.Method(typeof(bca), "lpj", new[] { typeof(Vector3), typeof(uint), typeof(HeroType), typeof(string) }),
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

        private static void InstallCardCollection()
        {
            var missing = Compat.FindCardMissing();
            if (missing.Count > 0)
            {
                CardCollection.UnavailableReason = "卡牌功能不兼容，缺失 " + string.Join("，", missing);
                Log.Error(CardCollection.UnavailableReason);
                return;
            }
            // 独立 owner，任一安全补丁失败就撤销这一组，其他 Mod 和排行榜保护继续工作。
            var cards = new Harmony(ModKitPlugin.Guid + ".cards");
            bool ok =
                TryPatch(cards, typeof(CardCollectionHooks), "卡牌保存隔离",
                    AccessTools.Method(typeof(df), "dou", new[] { typeof(bool) }),
                    prefix: nameof(CardCollectionHooks.SavePrefix), finalizer: nameof(CardCollectionHooks.SaveFinalizer))
                & TryPatch(cards, typeof(CardCollectionHooks), "卡牌读盘",
                    AccessTools.Method(typeof(df), "dph", new[] { typeof(df.GameSaveData) }),
                    prefix: nameof(CardCollectionHooks.ReadPrefix), finalizer: nameof(CardCollectionHooks.ReadFinalizer))
                & TryPatch(cards, typeof(CardCollectionHooks), "卡牌旧档初始化",
                    AccessTools.Method(typeof(df), "dpp", new[] { typeof(bool) }),
                    prefix: nameof(CardCollectionHooks.ReadPrefix), finalizer: nameof(CardCollectionHooks.ReadFinalizer))
                & TryPatch(cards, typeof(CardCollectionHooks), "卡牌重置存档隔离",
                    AccessTools.Method(typeof(df), "dpo", Type.EmptyTypes),
                    prefix: nameof(CardCollectionHooks.ReadPrefix), finalizer: nameof(CardCollectionHooks.ReadFinalizer))
                & TryPatch(cards, typeof(CardCollectionHooks), "卡牌真实获得",
                    AccessTools.Method(typeof(zt), "kfn", new[] { typeof(int) }), postfix: nameof(CardCollectionHooks.GainPostfix))
                & TryPatch(cards, typeof(CardCollectionHooks), "卡牌真实收集进度",
                    AccessTools.Method(typeof(zt), "kfv", new[] { typeof(int), typeof(int) }), postfix: nameof(CardCollectionHooks.ProgressPostfix))
                & TryPatch(cards, typeof(CardCollectionHooks), "卡牌卸下返还",
                    AccessTools.Method(typeof(zt), "kfo", new[] { typeof(int) }),
                    prefix: nameof(CardCollectionHooks.ReturnPrefix), finalizer: nameof(CardCollectionHooks.ReturnFinalizer))
                & TryPatch(cards, typeof(CardCollectionHooks), "卡牌合成拦截",
                    AccessTools.Method(typeof(bpy), "pgj", Type.EmptyTypes), prefix: nameof(CardCollectionHooks.CraftPrefix))
                & TryPatch(cards, typeof(CardCollectionHooks), "卡牌合成库存拦截",
                    AccessTools.Method(typeof(bpy), "pgk", new[] { typeof(int), typeof(int) }), prefix: nameof(CardCollectionHooks.CraftPrefix))
                & TryPatch(cards, typeof(CardCollectionHooks), "卡牌分解拦截",
                    AccessTools.Method(typeof(bpy), "pgd", new[] { typeof(int), typeof(int) }), prefix: nameof(CardCollectionHooks.CraftPrefix))
                & TryPatch(cards, typeof(CardCollectionHooks), "卡牌分解库存拦截",
                    AccessTools.Method(typeof(bpy), "pge", new[] { typeof(int), typeof(int) }), prefix: nameof(CardCollectionHooks.CraftPrefix))
                & TryPatch(cards, typeof(CardCollectionHooks), "卡牌扩容拦截",
                    AccessTools.Method(typeof(bpy), "pgq", Type.EmptyTypes), prefix: nameof(CardCollectionHooks.CraftPrefix));
            if (!ok)
            {
                cards.UnpatchSelf();
                CardCollection.UnavailableReason = "卡牌安全补丁安装失败，功能已停用";
                return;
            }
            CardCollection.Available = true;
            CardCollection.UnavailableReason = null;
            Log.Info("卡牌收藏保护补丁已安装（默认关闭全解锁）");
        }

        private static bool TryPatch(Harmony harmony, Type hooks, string feature, MethodBase target,
            string prefix = null, string postfix = null, string finalizer = null)
        {
            // 这些组只观察伤害/波次或替玩家操作；卡牌收藏按玩家决定照常保留。
            // 其余都是玩法扩展（含 Mod 资源、皮肤和重试快照），打榜模式保守跳过。
            if (Ranked.ModeActive && hooks != typeof(LeaderboardHooks) && hooks != typeof(CardCollectionHooks)
                && hooks != typeof(AutoCastHooks) && hooks != typeof(WaveHooks) && hooks != typeof(MoveHooks)
                && hooks != typeof(DamageContextHooks) && hooks != typeof(RawDamageHooks))
            {
                Log.Info($"打榜模式：跳过补丁 {feature}（{hooks.Name}）");
                return false;
            }
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

    internal static class CardCollectionHooks
    {
        internal static bool SavePrefix(out IDisposable __state)
        {
            __state = null;
            try { __state = GameRaw.BeginCardSave(); return true; }
            catch (Exception e) { CardCollection.Fail("保存隔离失败，已阻止本次保存 " + e); return false; }
        }

        internal static Exception SaveFinalizer(Exception __exception, IDisposable __state)
        {
            __state?.Dispose();
            return __exception;
        }

        internal static void ReadPrefix(out bool __state)
        {
            __state = false;
            GameRaw.BeginCardRead();
            __state = true;
        }

        internal static Exception ReadFinalizer(Exception __exception, bool __state)
        {
            if (__state)
            {
                try { GameRaw.EndCardRead(__exception == null); }
                catch (Exception e) { CardCollection.Fail("读盘后套用卡牌全解锁失败 " + e); }
            }
            return __exception;
        }

        internal static void GainPostfix(int __0) => GameRaw.TrackCardGain(__0);
        internal static void ProgressPostfix(int __0, int __1) => GameRaw.TrackCardProgress(__0, __1);
        internal static void ReturnPrefix() => GameRaw.BeginCardReturn();
        internal static Exception ReturnFinalizer(Exception __exception)
        {
            GameRaw.EndCardReturn();
            return __exception;
        }
        internal static bool CraftPrefix() => !CardCollection.BlockCrafting();
    }

    // 作者允许显示和自动操作类白名单 Mod 打榜；上传现场检查，任何不确定情况拦截。
    // 两道拦截：自己上传（bzy.sdi），以及在别人房间里把构筑快照交给房主上传（bca.lhi）。
    // bzy.sdi 是排行榜上传入口（由主机 MyServerNetworkManager 调用；bxl.nlq 是同签名的诱饵）。
    // 不合格时按游戏自己的失败流程回调，结算界面会显示上传失败。
    internal static class LeaderboardHooks
    {
        private static readonly MethodInfo makeFailure =
            AccessTools.Method(typeof(bzy), "sds", new[] { typeof(LeaderboardUploadFailure), typeof(string) });

        internal static bool UploadPrefix(Action<bzw> __3)
        {
            string message = "打榜：资格检查异常，本局不能上传；可以在 Mod 菜单打开打榜模式";
            try
            {
                // 房主的上传入口包含整队成绩，必须检查所有可见的 ModKit 队友。
                var result = Ranked.Check("上传成绩");
                if (result.Eligible)
                {
                    Log.Info("打榜：本局成绩照常上传");
                    ScreenNotice.Show("打榜：本局成绩照常上传", 8f);
                    return true;
                }
                message = "打榜：本局成绩不上传，" + result.Brief + "。可以在 Mod 菜单打开打榜模式";
                Log.Warning(message);
                ScreenNotice.Show(message, 10f);
            }
            catch (Exception e)
            {
                Safe.Run("打榜判断异常", () => RunLog.Error(ModKitPlugin.Name, "打榜资格", "上传检查异常，拦截", ("异常", e.ToString())));
            }
            if (__3 != null)
                Safe.Run("排行榜拦截回调", () =>
                    __3((bzw)makeFailure.Invoke(null, new object[] { LeaderboardUploadFailure.InvalidData, message })));
            return false;
        }

        // bca.lhi(局, 回合)：国王对决结算时把本地玩家的构筑快照（ez.efq）用 CmdUploadLocalKingData 发给房主（ody/gif/hjf 是诱饵副本）。
        // 房主（MyServerNetworkManager.rpz）把各人的快照汇总进队伍数据，再用 bzy.sdi 一起上传排行榜，只要求有房主自己的快照
        // 别人的可以缺。非房主不合格时不发自己的快照，原版房主仍可上传不含自己的队伍成绩。
        // 房主也先检查资格，最终上传时再检查全队；不能因为自己是主机就跳过检查。
        internal static bool KingSnapshotPrefix()
        {
            try
            {
                // 两端都检查可见的队友；客户端只控制自己的快照，房主最终控制整队上传。
                var result = Ranked.Check("国王快照");
                if (result.Eligible) return true;
                string message = "打榜：本局构筑不提交排行榜，" + result.Brief + "。可以在 Mod 菜单打开打榜模式";
                Log.Warning(message);
                ScreenNotice.Show(message, 10f);
            }
            catch (Exception e)
            {
                Safe.Run("打榜判断异常", () => RunLog.Error(ModKitPlugin.Name, "打榜资格", "快照检查异常，拦截", ("异常", e.ToString())));
            }
            return false;
        }
    }

    // ez.ecc(ActiveSkillEnum, bbe, int bookId)：游戏的自动释放入口，技能冷却好、蓝够时每帧调用
    // ez.ecd(bbe, ActiveSkillData)：dxx 里选目标，原版找施法范围内最近的敌人
    internal static class AutoCastHooks
    {
        // dxx 执行期间的决策，dxy 据此选目标
        [ThreadStatic] private static AutoCastVerdict? current;

        internal struct State
        {
            public SkillSlot Slot;
            public AutoCastVerdict Verdict;
        }

        internal static bool DecidePrefix(ActiveSkillEnum __0, bca __1, ref bool __result, out State? __state)
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

        internal static bool TargetPrefix(bca __0, ActiveSkillData __1, ref yn __result)
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
            __result = (yn)target?.Native;
            return false;
        }

        // bht.kcac92…()：bri.pqk 每帧在冷却好时调用的自动释放入口。开关技能开着（bhnv）时它会直接关掉技能，
        // 这里先问处理器：Skip = 保持开着，其余 = 照原版关掉
        internal static bool TogglePrefix(biy __instance, ref bool __result, out State? __state)
        {
            __state = null;
            var handler = AutoCast.Handler;
            var native = __instance.bjsj?.blqx;
            if (handler == null || native == null || !native.bjsy || __instance.bjsg == null || __instance.bjsg != GameRaw.LocalPlayer())
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

    // 自定义遗物（见 CustomRelics）：
    // ez.efq(_, _)：本地玩家的构筑数据（PlayerKingData），遗物数组直接从 playerAttribute.bfna 生成；同步给队友（CmdUploadSyncPlayerData）、
    //   国王对决、信息面板、结算界面都用它（eiu / dpt / hlq / obw 是没有调用者的诱饵副本）
    // ez.eej(遗物表, 排除, 已有)：三选一候选池按品级分组，ead（随机）和 eaa（固定品级）都经过它
    // bca.lbx(效果名)：遗物表 skill 列 → rn 子类的大 switch，未知名字返回 new rn()
    internal static class RelicHooks
    {
        internal static void KingDataPostfix(ref df.PlayerKingData __result)
        {
            try
            {
                GameRaw.FilterKingRelics(ref __result);
            }
            catch (Exception e)
            {
                Safe.Report("自定义遗物（构筑数据过滤）", e);
            }
        }

        internal static void PoolPostfix(Dictionary<int, List<ItemType>> __result)
        {
            try
            {
                GameRaw.FilterRelicPool(__result);
            }
            catch (Exception e)
            {
                Safe.Report("自定义遗物（候选池）", e);
            }
        }

        internal static void CreatePostfix(string __0, ref sc __result)
        {
            try
            {
                var ours = GameRaw.CreateModRelic(__0);
                if (ours != null) __result = ours;
            }
            catch (Exception e)
            {
                Safe.Report("自定义遗物（创建）", e);
            }
        }
    }

    // 自定义装备和特效（见 CustomEquipment、CustomEquipEffects）：
    // ez.efn()：本地玩家的装备构筑数据（PlayerKingEquipData[]，没有装备时 null），ez.efq 和 bca.lhz（穿脱时 CmdUploadSyncSkillEquipData）都用它
    // pu.gpg(编号, 行)：神器三选一（pq.ghn）的候选条件，isArtifact 列或编号 101~999
    // kd.Init(RoleType)：按装备表初始化装备实例（特效列表 bayo / bayp），商店、神器、发放（pu.gpd）、说明预览（kd.fan）都经过它
    // bbt.kuy(特效, out lm, out ke)：特效类型 → lm / ke 子类的大 switch，未知类型两个都是 null（调用者 bbt.kqj、镜像 Boss 的 bbd.kqj）
    internal static class EquipmentHooks
    {
        internal static void KingEquipPostfix(ref df.PlayerKingEquipData[] __result)
        {
            try
            {
                __result = GameRaw.FilterKingEquip(__result);
            }
            catch (Exception e)
            {
                Safe.Report("自定义装备（构筑数据过滤）", e);
            }
        }

        internal static void ArtifactPostfix(int __0, ref bool __result)
        {
            try
            {
                if (__result && !GameRaw.ArtifactAllowed(__0)) __result = false;
            }
            catch (Exception e)
            {
                Safe.Report("自定义装备（神器候选）", e);
            }
        }

        internal static void InitPostfix(kd __instance, RoleType __0)
        {
            try
            {
                GameRaw.AttachEquipEffects(__instance, __0);
            }
            catch (Exception e)
            {
                Safe.Report("自定义装备特效（追加）", e);
            }
        }

        internal static void CreatePostfix(EquipSkillType __0, ref kj __2)
        {
            if (__2 != null) return;
            try
            {
                __2 = GameRaw.CreateModEquipEffect(__0);
            }
            catch (Exception e)
            {
                Safe.Report("自定义装备特效（创建）", e);
            }
        }
    }

    // 自定义被动、主动技能（见 PassiveSkills、ActiveSkills）：
    // ez.edh(实例编号, 技能, 位置, 施法者, _, 朝向, 目标, 技能书, 工厂, 朝向覆盖?)：每个客户端收到技能释放后创建技能实例（ey 里另有几个同签名、没有调用者的诱饵副本）
    // ez.efe(类名)：被动表 class 列 → qp 实例的大 switch，未知类名返回 null。学技能（yn.jez / iuz）、镜像 Boss（baj.kmx 兜底）都经过它
    // cm.djd(键, 默认值, 找不到时返回键)：dfe / dff 的真正实现（mee 是同签名的诱饵）
    // ez.efm()：构筑里的技能列表（"p_" / "a_" + 编号），ez.efq 用它生成同步给队友、上传国王对决的数据
    // bbq.kry()：镜像 Boss 用的本地玩家快照（jwm 是没有调用者的副本）
    internal static class PassiveHooks
    {
        internal static void CreatePostfix(string __0, ref qy __result)
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

        internal static void SkillCreatedPostfix(uint __0, ActiveSkillEnum __1, Vector3 __2, yn __3, float __5, int __6)
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

        internal static void KingSkillsPostfix(ref df.PlayerKingSkillData[] __result)
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

    // 变体英雄（见 HeroVariants）：bca.lpj(位置, 编号, 英雄, Steam 名)，每个客户端对每个玩家都会调用，只处理本地玩家
    internal static class VariantHooks
    {
        internal static void SpawnPrefix(bca __instance, HeroType __2, out object __state)
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

        internal static void SpawnPostfix(bca __instance)
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

    // bca.lem(bool)：待机/移动状态每帧的控制逻辑（含官方自动战斗）；bca.lel()：攻击状态每帧，玩家有移动输入时会打断攻击。
    // 两者都从 pb.beke / pb.bekf 读移动输入。处理器要移动时，在调用期间把方向写进输入、结束后还原，
    // 游戏就会按"玩家按住方向键"处理：转向、移速、碰撞、打断攻击都走原版逻辑。
    internal static class MoveHooks
    {
        internal struct Saved
        {
            public float X, Z;
        }

        internal static void Prefix(bca __instance, bool __0, out Saved? __state)
        {
            __state = __0 ? Inject(__instance, false) : null;
        }

        internal static void AttackPrefix(bca __instance, out Saved? __state)
        {
            __state = Inject(__instance, true);
        }

        internal static void Postfix(Saved? __state)
        {
            if (__state is Saved s)
            {
                pb.beke = s.X;
                pb.bekf = s.Z;
            }
        }

        private static Saved? Inject(bca player, bool inAttack)
        {
            var handler = MoveControl.Handler;
            if (handler == null || player == null || player != GameRaw.LocalPlayer()) return null;
            if (player.roleType == RoleType.King || nt.bdhf) return null;
            // 玩家自己在操作
            if (!Mathf.Approximately(pb.beke, 0f) || !Mathf.Approximately(pb.bekf, 0f)) return null;

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
            var saved = new Saved { X = pb.beke, Z = pb.bekf };
            pb.beke = cmd.Direction.x;
            pb.bekf = cmd.Direction.z;
            return saved;
        }
    }

    // 只改本地玩家技能的补丁（见 SkillTuning）：
    // ez.edh(…, 技能, …, 施法者, …)：所有技能实例的工厂，范围、持续时间、damageExValue 等都在这里从表里读出来交给技能实例；
    // ez.ecj(技能, 玩家, 技能书)：按键后检查蓝量、选瞄准方式；ez.eck(玩家, 技能, …, 技能数据, …)：真正放出，扣蓝时 ez.ecl 重新查表。
    // 施法者是本地玩家时，调用期间把 bcrw[技能] 换成调过的副本，结束后（finalizer，原方法抛异常也会执行）换回原版。
    // yn.jgi(攻击者, 伤害, …, 技能 g, 来源, 技能实例 i, …)：在受害者身上调用的真正受伤方法，伤害只在攻击方客户端算
    internal static class TuningHooks
    {
        internal static void FactoryPrefix(ActiveSkillEnum __1, yn __3, out bool __state) => __state = GameRaw.BeginSwap(__3, (int)__1);

        internal static void CheckPrefix(ActiveSkillEnum __0, bca __1, out bool __state) => __state = GameRaw.BeginSwap(__1, (int)__0);

        internal static void CastPrefix(bca __0, ActiveSkillEnum __1, ref ActiveSkillData __3, out bool __state)
        {
            __state = GameRaw.BeginSwap(__0, (int)__1);
            if (__state) __3 = GameRaw.CurrentRow(__1);
        }

        internal static void Restore(bool __state)
        {
            if (__state) GameRaw.EndSwap();
        }

        // 技能实例 i 的 bgwi 是技能编号；有的技能只传了 g（例如撕咬、雷之一闪两者都有，闪烁、炸弹只有 i）
        internal static void DamagePrefix(yn __0, ref double __1, ActiveSkillEnum __6, bgg __8)
        {
            if (__0 == null || __0 != GameRaw.LocalPlayer()) return;
            var skill = __8 != null ? __8.bjbf : __6;
            if (skill == ActiveSkillEnum.None) return;
            float m = SkillTuning.Get((int)skill).Damage;
            if (m != 1f) __1 *= m;
        }
    }

    // 重试波次（wb）：wk.hgp 在 OnStartRest 之后的下一次网络循环里拍快照（wk.brzk 为真表示快照有效）；
    // wk.hgr 是点"重试"后的还原入口，同步执行完还原，返回是否成功
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

    // ox.ghp()：每局开始把 enemy 表里还没有的怪物加进 bcru（已有的不覆盖），加完后补上倍率
    // ox.ghr(EnemyType, EnemySpawnKind, int 出场次数)：刷怪时取属性。bcru 里的值已乘过倍率，
    // 但命中 enemyOccurrence（bcrv）时会用表里的值直接覆盖生命、攻击、护盾，这几项要再乘一次（gii、qy 是同签名的诱饵）
    internal static class GameDataHooks
    {
        internal static void EnemyTablePostfix() => Safe.Run("怪物倍率（表加载）", GameData.OnEnemyTableLoaded);

        internal static void EnemyAttributePostfix(ox __instance, EnemyType __0, EnemySpawnKind __1, int __2, ref RoleAttribute __result)
        {
            string key = __0.ToString();
            if (GameData.HasEnemyScale(key) && OccurrenceOverrides(__instance, __0, __1, __2))
            {
                __result.hp = ScaleInt(__result.hp, GameData.EnemyScale(key, "hp"));
                __result.attackPower = ScaleInt(__result.attackPower, GameData.EnemyScale(key, "attackPower"));
                __result.shiled *= (float)GameData.EnemyScale(key, "shiled");
            }
            // Mod 叠加的倍率（EnemyBoosts）：返回的是结构体副本，每次刷怪都从原值乘，不会越乘越大。
            // 攻击不在这里乘：小怪表里的攻击只有 1~3，乘完取整等于没乘，改在受伤时乘（EnemyDamagePrefix）
            if (!EnemyBoosts.Active) return;
            __result.hp = ScaleInt(__result.hp, EnemyBoosts.Hp);
            __result.shiled *= EnemyBoosts.Shield;
        }

        // yn.jgi 挂在受伤的一方：玩家被怪物（xo）打时按 EnemyBoosts.Attack 放大伤害。
        // 联机时每只怪的控制权在它的主人那边（bey.cs 200），怪的 AI 和命中都在主人本机算（xo 的命中要求 bqbw），
        // 打到别的玩家时再在 iwk 里改发 CmdAttackOtherPlayer。所以只在"攻击的怪归本机"时乘，每次伤害只乘一次。
        // 单人时所有怪都归自己，和以前一样
        internal static void EnemyDamagePrefix(yn __instance, yn __0, ref double __1)
        {
            if (!EnemyBoosts.Active || EnemyBoosts.Attack == 1f || !(__0 is ya)) return;
            if (!__0.btch || __instance.roleType != RoleType.Player) return;
            __1 *= EnemyBoosts.Attack;
        }

        // 与 gaf 的判断一致：出场类型不是 None、出场次数 ≥ 0，且 bcrv 里有出场次数不超过它的条目
        private static bool OccurrenceOverrides(ox table, EnemyType enemy, EnemySpawnKind kind, int occurrence)
        {
            if (kind == EnemySpawnKind.None || occurrence < 0 || table?.beii == null) return false;
            // 键的格式同 ox.ghs：(int)怪物 + "_" + (int)出场类型
            if (!table.beii.TryGetValue((int)enemy + "_" + (int)kind, out var list) || list == null) return false;
            foreach (var n in list)
                if (n != null && n.bdrv <= occurrence) return true;
            return false;
        }

        private static int ScaleInt(int v, double scale) => (int)Math.Round(Math.Min(int.MaxValue, v * scale));
    }

    internal static class ChoiceIconHooks
    {
        internal static void CardPostfix(brz __instance, RoguelikeUIData __0)
        {
            if (ModAssets.IsOurs(__0.icon) && __instance.iconImg != null) __instance.iconImg.color = Color.white;
        }
    }

    internal static class SkillFormHooks
    {
        // kjv：哥布林之王击杀时 bpmz（累计成长，背后是 bfpe）+= 成长量，再 iww(成长量) 加生命上限。前缀记下之前的值，后缀按差额补上多出来的部分
        internal static void BitePrefix(bca __instance, out long __state) => __state = __instance.bskn;

        internal static void BitePostfix(bca __instance, long __state)
        {
            float m = SkillForms.BiteMultiplier;
            if (m <= 1f || (object)__instance != nt.bryb) return;
            long gained = __instance.bskn - __state;
            if (gained <= 0) return;
            long extra = (long)Math.Min(long.MaxValue / 4, gained * (double)(m - 1f));
            if (extra <= 0) return;
            __instance.bskn = __instance.bskn + Math.Min(extra, long.MaxValue - __instance.bskn);
            __instance.jgu(extra);
            (__instance.bsey as PlayerBiteMode)?.kpy();
        }

        // msj(技能, 施法者, 位置, 表, 敏捷, 时长, 范围倍率)：十字长度 biej，宽度是 biej / 6，爆炸特效按 biej / 40 缩放，都跟着变。
        // 每个客户端都会生成炸弹实例；伤害只在施法者本机算，所以只改本地玩家放的
        internal static void BombPostfix(yn __1, ref float ___bjqk, GameObject ___bjql)
        {
            float m = SkillForms.BombMultiplier;
            if (m <= 1f || (object)__1 != nt.bryb) return;
            ___bjqk *= m;
            if (___bjql != null) ___bjql.transform.localScale *= 1f + (m - 1f) * 0.5f;
        }
    }

    internal static class LethalHooks
    {
        // 联机客户端：血量是服务器同步回来的，刚打出去的伤害还没扣到本机的 hp 上。短时间内的伤害记在这里，估算时一起减掉
        private static double unsynced;
        private static float unsyncedUntil;

        // iwl(伤害, 类型, 攻击方, 角度, 乘全局倍率, ...)：本地玩家这一下会死（伤害 ≥ 血 + 护盾）时问各个 Mod，有一个要拦就把伤害改成 0。
        // 估算和 iwl 一致：e 为真时乘 nt.fyi()，按 nu.fzv 四舍五入取整；破盾的额外伤害只打护盾，不影响会不会死。
        // 已经死了不拦：iwr（持续伤害）和 lfr（远端伤害）不查死亡就进 iwl，死后 hp = 0 的任何伤害都会被当成致命
        internal static void SettlePrefix(yn __instance, ref double __0, bool __4)
        {
            if (!Combat.HasLethalGuards || __0 <= 0.0 || __instance.bgij || (object)__instance != nt.bryb) return;
            if (GameRaw.Hp(__instance) <= 0 || __instance.bsew == RoleState.Dead) return;
            double damage = Math.Round(__0 * (__4 ? nt.fyi() : 1f));
            bool client = !cmv.bpul;
            if (client && Time.time > unsyncedUntil) unsynced = 0;
            double left = GameRaw.Hp(__instance) + GameRaw.Shield(__instance) - (client ? unsynced : 0);
            if (damage >= left && Combat.RaiseLethal(damage))
            {
                __0 = 0.0;
                unsynced = 0;
                return;
            }
            if (client)
            {
                unsynced += damage;
                unsyncedUntil = Time.time + 0.3f;
            }
        }
    }

    internal static class SoulDevourerHooks
    {
        internal static void StrikeEndPrefix(object __instance) => SoulDevourer.OnStrikeEnd(__instance);
    }

    // MyServerNetworkManager.ros(ClientNetMessage)：客户端（含主机自己）收到服务器消息后的分发（在 cmh.wuu 里注册，是真方法）。
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

    // bxc.rco(英雄编号, out 文件夹, out manifest)：查找英雄模型文件夹的唯一入口，出生、选英雄预览、变身、英雄名和头像覆盖都经过它
    //   （wy 是同签名的诱饵）。原版顺序：PlayerPrefs Workshop.LocalHeroModel.<编号> → 设置里的本地文件 Mod → 工坊绑定。
    //   只按英雄查本机设置，所以原版在你这边会把同英雄的其他玩家也换掉，名字（roleName）也换成模型里的 heroName
    // bca.lpj：出生（ClientRpcBornPos），每个客户端对每个玩家都会调用；里面 yn.jef → ez.dzr 算名字、bxc.rcb 换模型
    // bhx.nnv（变身技能）、bcq.mjz（变身状态恢复）：通过 bxc.rcd → pwl 给新模型换上 additionalModels
    internal static class HeroSkinHooks
    {
        // 正在出生或变身的角色；不在这些方法里时为 null（例如选英雄界面的预览）
        private static yn owner;

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
            if (folder != null && !bxi.rer(folder, out manifest)) return true;
            __1 = folder ?? string.Empty;
            __2 = manifest;
            __result = folder != null;
            return false;
        }

        internal static void SpawnPrefix(bca __instance, out yn __state)
        {
            __state = owner;
            owner = __instance;
        }

        internal static void SkillTransformPrefix(bhx __instance, out yn __state)
        {
            __state = owner;
            owner = __instance.bjbc;
        }

        internal static void CheckpointTransformPrefix(bca __0, out yn __state)
        {
            __state = owner;
            owner = __0;
        }

        // 用 finalizer 而不是 postfix：原方法抛异常时也要恢复
        internal static void RestoreOwner(yn __state) => owner = __state;

        // Mod 自己给某个角色重新换模型时用（游戏里同样的操作只会对本地玩家做）
        internal static void WithOwner(yn role, Action action)
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

    // bee.mtf(攻击者, 目标, 伤害, AttackType, 目标护盾, …)：触发命中事件 bgfu 前用 bep.mvi 把伤害截到目标的护盾 + 血量，
    // 打死目标时溢出的部分不算（goz、gxk 是同签名的诱饵）。这里记下截断前的伤害；bgfu 里的订阅者可能造成新的伤害（嵌套），用栈
    internal static class RawDamageHooks
    {
        private struct Entry
        {
            public yn Attacker, Target;
            public long Damage;
        }

        private static readonly List<Entry> stack = new List<Entry>();

        internal static void Prefix(yn __0, yn __1, long __2, out bool __state)
        {
            stack.Add(new Entry { Attacker = __0, Target = __1, Damage = __2 });
            __state = true;
        }

        internal static void Pop(bool __state)
        {
            if (__state && stack.Count > 0) stack.RemoveAt(stack.Count - 1);
        }

        // 最内层的调用对得上这次命中的目标和攻击者才算
        internal static bool TryGet(yn target, yn attacker, out long damage)
        {
            damage = 0;
            if (stack.Count == 0) return false;
            var e = stack[stack.Count - 1];
            damage = e.Damage;
            return e.Target == target && e.Attacker == attacker;
        }
    }

    // yn.jgi(攻击者, 伤害, 角度, AttackType, 暴击, …, ActiveSkillEnum, DamageSourceKind, 技能实例 bfe, …)：目标受到伤害的入口
    //   （doi、hon 是同签名的诱饵）；yn.jgp(AttackType, 伤害, 暴击, 攻击者, …)：Buff 持续伤害、真实伤害，直接调 iwl。
    // 两者里面都会走到 bee.mtf 触发命中事件 bgfu，这里在调用期间记下伤害的类型、来源和技能实例。可能嵌套（命中触发别的伤害），用栈
    internal static class DamageContextHooks
    {
        internal struct Context
        {
            public yn Target, Attacker;
            public AttackType Type;
            public DamageSourceKind Source;
            public bgg Skill;
            // 没有技能实例时，调用栈里发起这次伤害的被动 / 装备特效 / 遗物 / Buff 的类型
            public Type Caller;
        }

        // 往上最多看这么多层（调用者 → ez.ebl 之类的中转 → iwk）
        private const int MaxFrames = 10;

        private static readonly List<Context> stack = new List<Context>();

        internal static void HitPrefix(yn __instance, yn __0, AttackType __3, DamageSourceKind __7, bgg __8, out bool __state)
        {
            var ctx = new Context { Target = __instance, Attacker = __0, Type = __3, Source = __7, Skill = __8 };
            if (__8 == null && __3 != AttackType.Normal && ShouldTrace(__0)) ctx.Caller = FindCaller();
            stack.Add(ctx);
            __state = true;
        }

        internal static void DotPrefix(yn __instance, AttackType __0, yn __3, out bool __state)
        {
            var ctx = new Context { Target = __instance, Attacker = __3, Type = __0, Source = DamageSourceKind.Default };
            if (ShouldTrace(__3)) ctx.Caller = FindCaller();
            stack.Add(ctx);
            __state = true;
        }

        // 取调用栈有开销：只在有人订阅命中事件、而且是本地玩家打出的伤害时取
        private static bool ShouldTrace(yn attacker)
            => attacker != null && attacker == nt.bryb && Combat.HasSubscribers;

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
        internal static bool TryGet(yn target, yn attacker, out Context ctx)
        {
            ctx = default;
            if (stack.Count == 0) return false;
            ctx = stack[stack.Count - 1];
            return ctx.Target == target && ctx.Attacker == attacker;
        }
    }
}
