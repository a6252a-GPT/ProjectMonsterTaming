using System.Collections.Generic;
using ProjectMT.Features.Expedition;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MainBattleCameraController : MonoBehaviour, IMonsterActiveFocusCamera // 메인전투 원근 구도·추적
    {
        [Header("References")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Collider framingGround;
        [SerializeField] private ExpeditionController expedition;

        [Header("Perspective Framing")]
        [SerializeField, Range(20f, 50f)] private float fieldOfView = 45f;
        [SerializeField, Min(5f)] private float distance = 18f;
        [SerializeField, Range(25f, 75f)] private float pitch = 45f;
        [SerializeField, Range(-180f, 180f)] private float yaw = -45f;
        [SerializeField] private Vector3 focusOffset = new Vector3(0f, 1f, 0f);
        [SerializeField, Min(0.01f)] private float nearClipPlane = 0.3f;
        [SerializeField, Min(10f)] private float farClipPlane = 500f;

        [Header("Battle Follow")]
        [SerializeField] private bool followBattleCenter; // 기준 구도는 고정, 필요할 때만 추적
        [SerializeField, Range(0f, 1f)] private float followWeight = 0.35f;
        [SerializeField, Min(0f)] private float maxFollowOffset = 1.8f;
        [SerializeField, Min(0.01f)] private float followSmoothTime = 0.85f;
        [SerializeField, Min(0f)] private float followMaxSpeed = 5f;
        [SerializeField] private bool disableGeneratedStageCameras = true;

        private readonly List<UnitActor> activeUnits = new List<UnitActor>(16);
        private Vector3 baseFocus;
        private Vector3 currentFocus;
        private Vector3 focusVelocity;
        private bool runtimeReady;
        private CombatWorld registeredCombatWorld;
        private UnitActor activeFocusCaster;
        private UnitActor activeFocusTarget;
        private MonsterActiveFocusPreset activeFocusPreset;
        private float activeFocusBlend;
        private bool activeFocusRequested;

        public Camera WorldCamera => worldCamera;

        private void OnEnable()
        {
            ResolveReferences();
            RefreshBaseFocus();
            currentFocus = baseFocus;
            focusVelocity = Vector3.zero;
            runtimeReady = Application.isPlaying;
            ApplyCameraSettings();
            ApplyPose(currentFocus, !Application.isPlaying);
            if (Application.isPlaying && disableGeneratedStageCameras)
            {
                DisableGeneratedStageCameras();
            }
            if (Application.isPlaying)
            {
                registeredCombatWorld = expedition != null
                    ? expedition.CombatWorld
                    : FindFirstObjectByType<CombatWorld>();
                registeredCombatWorld?.SetMonsterActiveFocusCamera(this);
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || !runtimeReady || worldCamera == null)
            {
                return;
            }

            RefreshBaseFocus();
            var unscaledDeltaTime = Time.unscaledDeltaTime;
            var blendDuration = activeFocusRequested
                ? activeFocusPreset.FadeIn
                : activeFocusPreset.CameraReleaseDuration;
            activeFocusBlend = Mathf.MoveTowards(
                activeFocusBlend,
                activeFocusRequested ? 1f : 0f,
                unscaledDeltaTime / Mathf.Max(0.05f, blendDuration));
            var desiredFocus = ResolveDesiredFocus();
            var smoothTime = activeFocusBlend > 0.001f
                ? Mathf.Min(activeFocusRequested ? 0.16f : 0.28f, followSmoothTime)
                : followSmoothTime;
            currentFocus = Vector3.SmoothDamp(
                currentFocus,
                desiredFocus,
                ref focusVelocity,
                Mathf.Max(0.01f, smoothTime),
                followMaxSpeed <= 0f ? Mathf.Infinity : followMaxSpeed,
                unscaledDeltaTime);
            if (!activeFocusRequested && activeFocusBlend <= 0f)
            {
                activeFocusCaster = null;
                activeFocusTarget = null;
            }
            ApplyCameraSettings();
            ApplyPose(currentFocus, false);
        }

        private void OnDisable()
        {
            registeredCombatWorld?.ClearMonsterActiveFocusCamera(this);
            registeredCombatWorld = null;
            runtimeReady = false;
            focusVelocity = Vector3.zero;
            ResetMonsterActiveFocus();
            activeUnits.Clear();
        }

        private Vector3 ResolveDesiredFocus()
        {
            var battleFocus = ResolveBattleFocus();
            if (activeFocusBlend <= 0f || activeFocusCaster == null || !activeFocusCaster.IsAlive)
            {
                return battleFocus;
            }

            var casterPosition = activeFocusCaster.transform.position;
            var focusPosition = activeFocusTarget != null && activeFocusTarget.IsAlive
                ? Vector3.Lerp(casterPosition, activeFocusTarget.transform.position, 0.2f)
                : casterPosition;
            focusPosition.y = battleFocus.y;
            var offset = focusPosition - battleFocus;
            offset.y = 0f;
            offset = Vector3.ClampMagnitude(offset, activeFocusPreset.CameraMaxOffset);
            return battleFocus + offset * activeFocusBlend;
        }

        private Vector3 ResolveBattleFocus()
        {
            if (!followBattleCenter || expedition == null || expedition.IsFormationPlacementActive)
            {
                return baseFocus;
            }

            expedition.CollectActiveUnits(activeUnits);
            if (activeUnits.Count == 0)
            {
                return baseFocus;
            }

            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var index = 0; index < activeUnits.Count; index++)
            {
                var actor = activeUnits[index];
                if (actor == null)
                {
                    continue;
                }

                var position = actor.transform.position;
                minimum = Vector2.Min(minimum, new Vector2(position.x, position.z));
                maximum = Vector2.Max(maximum, new Vector2(position.x, position.z));
            }

            if (!float.IsFinite(minimum.x) || !float.IsFinite(minimum.y))
            {
                return baseFocus;
            }

            var battleCenter = (minimum + maximum) * 0.5f;
            var offset = new Vector2(battleCenter.x - baseFocus.x, battleCenter.y - baseFocus.z) * followWeight;
            offset = Vector2.ClampMagnitude(offset, maxFollowOffset);
            return baseFocus + new Vector3(offset.x, 0f, offset.y);
        }

        private void RefreshBaseFocus()
        {
            var center = framingGround != null ? framingGround.bounds.center : Vector3.zero;
            baseFocus = center + focusOffset;
        }

        private void ApplyCameraSettings()
        {
            if (worldCamera == null)
            {
                return;
            }

            worldCamera.orthographic = false;
            worldCamera.fieldOfView = Mathf.Clamp(
                fieldOfView + activeFocusPreset.CameraFovDelta * activeFocusBlend,
                20f,
                50f);
            worldCamera.nearClipPlane = Mathf.Max(0.01f, nearClipPlane);
            worldCamera.farClipPlane = Mathf.Max(worldCamera.nearClipPlane + 10f, farClipPlane);
        }

        private void ApplyPose(Vector3 focus, bool resetCameraLocalPose)
        {
            if (worldCamera == null)
            {
                return;
            }

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            if (resetCameraLocalPose)
            {
                worldCamera.transform.localPosition = Vector3.zero;
                worldCamera.transform.localRotation = Quaternion.identity;
                worldCamera.GetComponent<CameraImpulseRig>()?.RebaseOrigin();
            }
        }

        private void ResolveReferences()
        {
            if (worldCamera == null)
            {
                worldCamera = GetComponentInChildren<Camera>(true);
            }
        }

        public void BeginMonsterActiveFocus(
            UnitActor caster,
            UnitActor target,
            MonsterActiveFocusPreset preset)
        {
            if (caster == null)
            {
                return;
            }
            activeFocusCaster = caster;
            activeFocusTarget = target;
            activeFocusPreset = preset;
            activeFocusRequested = true;
        }

        public void EndMonsterActiveFocus()
        {
            activeFocusRequested = false;
        }

        public void ResetMonsterActiveFocus()
        {
            activeFocusRequested = false;
            activeFocusBlend = 0f;
            activeFocusCaster = null;
            activeFocusTarget = null;
            activeFocusPreset = default;
            if (worldCamera != null)
            {
                ApplyCameraSettings();
            }
        }

        private void DisableGeneratedStageCameras()
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (camera == null || camera == worldCamera || camera.targetTexture != null)
                {
                    continue;
                }

                var rootName = camera.transform.root.name;
                if (!rootName.StartsWith("PF_StageMap_", System.StringComparison.Ordinal))
                {
                    continue;
                }

                camera.enabled = false; // 생성 맵의 데모 카메라 제외
                var listener = camera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            fieldOfView = Mathf.Clamp(fieldOfView, 20f, 50f);
            distance = Mathf.Max(5f, distance);
            nearClipPlane = Mathf.Max(0.01f, nearClipPlane);
            farClipPlane = Mathf.Max(nearClipPlane + 10f, farClipPlane);
            followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
            followMaxSpeed = Mathf.Max(0f, followMaxSpeed);
            maxFollowOffset = Mathf.Max(0f, maxFollowOffset);
            ResolveReferences();
            RefreshBaseFocus();
            if (!Application.isPlaying)
            {
                currentFocus = baseFocus;
                ApplyCameraSettings();
                ApplyPose(currentFocus, true);
            }
        }

        public void EditorConfigure(Camera camera, Collider ground, ExpeditionController expeditionController)
        {
            worldCamera = camera;
            framingGround = ground;
            expedition = expeditionController;
            RefreshBaseFocus();
            currentFocus = baseFocus;
            ApplyCameraSettings();
            ApplyPose(currentFocus, true);
        }
#endif
    }
}
