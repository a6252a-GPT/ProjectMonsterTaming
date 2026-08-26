using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    // ReadOnlyInInspector가 붙은 필드를 비활성화된 기본 프로퍼티 형태로 그린다.
    [CustomPropertyDrawer(typeof(ReadOnlyInInspectorAttribute))]
    public sealed class ReadOnlyInInspectorDrawer : PropertyDrawer
    {
        // 지정된 인스펙터 영역에서 필드 입력을 비활성화한 뒤 값을 표시한다.
        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(
                    position,
                    property,
                    label,
                    includeChildren: true);
            }
        }

        // 배열이나 중첩 필드도 잘리지 않도록 기본 프로퍼티 높이를 반환한다.
        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(
                property,
                label,
                includeChildren: true);
        }
    }
}
