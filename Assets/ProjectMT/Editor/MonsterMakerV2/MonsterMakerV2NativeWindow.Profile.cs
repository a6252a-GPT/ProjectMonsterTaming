using ProjectMT.Contents.CastleRaidHex;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Unit;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    public sealed partial class MonsterMakerV2Window
    {
        private static readonly string[] ProfileRarityClasses =
        {
            "rarity-common-bg",
            "rarity-rare-bg",
            "rarity-epic-bg",
            "rarity-legendary-bg",
            "rarity-mythic-bg"
        };

        private void UpdateProfileSummary()
        {
            var current = state?.WorkingDraft;
            if (current == null || profileName == null)
            {
                return;
            }

            profilePortrait.sprite = current.Portrait;
            profilePortrait.style.display =
                current.Portrait == null ? DisplayStyle.None : DisplayStyle.Flex;
            profileName.text = string.IsNullOrWhiteSpace(current.DisplayName)
                ? "이름 미지정"
                : current.DisplayName;
            profileId.text = string.IsNullOrWhiteSpace(current.MonsterId)
                ? "ID 미입력"
                : current.MonsterId;
            profileRarity.text = GetRarityLabel(current.Rarity);
            for (var index = 0; index < ProfileRarityClasses.Length; index++)
            {
                profileRarity.RemoveFromClassList(ProfileRarityClasses[index]);
            }
            profileRarity.AddToClassList(GetProfileRarityClass(current.Rarity));

            profileType.text =
                $"{GetCombatTypeLabel(current.CombatType)} · " +
                BuildSkillUsageSummary(current);
            profileHealth.text = $"체력 {current.MaxHealth:0.##}";
            profileAttack.text = $"공격 {current.AttackPower:0.##}";
            profileDefense.text = $"방어 {current.Defense:0.##}";
            profileSpeed.text = $"공속 {current.AttackSpeed:0.##}";
            profileMove.text = $"이속 {current.MoveSpeed:0.##}";
            profileRange.text = $"사거리 {current.AttackRange:0.##}";
            profileBasicAttack.text = "기본공격 · " +
                (current.BasicAttackProfile == null
                    ? "미지정"
                    : current.BasicAttackProfile.DisplayName);
            profileImpact.text =
                $"타격/피격 · {GetImpactLabel(current.ImpactStrength)} / " +
                GetReactionLabel(current.ReactionWeight);
            profileSkill.text = "스킬 · " + BuildSkillSummary(current);
            profileMainAi.text =
                $"메인 전투 · {GetRoleLabel(current.MainBattleRole)} · " +
                GetTargetLabel(current.MainBattleTargetPriority);
            profileDistance.text =
                $"전투 거리 · 희망 {current.MainBattlePreferredRangeRatio:0.##} · " +
                $"후퇴 {current.MainBattleRetreatRangeRatio:0.##} · " +
                $"재탐색 {current.MainBattleRetargetInterval:0.##}초";
            profileCastleAi.text =
                $"군단 역습 · {GetCastlePatternLabel(current.CastleRaidAiPattern)}";

            previewState.text = current.VendorPrefab == null
                ? "모델 미지정"
                : string.IsNullOrWhiteSpace(preview?.CurrentClipName)
                    ? $"{current.VendorPrefab.name} · 모션 대기"
                    : $"{current.VendorPrefab.name} · {preview.CurrentClipName}";
        }

        private static string BuildSkillUsageSummary(
            ProjectMT.EditorTools.MonsterMaker.MonsterMakerDraft current)
        {
            if (current.UsePassiveSkill && current.UseActiveSkill)
            {
                return "패시브·액티브 사용";
            }
            if (current.UsePassiveSkill)
            {
                return "패시브 사용";
            }
            if (current.UseActiveSkill)
            {
                return "액티브 사용";
            }
            return "스킬 미사용";
        }

        private static string BuildSkillSummary(
            ProjectMT.EditorTools.MonsterMaker.MonsterMakerDraft current)
        {
            var passive = current.UsePassiveSkill
                ? current.RarityPassiveSkill == null
                    ? "패시브 미지정"
                    : current.RarityPassiveSkill.DisplayName
                : "패시브 미사용";
            if (current.Rarity < MonsterRarity.Legendary)
            {
                return passive;
            }

            var active = current.UseActiveSkill
                ? current.RarityActiveSkill == null
                    ? current.ActiveAttackProfile != null
                        ? current.ActiveAttackProfile.DisplayName
                        : current.ActiveEffectProfile != null
                            ? current.ActiveEffectProfile.DisplayName
                            : "액티브 미지정"
                    : current.RarityActiveSkill.DisplayName
                : "액티브 미사용";
            return $"{passive} / {active}";
        }
        private static string GetProfileRarityClass(MonsterRarity rarity)
        {
            return rarity switch
            {
                MonsterRarity.Rare => "rarity-rare-bg",
                MonsterRarity.Epic => "rarity-epic-bg",
                MonsterRarity.Legendary => "rarity-legendary-bg",
                MonsterRarity.Mythic => "rarity-mythic-bg",
                _ => "rarity-common-bg"
            };
        }

        private static string GetCombatTypeLabel(MonsterCombatType type)
        {
            return type switch
            {
                MonsterCombatType.Ranged => "원거리",
                MonsterCombatType.Special => "특수",
                _ => "근거리"
            };
        }

        private static string GetImpactLabel(MonsterImpactStrength value)
        {
            return value switch
            {
                MonsterImpactStrength.Light => "가벼운 타격",
                MonsterImpactStrength.Heavy => "강한 타격",
                _ => "표준 타격"
            };
        }

        private static string GetReactionLabel(MonsterReactionWeight value)
        {
            return value switch
            {
                MonsterReactionWeight.Light => "가벼운 체급",
                MonsterReactionWeight.Heavy => "무거운 체급",
                _ => "표준 체급"
            };
        }

        private static string GetRoleLabel(MainBattleMonsterRole role)
        {
            return role switch
            {
                MainBattleMonsterRole.Guardian => "수호",
                MainBattleMonsterRole.Finisher => "마무리",
                MainBattleMonsterRole.Marksman => "사수",
                MainBattleMonsterRole.BacklineHunter => "후열 추적",
                _ => "선봉"
            };
        }

        private static string GetTargetLabel(UnitTargetPriority priority)
        {
            return priority switch
            {
                UnitTargetPriority.LowestHealth => "체력 낮은 적",
                UnitTargetPriority.RangedFirst => "원거리 우선",
                _ => "가까운 적"
            };
        }

        private static string GetCastlePatternLabel(HexCastleAssaultPattern pattern)
        {
            return pattern switch
            {
                HexCastleAssaultPattern.ResourceRaider => "자원 약탈",
                HexCastleAssaultPattern.TurretHunter => "포탑 사냥",
                HexCastleAssaultPattern.DefenderHunter => "수비대 사냥",
                HexCastleAssaultPattern.WallBreaker => "성벽 파괴",
                HexCastleAssaultPattern.ThreatSuppressor => "위협 억제",
                HexCastleAssaultPattern.TacticalSupport => "전술 지원",
                _ => "일반 전진"
            };
        }
    }
}
