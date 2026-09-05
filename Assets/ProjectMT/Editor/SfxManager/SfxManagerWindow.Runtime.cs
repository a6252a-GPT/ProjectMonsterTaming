using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.Audio
{
    public sealed partial class SfxManagerWindow
    {
        private const string RuntimeDirectory = "Assets/ProjectMT/06_Audio/SFX/Resources";
        private const string RuntimeCatalogPath = RuntimeDirectory + "/SfxEvents_ProjectMT.asset";

        private bool PublishRuntimeEvents(out string error)
        {
            error = null;
            var managed = AssetDatabase.LoadAssetAtPath<SfxEventCatalog>(RuntimeCatalogPath);
            if (managed != null && managed.IsManaged) return true; // 구 조사표가 공용 원본을 덮지 않음
            var entries = new List<SfxEventCatalog.Entry>();
            foreach (var space in spaceCatalog.Entries)
            {
                if (space == null || !SfxEvents.Supports(space.Id)) continue;
                if (space.AssignmentState == SfxSpaceAssignmentState.Assigned)
                {
                    if (space.Cue == null || !space.Cue.HasPlayableClip)
                    {
                        error = space.Id + ": 재생 가능한 Cue가 필요합니다.";
                        return false;
                    }
                    foreach (var clip in space.Cue.Clips)
                    {
                        if (clip != null && !space.Cue.TryResolvePlaybackRange(clip, out _, out _))
                        {
                            error = space.Id + ": 사운드 자르기 구간을 확인하세요.";
                            return false;
                        }
                    }
                    if (!space.Id.StartsWith("COM-") && space.Cue.SpatialBlend != 0f)
                    {
                        error = space.Id + ": 화면 효과음은 Cue 공간감을 2D(0)로 설정하세요.";
                        return false;
                    }
                }
                entries.Add(new SfxEventCatalog.Entry
                {
                    id = space.Id, state = space.AssignmentState, cue = space.Cue
                });
            }
            EnsureFolder(RuntimeDirectory);
            var asset = AssetDatabase.LoadAssetAtPath<SfxEventCatalog>(RuntimeCatalogPath);
            if (asset == null)
            {
                asset = CreateInstance<SfxEventCatalog>();
                AssetDatabase.CreateAsset(asset, RuntimeCatalogPath);
            }
            Undo.RecordObject(asset, "공용 SFX Runtime 저장");
            asset.EditorReplace(entries);
            EditorUtility.SetDirty(asset);
            SfxEvents.InvalidateCatalog();
            return true;
        }
    }
}
