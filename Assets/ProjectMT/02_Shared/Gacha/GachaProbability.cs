using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Gacha
{
    // 등급 하나에 대한 확률·천장 설정 한 줄.
    // ceilingPulls: 그 등급 "이상"이 확정으로 나오는 누적 뽑기 횟수 (0이면 천장 없음. 영웅/전설/신화용)
    // rareGuaranteeInterval: 그 등급 "이상"이 몇 뽑마다 최소 1번 보장되는지 (0이면 미사용. 희귀 전용)
    [Serializable]
    public sealed class GachaRarityRate
    {
        [SerializeField] private MonsterRarity rarity;
        [Header("뽑기 확률")]
        [SerializeField, Range(0f, 100f)] private float dropRatePercent;
        [Header("뽑기 천장")]
        [SerializeField] private int ceilingPulls;
        [Header("10뽑마다 희귀 등장")]
        [SerializeField] private int rareGuaranteeInterval;

        // 인스펙터 리스트의 "+" 버튼(빈 항목 생성)이 리플렉션으로 이 기본 생성자를 호출한다.
        public GachaRarityRate()
        {
        }

        public GachaRarityRate(MonsterRarity rarity, float dropRatePercent, int ceilingPulls, int rareGuaranteeInterval)
        {
            this.rarity = rarity;
            this.dropRatePercent = dropRatePercent;
            this.ceilingPulls = ceilingPulls;
            this.rareGuaranteeInterval = rareGuaranteeInterval;
        }

        public MonsterRarity Rarity => rarity;
        public float DropRatePercent => dropRatePercent;
        public int CeilingPulls => ceilingPulls;
        public int RareGuaranteeInterval => rareGuaranteeInterval;
    }

    // 뽑기 결과 확률 판정에 필요한 현재 천장 카운터 (GameProgressData.GachaPity에서 읽어온 값).
    public readonly struct GachaPityState
    {
        public GachaPityState(
            int pullsSinceRareOrBetter,
            int pullsSinceEpicOrBetter,
            int pullsSinceLegendaryOrBetter,
            int pullsSinceMythicOrBetter)
        {
            PullsSinceRareOrBetter = pullsSinceRareOrBetter;
            PullsSinceEpicOrBetter = pullsSinceEpicOrBetter;
            PullsSinceLegendaryOrBetter = pullsSinceLegendaryOrBetter;
            PullsSinceMythicOrBetter = pullsSinceMythicOrBetter;
        }

        public int PullsSinceRareOrBetter { get; }
        public int PullsSinceEpicOrBetter { get; }
        public int PullsSinceLegendaryOrBetter { get; }
        public int PullsSinceMythicOrBetter { get; }
    }

    // 등급별 뽑기 확률과 천장(구간 확정) 규칙을 담는 설정 에셋.
    // 실제 천장 카운터 저장은 GameProgressData가 담당하고, 여긴 확률 계산 규칙만 가진다.
    [CreateAssetMenu(menuName = "ProjectMT/Gacha/Gacha Probability", fileName = "GachaProbability")]
    public sealed class GachaProbability : ScriptableObject
    {
        [SerializeField] private List<GachaRarityRate> rarityRates = new List<GachaRarityRate>();

        public IReadOnlyList<GachaRarityRate> RarityRates => rarityRates;

        // 기획서 기본값 (일반40/고급28/희귀20/영웅8/전설3/신화1, 희귀 10뽑 확정, 영웅30/전설100/신화300 천장).
        // 에셋을 새로 만들 때 자동으로 채워지고, 인스펙터에서 값만 조정하면 된다.
        private void Reset()
        {
            rarityRates = new List<GachaRarityRate>
            {
                new GachaRarityRate(MonsterRarity.Common, 40f, ceilingPulls: 0, rareGuaranteeInterval: 0),
                new GachaRarityRate(MonsterRarity.Uncommon, 28f, ceilingPulls: 0, rareGuaranteeInterval: 0),
                new GachaRarityRate(MonsterRarity.Rare, 20f, ceilingPulls: 0, rareGuaranteeInterval: 10),
                new GachaRarityRate(MonsterRarity.Epic, 8f, ceilingPulls: 30, rareGuaranteeInterval: 0),
                new GachaRarityRate(MonsterRarity.Legendary, 3f, ceilingPulls: 100, rareGuaranteeInterval: 0),
                new GachaRarityRate(MonsterRarity.Mythic, 1f, ceilingPulls: 300, rareGuaranteeInterval: 0),
            };
        }

        // 이번 뽑기의 등급을 정한다. 천장(영웅→전설→신화, 급한 순)을 먼저 확인하고,
        // 아무 천장도 안 걸리면 가중치 랜덤으로 뽑은 뒤 희귀 확정 보정을 적용한다.
        public MonsterRarity Roll(GachaPityState pity)
        {
            if (rarityRates == null || rarityRates.Count == 0)
            {
                return MonsterRarity.Common; // 설정이 비어 있을 때의 안전한 기본값
            }

            if (IsCeilingHit(MonsterRarity.Mythic, pity.PullsSinceMythicOrBetter))
            {
                return MonsterRarity.Mythic;
            }

            if (IsCeilingHit(MonsterRarity.Legendary, pity.PullsSinceLegendaryOrBetter))
            {
                return MonsterRarity.Legendary;
            }

            if (IsCeilingHit(MonsterRarity.Epic, pity.PullsSinceEpicOrBetter))
            {
                return MonsterRarity.Epic;
            }

            var rolled = RollWeighted();
            if (IsRareGuaranteeTriggered(pity.PullsSinceRareOrBetter) && rolled < MonsterRarity.Rare)
            {
                rolled = MonsterRarity.Rare;
            }

            return rolled;
        }

        private bool IsCeilingHit(MonsterRarity rarity, int pullsSinceThisOrBetter)
        {
            return TryGetRate(rarity, out var rate) &&
                   rate.CeilingPulls > 0 &&
                   pullsSinceThisOrBetter + 1 >= rate.CeilingPulls; // 이번 뽑기가 N번째 뽑기인지 확인
        }

        private bool IsRareGuaranteeTriggered(int pullsSinceRareOrBetter)
        {
            return TryGetRate(MonsterRarity.Rare, out var rate) &&
                   rate.RareGuaranteeInterval > 0 &&
                   pullsSinceRareOrBetter + 1 >= rate.RareGuaranteeInterval;
        }

        private MonsterRarity RollWeighted()
        {
            var totalWeight = 0f;
            for (var index = 0; index < rarityRates.Count; index++)
            {
                totalWeight += Mathf.Max(0f, rarityRates[index].DropRatePercent);
            }

            if (totalWeight <= 0f)
            {
                return MonsterRarity.Common;
            }

            var roll = UnityEngine.Random.value * totalWeight;
            var cumulative = 0f;
            for (var index = 0; index < rarityRates.Count; index++)
            {
                cumulative += Mathf.Max(0f, rarityRates[index].DropRatePercent);
                if (roll <= cumulative)
                {
                    return rarityRates[index].Rarity;
                }
            }

            return rarityRates[rarityRates.Count - 1].Rarity;
        }

        private bool TryGetRate(MonsterRarity rarity, out GachaRarityRate rate)
        {
            for (var index = 0; index < rarityRates.Count; index++)
            {
                if (rarityRates[index].Rarity == rarity)
                {
                    rate = rarityRates[index];
                    return true;
                }
            }

            rate = null;
            return false;
        }
    }
}
