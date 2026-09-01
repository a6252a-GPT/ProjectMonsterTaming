using UnityEngine;

namespace ProjectMT.Features.OfflineReward
{
    // 방치 보상 팝업의 "광고시청 2배" 1일 1회 제한을 기기 로컬에 저장한다.
    // 실제 광고 SDK가 아닌 시뮬레이션 기능이라 계정 저장 데이터(GameProgressData)까지
    // 얽히지 않고 가볍게 로컬로만 관리한다. 리셋 기준은 GrowthDungeonDailyKeyRules와
    // 동일한 KST 05:00 경계 일자 ID(period)를 그대로 사용한다.
    public static class OfflineRewardAdClaimStore
    {
        private const string PlayerPrefsKey = "ProjectMT.OfflineRewardAdClaim.LastPeriod";

        public static long LoadLastClaimedPeriod()
        {
            var raw = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            return long.TryParse(raw, out var period) ? period : -1L;
        }

        public static void SaveLastClaimedPeriod(long period)
        {
            PlayerPrefs.SetString(PlayerPrefsKey, period.ToString());
            PlayerPrefs.Save();
        }

        // 디버그 "저장 데이터 초기화"에서 계정 세이브와 함께 로컬 광고 쿨다운도 같이 초기화하기 위한 용도.
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
