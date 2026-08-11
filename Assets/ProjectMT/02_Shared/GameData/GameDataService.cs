using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Stats;

namespace ProjectMT.Shared.GameData
{
    public interface IGameProgressService // 진행 조회·변경·저장 계약
    {
        GameProgressView View { get; }
        bool IsLoaded { get; }
        event Action Changed;
        Task<bool> TryApplyAndSaveAsync(GameProgressChange change);
        Task SaveCurrentAsync();
    }

    public sealed class GameDataService : IGameProgressService // 사용자 진행 단일 관리자
    {
        private readonly SaveService saveService; // 저장 직렬화 담당
        private readonly CommanderGrowthConfig commanderGrowthConfig; // 군단장 경험치 곡선
        private readonly ItemCatalog itemCatalog; // 일반 아이템 정의 등록부
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1); // 동시 변경 직렬화
        private GameProgressData current = GameProgressData.CreateDefault(); // 현재 확정 데이터

        public GameDataService(
            SaveService saveService,
            CommanderGrowthConfig growthConfig = null,
            ItemCatalog catalog = null)
        {
            this.saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            commanderGrowthConfig = growthConfig ?? CommanderGrowthConfig.RuntimeDefault;
            itemCatalog = catalog;
        }

        public GameProgressView View => new GameProgressView(current); // 외부에는 읽기 전용 값만 제공
        public bool IsLoaded { get; private set; }
        public event Action Changed;

        public async Task LoadAsync()
        {
            await gate.WaitAsync();
            try
            {
                current = await saveService.LoadAsync();
                current.Repair(commanderGrowthConfig); // 손상 가능한 범위값 보정
                IsLoaded = true;
            }
            finally
            {
                gate.Release();
            }

            Changed?.Invoke();
        }

        public async Task<bool> TryApplyAndSaveAsync(GameProgressChange change)
        {
            await gate.WaitAsync();
            try
            {
                if (!IsLoaded)
                {
                    return false;
                }

                var candidate = current.Clone(); // 원본 보존 후 변경 검증
                if (!candidate.TryApply(change, commanderGrowthConfig, itemCatalog))
                {
                    return false;
                }

                await saveService.SaveAsync(candidate); // 저장 성공을 먼저 확인
                current = candidate; // 성공한 후보만 확정
            }
            finally
            {
                gate.Release();
            }

            Changed?.Invoke();
            return true;
        }

        public async Task SaveCurrentAsync()
        {
            await gate.WaitAsync();
            try
            {
                if (IsLoaded)
                {
                    await saveService.SaveAsync(current);
                }
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task ResetToDefaultAsync() // 디버그 초기화도 저장 성공 뒤 확정
        {
            await gate.WaitAsync();
            try
            {
                if (!IsLoaded)
                {
                    throw new InvalidOperationException("Game data must be loaded before reset.");
                }

                var reset = GameProgressData.CreateDefault();
                reset.Repair(commanderGrowthConfig);
                await saveService.SaveAsync(reset); // 파일 초기화를 먼저 확정
                current = reset; // 저장 성공한 기본값만 메모리에 반영
            }
            finally
            {
                gate.Release();
            }

            Changed?.Invoke();
        }
    }
}
