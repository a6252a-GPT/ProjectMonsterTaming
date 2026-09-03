using System;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public static class MonsterEffectActiveAuthoringService // 효과형 액티브 저장·검증 경계
    {
        public const string ProfileRoot = "Assets/ProjectMT/02_Shared/Unit/Data/ActiveEffectProfiles";
        public const string CustomProfileRoot = ProfileRoot + "/Custom";

        public static bool TryValidate(
            MonsterEffectActiveProfile source,
            MonsterEffectActiveProfile excludedProfile,
            out string error)
        {
            if (source == null)
            {
                error = "효과형 액티브 작업 사본이 없습니다.";
                return false;
            }
            if (!source.TryValidate(out error)) return false;
            if (excludedProfile != null && !string.Equals(
                    source.ProfileId,
                    excludedProfile.ProfileId,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = $"저장된 프리셋의 ID는 바꿀 수 없습니다. 새 ID가 필요하면 복제하세요: {excludedProfile.ProfileId}";
                return false;
            }

            var guids = AssetDatabase.IsValidFolder(ProfileRoot)
                ? AssetDatabase.FindAssets("t:MonsterEffectActiveProfile", new[] { ProfileRoot })
                : Array.Empty<string>();
            for (var index = 0; index < guids.Length; index++)
            {
                var candidate = AssetDatabase.LoadAssetAtPath<MonsterEffectActiveProfile>(
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
            MonsterEffectActiveProfile source,
            out MonsterEffectActiveProfile createdAsset,
            out string assetPath,
            out string error)
        {
            createdAsset = null;
            assetPath = string.Empty;
            if (!TryValidate(source, null, out error)) return false;
            EnsureCustomFolder();
            assetPath = $"{CustomProfileRoot}/EAP_{source.ProfileId}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                error = $"같은 ID의 프리셋 자산이 이미 있습니다: {assetPath}";
                return false;
            }

            var asset = ScriptableObject.CreateInstance<MonsterEffectActiveProfile>();
            try
            {
                EditorUtility.CopySerialized(source, asset);
                asset.name = "EAP_" + source.ProfileId;
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
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
                else if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
                assetPath = string.Empty;
                error = $"효과형 액티브 프리셋을 저장하지 못했습니다: {exception.Message}";
                return false;
            }
        }

        public static bool TryUpdate(
            MonsterEffectActiveProfile source,
            MonsterEffectActiveProfile target,
            out string error)
        {
            if (target == null)
            {
                error = "업데이트할 효과형 액티브 프리셋이 없습니다.";
                return false;
            }
            if (!IsManagedProfile(target))
            {
                error = $"관리 경로 밖의 효과형 프리셋은 업데이트할 수 없습니다: {AssetDatabase.GetAssetPath(target)}";
                return false;
            }
            if (!TryValidate(source, target, out error)) return false;

            try
            {
                var flags = target.hideFlags;
                var assetName = ResolveAssetName(target);
                Undo.RecordObject(target, "효과형 액티브 프리셋 업데이트");
                EditorUtility.CopySerialized(source, target);
                target.name = assetName;
                target.hideFlags = flags;
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssetIfDirty(target);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = $"효과형 액티브 프리셋을 업데이트하지 못했습니다: {exception.Message}";
                return false;
            }
        }

        public static bool IsManagedProfile(MonsterEffectActiveProfile profile)
        {
            if (profile == null) return false;
            var path = AssetDatabase.GetAssetPath(profile).Replace('\\', '/');
            return path.StartsWith(ProfileRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveAssetName(MonsterEffectActiveProfile profile)
        {
            var path = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(path))
            {
                return profile != null ? profile.name : string.Empty;
            }

            return System.IO.Path.GetFileNameWithoutExtension(path);
        }

        public static void EnsureProfileFolder()
        {
            const string dataRoot = "Assets/ProjectMT/02_Shared/Unit/Data";
            if (!AssetDatabase.IsValidFolder(ProfileRoot))
            {
                AssetDatabase.CreateFolder(dataRoot, "ActiveEffectProfiles");
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
