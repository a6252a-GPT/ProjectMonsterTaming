#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools
{
    // Unity 6000.3.15f1 + 임베디드 패키지(Packages 폴더 안 hera-agent-unity 등) 조합에서,
    // 종료 시 저장된 Library/PackageManager 캐시를 다음 실행 때 복원하며 "live-verify"하는 단계가
    // Packages 폴더를 못 찾는다고 오판해 즉시 크래시하는 에디터 버그가 있다.
    // 이 캐시가 아예 없으면 그 복원 단계 자체를 타지 않고 매니페스트 기준으로 새로 해석해 정상 동작한다.
    // 그래서 에디터가 뜰 때마다(도메인 리로드 시점) 캐시를 미리 지워, 다음 실행이 항상 "캐시 없음" 상태로
    // 시작하도록 만든다.
    [InitializeOnLoad]
    internal static class PackageManagerLaunchCacheGuard
    {
        private static readonly string[] StaleCacheFileNames =
        {
            "ProjectCache",
            "ProjectCache.md5",
            "projectResolution.json"
        };

        static PackageManagerLaunchCacheGuard()
        {
            ClearStaleCache();
        }

        private static void ClearStaleCache()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return;
            }

            var cacheFolder = Path.Combine(projectRoot, "Library", "PackageManager");
            if (!Directory.Exists(cacheFolder))
            {
                return;
            }

            var clearedAny = false;
            foreach (var fileName in StaleCacheFileNames)
            {
                var filePath = Path.Combine(cacheFolder, fileName);
                if (!File.Exists(filePath))
                {
                    continue;
                }

                File.Delete(filePath);
                clearedAny = true;
            }

            if (clearedAny)
            {
                Debug.Log("[PackageManagerLaunchCacheGuard] Stale Package Manager resolution cache cleared.");
            }
        }
    }
}
#endif
