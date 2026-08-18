using System;
using System.Collections;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.CastleRaid
{
    public enum CastleTargetKind // 성 공격 목표 종류
    {
        Wall,
        Defender,
        Building,
        MainCastle
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(HealthComponent))]
    public sealed class CastleTarget : MonoBehaviour // 파괴 가능한 성 목표
    {
        [SerializeField] private CastleTargetKind targetKind; // 성벽·수비대·건물·본성
        [SerializeField, Min(1f)] private float maxHealth = 50f; // 목표 최대 체력
        [SerializeField] private HealthComponent health; // 공용 체력 부품
        [SerializeField] private AttackSlotProvider attackSlots; // 주변 공격 자리
        [SerializeField] private NavMeshObstacle linkedObstacle; // 생존 중 길 차단
        [SerializeField] private UnitVisualFeedback visualFeedback; // 피격·사망 펄스
        [SerializeField] private Renderer[] targetRenderers; // 사망 뒤 숨길 외형
        [SerializeField] private Collider[] targetColliders; // 사망 즉시 끌 충돌체

        private Coroutine hidePresentationRoutine; // 사망 펄스 종료 대기

        public CastleTargetKind TargetKind => targetKind;
        public HealthComponent Health => health;
        public AttackSlotProvider AttackSlots => attackSlots;
        public bool IsAlive => health != null && health.IsAlive;
        public bool BlocksNavigation => linkedObstacle != null; // 파괴 뒤 경로 갱신 필요 여부

        public event Action<CastleTarget> Destroyed;
        public event Action<CastleTarget, DamageReport> Damaged;

        private void Awake()
        {
            ResolveReferences();
        }

        public void Initialize()
        {
            ResolveReferences();
            StopHidePresentationRoutine();
            SetPresentationEnabled(true);
            if (linkedObstacle != null)
            {
                linkedObstacle.enabled = true;
                linkedObstacle.carving = true; // 살아 있는 목표가 NavMesh 차단
            }

            attackSlots?.ReleaseAll();
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
            health.Initialize(maxHealth);
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        public void Shutdown()
        {
            StopHidePresentationRoutine();
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }

            attackSlots?.ReleaseAll();
            Destroyed = null;
            Damaged = null;
        }

        private void HandleDamaged(DamageReport report)
        {
            visualFeedback?.PlayHit();
            Damaged?.Invoke(this, report);
        }

        private void HandleDied(DamageReport report)
        {
            visualFeedback?.PlayDeath();
            attackSlots?.ReleaseAll();
            if (linkedObstacle != null)
            {
                linkedObstacle.carving = false;
                linkedObstacle.enabled = false; // 파괴 즉시 통로 개방
            }

            SetCollidersEnabled(false); // 죽은 목표 재선택 방지
            if (visualFeedback != null && isActiveAndEnabled)
            {
                hidePresentationRoutine = StartCoroutine(HideRenderersAfterDeath());
            }
            else
            {
                SetRenderersEnabled(false);
            }

            Destroyed?.Invoke(this);
        }

        private IEnumerator HideRenderersAfterDeath()
        {
            yield return new WaitForSeconds(UnitVisualFeedback.DeathPulseDurationSeconds); // 펄스를 끝까지 표시
            SetRenderersEnabled(false);
            hidePresentationRoutine = null;
        }

        private void StopHidePresentationRoutine()
        {
            if (hidePresentationRoutine == null)
            {
                return;
            }

            StopCoroutine(hidePresentationRoutine);
            hidePresentationRoutine = null;
        }

        private void ResolveReferences()
        {
            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (attackSlots == null)
            {
                attackSlots = GetComponent<AttackSlotProvider>();
            }

            if (visualFeedback == null)
            {
                visualFeedback = GetComponent<UnitVisualFeedback>();
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (targetColliders == null || targetColliders.Length == 0)
            {
                targetColliders = GetComponentsInChildren<Collider>(true);
            }
        }

        private void SetPresentationEnabled(bool enabled)
        {
            SetRenderersEnabled(enabled);
            SetCollidersEnabled(enabled);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            foreach (var targetRenderer in targetRenderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.enabled = enabled;
                }
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (var targetCollider in targetColliders)
            {
                if (targetCollider != null)
                {
                    targetCollider.enabled = enabled;
                }
            }
        }

        public void Configure(
            CastleTargetKind kind,
            float healthValue,
            AttackSlotProvider slots,
            NavMeshObstacle obstacle)
        {
            targetKind = kind;
            maxHealth = Mathf.Max(1f, healthValue);
            attackSlots = slots;
            linkedObstacle = obstacle;
            ResolveReferences();
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CastleTargetKind kind,
            float healthValue,
            AttackSlotProvider slots,
            NavMeshObstacle obstacle)
        {
            Configure(kind, healthValue, slots, obstacle);
        }
#endif
    }
}
