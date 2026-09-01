using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [System.Flags]
    public enum DamageFeedbackFlags
    {
        None = 0,
        BasicAttackFeelTargetMotion = 1 << 0,
        PassiveEnhancedNumber = 1 << 1,
        SeparateFloatingNumber = 1 << 2 // 다단 피해는 타격별 숫자를 각각 표시
    }

    public readonly struct DamageRequest // 피해 적용 전 요청값
    {
        public DamageRequest(UnitActor source, float amount, Vector3 hitPoint)
            : this(source, amount, hitPoint, false)
        {
        }

        public DamageRequest(UnitActor source, float amount, Vector3 hitPoint, bool isCritical)
            : this(source, amount, hitPoint, isCritical, DamageFeedbackFlags.None)
        {
        }

        public DamageRequest(
            UnitActor source,
            float amount,
            Vector3 hitPoint,
            bool isCritical,
            DamageFeedbackFlags feedbackFlags)
        {
            Source = source;
            Amount = amount;
            HitPoint = hitPoint;
            IsCritical = isCritical;
            FeedbackFlags = feedbackFlags;
        }

        public UnitActor Source { get; } // 공격 주체
        public float Amount { get; } // 요청 피해량
        public Vector3 HitPoint { get; } // 연출 발생 위치
        public bool IsCritical { get; } // 치명타 피드백 구분
        public DamageFeedbackFlags FeedbackFlags { get; } // 이번 피해만의 피드백 소유권
    }

    public readonly struct DamageReport // 피해 적용 후 확정값
    {
        public DamageReport(DamageRequest request, float appliedDamage, float remainingHealth, bool killed)
        {
            Request = request;
            AppliedDamage = appliedDamage;
            RemainingHealth = remainingHealth;
            Killed = killed;
        }

        public DamageRequest Request { get; }
        public float AppliedDamage { get; } // 실제 반영 피해
        public float RemainingHealth { get; } // 남은 체력
        public bool Killed { get; } // 이번 피해 사망 여부
    }
}
