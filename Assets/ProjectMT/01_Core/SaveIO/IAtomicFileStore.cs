using System.Threading.Tasks;

namespace ProjectMT.Core.SaveIO
{
    public interface IAtomicFileStore // 저장 형식과 분리된 파일 계약
    {
        Task<byte[]> ReadAsync(string path); // 원본 바이트 읽기
        Task ReplaceAsync(string path, byte[] bytes); // 파일 안전 교체
    }
}
