using UnityEditor;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    internal static class MonsterMakerV2HelpPreferences // 숙련자용 설명 밀도는 모든 V2 보조 창이 공유
    {
        private const string ShowContextHelpKey = "ProjectMT.MonsterMakerV2.ShowContextHelp";

        public static bool ShowContextHelp
        {
            get => EditorPrefs.GetBool(ShowContextHelpKey, true);
            set => EditorPrefs.SetBool(ShowContextHelpKey, value);
        }
    }
}
