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
        public bool ShouldDeferMonsterBasicAttack(UnitActor caster)
        {
            if (caster == null)
            {
                return false;
            }
            if (activeFocus != null)
            {
                return activeFocus.Caster == caster;
            }
            return activeFocusQueue.Count > 0 && activeFocusQueue[0]?.Caster == caster;
        }

        public void TrackMonsterActiveSkill(UnitActor unit)
        {
            feedbackPlayer?.TrackMonsterActiveSkill(unit);
        }

        public bool RequestMonsterActiveFocus(
            UnitActor caster,
            MonsterActiveSkill skill,
            System.Action commit,
            float commitDelay = 0.24f,
            float totalDuration = 0.72f,
            System.Action begin = null)
        {
            return RequestMonsterActiveFocus(
                caster,
                skill,
                () => caster != null ? caster.Target : null,
                () => true,
                begin,
                () =>
                {
                    commit?.Invoke();
                    return true;
                },
                null,
                null,
                commitDelay,
                totalDuration);
        }

        public bool RequestMonsterActiveFocus(
            UnitActor caster,
            MonsterActiveSkill skill,
            System.Func<UnitActor> targetResolver,
            System.Func<bool> canArm,
            System.Action begin,
            System.Func<bool> commit,
            System.Action cancel,
            System.Func<bool> commitSignal,
            float commitDelay = 0.24f,
            float totalDuration = 0.42f,
            System.Func<bool> completionSignal = null,
            System.Func<float> progressSignal = null)
        {
            if (caster == null || skill == null || commit == null || HasMonsterActiveFocusRequest(caster))
            {
                return false;
            }

            activeFocusQueue.Add(new ActiveFocusRequest(
                caster,
                skill,
                targetResolver,
                canArm,
                begin,
                commit,
                cancel,
                commitSignal,
                completionSignal,
                progressSignal,
                commitDelay,
                totalDuration,
                Time.unscaledTime,
                caster.ActiveFocusPartySlotIndex,
                nextActiveFocusSequence++));
            activeFocusQueue.Sort(ActiveFocusRequest.Compare);
            // 같은 프레임에 준비된 요청을 모두 받은 뒤 다음 CombatWorld Tick에서 안정 정렬합니다.
            return true;
        }

        private void TickMonsterActiveFocus(float unscaledDeltaTime)
        {
            if (activeFocus == null)
            {
                BeginNextMonsterActiveFocus();
                return;
            }
            if (activeFocus.Caster == null || !activeFocus.Caster.IsAlive)
            {
                CompleteMonsterActiveFocus(false, true);
                return;
            }

            var step = Mathf.Clamp(unscaledDeltaTime, 0f, 0.1f);
            if (!activeFocus.Armed)
            {
                activeFocusReadyWait += step;
                if (!TryArmMonsterActiveFocus())
                {
                    if (activeFocusReadyWait >= ActiveFocusRequest.MaxReadyWait)
                    {
                        CompleteMonsterActiveFocus(false, true);
                    }
                    return;
                }
            }

            activeFocusElapsed += step;
            var focusStart = Mathf.Max(0f, activeFocus.CommitDelay - activeFocusPreset.FocusLead);
            if (!activeFocusVisible && !activeFocusCommitted && activeFocusElapsed >= focusStart)
            {
                ShowMonsterActiveFocusPresentation();
            }

            var commitSignalReached = false;
            if (!activeFocusCommitted && activeFocus.CommitSignal != null)
            {
                try
                {
                    commitSignalReached = activeFocus.CommitSignal();
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, activeFocus.Caster);
                }
            }
            if (!activeFocusCommitted &&
                (commitSignalReached || activeFocusElapsed >= activeFocus.CommitDelay))
            {
                if (!activeFocusVisible)
                {
                    ShowMonsterActiveFocusPresentation();
                }

                var committed = false;
                try
                {
                    committed = activeFocus.Commit?.Invoke() == true;
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, activeFocus.Caster);
                }

                if (!committed)
                {
                    CompleteMonsterActiveFocus(false, true);
                    return;
                }

                activeFocusCommitted = true;
                var isAttackFocus = activeFocus.Skill is MonsterAttackActiveSkill;
                if (isAttackFocus)
                {
                    activeFocusCameraReleaseAt = activeFocusElapsed +
                                                 activeFocusPreset.AttackCameraHoldAfterCommitDuration;
                }
                ReleaseMonsterActiveFocusPresentation(
                    false,
                    !isAttackFocus,
                    !isAttackFocus);
            }

            if (activeFocusCommitted &&
                activeFocusCameraReleaseAt >= 0f &&
                activeFocusElapsed >= activeFocusCameraReleaseAt)
            {
                ReleaseMonsterActiveFocusCamera(false);
            }

            if (activeFocusCommitted && activeFocus.CompletionSignal != null)
            {
                var skillCompleted = false;
                try
                {
                    skillCompleted = activeFocus.CompletionSignal();
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, activeFocus.Caster);
                    skillCompleted = true; // 완료 감시 오류가 Focus를 영구 점유하지 않게 한다.
                }

                var presentationFinishedAt = focusStart + activeFocusPreset.MinimumVisibleDuration;
                if (skillCompleted && activeFocusElapsed >= presentationFinishedAt)
                {
                    CompleteMonsterActiveFocus(false, false);
                    return;
                }
            }
            else if (activeFocusCommitted && activeFocusElapsed >= activeFocusResolvedDuration)
            {
                CompleteMonsterActiveFocus(false, false);
            }
        }

        private void BeginNextMonsterActiveFocus()
        {
            while (activeFocusQueue.Count > 0)
            {
                var next = activeFocusQueue[0];
                activeFocusQueue.RemoveAt(0);
                if (next.Caster == null || !next.Caster.IsAlive)
                {
                    next.Cancel?.Invoke();
                    continue;
                }

                activeFocus = next;
                activeFocusElapsed = 0f;
                activeFocusSlowStartedAt = -1f;
                activeFocusCameraReleaseAt = -1f;
                activeFocusReadyWait = 0f;
                activeFocusCommitted = false;
                activeFocusVisible = false;
                var config = MonsterActiveFocusPresentationConfig.Current;
                activeFocusPreset = config != null
                    ? config.ResolvePreset(next.Caster.Presentation.Rarity)
                    : default;
                var focusStart = Mathf.Max(0f, next.CommitDelay - activeFocusPreset.FocusLead);
                activeFocusResolvedDuration = Mathf.Max(
                    next.Duration,
                    focusStart + Mathf.Max(
                        activeFocusPreset.MinimumVisibleDuration,
                        next.Skill is MonsterAttackActiveSkill && next.ProgressSignal == null
                            ? activeFocusPreset.OtherUnitSlowTotalDuration
                            : 0f));
                TryArmMonsterActiveFocus();
                return;
            }
            activeFocus = null;
            activeFocusResolvedDuration = 0f;
        }

        private bool TryArmMonsterActiveFocus()
        {
            if (activeFocus == null || activeFocus.Armed)
            {
                return activeFocus?.Armed == true;
            }
            if (activeFocus.CanArm != null && !activeFocus.CanArm())
            {
                return false;
            }

            try
            {
                activeFocus.Begin?.Invoke();
                activeFocus.Armed = true;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, activeFocus.Caster);
                return false;
            }

            var focusStart = Mathf.Max(0f, activeFocus.CommitDelay - activeFocusPreset.FocusLead);
            if (focusStart <= 0f)
            {
                ShowMonsterActiveFocusPresentation();
            }
            return true;
        }

        private void ShowMonsterActiveFocusPresentation()
        {
            if (activeFocus == null || activeFocusVisible)
            {
                return;
            }

            if (activeFocusPresenter == null)
            {
                var host = feedbackPlayer != null ? feedbackPlayer.gameObject : gameObject;
                var prefab = MonsterActiveFocusPresentationConfig.Current?.PresenterPrefab;
                if (prefab != null)
                {
                    activeFocusPresenter = Instantiate(prefab, host.transform);
                    activeFocusPresenter.name = "MonsterActiveFocusHud";
                }
                else
                {
                    activeFocusPresenter = host.GetComponent<MonsterActiveFocusPresenter>() ??
                                           host.AddComponent<MonsterActiveFocusPresenter>();
                }
            }

            var target = activeFocus.TargetResolver?.Invoke();
            var camera = activeFocusCamera?.WorldCamera;
            activeFocusPresenter.Show(
                activeFocus.Caster,
                target,
                activeFocus.Skill,
                activeFocusPreset,
                camera);
            activeFocusCamera?.BeginMonsterActiveFocus(activeFocus.Caster, target, activeFocusPreset);
            activeFocusSlowStartedAt = activeFocusElapsed;
            var startSfx = MonsterActiveFocusPresentationConfig.Current?.ResolveStartSfx(
                activeFocus.Caster.Presentation.Rarity);
            if (startSfx != null)
            {
                PlayMonsterSfx(startSfx, activeFocus.Caster.transform.position);
            }
            var haloPrefab = MonsterActiveFocusPresentationConfig.Current?.ResolveHaloPrefab(
                activeFocus.Caster.Presentation.Rarity);
            if (haloPrefab != null)
            {
                activeFocusHaloInstance = RentMonsterObject(
                    haloPrefab,
                    activeFocus.Caster.transform.position,
                    activeFocus.Caster.transform.rotation,
                    activeFocus.Caster.transform);
                if (activeFocusHaloInstance != null)
                {
                    activeFocusHaloInstance.transform.localPosition = Vector3.zero;
                    MonsterBasicAttackVfxPlayback.RestartAtOffset(
                        activeFocusHaloInstance,
                        0f,
                        playbackSpeed: 1f);
                }
            }
            activeFocusVisible = true;
        }

        private void ReleaseMonsterActiveFocusPresentation(
            bool immediate,
            bool releaseTimeScale = true,
            bool releaseCamera = true)
        {
            if (releaseTimeScale)
            {
                for (var index = 0; index < units.Count; index++)
                {
                    units[index]?.SetActiveFocusTimeScale(1f);
                }
            }
            activeFocusVisible = false;
            if (activeFocusHaloInstance != null)
            {
                ReturnMonsterObject(activeFocusHaloInstance);
                activeFocusHaloInstance = null;
            }
            if (immediate)
            {
                activeFocusPresenter?.HideImmediate();
                ReleaseMonsterActiveFocusCamera(true);
            }
            else
            {
                activeFocusPresenter?.BeginRelease();
                if (releaseCamera)
                {
                    ReleaseMonsterActiveFocusCamera(false);
                }
            }
        }

        private void ReleaseMonsterActiveFocusCamera(bool immediate)
        {
            if (immediate)
            {
                activeFocusCamera?.ResetMonsterActiveFocus();
                activeFocusCameraReleaseAt = float.PositiveInfinity;
                return;
            }
            if (float.IsPositiveInfinity(activeFocusCameraReleaseAt))
            {
                return;
            }
            activeFocusCamera?.EndMonsterActiveFocus();
            activeFocusCameraReleaseAt = float.PositiveInfinity;
        }

        private void CompleteMonsterActiveFocus(bool clearQueue, bool cancelled = false)
        {
            if (cancelled && activeFocus != null && !activeFocusCommitted)
            {
                activeFocus.Cancel?.Invoke();
            }
            ReleaseMonsterActiveFocusPresentation(clearQueue || cancelled);
            activeFocus = null;
            activeFocusElapsed = 0f;
            activeFocusSlowStartedAt = -1f;
            activeFocusCameraReleaseAt = -1f;
            activeFocusResolvedDuration = 0f;
            activeFocusReadyWait = 0f;
            activeFocusCommitted = false;
            activeFocusPreset = default;
            if (clearQueue)
            {
                for (var index = 0; index < activeFocusQueue.Count; index++)
                {
                    activeFocusQueue[index].Cancel?.Invoke();
                }
                activeFocusQueue.Clear();
            }
            else if (!IsPaused)
            {
                BeginNextMonsterActiveFocus();
            }
        }

        public void CancelMonsterActiveFocus(UnitActor caster)
        {
            if (caster == null)
            {
                return;
            }

            if (activeFocus != null && activeFocus.Caster == caster)
            {
                CompleteMonsterActiveFocus(false, !activeFocusCommitted);
            }

            for (var index = activeFocusQueue.Count - 1; index >= 0; index--)
            {
                if (activeFocusQueue[index].Caster != caster)
                {
                    continue;
                }
                activeFocusQueue[index].Cancel?.Invoke();
                activeFocusQueue.RemoveAt(index);
            }
        }

        public void SetMonsterActiveFocusCamera(IMonsterActiveFocusCamera camera)
        {
            activeFocusCamera = camera;
        }

        public void ClearMonsterActiveFocusCamera(IMonsterActiveFocusCamera camera)
        {
            if (ReferenceEquals(activeFocusCamera, camera))
            {
                activeFocusCamera = null;
            }
        }

        public float GetMonsterActiveFocusTimeScale(UnitActor source)
        {
            if (activeFocus == null || source == null || source == activeFocus.Caster)
            {
                return 1f;
            }
            if (activeFocus.Skill is not MonsterAttackActiveSkill)
            {
                return activeFocusVisible ? activeFocusPreset.OtherUnitTimeScale : 1f;
            }
            if (activeFocusSlowStartedAt < 0f)
            {
                return 1f;
            }

            if (activeFocus.ProgressSignal != null)
            {
                try
                {
                    return ResolveAttackFocusProgressScale(activeFocus.ProgressSignal());
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, activeFocus.Caster);
                }
            }

            return ResolveAttackFocusSlowInScale();
        }

        private float ResolveAttackFocusProgressScale(float progress)
        {
            const float blendFraction = 0.2f; // 전체 스킬의 앞·뒤 20%를 완화 구간으로 사용
            progress = Mathf.Clamp01(progress);
            if (progress < blendFraction)
            {
                var ratio = Mathf.SmoothStep(0f, 1f, progress / blendFraction);
                return Mathf.Lerp(1f, activeFocusPreset.AttackOtherUnitTimeScale, ratio);
            }
            if (progress > 1f - blendFraction)
            {
                var ratio = Mathf.SmoothStep(
                    0f,
                    1f,
                    (progress - (1f - blendFraction)) / blendFraction);
                return Mathf.Lerp(activeFocusPreset.AttackOtherUnitTimeScale, 1f, ratio);
            }
            return activeFocusPreset.AttackOtherUnitTimeScale;
        }

        private float ResolveAttackFocusSlowInScale()
        {
            var slowElapsed = Mathf.Max(0f, activeFocusElapsed - activeFocusSlowStartedAt);
            var slowIn = activeFocusPreset.OtherUnitSlowInDuration;
            if (slowIn > 0f && slowElapsed < slowIn)
            {
                var ratio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(slowElapsed / slowIn));
                return Mathf.Lerp(1f, activeFocusPreset.AttackOtherUnitTimeScale, ratio);
            }
            slowElapsed = Mathf.Max(0f, slowElapsed - slowIn);
            if (slowElapsed < activeFocusPreset.OtherUnitSlowHoldDuration)
            {
                return activeFocusPreset.AttackOtherUnitTimeScale;
            }
            slowElapsed -= activeFocusPreset.OtherUnitSlowHoldDuration;
            var slowOut = activeFocusPreset.OtherUnitSlowOutDuration;
            if (slowOut <= 0f || slowElapsed >= slowOut)
            {
                return 1f;
            }
            var releaseRatio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(slowElapsed / slowOut));
            return Mathf.Lerp(activeFocusPreset.AttackOtherUnitTimeScale, 1f, releaseRatio);
        }

        private bool HasMonsterActiveFocusRequest(UnitActor caster)
        {
            if (activeFocus != null && activeFocus.Caster == caster)
            {
                return true;
            }
            for (var index = 0; index < activeFocusQueue.Count; index++)
            {
                if (activeFocusQueue[index].Caster == caster)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
