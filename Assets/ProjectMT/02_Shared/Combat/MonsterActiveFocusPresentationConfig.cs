using TMPro;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [CreateAssetMenu(
        menuName = "ProjectMT/Combat/Monster Active Focus Presentation Config",
        fileName = "MonsterActiveFocusPresentationConfig")]
    public sealed class MonsterActiveFocusPresentationConfig : ScriptableObject // 집중 배너의 한글 폰트 계약
    {
        private const string ResourcesPath = "MonsterActiveFocusPresentationConfig";
        [SerializeField] private TMP_FontAsset ownerFont;
        [SerializeField] private TMP_FontAsset skillFont;
        private static MonsterActiveFocusPresentationConfig cached;

        public TMP_FontAsset OwnerFont => ownerFont;
        public TMP_FontAsset SkillFont => skillFont != null ? skillFont : ownerFont;
        public static MonsterActiveFocusPresentationConfig Current => cached != null
            ? cached
            : cached = Resources.Load<MonsterActiveFocusPresentationConfig>(ResourcesPath);

        public bool TryValidate(out string error)
        {
            if (OwnerFont == null || SkillFont == null)
            {
                error = "몬스터 액티브 집중 배너의 한글 Font Asset이 비어 있습니다.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { cached = null; }

#if UNITY_EDITOR
        public void EditorConfigure(TMP_FontAsset body, TMP_FontAsset title)
        {
            ownerFont = body;
            skillFont = title != null ? title : body;
            cached = this;
        }
#endif
    }
}
