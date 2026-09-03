using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectMT.Features.Equipment;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;

namespace ProjectMT.Features.OfflineReward
{
    public interface IUtcClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemUtcClock : IUtcClock
    {
        public static readonly SystemUtcClock Instance = new SystemUtcClock();
        private SystemUtcClock() { }
        public DateTime UtcNow => DateTime.UtcNow;
    }

    public sealed class OfflineRewardCoordinator
    {
        private readonly IGameProgressService progress;
        private readonly OfflineRewardConfig config;
        private readonly EquipmentBalanceConfig equipmentBalance;
        private readonly IUtcClock clock;
        private readonly Random rewardRandom;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private bool inactive;
        private PendingOfflineSettlement pending;

        public OfflineRewardCoordinator(
            IGameProgressService progressService,
            OfflineRewardConfig rewardConfig,
            IUtcClock utcClock = null,
            EquipmentBalanceConfig equipmentBalanceConfig = null,
            Random random = null)
        {
            progress = progressService ?? throw new ArgumentNullException(nameof(progressService));
            config = rewardConfig ?? throw new ArgumentNullException(nameof(rewardConfig));
            equipmentBalance = equipmentBalanceConfig ?? EquipmentBalanceConfig.RuntimeDefault;
            clock = utcClock ?? SystemUtcClock.Instance;
            rewardRandom = random;
        }

        public OfflineRewardCalculationStatus LastStatus { get; private set; } =
            OfflineRewardCalculationStatus.NotDue;
        public bool HasPendingSettlement => pending != null;

        public Task<bool> PrepareOnLoginAsync() => TrySettleCurrentIntervalAsync(false);
        public Task<bool> ResumeAsync() => TrySettleCurrentIntervalAsync(true);
        public Task<bool> RetryPendingAsync() => pending == null
            ? Task.FromResult(true)
            : TrySettleCurrentIntervalAsync(false);

        public async Task<bool> MarkInactiveAsync()
        {
            await gate.WaitAsync();
            try
            {
                if (inactive)
                {
                    return true;
                }

                if (!progress.IsLoaded)
                {
                    return false;
                }

                if (pending != null)
                {
                    inactive = true;
                    return true; // 보류 중인 추첨의 시간 경계를 덮어쓰지 않는다.
                }

                var view = progress.View;
                var saved = await progress.TryApplyAndSaveAsync(
                    GameProgressChange.MarkOfflineInactive(
                        view.OfflineRewards.LastActiveUtc,
                        clock.UtcNow,
                        ExpeditionEquipmentLevelResolver.ResolveHighestClearedStage(view)));
                inactive = saved;
                return saved;
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task<bool> AcknowledgeAsync(IReadOnlyList<string> receiptIds)
        {
            await gate.WaitAsync();
            try
            {
                return await progress.TryApplyAndSaveAsync(
                    GameProgressChange.AcknowledgeOfflineRewards(receiptIds));
            }
            finally
            {
                gate.Release();
            }
        }

        public bool TryGetPendingPresentation(out OfflineRewardPresentation presentation)
        {
            return OfflineRewardPresentation.TryCreate(
                progress.View.OfflineRewards.PendingReceipts, out presentation);
        }

        private async Task<bool> TrySettleCurrentIntervalAsync(bool requireInactive)
        {
            await gate.WaitAsync();
            try
            {
                if (requireInactive && !inactive && pending == null)
                {
                    return true;
                }

                if (!progress.IsLoaded || !config.TryValidate(out _) || !equipmentBalance.TryValidate(out _))
                {
                    LastStatus = OfflineRewardCalculationStatus.InvalidData;
                    return false;
                }

                var nowUtc = clock.UtcNow.ToUniversalTime();
                while (true)
                {
                    var view = progress.View;
                    var offline = view.OfflineRewards;
                    if (pending != null && pending.ExpectedLastActiveUtc != offline.LastActiveUtc)
                    {
                        pending = null; // 외부 초기화 또는 이미 확정된 구간은 다시 지급하지 않는다.
                    }

                    if (pending == null)
                    {
                        if (!offline.HasLastActive ||
                            !OfflineRewardReceiptData.TryParseUtc(offline.LastActiveUtc, out var fromUtc) ||
                            nowUtc <= fromUtc)
                        {
                            LastStatus = OfflineRewardCalculationStatus.NotDue;
                            var marked = await progress.TryApplyAndSaveAsync(
                                GameProgressChange.MarkOfflineInactive(
                                    offline.LastActiveUtc,
                                    nowUtc,
                                    ExpeditionEquipmentLevelResolver.ResolveHighestClearedStage(view)));
                            if (marked) inactive = false;
                            return marked;
                        }

                        LastStatus = OfflineRewardCalculator.TryRoll(
                            fromUtc, nowUtc, offline.LastActiveStage, Guid.NewGuid().ToString("N"),
                            config, equipmentBalance, rewardRandom, out var snapshot);
                        if (LastStatus == OfflineRewardCalculationStatus.NotDue)
                        {
                            inactive = false;
                            return true;
                        }

                        if (LastStatus != OfflineRewardCalculationStatus.Ready)
                        {
                            return false;
                        }

                        pending = new PendingOfflineSettlement(
                            offline.LastActiveUtc, nowUtc,
                            ExpeditionEquipmentLevelResolver.ResolveHighestClearedStage(view), snapshot);
                    }

                    LastStatus = OfflineRewardCalculator.TryPlan(
                        pending.Snapshot, view, equipmentBalance, out var calculation);
                    if (LastStatus == OfflineRewardCalculationStatus.InventoryBlocked)
                    {
                        inactive = false;
                        return true; // 진입은 허용하고 인벤토리 정리 후 같은 추첨으로 재계획한다.
                    }

                    if (LastStatus != OfflineRewardCalculationStatus.Ready)
                    {
                        return false;
                    }

                    var currentPlan = pending;
                    var saved = await progress.TryApplyAndSaveAsync(
                        GameProgressChange.SettleOfflineReward(
                            currentPlan.ExpectedLastActiveUtc,
                            currentPlan.ToUtc,
                            currentPlan.NextBasisStage,
                            calculation.Receipt,
                            calculation.Rewards));
                    if (!saved)
                    {
                        return false; // 다음 호출에서도 ID·레벨·옵션·재화 추첨을 그대로 유지한다.
                    }

                    pending = null;
                    inactive = false;
                    if (currentPlan.ToUtc >= nowUtc)
                    {
                        return true;
                    }
                    // 이전 실패 구간 저장이 끝난 뒤 현재 시각까지의 다음 구간을 따로 계산한다.
                }
            }
            finally
            {
                gate.Release();
            }
        }

        private sealed class PendingOfflineSettlement
        {
            public PendingOfflineSettlement(
                string expectedLastActiveUtc,
                DateTime toUtc,
                int nextBasisStage,
                OfflineRewardRollSnapshot snapshot)
            {
                ExpectedLastActiveUtc = expectedLastActiveUtc;
                ToUtc = toUtc;
                NextBasisStage = nextBasisStage;
                Snapshot = snapshot;
            }

            public string ExpectedLastActiveUtc { get; }
            public DateTime ToUtc { get; }
            public int NextBasisStage { get; }
            public OfflineRewardRollSnapshot Snapshot { get; }
        }
    }
}
