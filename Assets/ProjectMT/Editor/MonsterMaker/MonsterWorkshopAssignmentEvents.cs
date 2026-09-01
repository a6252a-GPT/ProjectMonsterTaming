using System;

namespace ProjectMT.EditorTools.MonsterMaker
{
    // 조립소 UI 종류와 무관하게 Maker가 프리셋 배정·업데이트 완료를 구독하는 공용 경계.
    internal static class MonsterWorkshopAssignmentEvents
    {
        internal static event Action PresetAssigned;

        internal static void NotifyPresetAssigned()
        {
            PresetAssigned?.Invoke();
        }
    }
}
