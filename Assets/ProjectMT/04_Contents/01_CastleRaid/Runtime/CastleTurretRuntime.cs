using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    public sealed class CastleTurretRuntime : MonoBehaviour // 생성 포탑의 조준·장전·발사 실행기
    {
        private const float TargetRefreshInterval = 0.18f;

        private CastleRaidController controller;
        private CastleTarget structure;
        private CastleTurretVisual visual;
        private CastleTurretAttackProfile profile;
        private CastleTurretAttackProfileData attack;
        private CastleAssaultUnit currentTarget;
        private Quaternion baseYawRotation;
        private Quaternion basePitchRotation;
        private Vector3 basePitchPosition;
        private float currentYaw;
        private float currentPitch;
        private float cooldownRemaining;
        private int firedProjectileCount;
        private int pendingProjectileCount;
        private float nextVolleyTime;
        private float nextTargetRefreshTime;
        private float recoilStartedAt = float.NegativeInfinity;

        public CastleTurretAttackProfile Profile => profile;
        public CastleTarget Structure => structure;
        public CastleAssaultUnit CurrentTarget => currentTarget;
        public int ProjectilesFired { get; private set; }
        public int HitCount { get; private set; }
        public float RequestedDamage { get; private set; }

        public void Configure(
            CastleRaidController raidController,
            CastleTarget linkedStructure,
            CastleTurretVisual turretVisual,
            CastleTurretAttackProfile attackProfile)
        {
            controller = raidController != null ? raidController : throw new System.ArgumentNullException(nameof(raidController));
            structure = linkedStructure != null ? linkedStructure : throw new System.ArgumentNullException(nameof(linkedStructure));
            visual = turretVisual != null ? turretVisual : throw new System.ArgumentNullException(nameof(turretVisual));
            profile = attackProfile != null && attackProfile.IsValid
                ? attackProfile
                : throw new System.ArgumentException("유효한 포탑 공격 프로필이 필요합니다.", nameof(attackProfile));
            attack = profile.Data;
            baseYawRotation = visual.YawPivot.localRotation;
            basePitchRotation = visual.PitchPivot.localRotation;
            basePitchPosition = visual.PitchPivot.localPosition;
            currentYaw = 0f;
            currentPitch = 0f;
            cooldownRemaining = attack.cooldown * ResolveInitialCooldownRatio(gameObject.name);
            firedProjectileCount = 0;
            pendingProjectileCount = 0;
            nextTargetRefreshTime = Time.time + TargetRefreshInterval * ResolveInitialCooldownRatio(gameObject.name);
            ProjectilesFired = 0;
            HitCount = 0;
            RequestedDamage = 0f;
            visual.SetAllLoadedProjectilesVisible(true);
        }

        private void Update()
        {
            if (controller == null || structure == null || visual == null || profile == null ||
                !controller.IsRunning || !structure.IsAlive)
            {
                StopPresentation();
                return;
            }

            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            RestoreLoadedProjectilesWhenReady();
            if (Time.time >= nextTargetRefreshTime || currentTarget != null && !currentTarget.IsAlive)
            {
                RefreshTarget();
            }

            if (currentTarget == null)
            {
                pendingProjectileCount = 0;
                ApplyAimAndRecoil(null, Time.deltaTime);
                return;
            }

            ApplyAimAndRecoil(currentTarget, Time.deltaTime);
            if (pendingProjectileCount > 0)
            {
                if (Time.time >= nextVolleyTime)
                {
                    FireNextVolley();
                }

                return;
            }

            if (cooldownRemaining <= 0f && IsAimedAt(currentTarget))
            {
                BeginAttackSequence();
            }
        }

        private void RefreshTarget()
        {
            nextTargetRefreshTime = Time.time + TargetRefreshInterval;
            currentTarget = controller.FindTurretTarget(
                visual.Muzzle.position,
                attack.searchRange,
                attack.targetPriority,
                attack.projectileHitRadius);
        }

        private void BeginAttackSequence()
        {
            cooldownRemaining = attack.cooldown;
            firedProjectileCount = 0;
            pendingProjectileCount = attack.projectileCount;
            FireNextVolley();
        }

        private void FireNextVolley()
        {
            if (pendingProjectileCount <= 0 || currentTarget == null || !currentTarget.IsAlive)
            {
                pendingProjectileCount = 0;
                return;
            }

            var volleySize = attack.fireSequentially
                ? Mathf.Clamp(attack.projectileVolleySize, 1, pendingProjectileCount)
                : pendingProjectileCount;
            var muzzle = visual.Muzzle;
            var targetPoint = currentTarget.transform.position + Vector3.up * attack.targetAimHeight;
            if (controller.IsTurretLineBlocked(muzzle.position, targetPoint, attack.projectileHitRadius))
            {
                pendingProjectileCount = 0;
                currentTarget = null;
                return;
            }

            var baseDirection = (targetPoint - muzzle.position).normalized;
            for (var index = 0; index < volleySize; index++)
            {
                var spread = volleySize <= 1
                    ? 0f
                    : Mathf.Lerp(-attack.spreadAngle * 0.5f, attack.spreadAngle * 0.5f, index / (float)(volleySize - 1));
                var direction = Quaternion.AngleAxis(spread, Vector3.up) * baseDirection;
                var projectileObject = controller.RentTurretObject(
                    attack.projectilePrefab,
                    muzzle.position,
                    Quaternion.LookRotation(direction, Vector3.up));
                if (projectileObject == null)
                {
                    continue;
                }

                projectileObject.transform.localScale = Vector3.one * attack.projectileScale;
                var projectile = projectileObject.GetComponent<CastleTurretProjectile>();
                if (projectile == null)
                {
                    projectile = projectileObject.AddComponent<CastleTurretProjectile>();
                }

                projectile.Configure(controller, this, profile, direction, targetPoint);
                visual.SetLoadedProjectileVisible(firedProjectileCount, false);
                firedProjectileCount++;
                ProjectilesFired++;
            }

            pendingProjectileCount -= volleySize;
            visual.PlayMuzzleVfx();
            controller.PlayTurretCue(attack.fireSfx, muzzle.position);
            recoilStartedAt = Time.time;
            if (pendingProjectileCount > 0)
            {
                nextVolleyTime = Time.time + Mathf.Max(0.01f, attack.projectileFireDelay);
            }
        }

        private void ApplyAimAndRecoil(CastleAssaultUnit target, float deltaTime)
        {
            var desiredYaw = currentYaw;
            var desiredPitch = currentPitch;
            if (target != null)
            {
                var targetPoint = target.transform.position + Vector3.up * attack.targetAimHeight;
                var yawDirection = visual.YawPivot.parent.InverseTransformDirection(targetPoint - visual.YawPivot.position);
                desiredYaw = Mathf.Atan2(yawDirection.x, yawDirection.z) * Mathf.Rad2Deg;

                var pitchDirection = visual.PitchPivot.parent.InverseTransformDirection(targetPoint - visual.PitchPivot.position);
                var horizontal = Mathf.Sqrt(pitchDirection.x * pitchDirection.x + pitchDirection.z * pitchDirection.z);
                desiredPitch = -Mathf.Atan2(pitchDirection.y, Mathf.Max(0.001f, horizontal)) * Mathf.Rad2Deg;
            }

            currentYaw = Mathf.MoveTowardsAngle(currentYaw, desiredYaw, attack.headTurnSpeed * deltaTime);
            currentPitch = Mathf.MoveTowardsAngle(currentPitch, desiredPitch, attack.headTurnSpeed * deltaTime);
            EvaluateRecoil(out var recoilDistance, out var recoilTilt);
            visual.YawPivot.localRotation = baseYawRotation * Quaternion.Euler(0f, currentYaw, 0f);
            visual.PitchPivot.localRotation = basePitchRotation * Quaternion.Euler(currentPitch - recoilTilt, 0f, 0f);
            visual.PitchPivot.localPosition = basePitchPosition + Vector3.back * recoilDistance;
        }

        private void EvaluateRecoil(out float distance, out float tilt)
        {
            distance = 0f;
            tilt = 0f;
            var elapsed = Time.time - recoilStartedAt;
            if (elapsed < 0f)
            {
                return;
            }

            if (elapsed <= attack.recoilKickDuration)
            {
                var ratio = Mathf.SmoothStep(0f, 1f, elapsed / attack.recoilKickDuration);
                distance = attack.recoilDistance * ratio;
                tilt = attack.recoilTiltAngle * ratio;
                return;
            }

            elapsed -= attack.recoilKickDuration;
            if (elapsed <= attack.recoilReturnDuration)
            {
                var ratio = Mathf.SmoothStep(0f, 1f, elapsed / attack.recoilReturnDuration);
                distance = Mathf.Lerp(attack.recoilDistance, -attack.recoilDistance * attack.recoilSettleDistanceRatio, ratio);
                tilt = Mathf.Lerp(attack.recoilTiltAngle, -attack.recoilTiltAngle * attack.recoilSettleTiltRatio, ratio);
                return;
            }

            elapsed -= attack.recoilReturnDuration;
            if (elapsed <= attack.recoilSettleDuration)
            {
                var ratio = Mathf.SmoothStep(0f, 1f, elapsed / attack.recoilSettleDuration);
                distance = Mathf.Lerp(-attack.recoilDistance * attack.recoilSettleDistanceRatio, 0f, ratio);
                tilt = Mathf.Lerp(-attack.recoilTiltAngle * attack.recoilSettleTiltRatio, 0f, ratio);
            }
        }

        private bool IsAimedAt(CastleAssaultUnit target)
        {
            if (target == null || visual.Muzzle == null)
            {
                return false;
            }

            var targetPoint = target.transform.position + Vector3.up * attack.targetAimHeight;
            return Vector3.Angle(visual.Muzzle.forward, targetPoint - visual.Muzzle.position) <= attack.fireAngleTolerance;
        }

        private void RestoreLoadedProjectilesWhenReady()
        {
            if (visual.LoadedProjectileCount == 0 || cooldownRemaining <= 0f)
            {
                visual.SetAllLoadedProjectilesVisible(true);
                return;
            }

            var progress = 1f - cooldownRemaining / Mathf.Max(0.05f, attack.cooldown);
            if (progress >= attack.loadedProjectileReloadRatio)
            {
                visual.SetAllLoadedProjectilesVisible(true);
            }
        }

        private void StopPresentation()
        {
            pendingProjectileCount = 0;
            currentTarget = null;
            if (visual == null)
            {
                return;
            }

            visual.SetAllLoadedProjectilesVisible(true);
            visual.PitchPivot.localPosition = basePitchPosition;
        }

        private static float ResolveInitialCooldownRatio(string placementId)
        {
            unchecked
            {
                var hash = 2166136261u;
                var value = placementId ?? string.Empty;
                for (var index = 0; index < value.Length; index++)
                {
                    hash = (hash ^ value[index]) * 16777619u;
                }

                return (hash % 36u) / 100f; // 첫 일제 사격만 0~0.35주기 안에서 분산
            }
        }

        public void ReportHit(float requestedDamage)
        {
            HitCount++;
            RequestedDamage += Mathf.Max(0f, requestedDamage);
        }
    }
}
