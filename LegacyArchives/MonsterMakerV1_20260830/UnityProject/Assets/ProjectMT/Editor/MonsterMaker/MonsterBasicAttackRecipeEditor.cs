using System;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal readonly struct MonsterBasicAttackRecipeEditorResult
    {
        public MonsterBasicAttackRecipeEditorResult(
            MonsterBasicAttackProfile profile,
            bool profileReferenceChanged,
            bool recipeChanged,
            string message,
            MessageType messageType)
        {
            Profile = profile;
            ProfileReferenceChanged = profileReferenceChanged;
            RecipeChanged = recipeChanged;
            Message = message;
            MessageType = messageType;
        }

        public MonsterBasicAttackProfile Profile { get; }
        public bool ProfileReferenceChanged { get; }
        public bool RecipeChanged { get; }
        public string Message { get; }
        public MessageType MessageType { get; }
    }

    internal static class MonsterBasicAttackRecipeEditor // Maker에는 선택·조립소 진입만 노출
    {
        public static MonsterBasicAttackRecipeEditorResult Draw(
            SerializedProperty profileProperty,
            MonsterMakerDraft draft)
        {
            var profile = profileProperty.objectReferenceValue as MonsterBasicAttackProfile;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        profile == null ? "기본공격 선택" : "기본공격 변경",
                        GUILayout.Height(30f)))
                {
                    ShowPresetMenu(draft, profileProperty);
                }

                if (GUILayout.Button("기본공격 조립소 열기", GUILayout.Height(30f)))
                {
                    MonsterBasicAttackWorkshopWindow.Open(draft);
                }
            }

            return new MonsterBasicAttackRecipeEditorResult(
                profile,
                false,
                false,
                null,
                MessageType.None);
        }

        private static void ShowPresetMenu(MonsterMakerDraft draft, SerializedProperty profileProperty)
        {
            var menu = new GenericMenu();
            var current = profileProperty.objectReferenceValue as MonsterBasicAttackProfile;
            var profiles = AssetDatabase.FindAssets(
                    "t:MonsterBasicAttackProfile",
                    new[] { MonsterBasicAttackPresetUtility.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => MonsterBasicAttackPresetUtility.IsBuiltInProfile(profile) ? 0 : 1)
                .ThenBy(profile => profile.AttackId, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (profiles.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("저장된 프리셋 없음"));
            }

            foreach (var profile in profiles)
            {
                var captured = profile;
                var group = MonsterBasicAttackPresetUtility.IsBuiltInProfile(profile)
                    ? "공식 기본공격 15종"
                    : "사용자 프리셋";
                menu.AddItem(
                    new GUIContent($"{group}/[{profile.AttackId}] {profile.DisplayName}"),
                    profile == current,
                    () => AssignPreset(draft, captured, profileProperty.serializedObject));
            }
            menu.ShowAsContext();
        }

        private static void AssignPreset(
            MonsterMakerDraft draft,
            MonsterBasicAttackProfile profile,
            SerializedObject serializedDraft)
        {
            if (draft == null || profile == null)
            {
                return;
            }

            Undo.RecordObject(draft, "기본공격 프리셋 선택");
            draft.EditorSetBasicAttackProfile(profile);
            draft.EditorAdoptBasicAttackProfileTuning();
            EditorUtility.SetDirty(draft);
            MonsterBasicAttackPresetUtility.InvalidateUsageCache();
            serializedDraft?.UpdateIfRequiredOrScript();
        }
    }
}
