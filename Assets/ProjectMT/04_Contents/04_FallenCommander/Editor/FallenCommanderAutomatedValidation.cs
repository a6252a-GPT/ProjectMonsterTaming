using System;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    public static class FallenCommanderAutomatedValidation
    {
        private const string RingPrefabPath =
            "Assets/ProjectMT/04_Contents/04_FallenCommander/Art/VFX/" +
            "FallenCommander_Skill_12_Donut.prefab";

        public static void RunAll()
        {
            ValidateBattleFlowReset();
            ValidateRingPrefab();
            ValidateEffectPlacement();
            Debug.Log("Fallen Commander automated validation passed.");
        }

        private static void ValidateBattleFlowReset()
        {
            var flow = new FallenCommanderBattleFlow();
            flow.Begin(80f, 2f);
            Require(flow.IsRunning, "Battle flow did not start.");
            Require(flow.IsStartDelayActive, "Battle start delay was not activated.");
            Require(Mathf.Approximately(flow.RemainingTime, 80f),
                "Battle time limit was not initialized.");

            flow.ReduceTime(10f);
            Require(Mathf.Approximately(flow.RemainingTime, 70f),
                "Battle time reduction is invalid.");
            flow.Reset();
            Require(!flow.IsRunning && !flow.IsFinishing &&
                !flow.IsStartDelayActive,
                "Battle flow state was not reset.");
        }

        private static void ValidateRingPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RingPrefabPath);
            Require(prefab != null, "Ring VFX prefab is missing.");
            Require(prefab.TryGetComponent<FallenCommanderRingVfxView>(out _),
                "Ring VFX view component is missing.");

            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var view = instance.GetComponent<FallenCommanderRingVfxView>();
                view.Configure(3.5f, 10f);
                var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                Require(particles.Length > 0, "Ring VFX has no particle systems.");
                for (var index = 0; index < particles.Length; index++)
                {
                    var shape = particles[index].shape;
                    Require(shape.shapeType == ParticleSystemShapeType.Donut,
                        "Ring particle shape is not Donut.");
                    Require(shape.radius > shape.donutRadius,
                        "Ring particle inner safe area is invalid.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateEffectPlacement()
        {
            var context = new FallenCommanderEffectPlacementContext(
                new Vector3(3f, 0f, 5f),
                Vector3.forward,
                Vector3.zero,
                Vector3.one,
                null);
            var placement = FallenCommanderEffectPlacementResolver.Resolve(
                null,
                FallenCommanderEffectStage.Resolve,
                context);
            Require(IsFinite(placement.Position) && IsFinite(placement.Scale),
                "Effect placement produced a non-finite value.");
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                float.IsFinite(value.y) &&
                float.IsFinite(value.z);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
