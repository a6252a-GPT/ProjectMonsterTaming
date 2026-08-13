using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectMT.Shared.GameData;

namespace ProjectMT.Features.OfflineReward
{
    public interface IUtcClock // 방치 계산을 기기 UTC와 테스트 시간에서 공용 사용
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemUtcClock : IUtcClock
    {
        public static readonly SystemUtcClock Instance = new SystemUtcClock();

        private SystemUtcClock()
        {
        }

        public DateTime UtcNow => DateTime.UtcNow;
    }

    public sealed class OfflineRewardCoordinator // 로그인·Pause·Resume 방치 정산 순서 관리
    {
        private readonly IGameProgressService progress;
        private readonly OfflineRewardConfig config;
        private readonly IUtcClock clock;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1); // Pause·Resume·확인 저장 직렬화
        private bool inactive; // 중복 Pause·활성 상태 Resume의 시작점 변경 차단

        public OfflineRewardCoordinator(
            IGameProgressService progressService,
            OfflineRewardConfig rewardConfig,
            IUtcClock utcClock = null)
        {
            progress = progressService ?? throw new ArgumentNullException(nameof(progressService));
            config = rewardConfig ?? throw new ArgumentNullException(nameof(rewardConfig));
            clock = utcClock ?? SystemUtcClock.Instance;
        }

        public Task<bool> PrepareOnLoginAsync()
        {
            return TrySettleCurrentIntervalAsync(false);
        }

        public Task<bool> ResumeAsync()
        {
            return TrySettleCurrentIntervalAsync(true);
        }

        public async Task<bool> MarkInactiveAsync()
        {
            await gate.WaitAsync();
            try
            {
                if (inactive)
                {
                    return true; // Pause·Quit 중복 콜백은 최초 시작점을 유지
                }

                if (!progress.IsLoaded)
                {
                    return false;
                }

                var view = progress.View;
                var saved = await progress.TryApplyAndSaveAsync(
                    GameProgressChange.MarkOfflineInactive(
                        view.OfflineRewards.LastActiveUtc,
                        clock.UtcNow,
                        Math.Max(1, view.LastClearedStage)));
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
                progress.View.OfflineRewards.PendingReceipts,
                out presentation);
        }

        private async Task<bool> TrySettleCurrentIntervalAsync(bool requireInactive)
        {
            await gate.WaitAsync();
            try
            {
                if (requireInactive && !inactive)
                {
                    return true; // 실제 Pause 없이 들어온 Resume는 활성 시간을 정산하지 않음
                }

                if (requireInactive)
                {
                    inactive = false; // Resume 이벤트는 한 번만 처리
                }

                if (!progress.IsLoaded || config == null || !config.TryValidate(out _))
                {
                    return false;
                }

                var view = progress.View;
                var offline = view.OfflineRewards;
                var nowUtc = clock.UtcNow.ToUniversalTime();
                if (!offline.HasLastActive ||
                    !OfflineRewardReceiptData.TryParseUtc(offline.LastActiveUtc, out var fromUtc) ||
                    nowUtc <= fromUtc)
                {
                    return await progress.TryApplyAndSaveAsync(
                        GameProgressChange.MarkOfflineInactive(
                            offline.LastActiveUtc,
                            nowUtc,
                            Math.Max(1, view.LastClearedStage)));
                }

                if (!OfflineRewardCalculator.TryCalculate(
                        fromUtc,
                        nowUtc,
                        offline.LastActiveStage,
                        Guid.NewGuid().ToString("N"),
                        config,
                        out var calculation))
                {
                    return true; // 최소 인정시간 전에는 기존 시작점을 유지
                }

                return await progress.TryApplyAndSaveAsync(
                    GameProgressChange.SettleOfflineReward(
                        offline.LastActiveUtc,
                        nowUtc,
                        Math.Max(1, view.LastClearedStage),
                        calculation.Receipt,
                        calculation.Rewards));
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
