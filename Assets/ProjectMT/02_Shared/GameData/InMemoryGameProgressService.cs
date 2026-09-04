using System;
using System.Threading.Tasks;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Quest;

namespace ProjectMT.Shared.GameData
{
    public sealed class InMemoryGameProgressService : IGameProgressService // DEV 단독 실행용 비저장 진행 데이터
    {
        private GameProgressData current = GameProgressData.CreateDefault();
        private readonly ItemCatalog itemCatalog;

        public InMemoryGameProgressService(ItemCatalog catalog = null)
        {
            itemCatalog = catalog;
        }

        public GameProgressView View => new GameProgressView(current);
        public QuestProgressView Quests => current.Quests;
        public bool IsLoaded => true;
        public event Action Changed;

        public Task<bool> TryApplyAndSaveAsync(GameProgressChange change)
        {
            var applied = current.TryApply(change, itemCatalog: itemCatalog);
            if (applied && !change.SuppressChangedNotification)
            {
                Changed?.Invoke();
            }

            return Task.FromResult(applied);
        }

        public Task SaveCurrentAsync()
        {
            return Task.CompletedTask;
        }
    }
}
