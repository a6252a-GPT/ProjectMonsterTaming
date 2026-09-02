using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed partial class MonsterActiveAttackExecutor // 조립된 Step을 순서대로 한 번씩 실행
    {
        private sealed class PendingHit
        {
            public UnitActor Target;
            public float Delay;
            public Vector3 EffectCenter;
        }

        private sealed class DeliveryVisual
        {
            public GameObject Instance;
            public Vector3 Start;
            public Vector3 End;
            public float Duration;
            public float Elapsed;
        }

        private sealed class TrackedVfx
        {
            public GameObject Instance;
            public MonsterActivePresentationEndPolicy EndPolicy;
        }

        private sealed class InFlightAttackBlock
        {
            public MonsterBasicAttackVfxContext MotionContext;
            public float Remaining;
        }

        private readonly List<UnitActor> targetBuffer = new List<UnitActor>();
        private readonly List<UnitActor> stepTargets = new List<UnitActor>();
        private readonly List<PendingHit> pendingHits = new List<PendingHit>();
        private readonly List<DeliveryVisual> deliveryVisuals = new List<DeliveryVisual>();
        private readonly List<TrackedVfx> trackedVfx = new List<TrackedVfx>();
        private readonly List<InFlightAttackBlock> inFlightAttackBlocks =
            new List<InFlightAttackBlock>();
        private readonly HashSet<int> uniqueTargets = new HashSet<int>();
        private readonly HashSet<string> playedOncePerStepSlots =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly MonsterBasicAttackExecutor attackBlockExecutor =
            new MonsterBasicAttackExecutor();
        private UnitActor owner;
        private CombatWorld world;
        private MonsterAttackActiveSkill skill;
        private UnitActor lockedTarget;
        private UnitActor previousStepTarget;
        private MonsterActiveAttackStep currentStep;
        private float currentDamageMultiplier;
        private int stepIndex;
        private float waitRemaining;
        private bool stepPrepared;
        private bool feelPlayedForStep;
        private bool firstStepMotionAlreadyPlaying;
        private bool stepFired;
        private float stepLifetimeRemaining;
        private float preparedMotionDuration;
        private float preparedMotionElapsed;
        private float preparedWaitDuration;
        private MonsterBasicAttackProfile currentAttackBlock;
        private MonsterActiveAttackPresentationBinding currentAttackBlockPresentation;
        private MonsterActionExecutionContext currentAttackBlockContext;
        private int currentAttackBlockSequence;
        private bool currentAttackBlockMotionBegun;
        private bool preparingFromPreviousLaunch;
        private float launchChainMinimumDelay;

        public bool IsRunning { get; private set; }
        public int CompletedStepCount { get; private set; }

        public bool Begin(
            UnitActor source,
            CombatWorld combatWorld,
            MonsterAttackActiveSkill active,
            UnitActor initialTarget,
            bool initialStepMotionAlreadyPlaying = false)
        {
            Reset();
            if (source == null || combatWorld == null || active == null ||
                active.Steps.Count == 0 || !active.TryValidate(out _))
            {
                return false;
            }

            owner = source;
            world = combatWorld;
            skill = active;
            lockedTarget = initialTarget;
            firstStepMotionAlreadyPlaying = initialStepMotionAlreadyPlaying;
            IsRunning = true;
            PrepareNextStep(false);
            return true;
        }

        public bool Tick(float deltaTime)
        {
            if (!IsRunning) return true;
            if (owner == null || world == null || skill == null || !owner.IsAlive)
            {
                Reset();
                return true;
            }

            var remainingDelta = Mathf.Max(0f, deltaTime);
            TickInFlightAttackBlocks(remainingDelta);
            if (currentStep == null)
            {
                TryFinishAfterInFlightSteps();
                return !IsRunning;
            }
            var safety = 0;
            while (IsRunning && safety++ < 96)
            {
                if (currentStep == null)
                {
                    TryFinishAfterInFlightSteps();
                    break;
                }
                if (stepFired)
                {
                    var activityRemaining = ResolveStepActivityRemaining();
                    TickPendingHits(remainingDelta);
                    TickDeliveryVisuals(remainingDelta);
                    stepLifetimeRemaining = Mathf.Max(0f, stepLifetimeRemaining - remainingDelta);
                    if (ResolveStepActivityRemaining() <= 0.0001f)
                    {
                        remainingDelta = Mathf.Max(0f, remainingDelta - activityRemaining);
                        CompleteCurrentStep();
                        continue;
                    }
                    break;
                }

                if (waitRemaining > 0f)
                {
                    if (remainingDelta + 0.0001f < waitRemaining)
                    {
                        waitRemaining -= remainingDelta;
                        break;
                    }
                    remainingDelta = Mathf.Max(0f, remainingDelta - waitRemaining);
                    waitRemaining = 0f;
                }

                if (!stepPrepared)
                {
                    PrepareCurrentStep();
                    if (!IsRunning) break;
                    // 타깃 부재로 PrepareCurrentStep이 현재 Step을 넘긴 경우에는
                    // 다음 Step을 같은 호출에서 새로 준비하고, 이전 Step을 발사하지 않는다.
                    if (!stepPrepared) continue;
                    if (waitRemaining > 0f) continue;
                }

                FireCurrentStep();
            }
            return !IsRunning;
        }

        public void Reset()
        {
            EndCurrentAttackBlockMotion();
            for (var index = inFlightAttackBlocks.Count - 1; index >= 0; index--)
            {
                MonsterBasicAttackVfxRuntime.EndMotion(inFlightAttackBlocks[index].MotionContext);
            }
            ReleaseTrackedVfx(null);
            for (var index = deliveryVisuals.Count - 1; index >= 0; index--)
            {
                if (deliveryVisuals[index].Instance != null) world?.ReturnMonsterObject(deliveryVisuals[index].Instance);
            }
            owner = null;
            world = null;
            skill = null;
            lockedTarget = null;
            previousStepTarget = null;
            currentStep = null;
            currentDamageMultiplier = 0f;
            stepIndex = 0;
            waitRemaining = 0f;
            stepPrepared = false;
            firstStepMotionAlreadyPlaying = false;
            stepFired = false;
            stepLifetimeRemaining = 0f;
            preparedMotionDuration = 0f;
            preparedMotionElapsed = 0f;
            preparedWaitDuration = 0f;
            currentAttackBlock = null;
            currentAttackBlockPresentation = null;
            currentAttackBlockContext = default;
            currentAttackBlockSequence = 0;
            currentAttackBlockMotionBegun = false;
            preparingFromPreviousLaunch = false;
            launchChainMinimumDelay = 0f;
            IsRunning = false;
            CompletedStepCount = 0;
            targetBuffer.Clear();
            stepTargets.Clear();
            pendingHits.Clear();
            deliveryVisuals.Clear();
            trackedVfx.Clear();
            inFlightAttackBlocks.Clear();
            uniqueTargets.Clear();
            playedOncePerStepSlots.Clear();
        }

        private UnitTeam OpponentTeam => owner.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;
    }
}
