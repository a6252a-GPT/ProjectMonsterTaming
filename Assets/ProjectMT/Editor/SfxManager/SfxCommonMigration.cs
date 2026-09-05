using System;
using System.Linq;
using ProjectMT.Shared.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ProjectMT.EditorTools.Audio
{
    public static class SfxCommonMigration
    {
        public const string CatalogPath = "Assets/ProjectMT/06_Audio/SFX/Resources/SfxEvents_ProjectMT.asset";
        private const string SpacePath = "Assets/ProjectMT/06_Audio/SFX/Catalogs/SfxSpaceCatalog_ProjectMT.asset";
        public static SfxEventCatalog Ensure()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SfxEventCatalog>(CatalogPath);
            if (asset != null && asset.IsManaged) return asset; // 삭제한 칸을 재생성하지 않음
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("공용 효과음 설정의 첫 이전은 Play 종료 후 진행하세요.");
            var legacyUi = ReadLegacyUi(); // 읽기 실패 시 반쪽 이전을 저장하지 않음
            if (asset == null)
            {
                const string parent = "Assets/ProjectMT/06_Audio/SFX";
                if (!AssetDatabase.IsValidFolder(parent + "/Resources")) AssetDatabase.CreateFolder(parent, "Resources");
                asset = ScriptableObject.CreateInstance<SfxEventCatalog>();
                AssetDatabase.CreateAsset(asset, CatalogPath);
            }
            var spaces = AssetDatabase.LoadAssetAtPath<SfxSpaceCatalog>(SpacePath);
            foreach (var definition in SfxEvents.Definitions)
            {
                asset.EditorAdd(definition.Id);
                var entry = asset.Entries.First(e => e.id == definition.Id);
                var previous = spaces?.Entries.FirstOrDefault(e => e != null && e.Id == definition.Id);
                if (entry.cue == null && previous != null && previous.AssignmentState != SfxSpaceAssignmentState.Undecided)
                {
                    entry.cue = previous.Cue;
                    entry.state = previous.AssignmentState;
                }
                if (entry.cue != null)
                {
                    if (AssetDatabase.GetAssetPath(entry.cue) != CatalogPath)
                    {
                        entry.cue = UnityEngine.Object.Instantiate(entry.cue); entry.cue.name = entry.id;
                        AssetDatabase.AddObjectToAsset(entry.cue, asset);
                    }
                    continue;
                }
                if (entry.state == SfxSpaceAssignmentState.Disabled) continue;
                var ui = legacyUi.FirstOrDefault(e => e.Id == definition.Id);
                if (ui != null)
                {
                    entry.cue = CreateCue(asset, entry.id, ui.Clips, ui.Volume);
                    entry.state = ui.Muted ? SfxSpaceAssignmentState.Disabled : SfxSpaceAssignmentState.Assigned;
                }
                else if (definition.Id.StartsWith("EVENT-Gacha", StringComparison.Ordinal))
                {
                    var original = Resources.Load<SfxCue>("GachaAudio/" + definition.Id.Substring("EVENT-Gacha".Length));
                    if (original != null)
                    {
                        entry.cue = UnityEngine.Object.Instantiate(original);
                        entry.cue.name = entry.id;
                        AssetDatabase.AddObjectToAsset(entry.cue, asset);
                    }
                }
                if (entry.state == SfxSpaceAssignmentState.Undecided) entry.state = SfxSpaceAssignmentState.Assigned;
            }
            asset.EditorSetManaged();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            SfxEvents.InvalidateCatalog();
            return asset;
        }
        public static SfxCue CreateCue(SfxEventCatalog asset, string id, AudioClip[] clips, float volume = 1f)
        {
            var cue = ScriptableObject.CreateInstance<SfxCue>();
            cue.name = id;
            cue.EditorConfigure(clips ?? Array.Empty<AudioClip>(), Vector2.one * volume, Vector2.one, 0f, id.StartsWith("SYS-", StringComparison.Ordinal) ? 0f : 0.08f, SfxPriority.Normal);
            AssetDatabase.AddObjectToAsset(cue, asset);
            EditorUtility.SetDirty(cue);
            return cue;
        }
        public static void Remove(SfxEventCatalog asset, string id)
        {
            var entry = asset.Entries.FirstOrDefault(e => e != null && e.id == id);
            if (entry == null) return;
            var cue = entry.cue;
            Undo.RecordObject(asset, "효과음 칸 삭제");
            asset.EditorRemove(id);
            if (cue != null && AssetDatabase.IsSubAsset(cue) && AssetDatabase.GetAssetPath(cue) == AssetDatabase.GetAssetPath(asset) &&
                !asset.Entries.Any(e => e != null && e.cue == cue)) Undo.DestroyObjectImmediate(cue);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            SfxEvents.InvalidateCatalog();
        }
        private sealed class LegacyUi
        {
            public string Id;
            public AudioClip[] Clips;
            public float Volume;
            public bool Muted;
        }
        private static LegacyUi[] ReadLegacyUi()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/ProjectMT/00_Scenes/00_Entry.unity");
            try
            {
                var manager = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<AudioManager>(true)).FirstOrDefault();
                if (manager == null) throw new InvalidOperationException("Entry 씬의 기존 AudioManager를 찾지 못했습니다.");
                var serialized = new SerializedObject(manager);
                return new[] { Read(serialized, SfxEvents.Button, "buttonClick", "ButtonClick"),
                    Read(serialized, SfxEvents.Open, "popupOpen", "PopupOpen"), Read(serialized, SfxEvents.Close, "popupClose", "PopupClose") };
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static LegacyUi Read(SerializedObject source, string id, string prefix, string muteSuffix)
        {
            var clips = source.FindProperty(prefix + "Clips");
            var values = new AudioClip[clips.arraySize];
            for (var i = 0; i < values.Length; i++) values[i] = clips.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
            return new LegacyUi { Id = id, Clips = values, Volume = source.FindProperty(prefix + "Volume").floatValue,
                Muted = source.FindProperty("mute" + muteSuffix).boolValue };
        }
    }
}
