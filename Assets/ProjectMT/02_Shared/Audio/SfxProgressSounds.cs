using ProjectMT.Shared.GameData;
using UnityEngine;
namespace ProjectMT.Shared.Audio
{
    public static class SfxProgressSounds // 저장 결과와 소리 선택의 좁은 연결점
    {
        public static string Resolve(GameProgressChange c)
        {
            if (c == null) return null;
            if (c.HasEquipItem || c.HasEquipItems) return SfxEvents.Equip;
            if (c.HasUnequipItem) return SfxEvents.Unequip;
            if (c.HasUpgradeEquipmentSlot) return SfxEvents.Upgrade;
            if (c.HasDismantleEquipment) return SfxEvents.Dismantle;
            if (c.HasSetEquipmentLock) return SfxEvents.EquipmentLock;
            if (c.HasLevelUpMonster) return SfxEvents.MonsterLevel;
            if (c.HasAscendMonster) return SfxEvents.MonsterAscend;
            if (c.HasLevelUpCommander) return SfxEvents.CommanderLevel;
            if (c.HasUpgradeCommanderLegionStat) return SfxEvents.LegionUpgrade;
            if (c.HasAssignCommanderPotentialSlot || c.HasRerollCommanderPotentialSlots || c.HasRerollCommanderPotentialValues) return SfxEvents.Potential;
            if (c.HasSetCommanderPotentialLocked) return SfxEvents.PotentialLock;
            if (c.HasEquipCommanderSkill) return SfxEvents.SkillEquip;
            if (c.HasLevelUpCommanderSkill) return SfxEvents.SkillLevel;
            if (c.HasRecordCommanderSkillSummon) return SfxEvents.SkillSummon;
            if (c.HasFormationChange) return SfxEvents.Formation;
            if (c.HasMainBattleFormation) return SfxEvents.Placement;
            if (c.HasAttendanceClaim) return SfxEvents.Attendance;
            if (c.HasClaimMail) return SfxEvents.MailClaim;
            if (c.HasClaimQuestReward || c.HasClaimQuestRewards || c.HasClaimRepeatingQuestReward) return SfxEvents.QuestClaim;
            if (c.HasClaimMonsterCollectionFiveStarReward) return SfxEvents.CollectionClaim;
            if (c.HasAcknowledgeOfflineRewards) return SfxEvents.OfflineClaim;
            if (c.HasUseItem) return SfxEvents.ItemUse;
            if (c.HasDiscardItem) return SfxEvents.ItemDiscard;
            if (c.HasAcquireEquipment && c.AcquireEquipmentInstances != null && c.AcquireEquipmentInstances.Count > 0) return SfxEvents.DropCollect;
            if (QuestCompleted(c)) return SfxEvents.QuestComplete;
            return null;
        }
        public static void Notify(GameProgressChange change, bool saved)
        {
            try
            {
                var id = Resolve(change);
                if (id != null) SfxEvents.Play2D(saved ? id : SfxEvents.Rejected);
            }
            catch (System.Exception exception) { Debug.LogException(exception); } // 소리 실패로 저장 성공을 뒤집지 않음
        }
        private static bool QuestCompleted(GameProgressChange c)
        {
            if (c.HasSetQuestProgress && c.ExpectedQuestProgress < c.QuestProgressTargetValue &&
                c.NewQuestProgress >= c.QuestProgressTargetValue) return true;
            if (c.HasSetQuestProgressBatch && c.QuestProgressUpdates != null)
                foreach (var update in c.QuestProgressUpdates)
                    if (update.ExpectedProgress < update.TargetValue && update.NewProgress >= update.TargetValue) return true;
            return false;
        }
    }
}
