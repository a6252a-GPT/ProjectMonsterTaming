using System;
using System.Collections.Generic;
using ProjectMT.Shared.Items;
using UnityEngine;

namespace ProjectMT.Shared.Gacha
{
    [CreateAssetMenu(menuName = "ProjectMT/Gacha/Gacha Cost Config", fileName = "GachaCostConfig")]
    public sealed class GachaCostConfig : ScriptableObject // 몬스터 소환권 우선 결제 규칙
    {
        public const int SingleDrawCount = 1;
        public const int TenDrawCount = 10;

        [SerializeField, Min(1L)] private long singleDrawDiamondCost = 300L;
        [SerializeField, Min(1L)] private long tenDrawDiamondCost = 2700L;
        [SerializeField, Min(1L)] private long mixedPaymentDiamondCostPerDraw = 270L;

        public long SingleDrawDiamondCost => Math.Max(1L, singleDrawDiamondCost);
        public long TenDrawDiamondCost => Math.Max(1L, tenDrawDiamondCost);
        public long MixedPaymentDiamondCostPerDraw => Math.Max(1L, mixedPaymentDiamondCostPerDraw);

        public GachaPaymentPlan CreatePaymentPlan(
            int drawCount,
            long availableTickets,
            long availableDiamonds)
        {
            if (drawCount != SingleDrawCount && drawCount != TenDrawCount)
            {
                return GachaPaymentPlan.Invalid(drawCount, availableTickets, availableDiamonds);
            }

            var normalizedTickets = Math.Max(0L, availableTickets);
            var ticketsUsed = (int)Math.Min(drawCount, normalizedTickets);
            long diamondCost;
            if (drawCount == SingleDrawCount)
            {
                diamondCost = ticketsUsed == SingleDrawCount ? 0L : SingleDrawDiamondCost;
            }
            else if (ticketsUsed == 0)
            {
                diamondCost = TenDrawDiamondCost;
            }
            else
            {
                diamondCost = (drawCount - ticketsUsed) * MixedPaymentDiamondCostPerDraw;
            }

            return new GachaPaymentPlan(
                drawCount,
                ticketsUsed,
                diamondCost,
                normalizedTickets,
                Math.Max(0L, availableDiamonds),
                true);
        }

        public bool TryValidate(out string error)
        {
            if (singleDrawDiamondCost <= 0L || tenDrawDiamondCost <= 0L ||
                mixedPaymentDiamondCostPerDraw <= 0L)
            {
                error = "Gacha cost settings must be positive.";
                return false;
            }

            error = null;
            return true;
        }
    }

    public readonly struct GachaPaymentPlan // 한 묶음의 확정 비용
    {
        internal GachaPaymentPlan(
            int drawCount,
            int ticketsUsed,
            long diamondCost,
            long availableTickets,
            long availableDiamonds,
            bool isValid)
        {
            DrawCount = drawCount;
            TicketsUsed = Math.Max(0, ticketsUsed);
            DiamondCost = Math.Max(0L, diamondCost);
            AvailableTickets = Math.Max(0L, availableTickets);
            AvailableDiamonds = Math.Max(0L, availableDiamonds);
            IsValid = isValid;
        }

        public int DrawCount { get; }
        public int TicketsUsed { get; }
        public long DiamondCost { get; }
        public long AvailableTickets { get; }
        public long AvailableDiamonds { get; }
        public bool IsValid { get; }
        public bool CanAfford => IsValid && AvailableDiamonds >= DiamondCost;

        public IReadOnlyList<ItemAmount> CreateItemCosts()
        {
            var costs = new List<ItemAmount>(2);
            if (TicketsUsed > 0)
            {
                costs.Add(new ItemAmount(ItemIds.MonsterSummonTicket, TicketsUsed));
            }

            if (DiamondCost > 0L)
            {
                costs.Add(new ItemAmount(ItemIds.Diamond, DiamondCost));
            }

            return costs;
        }

        internal static GachaPaymentPlan Invalid(
            int drawCount,
            long availableTickets,
            long availableDiamonds)
        {
            return new GachaPaymentPlan(
                drawCount,
                0,
                0L,
                availableTickets,
                availableDiamonds,
                false);
        }
    }
}
