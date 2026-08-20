using System;
using System.Threading.Tasks;

namespace ProjectMT.Features.Settings
{
    public static class AccountRuntimeBridge // 설정 UI와 AppRoot 계정 작업 경계
    {
        public static event Action LogoutRequested;
        public static Func<Task<bool>> DeleteProgressRequested { get; set; }

        public static void RequestLogout()
        {
            LogoutRequested?.Invoke();
        }

        public static Task<bool> RequestDeleteProgressAsync()
        {
            return DeleteProgressRequested?.Invoke() ?? Task.FromResult(false);
        }
    }
}
