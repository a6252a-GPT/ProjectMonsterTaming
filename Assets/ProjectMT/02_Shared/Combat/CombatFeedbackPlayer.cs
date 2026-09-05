using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatFeedbackPlayer : MonoBehaviour, ICombatFeedbackPlayer // 공용 피격·사망 연출 재생
    {
        [SerializeField] private ScenePoolScope poolScope; // VFX 재사용 창고
        [SerializeField] private GameObject hitVfxPrefab; // 공용 타격 이펙트
        [SerializeField] private CameraImpulseRig cameraImpulse; // 카메라 흔들림 장치
        [SerializeField] private FloatingNumberPresenter floatingNumbers; // 풀링 피해 숫자
        [SerializeField] private WorldHealthBarPresenter worldHealthBars; // 일반 유닛 피격 HP바
        [SerializeField] private SfxPool sfxPool; // 현재 전투 범위 SFX Voice 풀
        [SerializeField] private SfxCue hitSfx; // 일반 피격음
        [SerializeField] private SfxCue deathSfx; // 사망음
        [SerializeField] private SfxCue weakClimaxSfx; // 약한 클라이맥스음
        [SerializeField] private SfxCue strongClimaxSfx; // 강한 클라이맥스음
        [SerializeField, Min(1)] private int maxHitVfxPerFrame = 6; // 프레임당 VFX 상한
        [SerializeField, Min(1)] private int maxCameraImpulsesPerFrame = 1; // 프레임당 흔들림 상한

        private int hitVfxThisFrame;
        private int impulsesThisFrame;
        private float strongestImpulseThisFrame; // 같은 프레임의 더 강한 요청은 승격 허용
        private float nextHitImpulseTime;
        private float strongestRecentHitImpulse;
        private readonly HashSet<int> displaySuppressors = new HashSet<int>();
        private bool requestedFloatingNumbers = true;
        private bool requestedUnitHealthBars = true;
        private float recoilScale = 1f;
        private float actualKnockbackDistanceMultiplier;
        private float actualKnockbackMaxDistance;
        private float actualKnockbackDurationMultiplier = 1f;
        private float lightPostKnockbackStagger;
        private float standardPostKnockbackStagger;
        private float heavyPostKnockbackStagger;

        public bool IsDisplaySuppressed => displaySuppressors.Count > 0;

        public void SetRecoilScale(float scale)
        {
            recoilScale = Mathf.Clamp01(scale);
        }

        public void ConfigureActualKnockback(
            float distanceMultiplier,
            float maxDistance,
            float durationMultiplier,
            float lightStagger = 0f,
            float standardStagger = 0f,
            float heavyStagger = 0f)
        {
            actualKnockbackDistanceMultiplier = Mathf.Clamp(distanceMultiplier, 0f, 1.5f);
            actualKnockbackMaxDistance = Mathf.Clamp(maxDistance, 0f, 0.6f);
            actualKnockbackDurationMultiplier = Mathf.Clamp(durationMultiplier, 0.25f, 1.5f);
            lightPostKnockbackStagger = Mathf.Clamp(lightStagger, 0f, 0.3f);
            standardPostKnockbackStagger = Mathf.Clamp(standardStagger, 0f, 0.3f);
            heavyPostKnockbackStagger = Mathf.Clamp(heavyStagger, 0f, 0.3f);
        }

        private void Awake()
        {
            if (floatingNumbers == null)
            {
                floatingNumbers = GetComponent<FloatingNumberPresenter>();
            }

            if (sfxPool == null)
            {
                sfxPool = GetComponent<SfxPool>();
            }

            if (worldHealthBars == null)
            {
                worldHealthBars = GetComponentInChildren<WorldHealthBarPresenter>(true);
            }
        }

        public void PlayHit(UnitActor target, DamageReport report)
        {
            var source = report.Request.Source;
            var combatProfile = source?.RuntimeAssetSet?.CombatProfile;
            var ranged = combatProfile != null
                ? combatProfile.CombatType == MonsterCombatType.Ranged
                : source != null && source.IsRanged;
            var strength = combatProfile?.ImpactStrength ?? MonsterImpactStrength.Standard;
            var reactionWeight = target?.RuntimeAssetSet?.CombatProfile?.ReactionWeight ??
                                 MonsterReactionWeight.Standard;
            var preset = CombatImpactTuning.Resolve(
                strength,
                reactionWeight,
                ranged,
                report.Request.IsCritical,
                report.Killed);
            var feelOwnsTargetMotion =
                (report.Request.FeedbackFlags & DamageFeedbackFlags.BasicAttackFeelTargetMotion) != 0;
            var direction = source != null && target != null
                ? target.transform.position - source.transform.position
                : Vector3.zero;

            var visualOnlyTargetReaction = target != null && target.Team == UnitTeam.Player;
            if (!visualOnlyTargetReaction)
            {
                target?.ApplyLocalHitStop(preset.TargetHitStop);
            }

            if (!ranged)
            {
                source?.ApplyLocalHitStop(preset.AttackerHitStop);
            }

            var actualKnockbackApplied = false;
            if (!visualOnlyTargetReaction &&
                actualKnockbackDistanceMultiplier > 0f && actualKnockbackMaxDistance > 0f)
            {
                var actualDistance = Mathf.Min(
                    preset.RecoilDistance * recoilScale * actualKnockbackDistanceMultiplier,
                    actualKnockbackMaxDistance);
                actualKnockbackApplied = target != null && target.TryApplyCombatKnockback(
                    direction,
                    actualDistance,
                    preset.RecoilDuration * actualKnockbackDurationMultiplier,
                    ResolvePostKnockbackStagger(strength));
            }

            if (feelOwnsTargetMotion)
            {
                target?.VisualFeedback?.PlayHit(); // FEEL이 Visual 위치·회전·스케일을 소유
            }
            else
            {
                target?.VisualFeedback?.PlayImpact(
                    direction,
                    actualKnockbackApplied ? 0f : preset.RecoilDistance * recoilScale,
                    preset.RecoilHeight * recoilScale,
                    preset.RecoilDuration,
                    visualOnlyTargetReaction ? 0f : preset.TargetHitStop,
                    report.Killed);
            }
            if (!ranged)
            {
                source?.VisualFeedback?.PlayAttackLunge(
                    direction,
                    preset.AttackerLungeDistance * recoilScale,
                    preset.AttackerLungeDuration);
            }
            floatingNumbers?.ShowDamage(target, report);
            worldHealthBars?.ShowDamage(target);
            sfxPool?.Play(SfxEvents.ResolveShared(SfxEvents.Hit, hitSfx,
                target?.RuntimeAssetSet?.FeedbackProfile?.HitReceived?.Sfx != null), report.Request.HitPoint);
            if (poolScope != null && hitVfxPrefab != null && hitVfxThisFrame < maxHitVfxPerFrame)
            {
                hitVfxThisFrame++; // 과도한 동시 연출 제한
                var instance = poolScope.Rent(hitVfxPrefab, report.Request.HitPoint, Quaternion.identity);
                instance?.GetComponent<SeedFeedbackVfx>()?.Play(poolScope, new Color(1f, 0.88f, 0.35f), 0.22f, 0.25f);
            }

            if (!report.Killed && source != null && target != null &&
                source.Team == UnitTeam.Player && target.Team == UnitTeam.Enemy)
            {
                PlayHitImpulse(preset.CameraImpulse); // 플레이어 측 적중만 화면 반응
            }
        }

        public void PlayDeath(UnitActor target, DamageReport report)
        {
            target?.VisualFeedback?.PlayDeath();
            sfxPool?.Play(SfxEvents.ResolveShared(SfxEvents.Death, deathSfx,
                target?.RuntimeAssetSet?.FeedbackProfile?.Death?.Sfx != null), report.Request.HitPoint);
            var source = report.Request.Source;
            if (source != null && target != null && source.Team == UnitTeam.Player && target.Team == UnitTeam.Enemy)
            {
                var strength = source.RuntimeAssetSet?.CombatProfile?.ImpactStrength ??
                               MonsterImpactStrength.Standard;
                var deathImpulse = strength switch
                {
                    MonsterImpactStrength.Light => 0.035f,
                    MonsterImpactStrength.Heavy => 0.075f,
                    _ => 0.055f
                };
                PlayImpulse(deathImpulse); // 사망타의 일반 적중 흔들림을 대체
            }
            else if (source != null && target != null &&
                     source.Team == UnitTeam.Enemy && target.Team == UnitTeam.Player)
            {
                PlayImpulse(0.07f); // 아군 사망은 공격 강도와 무관한 한 번의 경고
            }
        }

        public void PlayClimax(Vector3 position, CombatClimaxStrength strength)
        {
            var isStrong = strength == CombatClimaxStrength.Strong;
            var color = isStrong
                ? new Color(1f, 0.45f, 0.15f)
                : new Color(1f, 0.78f, 0.25f);
            var duration = isStrong ? 0.6f : 0.34f;
            var size = isStrong ? 0.8f : 0.38f;
            var impulse = isStrong ? 0.24f : 0.1f;
            SfxEvents.Play(isStrong ? SfxEvents.Strong : SfxEvents.Weak, sfxPool, position, isStrong ? strongClimaxSfx : weakClimaxSfx);
            if (poolScope != null && hitVfxPrefab != null)
            {
                var instance = poolScope.Rent(hitVfxPrefab, position, Quaternion.identity);
                instance?.GetComponent<SeedFeedbackVfx>()?.Play(poolScope, color, duration, size);
            }

            PlayImpulse(impulse);
        }

        public void PlayDamage(
            Vector3 position,
            float amount,
            FloatingNumberStyle style,
            int mergeKey,
            DamageFeedbackFlags feedbackFlags = DamageFeedbackFlags.None)
        {
            var sizeMultiplier = (feedbackFlags & DamageFeedbackFlags.PassiveEnhancedNumber) != 0 ? 1.2f : 1f;
            var resolvedMergeKey = (feedbackFlags & DamageFeedbackFlags.SeparateFloatingNumber) != 0
                ? 0
                : mergeKey;
            floatingNumbers?.Queue(position, amount, style, resolvedMergeKey, sizeMultiplier); // 비 UnitActor 대상의 확정 피해 표시
            SfxEvents.Play(SfxEvents.Hit, sfxPool, position, hitSfx);
        }

        public void PlayFloatingNumber(Vector3 position, float amount, FloatingNumberStyle style, int mergeKey)
        {
            floatingNumbers?.Queue(position, amount, style, mergeKey);
        }

        public void PlayStatusText(Vector3 position, string text, CombatStatusTextStyle style, int queueKey)
        {
            floatingNumbers?.QueueText(position, text, style, queueKey);
        }

        public bool PlayMonsterCue(SfxCue cue, Vector3 position)
        {
            return sfxPool != null && sfxPool.Play(cue, position);
        }

        public void TrackMonsterActiveSkill(UnitActor target)
        {
            worldHealthBars?.TrackActiveSkill(target);
        }

        public void UntrackUnit(UnitActor target)
        {
            worldHealthBars?.Untrack(target);
        }

        public void SetDisplayOptions(bool showFloatingNumbers, bool showUnitHealthBars)
        {
            requestedFloatingNumbers = showFloatingNumbers;
            requestedUnitHealthBars = showUnitHealthBars;
            ApplyDisplayOptions();
        }

        public void SetDisplaySuppressed(Object owner, bool suppressed)
        {
            if (owner == null)
            {
                return;
            }

            var ownerId = owner.GetInstanceID();
            if (suppressed)
            {
                displaySuppressors.Add(ownerId);
            }
            else
            {
                displaySuppressors.Remove(ownerId);
            }

            ApplyDisplayOptions();
        }

        private void ApplyDisplayOptions()
        {
            var allowed = displaySuppressors.Count == 0;
            floatingNumbers?.SetVisible(allowed && requestedFloatingNumbers);
            worldHealthBars?.SetVisible(allowed && requestedUnitHealthBars);
        }

        private float ResolvePostKnockbackStagger(MonsterImpactStrength strength)
        {
            return strength switch
            {
                MonsterImpactStrength.Light => lightPostKnockbackStagger,
                MonsterImpactStrength.Heavy => heavyPostKnockbackStagger,
                _ => standardPostKnockbackStagger
            };
        }

        private void PlayImpulse(float strength)
        {
            strength = Mathf.Max(0f, strength);
            if (cameraImpulse == null || strength <= 0f)
            {
                return;
            }

            if (impulsesThisFrame >= maxCameraImpulsesPerFrame && strength <= strongestImpulseThisFrame)
            {
                return;
            }

            if (impulsesThisFrame < maxCameraImpulsesPerFrame)
            {
                impulsesThisFrame++;
            }

            strongestImpulseThisFrame = Mathf.Max(strongestImpulseThisFrame, strength);
            cameraImpulse.Impulse(strength);
        }

        private void PlayHitImpulse(float strength)
        {
            strength = Mathf.Max(0f, strength);
            if (strength <= 0f)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now < nextHitImpulseTime && strength <= strongestRecentHitImpulse)
            {
                return; // 다대다 약한 적중이 카메라를 계속 떠는 현상 제한
            }

            strongestRecentHitImpulse = strength;
            nextHitImpulseTime = now + 0.09f;
            PlayImpulse(strength);
        }

        private void LateUpdate()
        {
            hitVfxThisFrame = 0; // 다음 프레임 예산 복구
            impulsesThisFrame = 0; // 다음 프레임 예산 복구
            strongestImpulseThisFrame = 0f;
            if (Time.unscaledTime >= nextHitImpulseTime)
            {
                strongestRecentHitImpulse = 0f;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(ScenePoolScope pool, GameObject hitVfx, CameraImpulseRig impulse)
        {
            poolScope = pool;
            hitVfxPrefab = hitVfx;
            cameraImpulse = impulse;
        }

        public void EditorConfigureExtensions(FloatingNumberPresenter numbers, SfxPool audioPool)
        {
            floatingNumbers = numbers;
            sfxPool = audioPool;
        }

        public void EditorConfigureHealthBars(WorldHealthBarPresenter healthBars)
        {
            worldHealthBars = healthBars;
        }
#endif
    }
}
