using System;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public static class MonsterActiveAttackAuthoringService // GUI와 선택형 API가 공유하는 단일 프로필 저장 경계
    {
        public const string ProfileRoot = "Assets/ProjectMT/02_Shared/Unit/Data/ActiveAttackProfiles";
        public const string CustomProfileRoot = ProfileRoot + "/Custom";

        public static bool TryValidate(
            MonsterActiveAttackProfile source,
            MonsterActiveAttackProfile excludedProfile,
            out string error)
        {
            if (source == null)
            {
                error = "공격 액티브 작업 사본이 없습니다.";
                return false;
            }
            if (!source.TryValidate(out error)) return false;
            if (excludedProfile != null && !string.Equals(
                    source.ProfileId,
                    excludedProfile.ProfileId,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = $"저장된 프리셋의 ID는 바꿀 수 없습니다. 새 ID가 필요하면 새 프리셋으로 분기하세요: {excludedProfile.ProfileId}";
                return false;
            }

            var guids = AssetDatabase.IsValidFolder(ProfileRoot)
                ? AssetDatabase.FindAssets("t:MonsterActiveAttackProfile", new[] { ProfileRoot })
                : Array.Empty<string>();
            for (var index = 0; index < guids.Length; index++)
            {
                var candidate = AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(
                    AssetDatabase.GUIDToAssetPath(guids[index]));
                if (candidate == null || candidate == excludedProfile) continue;
                if (!string.Equals(candidate.ProfileId, source.ProfileId, StringComparison.OrdinalIgnoreCase)) continue;
                error = $"같은 프리셋 ID가 이미 있습니다: [{candidate.ProfileId}] {candidate.DisplayName}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryCreate(
            MonsterActiveAttackProfile source,
            out MonsterActiveAttackProfile createdAsset,
            out string assetPath,
            out string error)
        {
            createdAsset = null;
            assetPath = string.Empty;
            if (!TryValidate(source, null, out error)) return false;

            EnsureCustomFolder();
            assetPath = BuildCustomAssetPath(source.ProfileId);
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                error = $"같은 ID의 프리셋 자산이 이미 있습니다: {assetPath}";
                return false;
            }

            var asset = ScriptableObject.CreateInstance<MonsterActiveAttackProfile>();
            try
            {
                EditorUtility.CopySerialized(source, asset);
                asset.name = "AAP_" + source.ProfileId;
                asset.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(asset, assetPath);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                createdAsset = asset;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath); // 방금 실패한 신규 생성만 원자적으로 되돌린다.
                }
                else if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
                assetPath = string.Empty;
                error = $"공격 액티브 프리셋을 저장하지 못했습니다: {exception.Message}";
                return false;
            }
        }

        public static bool TryUpdate(
            MonsterActiveAttackProfile source,
            MonsterActiveAttackProfile target,
            out string error)
        {
            if (target == null)
            {
                error = "업데이트할 공격 액티브 프리셋이 없습니다.";
                return false;
            }
            if (!IsManagedProfile(target))
            {
                error = $"공격 액티브 관리 경로 밖의 자산은 업데이트할 수 없습니다: {AssetDatabase.GetAssetPath(target)}";
                return false;
            }
            if (!TryValidate(source, target, out error)) return false;

            try
            {
                var originalHideFlags = target.hideFlags;
                Undo.RecordObject(target, "공격 액티브 프리셋 업데이트");
                EditorUtility.CopySerialized(source, target);
                target.hideFlags = originalHideFlags;
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssetIfDirty(target);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = $"공격 액티브 프리셋을 업데이트하지 못했습니다: {exception.Message}";
                return false;
            }
        }

        public static bool IsManagedProfile(MonsterActiveAttackProfile profile)
        {
            if (profile == null) return false;
            var path = AssetDatabase.GetAssetPath(profile).Replace('\\', '/');
            return path.StartsWith(ProfileRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildCustomAssetPath(string profileId) =>
            $"{CustomProfileRoot}/AAP_{profileId}.asset";

        public static void EnsureProfileFolder()
        {
            const string dataRoot = "Assets/ProjectMT/02_Shared/Unit/Data";
            if (!AssetDatabase.IsValidFolder(ProfileRoot))
            {
                AssetDatabase.CreateFolder(dataRoot, "ActiveAttackProfiles");
            }
        }

        private static void EnsureCustomFolder()
        {
            EnsureProfileFolder();
            if (!AssetDatabase.IsValidFolder(CustomProfileRoot))
            {
                AssetDatabase.CreateFolder(ProfileRoot, "Custom");
            }
        }
    }
}
