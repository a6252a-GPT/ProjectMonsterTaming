using NUnit.Framework;
using ProjectMT.Contents.CastleRaid;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;

namespace ProjectMT.Tests.EditMode
{
    public sealed class CastleRaidDeploymentContractTests
    {
        [Test]
        public void StartData_UsesTenFormationSlotsAndThreeSummonsPerMonster()
        {
            var roster = MonsterRosterData.CreateDefault();
            var units = new BattleUnitSnapshot[MonsterRosterData.MainPartySlotCount];
            for (var index = 0; index < units.Length; index++)
            {
                units[index] = new BattleUnitSnapshot($"unit_{index + 1}", default);
            }

            var startData = new CastleRaidStartData(new BattlePartySnapshot(units), 3);
            var rosterSlots = typeof(MonsterRosterData)
                .GetField("mainPartySlots", System.Reflection.BindingFlags.Instance |
                                            System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(roster) as string[];

            Assert.That(MonsterRosterData.MainPartySlotCount, Is.EqualTo(10));
            Assert.That(rosterSlots, Has.Length.EqualTo(10));
            Assert.That(startData.UnitSlotCount, Is.EqualTo(10));
            Assert.That(startData.SummonsPerSlot, Is.EqualTo(3));
            Assert.That(startData.DeploymentLimit, Is.EqualTo(30));
        }
    }
}
