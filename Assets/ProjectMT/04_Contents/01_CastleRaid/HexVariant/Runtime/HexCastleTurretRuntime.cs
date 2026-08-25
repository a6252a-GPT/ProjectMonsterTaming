using System;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleTurretRuntime : MonoBehaviour // Hex 셀 기반 조준·장전·발사 실행기
    {
        private const float TargetRefreshInterval = 0.18f;

        [SerializeField] private HexCastleTurretCombatWorld world;
        [SerializeField] private HexCastleCellRuntime structure;
        [SerializeField] private HexCastleTurretVisual visual;
        [SerializeField] private HexCastleTurretAttackProfile profile;
        private HexCastleTurretAttackProfileData attack;
        private HexCastleAssaultUnit currentTarget;
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
        private bool configured;

        public HexCastleTurretCombatWorld World => world;
        public HexCastleTurretAttackProfile Profile => profile;
        public HexCastleCellRuntime Structure => structure;
        public int RangeCells => structure == null ? 0 : structure.TurretRangeCells;
        public HexCastleAssaultUnit CurrentTarget => currentTarget;
        public float SearchRangeWorld => world == null || structure == null
            ? 0f
            : structure.TurretRangeCells * world.CellSize * 1.7320508f;
        public int ProjectilesFired { get; private set; }
        public int HitCount { get; private set; }
        public float RequestedDamage { get; private set; }
        public int MuzzleVfxPlayCount { get; private set; }
        public int FireSfxPlayCount { get; private set; }
        public int ImpactVfxPlayCount { get; private set; }
        public int ImpactSfxPlayCount { get; private set; }

        private void Awake()
        {
            if (world != null && structure != null && visual != null && visual.IsComplete &&
                profile != null && profile.IsValid)
            {
                InitializeRuntimeState();
            }
        }

        public void Configure(
            HexCastleTurretCombatWorld combatWorld,
            HexCastleCellRuntime linkedStructure,
            HexCastleTurretVisual turretVisual,
            HexCastleTurretAttackProfile attackProfile)
        {
            world = combatWorld != null
                ? combatWorld
                : throw new ArgumentNullException(nameof(combatWorld));
            structure = linkedStructure != null
                ? linkedStructure
                : throw new ArgumentNullException(nameof(linkedStructure));
            visual = turretVisual != null && turretVisual.IsComplete
                ? turretVisual
                : throw new ArgumentException("완성된 육각 포탑 Visual이 필요합니다.", nameof(turretVisual));
            profile = attackProfile != null && attackProfile.IsValid
                ? attackProfile
                : throw new ArgumentException("유효한 육각 포탑 공격 프로필이 필요합니다.", nameof(attackProfile));

            if (structure.BuildingRole != HexCastleBuildingRole.Turret ||
                structure.TurretWeaponKind == HexCastleTurretWeaponKind.None ||
                structure.TurretRangeCells < 1)
            {
                throw new ArgumentException("포탑 전투 상태를 소유한 Hex Cell이 필요합니다.", nameof(linkedStructure));
            }

            if (structure.TurretWeaponKind != profile.WeaponKind ||
                visual.WeaponKind != profile.WeaponKind ||
                structure.BuildingGrade != profile.Level || visual.Level != profile.Level)
            {
                throw new ArgumentException("Hex Cell·Visual·공격 프로필의 포탑 종류와 레벨이 다릅니다.");
            }

            InitializeRuntimeState();
        }

        private void InitializeRuntimeState()
        {
            attack = profile.Data;
            baseYawRotation = visual.YawPivot.localRotation;
            basePitchRotation = visual.PitchPivot.localRotation;
            basePitchPosition = visual.PitchPivot.localPosition;
            currentYaw = 0f;
            currentPitch = 0f;
            var initialRatio = ResolveInitialCooldownRatio(gameObject.name);
            cooldownRemaining = attack.cooldown * initialRatio;
            firedProjectileCount = 0;
            pendingProjectileCount = 0;
            nextTargetRefreshTime = Time.time + TargetRefreshInterval * initialRatio;
            recoilStartedAt = float.NegativeInfinity;
            ProjectilesFired = 0;
            HitCount = 0;
            RequestedDamage = 0f;
            MuzzleVfxPlayCount = 0;
            FireSfxPlayCount = 0;
            ImpactVfxPlayCount = 0;
            ImpactSfxPlayCount = 0;
            visual.SetAllLoadedProjectilesVisible(true);
            world.RegisterCell(structure);
            configured = true;
        }

        private void Update()
        {
            Tick(Time.deltaTime, Time.time);
        }

        public void Tick(float deltaTime, float currentTime)
        {
            if (!configured || world == null || structure == null || visual == null || profile == null ||
                !world.IsRunning || !structure.IsAlive)
            {
                StopPresentation();
                return;
            }

            deltaTime = Mathf.Max(0f, deltaTime);
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);
            RestoreLoadedProjectilesWhenReady();
            if (currentTime >= nextTargetRefreshTime || currentTarget != null && !currentTarget.IsAlive)
            {
                RefreshTarget(currentTime);
            }

            if (currentTarget == null)
            {
                pendingProjectileCount = 0;
                ApplyAimAndRecoil(null, deltaTime, currentTime);
                return;
            }

            ApplyAimAndRecoil(currentTarget, deltaTime, currentTime);
            if (pendingProjectileCount > 0)
            {
                if (currentTime >= nextVolleyTime)
                {
                    FireNextVolley(currentTime);
                }

                return;
            }

            if (cooldownRemaining <= 0f && IsAimedAt(currentTarget))
            {
                BeginAttackSequence(currentTime);
            }
        }

        private void RefreshTarget(float currentTime)
        {
            nextTargetRefreshTime = currentTime + TargetRefreshInterval;
            currentTarget = world.FindTarget(
                structure,
                visual.Muzzle.position,
                structure.TurretRangeCells,
                attack.targetPriority,
                attack.projectileHitRadius,
                structure.TurretCanAttackAcrossWalls);
        }

        private void BeginAttackSequence(float currentTime)
        {
            cooldownRemaining = attack.cooldown;
            firedProjectileCount = 0;
            pendingProjectileCount = attack.projectileCount;
            FireNextVolley(currentTime);
        }

        private void FireNextVolley(float currentTime)
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
            if (!structure.TurretCanAttackAcrossWalls &&
                world.IsLineBlocked(muzzle.position, targetPoint, attack.projectileHitRadius, structure))
            {
                pendingProjectileCount = 0;
                currentTarget = null;
                return;
            }

            var baseDirection = targetPoint - muzzle.position;
            if (baseDirection.sqrMagnitude <= 0.000001f)
            {
                pendingProjectileCount = 0;
                return;
            }

            baseDirection.Normalize();
            for (var index = 0; index < volleySize; index++)
            {
                var spread = volleySize <= 1
                    ? 0f
                    : Mathf.Lerp(
                        -attack.spreadAngle * 0.5f,
                        attack.spreadAngle * 0.5f,
                        index / (float)(volleySize - 1));
                var direction = Quaternion.AngleAxis(spread, Vector3.up) * baseDirection;
                var projectileObject = world.RentObject(
                    attack.projectilePrefab,
                    muzzle.position,
                    Quaternion.LookRotation(direction, Vector3.up));
                if (projectileObject == null)
                {
                    continue;
                }

                projectileObject.transform.localScale = Vector3.one * attack.projectileScale;
                var projectile = projectileObject.GetComponent<HexCastleTurretProjectile>();
                if (projectile == null)
                {
                    projectile = projectileObject.AddComponent<HexCastleTurretProjectile>();
                }

                projectile.Configure(world, this, profile, direction, targetPoint);
                visual.SetLoadedProjectileVisible(firedProjectileCount, false);
                firedProjectileCount++;
                ProjectilesFired++;
            }

            pendingProjectileCount -= volleySize;
            if (visual.PlayMuzzleVfx())
            {
                MuzzleVfxPlayCount++;
            }

            if (world.PlayCue(attack.fireSfx, muzzle.position))
            {
                FireSfxPlayCount++;
            }
            recoilStartedAt = currentTime;
            if (pendingProjectileCount > 0)
            {
                nextVolleyTime = currentTime + Mathf.Max(0.01f, attack.projectileFireDelay);
            }
        }

        private void ApplyAimAndRecoil(HexCastleAssaultUnit target, float deltaTime, float currentTime)
        {
            var desiredYaw = currentYaw;
            var desiredPitch = 0f; // 표적이 사라지면 꺾인 포신을 수평으로 복귀시킨다
            if (target != null)
            {
                var targetPoint = target.transform.position + Vector3.up * attack.targetAimHeight;
                // 실제 탄환이 출발하는 총구를 기준으로 조준해야 긴 대포도 근거리 표적과 일치한다.
                var yawDirection = visual.YawPivot.parent.InverseTransformDirection(
                    targetPoint - visual.Muzzle.position);
                desiredYaw = Mathf.Atan2(yawDirection.x, yawDirection.z) * Mathf.Rad2Deg;

                var pitchDirection = visual.PitchPivot.parent.InverseTransformDirection(
                    targetPoint - visual.Muzzle.position);
                var horizontal = Mathf.Sqrt(
                    pitchDirection.x * pitchDirection.x + pitchDirection.z * pitchDirection.z);
                desiredPitch = -Mathf.Atan2(pitchDirection.y, Mathf.Max(0.001f, horizontal)) * Mathf.Rad2Deg;
            }

            currentYaw = Mathf.MoveTowardsAngle(currentYaw, desiredYaw, attack.headTurnSpeed * deltaTime);
            currentPitch = Mathf.MoveTowardsAngle(currentPitch, desiredPitch, attack.headTurnSpeed * deltaTime);
            EvaluateRecoil(currentTime, out var recoilDistance, out var recoilTilt);
            visual.YawPivot.localRotation = baseYawRotation * Quaternion.Euler(0f, currentYaw, 0f);
            visual.PitchPivot.localRotation = basePitchRotation * Quaternion.Euler(currentPitch - recoilTilt, 0f, 0f);
            visual.PitchPivot.localPosition = basePitchPosition + Vector3.back * recoilDistance;
        }

        private void EvaluateRecoil(float currentTime, out float distance, out float tilt)
        {
            distance = 0f;
            tilt = 0f;
            var elapsed = currentTime - recoilStartedAt;
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
                distance = Mathf.Lerp(
                    attack.recoilDistance,
                    -attack.recoilDistance * attack.recoilSettleDistanceRatio,
                    ratio);
                tilt = Mathf.Lerp(
                    attack.recoilTiltAngle,
                    -attack.recoilTiltAngle * attack.recoilSettleTiltRatio,
                    ratio);
                return;
            }

            elapsed -= attack.recoilReturnDuration;
            if (elapsed <= attack.recoilSettleDuration)
            {
                var ratio = Mathf.SmoothStep(0f, 1f, elapsed / attack.recoilSettleDuration);
                distance = Mathf.Lerp(
                    -attack.recoilDistance * attack.recoilSettleDistanceRatio,
                    0f,
                    ratio);
                tilt = Mathf.Lerp(
                    -attack.recoilTiltAngle * attack.recoilSettleTiltRatio,
                    0f,
                    ratio);
            }
        }

        private bool IsAimedAt(HexCastleAssaultUnit target)
        {
            if (target == null || visual.Muzzle == null)
            {
                return false;
            }

            var targetPoint = target.transform.position + Vector3.up * attack.targetAimHeight;
            return Vector3.Angle(visual.Muzzle.forward, targetPoint - visual.Muzzle.position) <=
                   attack.fireAngleTolerance;
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
            if (visual == null || !visual.IsComplete)
            {
                return;
            }

            visual.SetAllLoadedProjectilesVisible(true);
            if (configured)
            {
                currentYaw = 0f;
                currentPitch = 0f;
                visual.YawPivot.localRotation = baseYawRotation;
                visual.PitchPivot.localRotation = basePitchRotation;
                visual.PitchPivot.localPosition = basePitchPosition;
            }
        }

        private void OnDisable()
        {
            StopPresentation();
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

                return (hash % 36u) / 100f;
            }
        }

        public void ReportHit(float requestedDamage)
        {
            HitCount++;
            RequestedDamage += Mathf.Max(0f, requestedDamage);
        }

        public void ReportImpactPresentation(bool playedVfx, bool playedSfx)
        {
            if (playedVfx)
            {
                ImpactVfxPlayCount++;
            }

            if (playedSfx)
            {
                ImpactSfxPlayCount++;
            }
        }
    }
}
