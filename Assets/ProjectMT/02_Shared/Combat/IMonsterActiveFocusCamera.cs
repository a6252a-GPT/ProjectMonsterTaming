using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public interface IMonsterActiveFocusCamera // 콘텐츠 카메라와 Shared 전투의 최소 연결
    {
        Camera WorldCamera { get; }
        void BeginMonsterActiveFocus(
            UnitActor caster,
            UnitActor target,
            MonsterActiveFocusPreset preset);
        void EndMonsterActiveFocus();
        void ResetMonsterActiveFocus();
    }
}
