using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    internal static class FallenCommanderPreviewProjectilePlayer
    {
        public static GameObject Create(
            GameObject prefab,
            Transform parent,
            Vector3 position,
            Vector3 direction,
            float radius,
            ICollection<ParticleSystem> particles)
        {
            var projectile = prefab == null
                ? GameObject.CreatePrimitive(PrimitiveType.Sphere)
                : Object.Instantiate(prefab);
            projectile.name = "[미리보기] 기본 공격 투사체";
            projectile.hideFlags = HideFlags.HideAndDontSave;
            projectile.transform.SetParent(parent, true);
            projectile.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(direction, Vector3.up));
            projectile.transform.localScale *= Mathf.Max(0.1f, radius * 2f);

            foreach (var collider in projectile.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (var behaviour in projectile.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (var particle in projectile.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(0f, false, true, true);
                particle.Play(false);
                particles.Add(particle);
            }

            return projectile;
        }
    }
}
