using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [CreateAssetMenu(menuName = "ProjectMT/Castle Raid/Result Adapter", fileName = "CastleRaidResultAdapter")]
    public sealed class CastleRaidResultAdapter : ContentResultAdapter // 성 파괴를 진행 기록으로 번역
    {
        public override bool TryCreateProgressChange(IContentResultData result, out GameProgressChange change)
        {
            if (!(result is CastleRaidResult castleResult) || !castleResult.MainCastleDestroyed)
            {
                change = null;
                return false;
            }

            change = GameProgressChange.RecordCastleRaidClear(); // 첫 클리어 기록 요청
            return true;
        }
    }
}
