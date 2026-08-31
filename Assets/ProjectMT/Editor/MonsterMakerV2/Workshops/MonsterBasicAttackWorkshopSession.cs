using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    // EditorWindow 수명과 분리된 기본공격 제작 세션. V1 창 없이도 V2의 전체 저장 흐름을 소유한다.
    internal sealed class MonsterBasicAttackWorkshopSession : IDisposable
    {
        private const HideFlags EditableWorkCopyFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        private readonly List<MonsterBasicAttackProfile> profiles = new List<MonsterBasicAttackProfile>();
        private MonsterBasicAttackProfile workingProfile;
        private MonsterBasicAttackProfile loadedProfile;
        private MonsterMakerDraft originDraft;
        private BasicAttackWorkshopRecipe recipe;

        internal BasicAttackWorkshopRecipe Recipe => recipe;
        internal MonsterBasicAttackProfile WorkingProfile => workingProfile;
        internal MonsterBasicAttackProfile LoadedProfile => loadedProfile;
        internal bool IsDirty { get; private set; }
        internal string Message { get; private set; } = string.Empty;

        internal void Initialize(
            MonsterMakerDraft draft,
            MonsterBasicAttackProfile target,
            string recoveryJson = null,
            bool preserveDirty = false)
        {
            originDraft = draft;
            RefreshProfiles();
            if (target == null) StartBlank();
            else Load(target);

            if (string.IsNullOrWhiteSpace(recoveryJson)) return;
            recipe ??= new BasicAttackWorkshopRecipe();
            JsonUtility.FromJsonOverwrite(recoveryJson, recipe);
            recipe.Normalize();
            CompileWorkingProfile();
            IsDirty = preserveDirty;
            Message = preserveDirty
                ? "이전 Unity 세션의 미저장 기본공격 작업을 복구했습니다."
                : Message;
        }

        internal string CaptureJson() => recipe == null ? string.Empty : JsonUtility.ToJson(recipe);
        internal void SetOriginDraft(MonsterMakerDraft draft) => originDraft = draft;

        internal IReadOnlyList<MonsterBasicAttackProfile> FindProfiles()
        {
            RefreshProfiles();
            return profiles.Where(item => item != null)
                .OrderBy(item => item.AttackId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal int UsageCount(MonsterBasicAttackProfile target) =>
            target == null ? 0 : MonsterBasicAttackPresetUtility.CountDraftUsages(target);

        internal void StartBlank()
        {
            recipe ??= new BasicAttackWorkshopRecipe();
            recipe.ResetBlank();
            RefreshProfiles();
            recipe.attackId = FindNextPresetId(recipe.family);
            loadedProfile = null;
            IsDirty = false;
            Message = "빈 근거리 단일 공격에서 시작했습니다.";
            CompileWorkingProfile();
            recipe.vfxSlots = MonsterBasicAttackVfxContractTemplates.Build(workingProfile)
                .Select(BasicAttackWorkshopVfxSlot.From)
                .ToList();
            CompileWorkingProfile();
        }

        internal void Load(MonsterBasicAttackProfile target)
        {
            if (target == null) return;
            recipe ??= new BasicAttackWorkshopRecipe();
            recipe.Load(target);
            loadedProfile = target;
            IsDirty = false;
            Message = $"작업 사본으로 불러왔습니다: [{target.AttackId}] {target.DisplayName}";
            CompileWorkingProfile();
        }

        internal void Fork()
        {
            if (loadedProfile == null || recipe == null) return;
            loadedProfile = null;
            recipe.attackId = FindNextPresetId(recipe.family);
            IsDirty = true;
            Message = $"새 작업 사본으로 분기했습니다. 새 ID는 {recipe.attackId}입니다.";
            CompileWorkingProfile();
        }

        internal bool SaveAsNew(out MonsterBasicAttackProfile saved)
        {
            saved = null;
            CompileWorkingProfile();
            if (!ValidateIdentityAndRecipe(null, out var error))
            {
                SetError(error);
                return false;
            }

            EnsureCustomFolder();
            var fileName = SanitizeToken(recipe.attackId);
            var path = $"{MonsterBasicAttackPresetUtility.CustomProfileRoot}/{fileName}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                SetError($"같은 ID의 프리셋 자산이 이미 있습니다: {path}");
                return false;
            }

            var asset = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
            recipe.Compile(asset);
            asset.name = fileName;
            AssetDatabase.CreateAsset(asset, path);
            if (!MonsterBasicAttackPresetUtility.TrySaveRecipe(asset, out error))
            {
                AssetDatabase.DeleteAsset(path);
                UnityEngine.Object.DestroyImmediate(asset);
                SetError(error);
                return false;
            }

            loadedProfile = asset;
            saved = asset;
            IsDirty = false;
            Message = $"새 프리셋을 저장했습니다: {path}";
            RefreshProfiles();
            return true;
        }

        internal bool UpdateLoaded()
        {
            if (loadedProfile == null) return false;
            CompileWorkingProfile();
            if (!ValidateIdentityAndRecipe(loadedProfile, out var error))
            {
                SetError(error);
                return false;
            }

            var usageCount = MonsterBasicAttackPresetUtility.CountDraftUsages(loadedProfile);
            if (usageCount > 0 && !EditorUtility.DisplayDialog(
                    "공유 프리셋 업데이트",
                    $"이 프리셋을 {usageCount}마리가 사용 중입니다. 저장하면 모두에게 적용됩니다.",
                    "업데이트", "취소"))
                return false;

            Undo.RecordObject(loadedProfile, "기본공격 프리셋 업데이트");
            recipe.Compile(loadedProfile);
            if (!MonsterBasicAttackPresetUtility.TrySaveRecipe(loadedProfile, out error))
            {
                Undo.PerformUndo();
                SetError(error);
                return false;
            }

            IsDirty = false;
            Message = $"현재 프리셋을 저장했습니다: [{loadedProfile.AttackId}] {loadedProfile.DisplayName}";
            RefreshProfiles();
            return true;
        }

        internal bool AssignToOrigin()
        {
            if (originDraft == null || loadedProfile == null || IsDirty) return false;
            originDraft.EditorSetBasicAttackProfile(loadedProfile);
            originDraft.EditorAdoptBasicAttackProfileTuning();
            EditorUtility.SetDirty(originDraft);
            AssetDatabase.SaveAssetIfDirty(originDraft);
            MonsterBasicAttackPresetUtility.InvalidateUsageCache();
            Message = $"{originDraft.MonsterId}에게 [{loadedProfile.AttackId}]을 배정했습니다.";
            return true;
        }

        internal bool Validate(out string error)
        {
            CompileWorkingProfile();
            return ValidateIdentityAndRecipe(loadedProfile, out error);
        }

        internal void NotifyChanged(bool reconcileContracts)
        {
            recipe ??= new BasicAttackWorkshopRecipe();
            recipe.Normalize();
            CompileWorkingProfile();
            if (reconcileContracts)
            {
                recipe.vfxSlots = MonsterBasicAttackVfxContractTemplates.Reconcile(
                    workingProfile, recipe.vfxSlots, out var result);
                CompileWorkingProfile();
                Message = $"공격 방식에 맞춰 VFX 공간을 정리했습니다. 유지 {result.Retained} · 추가 {result.Added} · 제외 {result.Archived}";
            }
            IsDirty = true;
        }

        private void CompileWorkingProfile()
        {
            if (recipe == null) return;
            if (workingProfile == null)
            {
                workingProfile = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
                workingProfile.hideFlags = EditableWorkCopyFlags;
            }
            recipe.Compile(workingProfile);
        }

        private bool ValidateIdentityAndRecipe(MonsterBasicAttackProfile excluded, out string error)
        {
            RefreshProfiles();
            var id = recipe.attackId?.Trim();
            if (string.IsNullOrWhiteSpace(id) || id.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
            {
                error = "프리셋 ID는 영문·숫자·밑줄만 사용할 수 있습니다.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(recipe.displayName))
            {
                error = "표시 이름을 입력해야 합니다.";
                return false;
            }
            if (!id.StartsWith(recipe.RequiredIdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                error = $"현재 공격 계열의 프리셋 ID는 {recipe.RequiredIdPrefix}로 시작해야 합니다.";
                return false;
            }
            if (excluded != null && !string.Equals(id, excluded.AttackId, StringComparison.OrdinalIgnoreCase))
            {
                error = $"저장된 프리셋의 ID는 바꿀 수 없습니다. 새 ID가 필요하면 복제하세요: {excluded.AttackId}";
                return false;
            }
            if (excluded != null)
            {
                var assetName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(excluded));
                if (!string.Equals(assetName, excluded.AttackId, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"프리셋 ID와 파일명이 다릅니다: {assetName} / {excluded.AttackId}";
                    return false;
                }
            }
            if (!workingProfile.TryValidate(out error)) return false;

            var duplicate = profiles.FirstOrDefault(profile => profile != null && profile != excluded &&
                string.Equals(profile.AttackId, id, StringComparison.OrdinalIgnoreCase));
            if (duplicate == null) return true;
            error = $"같은 프리셋 ID가 이미 있습니다: {AssetDatabase.GetAssetPath(duplicate)}";
            return false;
        }

        private void RefreshProfiles()
        {
            profiles.Clear();
            if (!AssetDatabase.IsValidFolder(MonsterBasicAttackPresetUtility.ProfileRoot)) return;
            profiles.AddRange(AssetDatabase.FindAssets(
                    "t:MonsterBasicAttackProfile", new[] { MonsterBasicAttackPresetUtility.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>)
                .Where(profile => profile != null));
        }

        private string FindNextPresetId(BasicAttackWorkshopFamily family)
        {
            var prefix = family switch
            {
                BasicAttackWorkshopFamily.Ranged => "BA_R_",
                BasicAttackWorkshopFamily.Special => "BA_S_",
                _ => "BA_M_"
            };
            var maximum = 0;
            foreach (var profile in profiles)
            {
                if (profile == null || !profile.AttackId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (int.TryParse(profile.AttackId.Substring(prefix.Length), out var number)) maximum = Mathf.Max(maximum, number);
            }
            return $"{prefix}{maximum + 1:00}";
        }

        private static void EnsureCustomFolder()
        {
            if (!AssetDatabase.IsValidFolder(MonsterBasicAttackPresetUtility.CustomProfileRoot))
                AssetDatabase.CreateFolder(MonsterBasicAttackPresetUtility.ProfileRoot, "Custom");
        }

        private static string SanitizeToken(string value) => new string((value ?? string.Empty).Trim()
            .Where(character => char.IsLetterOrDigit(character) || character == '_').ToArray());

        private void SetError(string error) => Message = "오류: " + error;

        public void Dispose()
        {
            if (workingProfile != null) UnityEngine.Object.DestroyImmediate(workingProfile);
            workingProfile = null;
        }
    }
}
