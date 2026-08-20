using System;
using System.Text;
using System.Threading.Tasks;
using ProjectMT.Core.SaveIO;
using UnityEngine;

namespace ProjectMT.Shared.GameData
{
    [Serializable]
    public sealed class SaveEnvelope // 버전이 포함된 저장 묶음
    {
        public int dataVersion; // 저장 형식 버전
        public string savedAtUtc; // UTC 저장 시각
        public GameProgressData gameData; // 실제 진행 데이터
    }

    public sealed class SaveService // 진행 데이터 직렬화 담당
    {
        public const int CurrentDataVersion = 19; // 출석·우편 진행 저장
        private const int MinimumSupportedDataVersion = 1;
        private const string LegacyFoodRiotBestKillsJsonKey = "\"vegetableRiotBestKills\""; // 개명 전 저장 키
        private const string FoodRiotBestKillsJsonKey = "\"foodRiotBestKills\""; // 현재 저장 키
        private const string LegacyGoldJsonKey = "\"temporaryGold\""; // 시드 골드 저장 키
        private const string GoldJsonKey = "\"gold\""; // 정식 골드 저장 키

        private readonly IAtomicFileStore fileStore; // 실제 파일 교체 계약
        private readonly string savePath; // 저장 파일 전체 경로

        public SaveService(IAtomicFileStore fileStore, string savePath)
        {
            this.fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
            this.savePath = savePath ?? throw new ArgumentNullException(nameof(savePath));
        }

        public async Task<GameProgressData> LoadAsync()
        {
            var bytes = await fileStore.ReadAsync(savePath);
            if (bytes == null || bytes.Length == 0)
            {
                var created = GameProgressData.CreateDefault();
                created.Repair();
                await SaveAsync(created); // 첫 실행 프로필을 MainBattle 전에 확정
                return created;
            }

            SaveEnvelope envelope;
            try
            {
                var json = MigrateLegacyFieldNames(Encoding.UTF8.GetString(bytes));
                envelope = JsonUtility.FromJson<SaveEnvelope>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Save load failed. A seed default will be used. {exception.Message}");
                return GameProgressData.CreateDefault(); // 손상 파일은 시드 기본값
            }

            if (envelope == null || envelope.gameData == null ||
                envelope.dataVersion < MinimumSupportedDataVersion ||
                envelope.dataVersion > CurrentDataVersion)
            {
                Debug.LogWarning("Save data is missing or uses an unsupported version. A seed default will be used.");
                return GameProgressData.CreateDefault(); // 미지원 저장은 시드 기본값
            }

            envelope.gameData.MigrateFromVersion(envelope.dataVersion); // 버전별 누락 필드를 먼저 복구
            if (envelope.dataVersion != CurrentDataVersion)
            {
                await SaveAsync(envelope.gameData); // 이전 진행값을 보존한 채 현재 형식으로 승격
            }

            return envelope.gameData;
        }

        public async Task SaveAsync(GameProgressData data)
        {
            var envelope = new SaveEnvelope
            {
                dataVersion = CurrentDataVersion,
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                gameData = data.Clone()
            };

            var json = JsonUtility.ToJson(envelope, true);
            var verification = JsonUtility.FromJson<SaveEnvelope>(json); // 쓰기 전 역직렬화 검증
            if (verification == null || verification.dataVersion != CurrentDataVersion || verification.gameData == null)
            {
                throw new InvalidOperationException("Serialized save verification failed.");
            }

            await fileStore.ReplaceAsync(savePath, Encoding.UTF8.GetBytes(json)); // 검증된 내용만 교체
        }

        private static string MigrateLegacyFieldNames(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return json;
            }

            var migrated = json;
            if (migrated.IndexOf(FoodRiotBestKillsJsonKey, StringComparison.Ordinal) < 0)
            {
                migrated = migrated.Replace(LegacyFoodRiotBestKillsJsonKey, FoodRiotBestKillsJsonKey); // 기존 최고 기록 보존
            }

            if (migrated.IndexOf(GoldJsonKey, StringComparison.Ordinal) < 0)
            {
                migrated = migrated.Replace(LegacyGoldJsonKey, GoldJsonKey); // 임시 골드를 정식 잔액으로 승격
            }

            return migrated;
        }
    }
}
