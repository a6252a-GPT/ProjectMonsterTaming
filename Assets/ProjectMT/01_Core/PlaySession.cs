using UnityEngine;

namespace ProjectMT.Core
{
    public static class PlaySession // 플레이 종료 중 라이프사이클 부작용 차단
    {
        public static bool IsEnding { get; private set; }
        public static bool CanMutateWorld => Application.isPlaying && !IsEnding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            IsEnding = false;
            Application.wantsToQuit -= HandleWantsToQuit;
            Application.wantsToQuit += HandleWantsToQuit;
            Application.quitting -= HandleQuitting;
            Application.quitting += HandleQuitting;
        }

        private static bool HandleWantsToQuit()
        {
            IsEnding = true;
            MagicaClothActivation.DisableAllIfManagerAlive();
            return true;
        }

        private static void HandleQuitting()
        {
            IsEnding = true;
        }
    }
}
