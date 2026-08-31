using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public static class FallenCommanderBossConfigViews
    {
        public static FallenCommanderBossStatsConfig CreateStats(
            FallenCommanderBossConfig source) => new(source);

        public static FallenCommanderAttackSetConfig CreateAttackSet(
            FallenCommanderBossConfig source) => new(source);

        public static FallenCommanderFinalChargeConfig CreateFinalCharge(
            FallenCommanderBossConfig source) => new(source);

        public static FallenCommanderPresentationConfig CreatePresentation(
            FallenCommanderBossConfig source) => new(source);
    }

    public readonly struct FallenCommanderBossStatsConfig
    {
        public FallenCommanderBossStatsConfig(FallenCommanderBossConfig source)
        {
            BaseMaxHealth = source.BaseMaxHealth;
            BaseDefense = source.BaseDefense;
            BaseMoveSpeed = source.BaseMoveSpeed;
            AttackInterval = source.AttackInterval;
            AttackRange = source.AttackRange;
            TurnSpeed = source.TurnSpeed;
        }

        public float BaseMaxHealth { get; }
        public float BaseDefense { get; }
        public float BaseMoveSpeed { get; }
        public float AttackInterval { get; }
        public float AttackRange { get; }
        public float TurnSpeed { get; }
    }

    public sealed class FallenCommanderAttackSetConfig
    {
        public FallenCommanderAttackSetConfig(FallenCommanderBossConfig source)
        {
            Basic = source.BasicAttack;
            Melee = source.MeleeAttack;
            MarkStrike = source.MarkStrike;
            TrackingMark = source.TrackingMark;
            TrackingMarkLockDuration = source.TrackingMarkLockDuration;
            BlackHole = source.BlackHole;
            BlackHoleActiveDuration = source.BlackHoleActiveDuration;
            BlackHoleCoreRadius = source.BlackHoleCoreRadius;
            BlackHoleSpawnMinDistance = source.BlackHoleSpawnMinDistance;
            BlackHoleSpawnMaxDistance = source.BlackHoleSpawnMaxDistance;
            BlackHoleOuterPullSpeed = source.BlackHoleOuterPullSpeed;
            BlackHoleInnerPullSpeed = source.BlackHoleInnerPullSpeed;
            BlackHolePullStrengthCurve = source.BlackHolePullStrengthCurve;
            BlackHoleArenaHalfExtents = source.BlackHoleArenaHalfExtents;
            BlackHoleEndEffects = source.BlackHoleEndEffects;
            LineStrike = source.LineStrike;
            CorruptionRing = source.CorruptionRing;
            CorruptionRingSafeRadius = source.CorruptionRingSafeRadius;
            TwistedBattlefield = source.TwistedBattlefield;
            FallingBarrage = source.FallingBarrage;
            CloseAttackDistance = source.CloseAttackDistance;
            LineStrikeAlignmentThreshold = source.LineStrikeAlignmentThreshold;
            PhaseConfig = source.PhaseConfig;
        }

        public FallenCommanderBasicAttackData Basic { get; }
        public FallenCommanderAttackData Melee { get; }
        public FallenCommanderAttackData MarkStrike { get; }
        public FallenCommanderAttackData TrackingMark { get; }
        public float TrackingMarkLockDuration { get; }
        public FallenCommanderAttackData BlackHole { get; }
        public float BlackHoleActiveDuration { get; }
        public float BlackHoleCoreRadius { get; }
        public float BlackHoleSpawnMinDistance { get; }
        public float BlackHoleSpawnMaxDistance { get; }
        public float BlackHoleOuterPullSpeed { get; }
        public float BlackHoleInnerPullSpeed { get; }
        public AnimationCurve BlackHolePullStrengthCurve { get; }
        public Vector2 BlackHoleArenaHalfExtents { get; }
        public FallenCommanderAttackEffectData BlackHoleEndEffects { get; }
        public FallenCommanderAttackData LineStrike { get; }
        public FallenCommanderAttackData CorruptionRing { get; }
        public float CorruptionRingSafeRadius { get; }
        public FallenCommanderTwistedBattlefieldData TwistedBattlefield { get; }
        public FallenCommanderFallingBarrageData FallingBarrage { get; }
        public float CloseAttackDistance { get; }
        public float LineStrikeAlignmentThreshold { get; }
        public FallenCommanderPhaseConfig PhaseConfig { get; }
    }

    public readonly struct FallenCommanderFinalChargeConfig
    {
        public FallenCommanderFinalChargeConfig(FallenCommanderBossConfig source)
        {
            HealthRatio = source.FinalChargeHealthRatio;
            Duration = source.FinalChargeDuration;
            TelegraphPrefab = source.FinalChargeTelegraphPrefab;
            TelegraphHoldDuration = source.FinalChargeTelegraphHoldDuration;
            Radius = source.FinalChargeRadius;
            DamageDelay = source.FinalChargeDamageDelay;
            WarningMessage = source.FinalChargeWarningMessage;
            UseStun = source.FinalChargeUseStun;
            StunDuration = source.FinalChargeStunDuration;
            Effects = source.FinalChargeEffects;
            PreCastMotion = source.FinalChargePreCastMotion;
            PreCastMotionSpeed = source.FinalChargePreCastMotionSpeed;
            PreCastMotionStart = source.FinalChargePreCastMotionStart;
            PreCastMotionEnd = source.FinalChargePreCastMotionEnd;
            CastMotion = source.FinalChargeCastMotion;
            CastMotionSpeed = source.FinalChargeCastMotionSpeed;
            CastMotionStart = source.FinalChargeCastMotionStart;
            CastMotionEnd = source.FinalChargeCastMotionEnd;
            CastMotionDuration = source.FinalChargeCastMotionDuration;
            StartEffectOffset = source.FinalChargeStartEffectOffset;
        }

        public float HealthRatio { get; }
        public float Duration { get; }
        public GameObject TelegraphPrefab { get; }
        public float TelegraphHoldDuration { get; }
        public float Radius { get; }
        public float DamageDelay { get; }
        public string WarningMessage { get; }
        public bool UseStun { get; }
        public float StunDuration { get; }
        public FallenCommanderAttackEffectData Effects { get; }
        public AnimationClip PreCastMotion { get; }
        public float PreCastMotionSpeed { get; }
        public float PreCastMotionStart { get; }
        public float PreCastMotionEnd { get; }
        public AnimationClip CastMotion { get; }
        public float CastMotionSpeed { get; }
        public float CastMotionStart { get; }
        public float CastMotionEnd { get; }
        public float CastMotionDuration { get; }
        public Vector3 StartEffectOffset { get; }
    }

    public readonly struct FallenCommanderPresentationConfig
    {
        public FallenCommanderPresentationConfig(FallenCommanderBossConfig source)
        {
            DeathMotion = source.DeathMotion;
            DeathMotionDuration = source.DeathMotionDuration;
            CommanderDeathMotion = source.CommanderDeathMotion;
            CommanderDeathMotionDuration = source.CommanderDeathMotionDuration;
            DeathResultDelay = source.DeathResultDelay;
            BreakMotion = source.BreakMotion;
            BreakMotionDuration = source.BreakMotionDuration;
        }

        public AnimationClip DeathMotion { get; }
        public float DeathMotionDuration { get; }
        public AnimationClip CommanderDeathMotion { get; }
        public float CommanderDeathMotionDuration { get; }
        public float DeathResultDelay { get; }
        public AnimationClip BreakMotion { get; }
        public float BreakMotionDuration { get; }
    }
}
