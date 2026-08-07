using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.GuardianTrial
{
    // 08.06 안건준 추가 - 수호자의 탑 전용 시작값 생성기 (식량 대소동 Factory와 독립)
    [CreateAssetMenu(menuName = "ProjectMT/Guardian Trial/Guardians Tower Start Data Factory", fileName = "GuardiansTowerStartDataFactory")]
    public sealed class GuardiansTowerStartDataFactory : ContentStartDataFactory
    {
        [SerializeField, Min(1f)] private float durationSeconds = 60f; // 제한 시간 1분
        [SerializeField, Range(1, 100)] private int enemyCount = 50; // 08.07 안건준 수정 - 시작할 때 한 번만 스폰할 적 수(8 -> 50)

        public override IContentStartData Create(BattlePartySnapshot party)
        {
            return new GuardiansTowerStartData(party, durationSeconds, enemyCount);
        }
    }
}
