using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [CreateAssetMenu(
        fileName = "FallenCommanderPhaseConfig",
        menuName = "ProjectMT/타락한 과거의 군단장/페이즈 설정 데이터")]
    public sealed class FallenCommanderPhaseConfig : ScriptableObject
    {
        [SerializeField, InspectorName("페이즈 목록")]
        private List<FallenCommanderPhaseData> phases = new();

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
                error = "1·2·3 페이즈 데이터가 모두 필요합니다.";
                return false;
            }

            if (phaseOne.HealthRatio <= phaseTwo.HealthRatio ||
                phaseTwo.HealthRatio <= phaseThree.HealthRatio)
            {
                error = "진입 체력 비율은 1페이즈 > 2페이즈 > 3페이즈 순서여야 합니다.";
                return false;
            }

            for (var index = 0; index < phases.Count; index++)
            {
                if (phases[index] == null || phases[index].AvailableAttacks.Count == 0)
                {
                    error = "모든 페이즈에는 공격이 하나 이상 필요합니다.";
                    return false;
                }

                if (phases[index].Allows(FallenCommanderAttackPattern.Basic))
                {
                    error = "기본 투사체는 '기본 투사체 중복 공격 허용' 항목으로 설정해 주세요.";
                    return false;
                }

                for (var otherIndex = index + 1; otherIndex < phases.Count; otherIndex++)
                {
                    if (phases[otherIndex] != null &&
                        phases[index].Phase == phases[otherIndex].Phase)
                    {
                        error = $"같은 페이즈 데이터가 중복되었습니다: {phases[index].Phase}.";
                        return false;
                    }
                }

                if (phases[index].HasSignatureAttack &&
                    !phases[index].Allows(phases[index].SignatureAttack))
                {
                    error = $"{phases[index].Phase} 대표 공격은 사용할 공격 목록에도 포함되어야 합니다.";
                    return false;
                }

                if (phases[index].Allows(FallenCommanderAttackPattern.Mark) &&
                    !phases[index].MarkStrikePattern.TryValidate(out var markStrikeError))
                {
                    error = $"{phases[index].Phase} 위치 공격 설정 오류: {markStrikeError}";
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
        [SerializeField, InspectorName("페이즈")]
        private FallenCommanderBossPhase phase = FallenCommanderBossPhase.Phase1;
        [SerializeField, InspectorName("진입 체력 비율 (읽기 전용)"), ReadOnlyInInspector]
        private float healthRatio = 1f;
        [SerializeField, InspectorName("사용할 공격 목록")]
        private List<FallenCommanderAttackPattern> availableAttacks = new();
        [SerializeField, InspectorName("기본 투사체 중복 공격 허용")]
        private bool allowOverlappingBasicAttack = true;
        [SerializeField, InspectorName("페이즈 대표 공격 사용")]
        private bool hasSignatureAttack;
        [SerializeField, InspectorName("페이즈 대표 공격")]
        private FallenCommanderAttackPattern signatureAttack;
        [SerializeField, InspectorName("페이즈 전환 문구")]
        private string transitionMessage = "1 페이즈";
        [SerializeField, InspectorName("페이즈 전환 사운드")]
        private AudioClip transitionSound;
        [SerializeField, InspectorName("페이즈 전환시간"), Min(0.1f)]
        private float transitionDuration = 1f;
        [SerializeField, InspectorName("위치 공격 다중 패턴 설정")]
        private FallenCommanderMarkStrikePhaseData markStrikePattern = new();

        public FallenCommanderBossPhase Phase => phase;
        public float HealthRatio => healthRatio;
        public IReadOnlyList<FallenCommanderAttackPattern> AvailableAttacks => availableAttacks;
        public bool AllowOverlappingBasicAttack => allowOverlappingBasicAttack;
        public bool HasSignatureAttack => hasSignatureAttack;
        public FallenCommanderAttackPattern SignatureAttack => signatureAttack;
        public string TransitionMessage => transitionMessage;
        public AudioClip TransitionSound => transitionSound;
        public float TransitionDuration => transitionDuration;
        public FallenCommanderMarkStrikePhaseData MarkStrikePattern => markStrikePattern;

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
            if (selected == FallenCommanderAttackPattern.BlackHole &&
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

    [System.Serializable]
    public sealed class FallenCommanderMarkStrikePhaseData
    {
        [SerializeField, InspectorName("총 공격 개수"), Min(1)]
        private int totalCount = 3;
        [SerializeField, InspectorName("동시 생성 개수"), Min(1)]
        private int simultaneousCount = 3;
        [SerializeField, InspectorName("다음 묶음 생성 간격"), Min(0f)]
        private float groupInterval = 0.45f;
        [SerializeField, InspectorName("개별 공격 경고시간"), Min(0.1f)]
        private float warningDuration = 1f;
        [SerializeField, InspectorName("랜덤 생성 영역 반경")]
        private Vector2 arenaHalfExtents = new Vector2(6f, 4f);
        [SerializeField, InspectorName("랜덤 위치 최소 간격"), Min(0f)]
        private float minimumSpacing = 2.5f;
        [SerializeField, InspectorName("묶음 밀집 배치")]
        private bool clusterGroups;
        [SerializeField, InspectorName("묶음 배치 반경"), Min(0f)]
        private float clusterRadius = 1.35f;
        [SerializeField, InspectorName("묶음당 최대 피해 횟수"), Min(1)]
        private int maxDamagePerGroup = 1;
        [SerializeField, InspectorName("피격 기절시간"), Min(0f)]
        private float stunDuration;

        public int TotalCount => Mathf.Max(1, totalCount);
        public int SimultaneousCount => Mathf.Clamp(simultaneousCount, 1, TotalCount);
        public float GroupInterval => Mathf.Max(0f, groupInterval);
        public float WarningDuration => Mathf.Max(0.1f, warningDuration);
        public Vector2 ArenaHalfExtents => new Vector2(
            Mathf.Max(0.1f, arenaHalfExtents.x),
            Mathf.Max(0.1f, arenaHalfExtents.y));
        public float MinimumSpacing => Mathf.Max(0f, minimumSpacing);
        public bool ClusterGroups => clusterGroups;
        public float ClusterRadius => Mathf.Max(0f, clusterRadius);
        public int MaxDamagePerGroup => Mathf.Max(1, maxDamagePerGroup);
        public float StunDuration => Mathf.Max(0f, stunDuration);

        // 다중 위치 공격의 개수·시간·생성 영역 조합이 실행 가능한지 검사한다.
        public bool TryValidate(out string error)
        {
            if (totalCount < 1 || simultaneousCount < 1 || simultaneousCount > totalCount)
            {
                error = "동시 생성 개수는 1 이상이며 총 공격 개수를 넘을 수 없습니다.";
                return false;
            }

            if (warningDuration < 0.1f || groupInterval < 0f)
            {
                error = "경고시간은 0.1초 이상이고 묶음 간격은 0초 이상이어야 합니다.";
                return false;
            }

            if (arenaHalfExtents.x <= 0f || arenaHalfExtents.y <= 0f)
            {
                error = "랜덤 생성 영역은 X·Y 모두 0보다 커야 합니다.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
