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
        public static void SetMonsterBasicAttackHitAreasVisible(bool visible)
        {
            showMonsterBasicAttackHitAreas = visible;
            if (visible)
            {
                return;
            }

            var worlds = FindObjectsByType<CombatWorld>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < worlds.Length; index++)
            {
                worlds[index]?.ClearMonsterBasicAttackHitAreas();
            }
        }

        public void SetUnitEmissionBrightnessScale(float scale)
        {
            unitEmissionBrightnessScale = Mathf.Clamp01(scale);
            for (var index = 0; index < units.Count; index++)
            {
                var unit = units[index];
                unit?.VisualFeedback?.SetEmissionBrightnessScale(unitEmissionBrightnessScale);
            }
        }

        public void SetMonsterVfxBrightnessScale(float scale)
        {
            monsterVfxBrightnessScale = float.IsNaN(scale) || float.IsInfinity(scale)
                ? 1f
                : Mathf.Clamp01(scale);
        }

        public void ShowMonsterBasicAttackArea(
            MonsterBasicAttackProfile profile,
            UnitActor source,
            Vector3 origin,
            Vector3 forward,
            Vector3 primaryTarget,
            float attackRange)
        {
            if (!showMonsterBasicAttackHitAreas || profile == null || source == null)
            {
                return;
            }

            var color = source.Team == UnitTeam.Player
                ? new Color(0.1f, 0.9f, 1f, 0.72f)
                : new Color(1f, 0.25f, 0.18f, 0.72f);
            monsterBasicAttackHitAreas.RemoveAll(indicator => indicator == null);
            var indicator = MonsterAttackAreaIndicator.Create(
                transform,
                profile,
                origin,
                forward,
                primaryTarget,
                attackRange,
                color);
            if (indicator != null)
            {
                monsterBasicAttackHitAreas.Add(indicator);
            }
        }

        private void ClearMonsterBasicAttackHitAreas()
        {
            for (var index = 0; index < monsterBasicAttackHitAreas.Count; index++)
            {
                var indicator = monsterBasicAttackHitAreas[index];
                if (indicator == null)
                {
                    continue;
                }

                indicator.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(indicator.gameObject);
                }
                else
                {
                    DestroyImmediate(indicator.gameObject);
                }
            }

            monsterBasicAttackHitAreas.Clear();
        }

        public void PlayMonsterFeedback(
            MonsterFeedbackCue cue,
            MonsterAnimationDriver animationDriver,
            string socketOverride,
            float bodyVfxScale = 1f)
        {
            if (cue == null || !cue.HasAnyFeedback)
            {
                return;
            }

            var socket = animationDriver != null
                ? animationDriver.ResolveSocket(socketOverride)
                : null;
            var position = socket != null ? socket.position : transform.position;
            var rotation = socket != null ? socket.rotation : Quaternion.identity;
            PlayMonsterFeedbackAt(cue, position, rotation, bodyVfxScale);
        }

        public void PlayMonsterFeedbackAt(
            MonsterFeedbackCue cue,
            Vector3 position,
            Quaternion rotation,
            float vfxScale = 1f)
        {
            PlayMonsterFeedbackAt(cue, position, rotation, vfxScale, 0f);
        }

        public void PlayMonsterFeedbackAt(
            MonsterFeedbackCue cue,
            Vector3 position,
            Quaternion rotation,
            float vfxScale,
            float vfxLifetimeOverride)
        {
            var instance = SpawnMonsterFeedbackVfx(cue, position, rotation, null, vfxScale);
            if (instance == null) return;
            var lifetime = vfxLifetimeOverride > 0f ? vfxLifetimeOverride : cue.VfxLifetime;
            StartCoroutine(ReturnMonsterObjectAfter(instance, lifetime));
        }

        public GameObject SpawnMonsterFeedbackVfx(
            MonsterFeedbackCue cue,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            float vfxScale = 1f)
        {
            if (cue == null || !cue.HasAnyFeedback) return null;
            position += rotation * cue.LocalPosition;
            rotation *= cue.LocalRotation;
            PlayMonsterSfx(cue.Sfx, position);
            if (cue.VfxPrefab == null) return null;

            var frame = Time.frameCount;
            if (monsterVfxFrame != frame)
            {
                monsterVfxFrame = frame;
                monsterVfxCount = 0;
            }
            if (monsterVfxCount >= Mathf.Max(1, maxMonsterVfxPerFrame)) return null;

            monsterVfxCount++;
            var instance = RentMonsterObject(cue.VfxPrefab, position, rotation, parent);
            if (instance == null) return null;
            var scale = cue.Scale * Mathf.Max(0.01f, vfxScale);
            instance.transform.localScale = cue.VfxPrefab.transform.localScale * scale;
            MonsterBasicAttackVfxPlayback.RestartAtOffset(instance, 0f, playbackSpeed: 1f);
            return instance;
        }

        public GameObject SpawnMonsterActiveVfx(
            MonsterFeedbackCue cue,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            float vfxScale = 1f,
            float playbackSpeed = 1f)
        {
            if (cue == null || !cue.HasAnyFeedback) return null;
            position += rotation * cue.LocalPosition;
            rotation *= cue.LocalRotation;
            PlayMonsterSfx(cue.Sfx, position);
            if (cue.VfxPrefab == null) return null;

            var frame = Time.frameCount;
            if (monsterActiveVfxFrame != frame)
            {
                monsterActiveVfxFrame = frame;
                monsterActiveVfxCount = 0;
            }
            if (monsterActiveVfxCount >= Mathf.Max(1, maxMonsterActiveVfxPerFrame)) return null;

            monsterActiveVfxCount++;
            var instance = RentMonsterObject(cue.VfxPrefab, position, rotation, parent);
            if (instance == null) return null;
            var scale = cue.Scale * Mathf.Max(0.01f, vfxScale);
            instance.transform.localScale = cue.VfxPrefab.transform.localScale * scale;
            MonsterBasicAttackVfxPlayback.RestartAtOffset(
                instance,
                0f,
                playbackSpeed: Mathf.Max(0.05f, playbackSpeed));
            return instance;
        }

        public GameObject SpawnBasicAttackVfx(
            MonsterBasicAttackVfxBinding binding,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            float bodyVfxScale = 1f,
            float playbackSpeedMultiplier = 1f)
        {
            if (binding == null || !binding.IsAssigned)
            {
                return null;
            }

            var frame = Time.frameCount;
            if (monsterVfxFrame != frame)
            {
                monsterVfxFrame = frame;
                monsterVfxCount = 0;
            }
            if (monsterVfxCount >= Mathf.Max(1, maxMonsterVfxPerFrame))
            {
                return null;
            }

            monsterVfxCount++;
            position += rotation * binding.LocalPosition;
            rotation *= binding.LocalRotation;
            var instance = RentMonsterObject(binding.Prefab, position, rotation, parent);
            if (instance != null)
            {
                MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                    instance,
                    binding.Prefab.transform.localScale *
                    binding.Scale * Mathf.Max(0.01f, bodyVfxScale));
                MonsterBasicAttackVfxPlayback.RestartAtOffset(
                    instance,
                    binding.PlaybackOffset,
                    playbackSpeed: binding.PlaybackSpeed *
                                   Mathf.Max(0.05f, playbackSpeedMultiplier));
            }
            return instance;
        }

        public bool WillPlayBasicAttackFeelTargetMotion(
            BasicAttackFeelCue cue,
            GameObject target,
            float intensity = 1f)
        {
            if (cue == null || !cue.HasFeel || target == null || poolScope == null)
            {
                return false;
            }

            RefreshMonsterFeelFrameBudget();
            if (monsterFeelCount >= Mathf.Max(1, maxMonsterFeelPerFrame))
            {
                return false;
            }

            var runtime = cue.Prefab.GetComponent(typeof(IBasicAttackFeelRuntime)) as IBasicAttackFeelRuntime;
            return runtime?.IsBasicAttackFeelConfigured == true &&
                   runtime.HasBasicAttackTargetMotion(intensity);
        }

        public void PlayBasicAttackFeelAt(
            BasicAttackFeelCue cue,
            Vector3 position,
            Quaternion rotation,
            float bodyScale = 1f,
            GameObject target = null,
            float intensity = 1f)
        {
            if (cue == null || !cue.HasFeel)
            {
                return;
            }

            RefreshMonsterFeelFrameBudget();

            if (monsterFeelCount >= Mathf.Max(1, maxMonsterFeelPerFrame))
            {
                return;
            }

            monsterFeelCount++;
            position += rotation * cue.LocalPosition;
            rotation *= cue.LocalRotation;
            var instance = RentMonsterObject(cue.Prefab, position, rotation);
            if (instance == null)
            {
                return;
            }

            instance.transform.localScale = cue.Prefab.transform.localScale *
                cue.Scale * Mathf.Max(0.01f, bodyScale);
            PlayBasicAttackFeelRuntime(instance, target, intensity);
            StartCoroutine(ReturnMonsterObjectAfter(instance, cue.Lifetime));
        }

        private void RefreshMonsterFeelFrameBudget()
        {
            var frame = Time.frameCount;
            if (monsterFeelFrame == frame)
            {
                return;
            }
            monsterFeelFrame = frame;
            monsterFeelCount = 0;
        }

        public void PlayBasicAttackFeelRuntime(
            GameObject instance,
            GameObject target = null,
            float intensity = 1f)
        {
            if (instance == null)
            {
                return;
            }

            var feelRuntime = instance.GetComponent(typeof(IBasicAttackFeelRuntime)) as IBasicAttackFeelRuntime;
            feelRuntime?.PlayBasicAttackFeel(
                instance.transform.position,
                target,
                intensity,
                BasicAttackFeelPlaybackOptions.None); // 실전 전역 카메라·히트스탑은 공용 전투 계층만 소유
        }

        public void PlayMonsterSfx(SfxCue cue, Vector3 position)
        {
            feedbackPlayer?.PlayMonsterCue(cue, position);
        }

        public void PlayClimax(Vector3 position, CombatClimaxStrength strength)
        {
            feedbackPlayer?.PlayClimax(position, strength);
        }
    }
}
