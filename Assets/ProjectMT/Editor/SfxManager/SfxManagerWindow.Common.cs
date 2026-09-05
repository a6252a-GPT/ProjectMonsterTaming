using System;
using System.Linq;
using ProjectMT.Shared.Audio;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
namespace ProjectMT.EditorTools.Audio
{
    public sealed partial class SfxManagerWindow
    {
        [SerializeField] private bool expertLibrary;
        [SerializeField] private string commonCategory = "전체";
        [SerializeField] private string commonSearch = "";
        private SfxEventCatalog commonCatalog;
        private VisualElement commonCards;
        private Label commonSummary;
        private void HandleCommonPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && !expertLibrary) BuildCommonWindow();
        }
        private void BuildCommonWindow()
        {
            rootVisualElement.Clear();
            minSize = new Vector2(740, 520);
            rootVisualElement.style.paddingLeft = 16; rootVisualElement.style.paddingRight = 16;
            rootVisualElement.style.paddingTop = 12;
            var title = new Label("공용 효과음"); title.style.fontSize = 23; title.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(title);
            var help = new Label("소리 파일을 넣고 저장하세요. 필요 없는 칸은 삭제하면 게임에서도 무음이 됩니다. 몬스터 고유 소리는 몬스터 메이커에서 설정합니다.");
            help.style.whiteSpace = WhiteSpace.Normal; help.style.marginTop = 6; help.style.marginBottom = 10;
            rootVisualElement.Add(help);
            try { commonCatalog = SfxCommonMigration.Ensure(); }
            catch (Exception e) { rootVisualElement.Add(new HelpBox(e.Message, HelpBoxMessageType.Warning)); return; }
            var bar = new Toolbar();
            var categories = new[] { "전체" }.Concat(SfxEvents.Definitions.Select(d => d.Category).Distinct()).ToList();
            var category = new PopupField<string>(categories, categories.Contains(commonCategory) ? commonCategory : "전체");
            category.RegisterValueChangedCallback(e => { commonCategory = e.newValue; RefreshCommonCards(); }); bar.Add(category);
            var search = new ToolbarSearchField(); search.SetValueWithoutNotify(commonSearch); search.style.flexGrow = 1;
            search.RegisterValueChangedCallback(e => { commonSearch = e.newValue; RefreshCommonCards(); }); bar.Add(search);
            bar.Add(new ToolbarButton(AddCommonMenu) { text = "+ 효과음 칸" });
            bar.Add(new ToolbarButton(SaveCommon) { text = "저장" });
            bar.Add(new ToolbarButton(StopPreview) { text = "미리듣기 정지" });
            rootVisualElement.Add(bar);
            commonSummary = new Label(); commonSummary.style.marginTop = 8; commonSummary.style.marginBottom = 8;
            rootVisualElement.Add(commonSummary);
            var scroll = new ScrollView(); scroll.style.flexGrow = 1; commonCards = scroll.contentContainer;
            rootVisualElement.Add(scroll);
            var expert = new Button(() =>
            {
                expertLibrary = true; workspaceMode = SfxWorkspaceMode.CueLibrary; CreateGUI();
            }) { text = "고급 · 기존 Cue 보관함 열기" };
            expert.style.marginTop = 8; expert.style.marginBottom = 8; rootVisualElement.Add(expert);
            RefreshCommonCards();
        }
        private void RefreshCommonCards()
        {
            commonCards.Clear();
            var count = 0;
            foreach (var definition in SfxEvents.Definitions)
            {
                var entry = commonCatalog.Entries.FirstOrDefault(e => e != null && e.id == definition.Id);
                if (entry == null || (commonCategory != "전체" && commonCategory != definition.Category)) continue;
                if (!string.IsNullOrWhiteSpace(commonSearch) &&
                    (definition.Name + " " + definition.Description + " " + definition.Category).IndexOf(commonSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                commonCards.Add(BuildCommonCard(definition, entry)); count++;
            }
            commonSummary.text = $"표시 {count}개 / 전체 {commonCatalog.Entries.Count}개  ·  빈 칸과 삭제한 칸은 소리가 나지 않습니다. 변경 후 저장하세요.";
        }
        private VisualElement BuildCommonCard(SfxEventDefinition definition, SfxEventCatalog.Entry entry)
        {
            var card = new VisualElement(); card.style.paddingLeft = 12; card.style.paddingRight = 12;
            card.style.paddingTop = 10; card.style.paddingBottom = 10; card.style.marginBottom = 8;
            card.style.backgroundColor = new Color(0.19f, 0.20f, 0.22f);
            var header = new VisualElement(); header.style.flexDirection = FlexDirection.Row;
            var name = new Label(definition.Category + "  /  " + definition.Name); name.style.flexGrow = 1;
            name.style.unityFontStyleAndWeight = FontStyle.Bold; header.Add(name);
            var state = new Label();
            void UpdateState() => state.text = entry.state == SfxSpaceAssignmentState.Disabled ? "꺼짐" : entry.cue == null || !entry.cue.HasPlayableClip ? "비어 있음" : "사용 중";
            UpdateState(); header.Add(state);
            header.Add(new Button(() =>
            {
                StopPreview(); SfxCommonMigration.Remove(commonCatalog, entry.id); RefreshCommonCards();
            }) { text = "삭제", tooltip = "이 칸과 전용 설정을 삭제합니다. 원본 음원 파일은 보존합니다. Ctrl+Z로 실행 취소할 수 있습니다." });
            card.Add(header);
            var description = new Label(definition.Description); description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginTop = 4; description.style.marginBottom = 6; card.Add(description);
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center;
            var enabled = new Toggle("사용"); enabled.SetValueWithoutNotify(entry.state != SfxSpaceAssignmentState.Disabled);
            enabled.RegisterValueChangedCallback(e => { Undo.RecordObject(commonCatalog, "효과음 사용 변경"); entry.state = e.newValue ? SfxSpaceAssignmentState.Assigned : SfxSpaceAssignmentState.Disabled; DirtyCommon(); UpdateState(); }); row.Add(enabled);
            var clip = new ObjectField { objectType = typeof(AudioClip), allowSceneObjects = false };
            clip.SetValueWithoutNotify(entry.cue != null ? entry.cue.PrimaryClip : null); clip.style.flexGrow = 1;
            clip.RegisterValueChangedCallback(e =>
            {
                var cue = OwnCommonCue(entry);
                Undo.RecordObject(cue, "효과음 파일 변경");
                cue.EditorConfigure(e.newValue == null ? Array.Empty<AudioClip>() : new[] { (AudioClip)e.newValue },
                    cue.VolumeRange, cue.PitchRange, cue.SpatialBlend, cue.DuplicateCooldown, cue.Priority, cue.StartOffsetSeconds, cue.EndCutSeconds);
                EditorUtility.SetDirty(cue); DirtyCommon(); UpdateState();
            }); row.Add(clip);
            row.Add(new Button(() => { if (entry.cue != null) PreviewCue(entry.cue); }) { text = "▶ 듣기" }); card.Add(row);
            var volume = new Slider("음량", 0f, 1f) { showInputField = true };
            volume.SetValueWithoutNotify(entry.cue != null ? entry.cue.VolumeRange.y : 1f);
            volume.RegisterValueChangedCallback(e =>
            {
                var cue = OwnCommonCue(entry); Undo.RecordObject(cue, "효과음 음량 변경");
                cue.EditorConfigure(cue.Clips?.ToArray(), Vector2.one * e.newValue, cue.PitchRange, cue.SpatialBlend,
                    cue.DuplicateCooldown, cue.Priority, cue.StartOffsetSeconds, cue.EndCutSeconds);
                EditorUtility.SetDirty(cue); DirtyCommon();
            }); card.Add(volume);
            var advanced = new Foldout { text = "세부 조절 · 랜덤 음원 / 높낮이 / 자르기 / 반복 제한", value = false };
            advanced.RegisterValueChangedCallback(e =>
            {
                if (e.target != advanced || !e.newValue || advanced.contentContainer.childCount > 0) return;
                var cue = OwnCommonCue(entry); var serialized = new SerializedObject(cue);
                foreach (var propertyName in new[] { "clips", "pitchRange", "startOffsetSeconds", "endCutSeconds", "duplicateCooldown" })
                {
                    var field = new PropertyField(serialized.FindProperty(propertyName), CommonPropertyLabel(propertyName));
                    field.Bind(serialized); advanced.Add(field);
                }
                advanced.RegisterCallback<SerializedPropertyChangeEvent>(_ => { DirtyCommon(); UpdateState(); clip.SetValueWithoutNotify(cue.PrimaryClip); });
            }); card.Add(advanced);
            return card;
        }
        private static string CommonPropertyLabel(string value) => value switch
        { "clips" => "랜덤 재생 파일", "pitchRange" => "높낮이 범위", "startOffsetSeconds" => "앞부분 자르기 (초)",
          "endCutSeconds" => "뒷부분 자르기 (초)", _ => "같은 소리 최소 간격 (초)" };
        private SfxCue OwnCommonCue(SfxEventCatalog.Entry entry)
        {
            if (entry.cue != null && AssetDatabase.GetAssetPath(entry.cue) == SfxCommonMigration.CatalogPath) return entry.cue;
            Undo.RecordObject(commonCatalog, "효과음 전용 설정");
            if (entry.cue == null) entry.cue = SfxCommonMigration.CreateCue(commonCatalog, entry.id, Array.Empty<AudioClip>());
            else
            {
                entry.cue = Instantiate(entry.cue); entry.cue.name = entry.id;
                AssetDatabase.AddObjectToAsset(entry.cue, commonCatalog);
            }
            Undo.RegisterCreatedObjectUndo(entry.cue, "효과음 전용 설정"); DirtyCommon(); return entry.cue;
        }
        private void DirtyCommon() { EditorUtility.SetDirty(commonCatalog); SfxEvents.InvalidateCatalog(); }
        private void SaveCommon()
        {
            foreach (var entry in commonCatalog.Entries)
            {
                if (entry == null || entry.state == SfxSpaceAssignmentState.Disabled || entry.cue == null) continue;
                if (!entry.id.StartsWith("COM-", StringComparison.Ordinal) && entry.cue.SpatialBlend != 0f)
                { EditorUtility.DisplayDialog("저장 확인", entry.id + ": 화면 효과음은 2D여야 합니다.", "확인"); return; }
                foreach (var clip in entry.cue.Clips ?? Array.Empty<AudioClip>())
                    if (clip != null && !entry.cue.TryResolvePlaybackRange(clip, out _, out _))
                    { EditorUtility.DisplayDialog("저장 확인", entry.id + ": 자르기 구간을 확인하세요.", "확인"); return; }
            }
            EditorUtility.SetDirty(commonCatalog); AssetDatabase.SaveAssetIfDirty(commonCatalog);
            SfxEvents.InvalidateCatalog(); commonSummary.text = "저장했습니다. 게임은 이 설정을 직접 사용합니다.";
        }
        private void AddCommonMenu()
        {
            var menu = new GenericMenu();
            foreach (var definition in SfxEvents.Definitions)
            {
                if (commonCatalog.Entries.Any(e => e != null && e.id == definition.Id)) continue;
                var id = definition.Id;
                menu.AddItem(new GUIContent(definition.Category + "/" + definition.Name), false, () =>
                { Undo.RecordObject(commonCatalog, "효과음 칸 추가"); commonCatalog.EditorAdd(id); DirtyCommon(); RefreshCommonCards(); });
            }
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("모든 재생 위치가 이미 들어 있습니다"));
            menu.ShowAsContext();
        }
    }
}
