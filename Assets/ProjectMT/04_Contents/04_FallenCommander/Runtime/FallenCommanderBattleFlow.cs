using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public sealed class FallenCommanderBattleFlow
    {
        public bool IsRunning { get; private set; }
        public bool IsFinishing { get; private set; }
        public bool IsStartDelayActive { get; private set; }
        public float StartDelayRemaining { get; private set; }
        public float RemainingTime { get; private set; }

        // 전투 시작 상태 초기화
        public void Begin(float timeLimitSeconds, float startDelaySeconds)
        {
            RemainingTime = Mathf.Max(0f, timeLimitSeconds);
            StartDelayRemaining = Mathf.Max(0f, startDelaySeconds);
            IsStartDelayActive = StartDelayRemaining > 0f;
            IsFinishing = false;
            IsRunning = true;
        }

        // 전투 준비시간 감소
        public bool TickStartDelay(float deltaTime)
        {
            if (!IsRunning || !IsStartDelayActive)
            {
                return false;
            }

            StartDelayRemaining = Mathf.Max(0f, StartDelayRemaining - deltaTime);
            if (StartDelayRemaining > 0f)
            {
                return false;
            }

            IsStartDelayActive = false;
            return true;
        }

        public bool TickTimeLimit(float deltaTime)
        {
            if (!IsRunning || IsFinishing || IsStartDelayActive)
            {
                return false;
            }

            RemainingTime = Mathf.Max(0f, RemainingTime - deltaTime);
            return RemainingTime <= 0f;
        }

        public bool ReduceTime(float seconds)
        {
            if (!IsRunning || IsFinishing)
            {
                return false;
            }

            RemainingTime = Mathf.Max(0f, RemainingTime - Mathf.Max(0f, seconds));
            return RemainingTime <= 0f;
        }

        public bool TryBeginFinishing()
        {
            if (!IsRunning || IsFinishing)
            {
                return false;
            }

            IsFinishing = true;
            IsRunning = false;
            return true;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void Reset()
        {
            IsRunning = false;
            IsFinishing = false;
            IsStartDelayActive = false;
            StartDelayRemaining = 0f;
            RemainingTime = 0f;
        }
    }
}
