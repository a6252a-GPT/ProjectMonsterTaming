using System;
using System.Linq;
using ProjectMT.Shared.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal static class MonsterBasicAttackVfxPrefabUtility // 원본 VFX를 보존하는 몬스터 전용 래퍼
    {
        public static bool TryCreateWrapper(
            string monsterId,
            string attackId,
            string slotId,
            string motionId,
            GameObject sourcePrefab,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            float scale,
            out GameObject wrapperPrefab,
            out string error)
        {
            wrapperPrefab = null;
            error = null;
            var sourcePath = sourcePrefab == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(sourcePrefab);
            if (sourcePrefab == null || string.IsNullOrWhiteSpace(sourcePath) ||
                PrefabUtility.GetPrefabAssetType(sourcePrefab) == PrefabAssetType.NotAPrefab)
            {
                error = "Project에 저장된 VFX Prefab을 먼저 지정하세요.";
                return false;
            }

            var safeMonsterId = SanitizeToken(monsterId, "monster");
            var safeAttackId = SanitizeToken(attackId, "basic_attack");
            var safeSlotId = SanitizeToken(slotId, "vfx");
            var safeMotionId = string.IsNullOrWhiteSpace(motionId)
                ? string.Empty
                : "_" + SanitizeToken(motionId, "motion");
            var monsterFolder = $"{MonsterMakerAssetWriter.ArtRoot}/{safeMonsterId}";
            var vfxFolder = monsterFolder + "/VFX";
            EnsureFolder(MonsterMakerAssetWriter.ArtRoot, safeMonsterId);
            EnsureFolder(monsterFolder, "VFX");

            var fileName = $"PF_{safeMonsterId}_{safeAttackId}_{safeSlotId}{safeMotionId}_VFX.prefab";
            var outputPath = AssetDatabase.GenerateUniqueAssetPath(vfxFolder + "/" + fileName);
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(fileName[..^".prefab".Length]);
                SceneManager.MoveGameObjectToScene(root, previewScene);
                var child = PrefabUtility.InstantiatePrefab(sourcePrefab, previewScene) as GameObject;
                if (child == null)
                {
                    error = "원본 VFX Prefab 인스턴스를 만들지 못했습니다.";
                    return false;
                }

                child.name = "VFX";
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = localPosition;
                child.transform.localRotation = Quaternion.Euler(localEulerAngles);
                MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                    child,
                    sourcePrefab.transform.localScale * Mathf.Max(0.01f, scale));

                var saved = PrefabUtility.SaveAsPrefabAsset(root, outputPath);
                if (saved == null)
                {
                    error = "몬스터 전용 VFX 래퍼 Prefab 저장에 실패했습니다.";
                    return false;
                }

                wrapperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
                if (wrapperPrefab == null)
                {
                    error = "저장된 VFX 래퍼 Prefab을 다시 불러오지 못했습니다.";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        public static bool IsMonsterWrapper(GameObject prefab, string monsterId)
        {
            var path = prefab == null ? string.Empty : AssetDatabase.GetAssetPath(prefab);
            var folder = $"{MonsterMakerAssetWriter.ArtRoot}/{SanitizeToken(monsterId, "monster")}/VFX/";
            return !string.IsNullOrWhiteSpace(path) &&
                   path.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static string SanitizeToken(string value, string fallback)
        {
            var safe = new string((value ?? string.Empty)
                .Trim()
                .Select(character =>
                    char.IsLetterOrDigit(character) || character is '_' or '-'
                        ? character
                        : '_')
                .ToArray());
            return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
        }
    }
}
