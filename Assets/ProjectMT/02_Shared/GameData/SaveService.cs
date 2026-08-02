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
        public const int CurrentDataVersion = 1;

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
                return GameProgressData.CreateDefault(); // 첫 실행 기본값
            }

            try
            {
                var json = Encoding.UTF8.GetString(bytes);
                var envelope = JsonUtility.FromJson<SaveEnvelope>(json);
                if (envelope == null || envelope.dataVersion != CurrentDataVersion || envelope.gameData == null)
                {
                    Debug.LogWarning("Save data is missing or uses an unsupported version. A seed default will be used.");
                    return GameProgressData.CreateDefault(); // 미지원 저장은 시드 기본값
                }

                envelope.gameData.Repair();
                return envelope.gameData;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Save load failed. A seed default will be used. {exception.Message}");
                return GameProgressData.CreateDefault(); // 손상 파일은 시드 기본값
            }
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
    }
}
