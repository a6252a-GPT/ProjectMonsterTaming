using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastleAssaultAIProfileSyncUtility // 사각 설정을 Hex 독립 카탈로그로 복사한다
    {
        private const string SourcePath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/Resources/CastleRaidAIProfileCatalog.asset";
        private const string OutputFolder =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Resources";
        private const string OutputPath = OutputFolder + "/HexCastleAssaultAIProfileCatalog.asset";

        [MenuItem("JC Tool/군단의 역습 육각/공격 AI/사각 AI 프로필 동기화")]
        public static void SyncFromSquareCatalog()
        {
            var source = AssetDatabase.LoadMainAssetAtPath(SourcePath);
            if (source == null)
            {
                throw new InvalidOperationException($"사각 공격 AI 카탈로그가 없습니다: {SourcePath}");
            }

            EnsureFolder(OutputFolder);
            var target = AssetDatabase.LoadAssetAtPath<HexCastleAssaultAIProfileCatalog>(OutputPath);
            if (target == null)
            {
                target = ScriptableObject.CreateInstance<HexCastleAssaultAIProfileCatalog>();
                AssetDatabase.CreateAsset(target, OutputPath);
            }

            var sourceEntries = new SerializedObject(source).FindProperty("entries");
            if (sourceEntries == null || !sourceEntries.isArray)
            {
                throw new InvalidOperationException("사각 공격 AI 카탈로그의 entries를 읽지 못했습니다.");
            }

            var copied = new List<HexCastleAssaultAIProfile>(sourceEntries.arraySize);
            for (var index = 0; index < sourceEntries.arraySize; index++)
            {
                var sourceEntry = sourceEntries.GetArrayElementAtIndex(index);
                var entry = new HexCastleAssaultAIProfile();
                entry.EditorConfigure(
                    sourceEntry.FindPropertyRelative("monsterId").stringValue,
                    MapSquarePattern(sourceEntry.FindPropertyRelative("pattern").enumValueIndex),
                    (HexCastleAssaultSupportFocus)sourceEntry.FindPropertyRelative("supportFocus").enumValueIndex,
                    sourceEntry.FindPropertyRelative("supportRange").floatValue,
                    sourceEntry.FindPropertyRelative("supportCooldown").floatValue,
                    sourceEntry.FindPropertyRelative("supportDuration").floatValue,
                    sourceEntry.FindPropertyRelative("healRatio").floatValue,
                    sourceEntry.FindPropertyRelative("attackBuffRate").floatValue,
                    sourceEntry.FindPropertyRelative("defenseDamageMultiplier").floatValue);
                copied.Add(entry);
            }

            target.EditorReplaceEntries(copied);
            if (!target.TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = target;
            Debug.Log($"[Hex Assault AI] 사각 AI 프로필 {copied.Count}개 독립 카탈로그 동기화 완료");
        }

        private static HexCastleAssaultPattern MapSquarePattern(int sourceValue)
        {
            switch (sourceValue)
            {
                case 1: return HexCastleAssaultPattern.ResourceRaider;
                case 2: return HexCastleAssaultPattern.TurretHunter;
                case 3: return HexCastleAssaultPattern.DefenderHunter;
                case 4: return HexCastleAssaultPattern.WallBreaker;
                case 5: return HexCastleAssaultPattern.ThreatSuppressor;
                case 6: return HexCastleAssaultPattern.TacticalSupport;
                default: return HexCastleAssaultPattern.GeneralAdvance;
            }
        }

        public static void RunOnceFromCommandLine()
        {
            SyncFromSquareCatalog();
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            var name = Path.GetFileName(assetFolder);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"자산 폴더 경로가 잘못됐습니다: {assetFolder}");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
