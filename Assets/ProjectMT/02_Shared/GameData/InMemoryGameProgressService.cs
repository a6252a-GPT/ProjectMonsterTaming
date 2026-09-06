using System;
using System.Threading.Tasks;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Quest;

namespace ProjectMT.Shared.GameData
{
    public sealed class InMemoryGameProgressService : IGameProgressService, ICommanderSkillSummonService // DEV 단독 실행용 비저장 진행 데이터
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
            return Task.FromResult(TryApply(change, out _));
        }

        private bool TryApply(GameProgressChange change, out CommanderSkillSummonReceipt receipt)
        {
            var candidate = current.Clone();
            var applied = candidate.TryApply(change, itemCatalog: itemCatalog);
            if (applied) current = candidate;
            receipt = applied ? candidate.CommanderSkillSummonReceipt : null;
            ProjectMT.Shared.Audio.SfxProgressSounds.Notify(change, applied);
            if (applied && !change.SuppressChangedNotification)
            {
                Changed?.Invoke();
            }

            return applied;
        }

        public Task SaveCurrentAsync()
        {
            return Task.CompletedTask;
        }

        public Task<CommanderSkillSummonReceipt> TrySummonCommanderSkillsAsync(GameProgressChange change)
        {
            if (change == null || !change.HasRecordCommanderSkillSummon)
                return Task.FromResult<CommanderSkillSummonReceipt>(null);
            TryApply(change, out var receipt);
            return Task.FromResult(receipt);
        }
    }
}
