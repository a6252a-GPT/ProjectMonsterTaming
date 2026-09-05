using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public interface IMonsterActiveStyleFeedback
    {
        void SetStyle(MonsterActiveFocusStyle style);
    }

    public interface IMonsterActiveCasterFeedback // 시전자 연출은 전투 상태를 변경하지 않는다
    {
        void BindCaster(Transform caster, float bodyRadius);
    }

    public interface IMonsterActiveFocusFeedback // 선택형 화면 연출 어댑터 계약
    {
        void PlayEnter(Color accentColor, bool isMythic);
        void PlayRelease(Color accentColor, bool isMythic);
        void StopImmediate();
    }
}
