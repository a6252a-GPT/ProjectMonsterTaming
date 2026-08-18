using System;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.GameData;
using UnityEngine;

namespace ProjectMT.Shared.CommanderSkill
{
    public interface ICommanderSkillContentBridge // 콘텐츠와 공용 스킬 런타임 사이의 최소 연결 계약
    {
        void Configure(
            IGameProgressService progress,
            CombatWorld world,
            Transform castOrigin,
            Func<bool> isInputBlocked,
            Func<float> damageMultiplier = null);

        void Shutdown();
    }

    public static class CommanderSkillContentBridgeLocator
    {
        public static ICommanderSkillContentBridge Find(Component owner)
        {
            if (owner == null)
            {
                return null;
            }

            var behaviours = owner.GetComponentsInChildren<MonoBehaviour>(true);
            for (var index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is ICommanderSkillContentBridge bridge)
                {
                    return bridge;
                }
            }

            return null;
        }
    }
}
