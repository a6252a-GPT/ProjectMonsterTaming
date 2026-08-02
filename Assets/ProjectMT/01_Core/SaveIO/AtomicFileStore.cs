using System;
using System.IO;
using System.Threading.Tasks;

namespace ProjectMT.Core.SaveIO
{
    public sealed class AtomicFileStore : IAtomicFileStore // 안전한 파일 교체 구현
    {
        public async Task<byte[]> ReadAsync(string path)
        {
            if (!File.Exists(path))
            {
                return null; // 첫 실행은 저장 파일 없음
            }

            return await File.ReadAllBytesAsync(path);
        }

        public async Task ReplaceAsync(string path, byte[] bytes)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = path + ".tmp"; // 완성 전 임시 파일
            var backupPath = path + ".bak"; // 교체 중 복구 파일
            await File.WriteAllBytesAsync(temporaryPath, bytes);

            if (!File.Exists(path))
            {
                File.Move(temporaryPath, path); // 첫 저장은 바로 이동
                return;
            }

            try
            {
                File.Replace(temporaryPath, path, backupPath, true); // 기존 파일 원자 교체
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(temporaryPath, path, true); // 미지원 플랫폼 대체 경로
                File.Delete(temporaryPath);
            }
        }
    }
}
