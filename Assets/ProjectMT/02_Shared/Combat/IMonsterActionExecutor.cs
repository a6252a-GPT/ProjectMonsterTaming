using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public readonly struct MonsterActionExecutionContext // Marker 한 번의 고정 실행 입력
    {
        public MonsterActionExecutionContext(
            CombatWorld world,
            UnitActor source,
            IDamageable primaryTarget,
            UnitStatsSnapshot stats,
            MonsterRuntimeAssetSet assetSet,
            MonsterAttackMarker marker,
            MonsterAnimationDriver animationDriver,
            MonsterBasicAttackProfile attackBlockOverride = null,
            IReadOnlyList<MonsterBasicAttackVfxBinding> attackBlockBindings = null,
            float damageMultiplier = 1f,
            float attackRangeOverride = 0f,
            float playbackSpeed = 1f,
            bool applyAsSkillDamage = false,
            Action<UnitActor, Vector3> hitCallback = null,
            string motionIdOverride = null,
            int? sequenceIdOverride = null)
        {
            World = world;
            Source = source;
            PrimaryTarget = primaryTarget;
            Stats = stats;
            AssetSet = assetSet;
            Marker = marker;
            AnimationDriver = animationDriver;
            AttackBlock = attackBlockOverride;
            AttackBlockBindings = attackBlockBindings;
            DamageMultiplier = Mathf.Max(0f, damageMultiplier);
            AttackRangeOverride = Mathf.Max(0f, attackRangeOverride);
            PlaybackSpeed = float.IsNaN(playbackSpeed) || float.IsInfinity(playbackSpeed)
                ? 1f
                : Mathf.Max(0.05f, playbackSpeed);
            ApplyAsSkillDamage = applyAsSkillDamage;
            HitCallback = hitCallback;
            MotionIdOverride = motionIdOverride;
            SequenceIdOverride = sequenceIdOverride;
        }

        public CombatWorld World { get; }
        public UnitActor Source { get; }
        public IDamageable PrimaryTarget { get; }
        public UnitStatsSnapshot Stats { get; }
        public MonsterRuntimeAssetSet AssetSet { get; }
        public MonsterAttackMarker Marker { get; }
        public MonsterAnimationDriver AnimationDriver { get; }
        public MonsterBasicAttackProfile AttackBlock { get; }
        public IReadOnlyList<MonsterBasicAttackVfxBinding> AttackBlockBindings { get; }
        public float DamageMultiplier { get; }
        public float AttackRangeOverride { get; }
        public float PlaybackSpeed { get; }
        public bool ApplyAsSkillDamage { get; }
        public Action<UnitActor, Vector3> HitCallback { get; }
        public string MotionIdOverride { get; }
        public int? SequenceIdOverride { get; }
        public MonsterBasicAttackProfile ResolvedAttackBlock =>
            AttackBlock ?? AssetSet?.CombatProfile?.Action?.BasicAttackProfile;
        public float ResolvedAttackRange => AttackRangeOverride > 0f
            ? AttackRangeOverride
            : Stats.attackRange;
        public IReadOnlyList<MonsterBasicAttackVfxBinding> ResolvedAttackBlockBindings =>
            AttackBlockBindings ?? AssetSet?.FeedbackProfile?.BasicAttackVfxBindings;
        public float Damage => Stats.damage *
                               (AttackBlock != null
                                   ? DamageMultiplier
                                   : AssetSet?.CombatProfile?.Action?.BasicAttackProfile == null
                                   ? Marker?.PowerRatio ?? 1f
                                   : 1f);
    }

    public interface IMonsterActionExecutor
    {
        bool Execute(MonsterActionExecutionContext context);
    }
}
