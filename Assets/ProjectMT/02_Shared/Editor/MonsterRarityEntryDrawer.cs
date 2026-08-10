using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Shared.Editor
{
    // 일반~영웅 목록 한 줄. 패시브 1개 칸만 항상 고정으로 보여준다 (액티브 칸 자체가 없음, 선택할 필요 없음).
    [CustomPropertyDrawer(typeof(MonsterCommonRarityEntry))]
    public sealed class MonsterCommonRarityEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var monsterProp = property.FindPropertyRelative("monster");
            var rarityProp = property.FindPropertyRelative("rarity");
            var passiveProp = property.FindPropertyRelative("passiveSkill");

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var y = position.y;

            EditorGUI.BeginProperty(position, label, property);

            var foldoutRect = new Rect(position.x, y, position.width, lineHeight);
            property.isExpanded = EditorGUI.Foldout(
                foldoutRect,
                property.isExpanded,
                MonsterRarityDrawerUtility.BuildHeaderLabel(monsterProp, (MonsterRarity)rarityProp.intValue),
                true);
            y += lineHeight + spacing;

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                MonsterRarityDrawerUtility.DrawField(ref y, position, lineHeight, spacing, monsterProp, null);
                MonsterRarityDrawerUtility.DrawField(ref y, position, lineHeight, spacing, rarityProp, null);
                MonsterRarityDrawerUtility.DrawField(
                    ref y, position, lineHeight, spacing, passiveProp, MonsterRarityDrawerUtility.PassiveLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var height = lineHeight + spacing; // 폴드아웃 한 줄

            if (!property.isExpanded)
            {
                return height;
            }

            height += (lineHeight + spacing) * 3; // Monster, Rarity, Passive Skill
            return height;
        }
    }

    // 전설·신화 목록 한 줄. 패시브 1개 + 액티브 1개 칸이 항상 같이 고정으로 보여진다 (선택할 필요 없음).
    [CustomPropertyDrawer(typeof(MonsterLegendaryRarityEntry))]
    public sealed class MonsterLegendaryRarityEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var monsterProp = property.FindPropertyRelative("monster");
            var rarityProp = property.FindPropertyRelative("rarity");
            var passiveProp = property.FindPropertyRelative("passiveSkill");
            var activeProp = property.FindPropertyRelative("activeSkill");

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var y = position.y;

            EditorGUI.BeginProperty(position, label, property);

            var foldoutRect = new Rect(position.x, y, position.width, lineHeight);
            property.isExpanded = EditorGUI.Foldout(
                foldoutRect,
                property.isExpanded,
                MonsterRarityDrawerUtility.BuildHeaderLabel(monsterProp, (MonsterRarity)rarityProp.intValue),
                true);
            y += lineHeight + spacing;

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                MonsterRarityDrawerUtility.DrawField(ref y, position, lineHeight, spacing, monsterProp, null);
                MonsterRarityDrawerUtility.DrawField(ref y, position, lineHeight, spacing, rarityProp, null);
                MonsterRarityDrawerUtility.DrawField(
                    ref y, position, lineHeight, spacing, passiveProp, MonsterRarityDrawerUtility.PassiveLabel);
                MonsterRarityDrawerUtility.DrawField(
                    ref y, position, lineHeight, spacing, activeProp, MonsterRarityDrawerUtility.ActiveLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var height = lineHeight + spacing; // 폴드아웃 한 줄

            if (!property.isExpanded)
            {
                return height;
            }

            height += (lineHeight + spacing) * 4; // Monster, Rarity, Passive Skill, Active Skill
            return height;
        }
    }

    // 두 드로어가 공통으로 쓰는 그리기 도우미.
    internal static class MonsterRarityDrawerUtility
    {
        public static readonly GUIContent PassiveLabel = new GUIContent("Passive Skill");
        public static readonly GUIContent ActiveLabel = new GUIContent("Active Skill");

        public static void DrawField(
            ref float y,
            Rect position,
            float lineHeight,
            float spacing,
            SerializedProperty prop,
            GUIContent overrideLabel)
        {
            var rect = new Rect(position.x, y, position.width, lineHeight);
            if (overrideLabel != null)
            {
                EditorGUI.PropertyField(rect, prop, overrideLabel);
            }
            else
            {
                EditorGUI.PropertyField(rect, prop);
            }

            y += lineHeight + spacing;
        }

        public static GUIContent BuildHeaderLabel(SerializedProperty monsterProp, MonsterRarity rarity)
        {
            var monster = monsterProp.objectReferenceValue as MonsterDefinition;
            var monsterName = monster != null ? monster.DisplayName : "(몬스터 미지정)";
            return new GUIContent($"{monsterName}  ·  {RarityLabel(rarity)}");
        }

        private static string RarityLabel(MonsterRarity rarity)
        {
            switch (rarity)
            {
                case MonsterRarity.Common: return "일반";
                case MonsterRarity.Rare: return "희귀";
                case MonsterRarity.Epic: return "영웅";
                case MonsterRarity.Legendary: return "전설";
                case MonsterRarity.Mythic: return "신화";
                default: return rarity.ToString();
            }
        }
    }
}
