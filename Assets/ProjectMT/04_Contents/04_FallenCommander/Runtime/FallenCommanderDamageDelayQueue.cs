using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public sealed class FallenCommanderDamageDelayQueue
    {
        private sealed class PendingAction
        {
            public float RemainingTime;
            public Action Apply;
        }

        private readonly List<PendingAction> pendingActions = new();

        public void Schedule(float delay, Action apply)
        {
            if (apply == null)
            {
                return;
            }

            if (delay <= 0f)
            {
                apply.Invoke();
                return;
            }

            pendingActions.Add(new PendingAction
            {
                RemainingTime = delay,
                Apply = apply
            });
        }

        public void Tick(float deltaTime)
        {
            var safeDeltaTime = Mathf.Max(0f, deltaTime);
            for (var index = pendingActions.Count - 1; index >= 0; index--)
            {
                var pending = pendingActions[index];
                pending.RemainingTime -= safeDeltaTime;
                if (pending.RemainingTime > 0f)
                {
                    continue;
                }

                pendingActions.RemoveAt(index);
                pending.Apply?.Invoke();
            }
        }

        public void Clear()
        {
            pendingActions.Clear();
        }
    }
}
