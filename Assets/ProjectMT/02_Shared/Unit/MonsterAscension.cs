using System;

namespace ProjectMT.Shared.Unit
{
    // 2·4돌파에서 부여될 특수 효과 자리표시자.
    // 효과 내용은 아직 미정이라 여기서는 구현하지 않고, 추후 별도 스킬/효과 스크립트가
    // 이 인터페이스를 구현해서 만들면 MonsterAscension 쪽 수정 없이 그대로 꽂아 쓸 수 있다.
    public interface IMonsterSpecialEffect
    {
        string EffectId { get; }
    }

    // 중복 획득(돌파) 규칙만 계산하는 순수 정적 클래스.
    // 실제 저장(돌파 횟수·전용 재화 적립)은 GameProgressData가 담당하고,
    // 이 클래스는 "몇 돌파까지가 최대인지", "능력치가 몇 % 오르는지"만 계산해서 알려준다.
    public static class MonsterAscension
    {
        public const int MaxAscensionLevel = 5; // 5돌파가 최대, 이후 중복 획득은 전용 재화로 전환
        private const float StatBoostPerMilestone = 0.1f; // 능력치 상승 마일스톤 한 번당 +10%

        // 1·3·5돌파에서 능력치 수치 상승
        public static bool IsStatBoostMilestone(int ascensionLevel)
        {
            return ascensionLevel == 1 || ascensionLevel == 3 || ascensionLevel == 5;
        }

        // 2·4돌파에서 특수 효과 획득 (효과 내용은 미정, IMonsterSpecialEffect 구현체가 나오면 연결)
        public static bool IsSpecialEffectMilestone(int ascensionLevel)
        {
            return ascensionLevel == 2 || ascensionLevel == 4;
        }

        public static bool IsMaxAscension(int ascensionLevel)
        {
            return ascensionLevel >= MaxAscensionLevel;
        }

        // 지금까지 도달한 능력치 상승 마일스톤(1·3·5) 개수만큼 10%씩 누적된 배율을 반환한다.
        // 예: 0~돌파=1.0(변화 없음), 1돌파=1.1, 2돌파=1.1(2돌파는 특수효과라 능력치 변화 없음),
        //     3돌파=1.2, 4돌파=1.2, 5돌파(최대)=1.3
        public static float GetStatMultiplier(int ascensionLevel)
        {
            var clamped = Math.Max(0, Math.Min(ascensionLevel, MaxAscensionLevel));
            var reachedMilestones = 0;
            for (var level = 1; level <= clamped; level++)
            {
                if (IsStatBoostMilestone(level))
                {
                    reachedMilestones++;
                }
            }

            return 1f + StatBoostPerMilestone * reachedMilestones;
        }

        // 몬스터 기본 능력치 하나에 돌파 배율을 곱해서 돌려주는 편의 함수.
        // (전투 스탯 계산 파이프라인에 실제로 연결하는 작업은 별도 요청 시 진행)
        public static float ApplyStatMultiplier(float baseStat, int ascensionLevel)
        {
            return baseStat * GetStatMultiplier(ascensionLevel);
        }
    }
}
