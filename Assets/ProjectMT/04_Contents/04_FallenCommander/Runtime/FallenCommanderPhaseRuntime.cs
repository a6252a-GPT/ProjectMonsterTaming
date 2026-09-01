using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public sealed class FallenCommanderPhaseRuntime
    {
        private FallenCommanderPhaseConfig config;

        public FallenCommanderBossPhase CurrentPhase { get; private set; } =
            FallenCommanderBossPhase.Phase1;
        public FallenCommanderBossPhase RequestedPhase { get; private set; } =
            FallenCommanderBossPhase.Phase1;
        public FallenCommanderAttackPattern PendingSignatureAttack { get; private set; } =
            FallenCommanderAttackPattern.Basic;
        public float TransitionRemainingTime { get; private set; }
        public float IntroNoticeRemainingTime { get; private set; }
        public bool IsTransitionActive { get; private set; }
        public bool IsWaitingForSignature { get; private set; }
        public string TransitionMessage { get; private set; } = string.Empty;

        public FallenCommanderPhaseData CurrentData => config?.GetPhase(CurrentPhase);

        public void Configure(FallenCommanderPhaseConfig phaseConfig)
        {
            config = phaseConfig;
            Reset();
        }

        public FallenCommanderPhaseData Begin(float startDelayRemaining)
        {
            Reset();
            var phaseData = CurrentData;
            if (phaseData == null)
            {
                return null;
            }

            IntroNoticeRemainingTime = startDelayRemaining > 0f
                ? Mathf.Min(startDelayRemaining, phaseData.TransitionDuration)
                : phaseData.TransitionDuration;
            TransitionMessage = phaseData.TransitionMessage;
            return phaseData;
        }

        public void TickNotice(float deltaTime)
        {
            IntroNoticeRemainingTime = Mathf.Max(
                0f,
                IntroNoticeRemainingTime - deltaTime);
        }

        public bool RequestForHealth(float healthRatio)
        {
            var phaseData = config?.GetPhaseForHealthRatio(healthRatio);
            if (phaseData == null || phaseData.Phase <= RequestedPhase)
            {
                return false;
            }

            RequestedPhase = phaseData.Phase;
            return true;
        }

        public bool TryBeginNextTransition(
            bool isBlocked,
            out FallenCommanderPhaseData phaseData)
        {
            phaseData = null;
            if (isBlocked ||
                IsTransitionActive ||
                IsWaitingForSignature ||
                RequestedPhase <= CurrentPhase)
            {
                return false;
            }

            CurrentPhase = (FallenCommanderBossPhase)((int)CurrentPhase + 1);
            phaseData = config?.GetPhase(CurrentPhase);
            if (phaseData == null)
            {
                return false;
            }

            PendingSignatureAttack = phaseData.HasSignatureAttack
                ? phaseData.SignatureAttack
                : FallenCommanderAttackPattern.Basic;
            TransitionRemainingTime = phaseData.TransitionDuration;
            IntroNoticeRemainingTime = 0f;
            TransitionMessage = phaseData.TransitionMessage;
            IsTransitionActive = true;
            return true;
        }

        public bool BeginForcedTransition(
            FallenCommanderBossPhase phase,
            out FallenCommanderPhaseData phaseData)
        {
            phaseData = config?.GetPhase(phase);
            if (phaseData == null)
            {
                return false;
            }

            CurrentPhase = phase;
            RequestedPhase = phase;
            PendingSignatureAttack = phaseData.HasSignatureAttack
                ? phaseData.SignatureAttack
                : FallenCommanderAttackPattern.Basic;
            TransitionRemainingTime = phaseData.TransitionDuration;
            IntroNoticeRemainingTime = 0f;
            TransitionMessage = phaseData.TransitionMessage;
            IsTransitionActive = true;
            IsWaitingForSignature = false;
            return true;
        }

        public bool TickTransition(
            float deltaTime,
            out FallenCommanderAttackPattern signatureAttack)
        {
            signatureAttack = FallenCommanderAttackPattern.Basic;
            if (!IsTransitionActive)
            {
                return false;
            }

            TransitionRemainingTime = Mathf.Max(
                0f,
                TransitionRemainingTime - deltaTime);
            if (TransitionRemainingTime > 0f)
            {
                return false;
            }

            IsTransitionActive = false;
            signatureAttack = PendingSignatureAttack;
            IsWaitingForSignature =
                signatureAttack != FallenCommanderAttackPattern.Basic;
            PendingSignatureAttack = FallenCommanderAttackPattern.Basic;
            return true;
        }

        public void CompleteSignatureIfIdle(bool isStateMachineIdle)
        {
            if (IsWaitingForSignature && isStateMachineIdle)
            {
                IsWaitingForSignature = false;
            }
        }

        public void CancelTransition()
        {
            IsTransitionActive = false;
            IsWaitingForSignature = false;
            TransitionRemainingTime = 0f;
            PendingSignatureAttack = FallenCommanderAttackPattern.Basic;
        }

        public void ForceRequestedPhase(FallenCommanderBossPhase phase)
        {
            RequestedPhase = phase;
        }

        public void Reset()
        {
            CurrentPhase = FallenCommanderBossPhase.Phase1;
            RequestedPhase = FallenCommanderBossPhase.Phase1;
            PendingSignatureAttack = FallenCommanderAttackPattern.Basic;
            TransitionRemainingTime = 0f;
            IntroNoticeRemainingTime = 0f;
            IsTransitionActive = false;
            IsWaitingForSignature = false;
            TransitionMessage = string.Empty;
        }
    }
}
