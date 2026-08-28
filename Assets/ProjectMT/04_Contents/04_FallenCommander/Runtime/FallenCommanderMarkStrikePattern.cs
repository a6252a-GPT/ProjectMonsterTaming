using System.Collections.Generic;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    // 페이즈별 다중 위치 공격의 랜덤 배치·경고·피해·정리를 전담한다.
    public sealed class FallenCommanderMarkStrikePattern
    {
        private sealed class ActiveStrike
        {
            public int GroupIndex;
            public Vector3 Position;
            public float RemainingTime;
            public FallenCommanderTelegraphView Telegraph;
        }

        private readonly List<ActiveStrike> activeStrikes = new();
        private readonly List<Vector3> usedPositions = new();
        private readonly List<Vector3> usedGroupCenters = new();
        private readonly Dictionary<int, int> groupDamageCounts = new();

        private FallenCommanderAttackData attack;
        private FallenCommanderMarkStrikePhaseData settings;
        private UnitActor bossActor;
        private Transform commanderRoot;
        private HealthComponent commanderHealth;
        private FallenCommanderBossAnimationPresenter animationPresenter;
        private Transform effectParent;
        private Vector3 arenaCenter;
        private Color telegraphColor;
        private System.Action<float> stunCommander;
        private float timeUntilNextGroup;
        private int spawnedCount;
        private int nextGroupIndex;
        private bool hasPlayedCastMotion;

        public bool IsActive { get; private set; }
        public FallenCommanderTelegraphView ActiveTelegraph =>
            activeStrikes.Count == 0 ? null : activeStrikes[0].Telegraph;

        // 현재 페이즈 설정으로 첫 위치 공격 묶음과 시전 연출을 시작한다.
        public bool Begin(
            FallenCommanderAttackData attackData,
            FallenCommanderMarkStrikePhaseData phaseSettings,
            UnitActor boss,
            Transform commander,
            HealthComponent health,
            FallenCommanderBossAnimationPresenter animations,
            Transform parent,
            Vector3 randomArenaCenter,
            Color warningColor,
            System.Action<float> stunAction)
        {
            Cancel();
            if (attackData == null ||
                attackData.TelegraphPrefab == null ||
                phaseSettings == null ||
                boss == null ||
                commander == null ||
                health == null)
            {
                return false;
            }

            attack = attackData;
            settings = phaseSettings;
            bossActor = boss;
            commanderRoot = commander;
            commanderHealth = health;
            animationPresenter = animations;
            effectParent = parent;
            arenaCenter = randomArenaCenter;
            telegraphColor = warningColor;
            stunCommander = stunAction;
            IsActive = true;

            animationPresenter?.PlayPreCast(
                attack.PreCastMotion,
                playbackSpeed: attack.PreCastMotionSpeed,
                normalizedStart: attack.PreCastMotionStart,
                normalizedEnd: attack.PreCastMotionEnd);
            FallenCommanderAttackEffectPlayer.PlayStart(
                attack.Effects,
                bossActor.transform.position,
                bossActor.transform.forward,
                effectParent,
                bossActor.transform,
                commanderRoot);

            SpawnNextGroup();
            timeUntilNextGroup = settings.GroupInterval;
            return true;
        }

        // 생성 간격과 각 장판의 경고 진행도를 갱신하고 패턴 종료 여부를 반환한다.
        public bool Tick(float deltaTime)
        {
            if (!IsActive)
            {
                return false;
            }

            var safeDeltaTime = Mathf.Max(0f, deltaTime);
            TickGroupSpawning(safeDeltaTime);
            TickActiveStrikes(safeDeltaTime);

            if (!IsActive)
            {
                return false;
            }

            if (spawnedCount < settings.TotalCount || activeStrikes.Count > 0)
            {
                return false;
            }

            ReleaseRuntimeState();
            return true;
        }

        // 다음 묶음 생성시각이 되면 동시 생성 개수만큼 장판을 추가한다.
        private void TickGroupSpawning(float deltaTime)
        {
            if (spawnedCount >= settings.TotalCount)
            {
                return;
            }

            timeUntilNextGroup -= deltaTime;
            if (timeUntilNextGroup > 0f)
            {
                return;
            }

            SpawnNextGroup();
            timeUntilNextGroup = settings.GroupInterval;
        }

        // 활성 장판을 갱신하고 경고시간이 끝난 장판을 발동한다.
        private void TickActiveStrikes(float deltaTime)
        {
            for (var index = activeStrikes.Count - 1; index >= 0; index--)
            {
                var strike = activeStrikes[index];
                strike.RemainingTime = Mathf.Max(0f, strike.RemainingTime - deltaTime);
                var fillRemaining = Mathf.Max(
                    0f,
                    strike.RemainingTime - attack.TelegraphHoldDuration);
                strike.Telegraph?.SetProgress(
                    1f - fillRemaining / settings.WarningDuration);

                if (strike.RemainingTime > 0f)
                {
                    continue;
                }

                activeStrikes.RemoveAt(index);
                ResolveStrike(strike);
                DestroyTelegraph(strike.Telegraph);
                if (!IsActive)
                {
                    return;
                }
            }
        }

        // 현재 묶음에서 필요한 개수만큼 랜덤 또는 밀집 위치를 생성한다.
        private void SpawnNextGroup()
        {
            var remainingCount = settings.TotalCount - spawnedCount;
            var groupCount = Mathf.Min(settings.SimultaneousCount, remainingCount);
            var groupCenter = ResolveRandomPosition(
                usedGroupCenters,
                settings.MinimumSpacing,
                settings.ClusterGroups ? settings.ClusterRadius : 0f);
            usedGroupCenters.Add(groupCenter);

            for (var index = 0; index < groupCount; index++)
            {
                var position = settings.ClusterGroups
                    ? ResolveClusterPosition(groupCenter, index, groupCount)
                    : ResolveRandomPosition(usedPositions, settings.MinimumSpacing, 0f);
                usedPositions.Add(position);
                SpawnStrike(nextGroupIndex, position);
                spawnedCount++;
            }

            nextGroupIndex++;
        }

        // 지정 위치에 개별 경고 장판을 생성하고 독립된 발동시간을 저장한다.
        private void SpawnStrike(int groupIndex, Vector3 position)
        {
            var telegraph = FallenCommanderTelegraphView.CreateCircle(
                attack.TelegraphPrefab,
                effectParent,
                position,
                attack.Radius,
                telegraphColor);
            telegraph?.SetProgress(0f);
            activeStrikes.Add(new ActiveStrike
            {
                GroupIndex = groupIndex,
                Position = position,
                RemainingTime = settings.WarningDuration + attack.TelegraphHoldDuration,
                Telegraph = telegraph
            });
        }

        // 장판 발동 연출을 재생하고 같은 묶음의 피해 제한 안에서 군단장에게 피해를 준다.
        private void ResolveStrike(ActiveStrike strike)
        {
            var direction = bossActor == null
                ? Vector3.forward
                : bossActor.transform.forward;
            FallenCommanderAttackEffectPlayer.PlayResolve(
                attack.Effects,
                strike.Position,
                direction,
                effectParent,
                bossActor == null ? null : bossActor.transform,
                commanderRoot);

            PlayCastMotionOnce();
            if (!IsCommanderInside(strike.Position) || !CanDamageGroup(strike.GroupIndex))
            {
                return;
            }

            FallenCommanderAttackEffectPlayer.PlayHit(
                attack.Effects,
                commanderRoot.position,
                direction,
                effectParent,
                bossActor == null ? null : bossActor.transform,
                commanderRoot);
            if (settings.StunDuration > 0f)
            {
                stunCommander?.Invoke(settings.StunDuration);
            }

            groupDamageCounts.TryGetValue(strike.GroupIndex, out var damageCount);
            groupDamageCounts[strike.GroupIndex] = damageCount + 1;
            commanderHealth.ApplyDamage(new DamageRequest(
                bossActor,
                1f,
                commanderRoot.position));
        }

        // 첫 장판이 발동할 때만 공격 모션을 한 번 재생한다.
        private void PlayCastMotionOnce()
        {
            if (hasPlayedCastMotion)
            {
                return;
            }

            hasPlayedCastMotion = true;
            animationPresenter?.Play(
                attack.CastMotion,
                stopAfterMotion: true,
                durationOverride: attack.CastMotionDuration,
                playbackSpeed: attack.CastMotionSpeed,
                normalizedStart: attack.CastMotionStart,
                normalizedEnd: attack.CastMotionEnd);
        }

        // 지정 묶음에 남은 피해 횟수가 있는지 확인한다.
        private bool CanDamageGroup(int groupIndex)
        {
            groupDamageCounts.TryGetValue(groupIndex, out var damageCount);
            return damageCount < settings.MaxDamagePerGroup;
        }

        // 군단장이 지정 장판의 원형 피해 범위 안에 있는지 검사한다.
        private bool IsCommanderInside(Vector3 position)
        {
            if (commanderRoot == null || commanderHealth == null || !commanderHealth.IsAlive)
            {
                return false;
            }

            var offset = commanderRoot.position - position;
            offset.y = 0f;
            return offset.sqrMagnitude <= attack.Radius * attack.Radius;
        }

        // 생성 영역 안에서 기존 기준점과 일정 거리를 둔 랜덤 위치를 찾는다.
        private Vector3 ResolveRandomPosition(
            IReadOnlyList<Vector3> existingPositions,
            float minimumSpacing,
            float extraMargin)
        {
            const int attemptCount = 24;
            var bestCandidate = arenaCenter;
            var bestDistanceSquared = -1f;
            for (var attempt = 0; attempt < attemptCount; attempt++)
            {
                var candidate = CreateRandomCandidate(extraMargin);
                var nearestDistanceSquared = ResolveNearestDistanceSquared(
                    candidate,
                    existingPositions);
                if (nearestDistanceSquared >= minimumSpacing * minimumSpacing)
                {
                    return candidate;
                }

                if (nearestDistanceSquared > bestDistanceSquared)
                {
                    bestDistanceSquared = nearestDistanceSquared;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        // 밀집 묶음의 중심을 기준으로 각 장판을 원형으로 분산하고 전장 안으로 보정한다.
        private Vector3 ResolveClusterPosition(
            Vector3 center,
            int index,
            int count)
        {
            if (count <= 1 || settings.ClusterRadius <= 0f)
            {
                return center;
            }

            var angle = index * Mathf.PI * 2f / count;
            var position = center + new Vector3(
                Mathf.Cos(angle) * settings.ClusterRadius,
                0f,
                Mathf.Sin(angle) * settings.ClusterRadius);
            return ClampToArena(position, 0f);
        }

        // 공격 반지름과 추가 여백을 제외한 전장 안에서 랜덤 후보를 만든다.
        private Vector3 CreateRandomCandidate(float extraMargin)
        {
            var extents = settings.ArenaHalfExtents;
            var margin = attack.Radius + Mathf.Max(0f, extraMargin);
            var allowedX = Mathf.Max(0f, extents.x - margin);
            var allowedZ = Mathf.Max(0f, extents.y - margin);
            return new Vector3(
                Random.Range(arenaCenter.x - allowedX, arenaCenter.x + allowedX),
                arenaCenter.y,
                Random.Range(arenaCenter.z - allowedZ, arenaCenter.z + allowedZ));
        }

        // 지정 위치를 공격 반지름만큼 가장자리에서 떨어진 전장 내부로 제한한다.
        private Vector3 ClampToArena(Vector3 position, float extraMargin)
        {
            var extents = settings.ArenaHalfExtents;
            var margin = attack.Radius + Mathf.Max(0f, extraMargin);
            var allowedX = Mathf.Max(0f, extents.x - margin);
            var allowedZ = Mathf.Max(0f, extents.y - margin);
            position.x = Mathf.Clamp(position.x, arenaCenter.x - allowedX, arenaCenter.x + allowedX);
            position.z = Mathf.Clamp(position.z, arenaCenter.z - allowedZ, arenaCenter.z + allowedZ);
            position.y = arenaCenter.y;
            return position;
        }

        // 후보 위치와 기존 위치 중 가장 가까운 평면 거리를 제곱값으로 반환한다.
        private static float ResolveNearestDistanceSquared(
            Vector3 candidate,
            IReadOnlyList<Vector3> existingPositions)
        {
            if (existingPositions.Count == 0)
            {
                return float.PositiveInfinity;
            }

            var nearestDistanceSquared = float.PositiveInfinity;
            for (var index = 0; index < existingPositions.Count; index++)
            {
                var offset = candidate - existingPositions[index];
                offset.y = 0f;
                nearestDistanceSquared = Mathf.Min(nearestDistanceSquared, offset.sqrMagnitude);
            }

            return nearestDistanceSquared;
        }

        // 활성 장판을 모두 제거하고 런타임 참조와 진행 상태를 초기화한다.
        public void Cancel()
        {
            for (var index = 0; index < activeStrikes.Count; index++)
            {
                DestroyTelegraph(activeStrikes[index].Telegraph);
            }

            ReleaseRuntimeState();
        }

        // 생성된 경고 장판 오브젝트를 안전하게 제거한다.
        private static void DestroyTelegraph(FallenCommanderTelegraphView telegraph)
        {
            if (telegraph != null)
            {
                Object.Destroy(telegraph.gameObject);
            }
        }

        // 다음 실행을 위해 컬렉션과 외부 참조를 초기 상태로 되돌린다.
        private void ReleaseRuntimeState()
        {
            activeStrikes.Clear();
            usedPositions.Clear();
            usedGroupCenters.Clear();
            groupDamageCounts.Clear();
            attack = null;
            settings = null;
            bossActor = null;
            commanderRoot = null;
            commanderHealth = null;
            animationPresenter = null;
            effectParent = null;
            arenaCenter = Vector3.zero;
            stunCommander = null;
            timeUntilNextGroup = 0f;
            spawnedCount = 0;
            nextGroupIndex = 0;
            hasPlayedCastMotion = false;
            IsActive = false;
        }
    }
}
