using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [CreateAssetMenu(
        fileName = "FallenCommanderPhaseConfig",
        menuName = "ProjectMT/Fallen Commander/Phase Config")]
    public sealed class FallenCommanderPhaseConfig : ScriptableObject
    {
        [SerializeField] private List<FallenCommanderPhaseData> phases = new();

        public IReadOnlyList<FallenCommanderPhaseData> Phases => phases;

        // 지정한 페이즈의 설정을 반환한다.
        public FallenCommanderPhaseData GetPhase(FallenCommanderBossPhase phase)
        {
            for (var index = 0; index < phases.Count; index++)
            {
                if (phases[index] != null && phases[index].Phase == phase)
                {
                    return phases[index];
                }
            }

            return null;
        }

        // 현재 체력 비율에 해당하는 가장 높은 페이즈 설정을 반환한다.
        public FallenCommanderPhaseData GetPhaseForHealthRatio(float healthRatio)
        {
            var clampedRatio = Mathf.Clamp01(healthRatio);
            FallenCommanderPhaseData result = null;

            for (var index = 0; index < phases.Count; index++)
            {
                var phase = phases[index];
                if (phase == null || clampedRatio > phase.HealthRatio)
                {
                    continue;
                }

                if (result == null || phase.Phase > result.Phase)
                {
                    result = phase;
                }
            }

            return result ?? GetPhase(FallenCommanderBossPhase.Phase1);
        }

        // 세 페이즈의 필수 데이터와 체력 구간 순서를 검증한다.
        public bool TryValidate(out string error)
        {
            var phaseOne = GetPhase(FallenCommanderBossPhase.Phase1);
            var phaseTwo = GetPhase(FallenCommanderBossPhase.Phase2);
            var phaseThree = GetPhase(FallenCommanderBossPhase.Phase3);

            if (phaseOne == null || phaseTwo == null || phaseThree == null)
            {
                error = "Phase1, Phase2, Phase3 data are all required.";
                return false;
            }

            if (phaseOne.HealthRatio <= phaseTwo.HealthRatio ||
                phaseTwo.HealthRatio <= phaseThree.HealthRatio)
            {
                error = "Phase health ratios must be ordered Phase1 > Phase2 > Phase3.";
                return false;
            }

            for (var index = 0; index < phases.Count; index++)
            {
                if (phases[index] == null || phases[index].AvailableAttacks.Count == 0)
                {
                    error = "Every phase needs at least one attack.";
                    return false;
                }

                if (phases[index].Allows(FallenCommanderAttackPattern.Basic))
                {
                    error = "Basic projectile is controlled by Allow Overlapping Basic Attack.";
                    return false;
                }

                for (var otherIndex = index + 1; otherIndex < phases.Count; otherIndex++)
                {
                    if (phases[otherIndex] != null &&
                        phases[index].Phase == phases[otherIndex].Phase)
                    {
                        error = $"Duplicate phase data: {phases[index].Phase}.";
                        return false;
                    }
                }

                if (phases[index].HasSignatureAttack &&
                    !phases[index].Allows(phases[index].SignatureAttack))
                {
                    error = $"{phases[index].Phase} signature attack must be in its attack list.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

    [System.Serializable]
    public sealed class FallenCommanderPhaseData
    {
        [SerializeField] private FallenCommanderBossPhase phase = FallenCommanderBossPhase.Phase1;
        [SerializeField, ReadOnlyInInspector] private float healthRatio = 1f;
        [SerializeField] private List<FallenCommanderAttackPattern> availableAttacks = new();
        [SerializeField] private bool allowOverlappingBasicAttack = true;
        [SerializeField] private bool hasSignatureAttack;
        [SerializeField] private FallenCommanderAttackPattern signatureAttack;
        [SerializeField] private string transitionMessage = "1 페이즈";
        [SerializeField] private AudioClip transitionSound;
        [SerializeField, Min(0.1f)] private float transitionDuration = 1f;

        public FallenCommanderBossPhase Phase => phase;
        public float HealthRatio => healthRatio;
        public IReadOnlyList<FallenCommanderAttackPattern> AvailableAttacks => availableAttacks;
        public bool AllowOverlappingBasicAttack => allowOverlappingBasicAttack;
        public bool HasSignatureAttack => hasSignatureAttack;
        public FallenCommanderAttackPattern SignatureAttack => signatureAttack;
        public string TransitionMessage => transitionMessage;
        public AudioClip TransitionSound => transitionSound;
        public float TransitionDuration => transitionDuration;

        // 이 페이즈의 스킬 목록에 지정 공격이 포함되는지 확인한다.
        public bool Allows(FallenCommanderAttackPattern attack)
        {
            return availableAttacks.Contains(attack);
        }

        // 거리 조건으로 고른 공격을 페이즈 스킬 목록과 연속 공격 방지 규칙에 맞춘다.
        public FallenCommanderAttackPattern ResolveAttack(
            FallenCommanderAttackPattern selected,
            FallenCommanderAttackPattern previous)
        {
            if (selected == FallenCommanderAttackPattern.Wide &&
                previous == FallenCommanderAttackPattern.Ring &&
                Allows(FallenCommanderAttackPattern.TrackingMark))
            {
                return FallenCommanderAttackPattern.TrackingMark;
            }

            if (Allows(selected) && selected != previous)
            {
                return selected;
            }

            if (selected == FallenCommanderAttackPattern.Mark &&
                Allows(FallenCommanderAttackPattern.TrackingMark) &&
                previous != FallenCommanderAttackPattern.TrackingMark)
            {
                return FallenCommanderAttackPattern.TrackingMark;
            }

            if (selected == FallenCommanderAttackPattern.TrackingMark &&
                Allows(FallenCommanderAttackPattern.Mark) &&
                previous != FallenCommanderAttackPattern.Mark)
            {
                return FallenCommanderAttackPattern.Mark;
            }

            if (Allows(FallenCommanderAttackPattern.Ring) &&
                previous != FallenCommanderAttackPattern.Ring)
            {
                return FallenCommanderAttackPattern.Ring;
            }

            if (Allows(FallenCommanderAttackPattern.Mark) &&
                previous != FallenCommanderAttackPattern.Mark)
            {
                return FallenCommanderAttackPattern.Mark;
            }

            for (var index = 0; index < availableAttacks.Count; index++)
            {
                if (availableAttacks[index] != previous)
                {
                    return availableAttacks[index];
                }
            }

            return availableAttacks.Count > 0
                ? availableAttacks[0]
                : FallenCommanderAttackPattern.Mark;
        }
    }
}
