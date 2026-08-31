using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public interface IMonsterActiveFocusFeedback // 선택형 화면 연출 어댑터 계약
    {
        void PlayEnter(Color accentColor, bool isMythic);
        void PlayRelease(Color accentColor, bool isMythic);
        void StopImmediate();
    }
}
