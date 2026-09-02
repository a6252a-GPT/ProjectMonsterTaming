using System.Collections;
using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed partial class CombatWorld
    {
        public GameObject RentMonsterObject(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            var instance = poolScope?.Rent(prefab, position, rotation, parent ?? transform);
            MonsterBasicAttackVfxPlayback.ApplyBrightnessScale(instance, monsterVfxBrightnessScale);
            return instance;
        }

        public void ReturnMonsterObject(GameObject instance)
        {
            MonsterBasicAttackVfxPlayback.RestoreBrightness(instance);
            MonsterBasicAttackVfxPlayback.StopAndClear(instance);
            poolScope?.Return(instance);
        }

        public void ScheduleMonsterObjectReturn(GameObject instance, float delay)
        {
            if (instance != null)
            {
                StartCoroutine(ReturnMonsterObjectAfter(instance, delay));
            }
        }

        public void ReturnProjectile(GameObject projectile)
        {
            MonsterBasicAttackVfxPlayback.RestoreBrightness(projectile);
            poolScope?.Return(projectile);
        }

        private IEnumerator ReturnMonsterObjectAfter(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, delay));
            ReturnMonsterObject(instance);
        }
    }
}
