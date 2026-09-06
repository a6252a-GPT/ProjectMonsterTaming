using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProjectMT.Shared.GameData;

namespace ProjectMT.Shared.CommanderSkill
{
    public enum CommanderSkillSummonResultKind { New, Duplicate, Converted }

    public readonly struct CommanderSkillSummonResult
    {
        public CommanderSkillSummonResult(string id, CommanderSkillSummonResultKind kind, long convertedUpgradeStones)
        { SkillId = id; Kind = kind; ConvertedUpgradeStones = convertedUpgradeStones; }
        public string SkillId { get; }
        public CommanderSkillSummonResultKind Kind { get; }
        public long ConvertedUpgradeStones { get; }
    }

    public sealed class CommanderSkillSummonReceipt
    {
        internal CommanderSkillSummonReceipt(IEnumerable<CommanderSkillSummonResult> results)
            => Results = System.Array.AsReadOnly(results.ToArray());
        public IReadOnlyList<CommanderSkillSummonResult> Results { get; }
        public long ConvertedUpgradeStones
        {
            get
            {
                var total = 0L;
                foreach (var result in Results) total = checked(total + result.ConvertedUpgradeStones);
                return total;
            }
        }
    }

    public interface ICommanderSkillSummonService
    {
        Task<CommanderSkillSummonReceipt> TrySummonCommanderSkillsAsync(GameProgressChange change);
    }
}
