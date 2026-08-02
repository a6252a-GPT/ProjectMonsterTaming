using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using UnityEngine;

namespace ProjectMT.Contents.VegetableRiot
{
    [CreateAssetMenu(menuName = "ProjectMT/Vegetable Riot/Result Adapter", fileName = "VegetableRiotResultAdapter")]
    public sealed class VegetableRiotResultAdapter : ContentResultAdapter // 처치 결과를 저장 변화로 변환
    {
        public override bool TryCreateProgressChange(IContentResultData result, out GameProgressChange change)
        {
            if (!(result is VegetableRiotResult vegetableResult))
            {
                change = null;
                return false;
            }

            change = GameProgressChange.RecordVegetableRiot(vegetableResult.KillCount, vegetableResult.KillCount); // 시드는 1마리당 골드 1
            return true;
        }
    }
}
