using System;
using ProjectMT.Shared.Audio;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Features.Equipment;
using ProjectMT.Features.MainBattle;
using ProjectMT.Features.Quest;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Expedition
{
    public sealed partial class ExpeditionController
    {
        private void TryScheduleNextWave()
        {
            var previousWave = Mathf.Clamp(nextWaveToSpawn - 1, 1, waveCount);
            var alive = previousWave < aliveEnemiesByWave.Length ? aliveEnemiesByWave[previousWave] : 0;
            if (alive <= 0)
            {
                StartWaveArrival(nextWaveToSpawn); // 전멸 시 대기 없이 증원 행군 시작
                return;
            }

            var initialCount = Mathf.Max(1, profile.GetEnemyCount(currentStage, previousWave));
            var aliveRatio = (float)alive / initialCount;
            var minimumDelay = Mathf.Max(
                profile.ReinforcementMinimumDelaySeconds,
                profile.GetWaveSpawnDelay(currentStage, nextWaveToSpawn));
            var warningLead = profile.ReinforcementWarningSeconds;
            var forceWarningTime = Mathf.Max(
                minimumDelay,
                profile.ReinforcementForceDelaySeconds - warningLead);
            var weakened = waveElapsed >= minimumDelay && aliveRatio <= profile.ReinforcementAliveRatio;
            var forced = waveElapsed >= forceWarningTime;
            if (weakened || forced)
            {
                BeginReinforcementWarning(nextWaveToSpawn);
            }
        }

        private void BeginReinforcementWarning(int wave)
        {
            reinforcementWarningActive = true;
            reinforcementWarningWave = wave;
            reinforcementWarningRemaining = profile.ReinforcementWarningSeconds;
            reinforcementNoticeRemaining = Mathf.Max(0.35f, reinforcementWarningRemaining + 0.15f);
            ShowReinforcementWarning(true);
            if (reinforcementWarningRemaining <= 0f)
            {
                reinforcementWarningActive = false;
                StartWaveArrival(wave);
            }
        }

        private void TickReinforcementWarning(float deltaTime)
        {
            if (!reinforcementWarningActive)
            {
                return;
            }

            reinforcementWarningRemaining = Mathf.Max(0f, reinforcementWarningRemaining - deltaTime);
            if (reinforcementWarningRemaining > 0f)
            {
                return;
            }

            var wave = reinforcementWarningWave;
            reinforcementWarningActive = false;
            reinforcementWarningWave = 0;
            StartWaveArrival(wave);
        }

        private void TickReinforcementNotice(float deltaTime)
        {
            if (reinforcementNoticeRemaining <= 0f)
            {
                return;
            }

            reinforcementNoticeRemaining = Mathf.Max(0f, reinforcementNoticeRemaining - deltaTime);
            if (reinforcementNoticeRemaining <= 0f)
            {
                ShowReinforcementWarning(false);
            }
        }

        private void StartWaveArrival(int wave)
        {
            if (profile == null || combatWorld == null || wave <= 0 || wave > waveCount)
            {
                return;
            }

            SfxEvents.Play2D(wave == 1 ? SfxEvents.BattleStart : SfxEvents.Wave);
            waveArrivalActive = true;
            arrivalWave = wave;
            arrivalTotalCount = Mathf.Max(0, profile.GetEnemyCount(currentStage, wave));
            arrivalNextSpawnIndex = 0;
            arrivalSpawnTimer = 0f;
            arrivingEnemies.Clear();
            currentWave = wave;
            nextWaveToSpawn = wave + 1;
            allWavesSpawned = nextWaveToSpawn > waveCount;
            waveElapsed = 0f;

            if (wave > 1)
            {
                reinforcementNoticeRemaining = Mathf.Max(
                    reinforcementNoticeRemaining,
                    Mathf.Max(0.35f, profile.ReinforcementWarningSeconds));
                ShowReinforcementWarning(true);
                ResolveEnemyFormationAxes(out _, out var formationForward);
                var cuePosition = ResolveEnemyEntryCuePosition(formationForward);
                combatWorld.PlayClimax(cuePosition, CombatClimaxStrength.Weak); // 기존 VFX/SFX로 증원 방향 강조
            }

            if (arrivalTotalCount <= 0)
            {
                CompleteWaveArrival();
                return;
            }

            SpawnNextArrivalEnemy(); // 첫 기는 경고 종료와 동시에 보이게 함
            arrivalSpawnTimer = profile.ResolveArrivalSpawnInterval(arrivalWave);
        }

        private void TickWaveArrival(float deltaTime)
        {
            if (!waveArrivalActive)
            {
                return;
            }

            arrivalSpawnTimer -= Mathf.Max(0f, deltaTime);
            while (arrivalNextSpawnIndex < arrivalTotalCount && arrivalSpawnTimer <= 0f)
            {
                SpawnNextArrivalEnemy();
                arrivalSpawnTimer += profile.ResolveArrivalSpawnInterval(arrivalWave);
                if (profile.ResolveArrivalSpawnInterval(arrivalWave) <= 0f)
                {
                    arrivalSpawnTimer = 0f;
                }
            }

            var allReached = arrivalNextSpawnIndex >= arrivalTotalCount;
            for (var index = 0; index < arrivingEnemies.Count; index++)
            {
                var arrival = arrivingEnemies[index];
                if (arrival.Actor == null || arrival.Reached)
                {
                    continue;
                }

                arrival.Elapsed += Mathf.Max(0f, deltaTime);
                var ratio = Mathf.Clamp01(arrival.Elapsed / arrival.Duration);
                var eased = ratio * ratio * (3f - 2f * ratio);
                arrival.Actor.transform.position = Vector3.Lerp(arrival.EntryPosition, arrival.ReadyPosition, eased);
                var direction = arrival.ReadyPosition - arrival.Actor.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    arrival.Actor.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }

                if (ratio < 1f)
                {
                    arrival.Actor.AnimationDriver?.PlayMove();
                    allReached = false;
                    continue;
                }

                arrival.Reached = true;
                arrival.Actor.AnimationDriver?.PlayIdle(true);
            }

            if (allReached)
            {
                CompleteWaveArrival();
            }
        }

        private void SpawnNextArrivalEnemy()
        {
            if (arrivalNextSpawnIndex >= arrivalTotalCount)
            {
                return;
            }

            var index = arrivalNextSpawnIndex++;
            var readyPosition = ResolveEnemyFormationPosition(arrivalWave, index, arrivalTotalCount);
            var readyLaneOffset = ExpeditionStageRules.GetFormationOffset(index, arrivalTotalCount).x;
            ResolveEnemyFormationAxes(out var formationRight, out var formationForward);
            var entryPosition = ResolveEnemyEntryPosition(
                readyPosition,
                readyLaneOffset,
                formationRight,
                formationForward,
                index);
            var appearanceSeed = CreateEnemyAppearanceSeed(currentStage, arrivalWave, index, operationVersion);
            var spawn = profile.ResolveSpawn(currentStage, arrivalWave, index, appearanceSeed);
            var enemyPrefab = enemyAppearanceSet == null
                ? enemyUnitPrefab
                : enemyAppearanceSet.ResolvePrefab(spawn.Appearance);
            var request = new UnitSpawnRequest(
                spawn.IsBoss ? $"boss_{currentDifficulty}_{currentStage}" :
                $"enemy_{currentDifficulty}_{currentStage}_{arrivalWave}_{index}",
                profile.CreateEnemyStats(currentStage, currentDifficulty, spawn),
                UnitTeam.Enemy,
                appearanceSeed: appearanceSeed,
                visualScaleMultiplier: spawn.IsBoss ? profile.BossVisualScaleMultiplier : 1f,
                isBoss: spawn.IsBoss);
            var direction = readyPosition - entryPosition;
            direction.y = 0f;
            var rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            var actor = combatWorld.SpawnUnit(enemyPrefab, request, entryPosition, rotation);
            if (actor == null)
            {
                return;
            }

            if (spawn.IsBoss) SfxEvents.Play2D(SfxEvents.Boss);
            ApplyEnemyAIProfile(actor, spawn.IsRanged);
            actor.SetCombatReady(false);
            actor.AnimationDriver?.PlayMove();
            TrackWaveEnemy(actor, arrivalWave);
            arrivingEnemies.Add(new EnemyArrivalUnit(
                actor,
                entryPosition,
                readyPosition,
                profile.ResolveArrivalMarchDuration(arrivalWave),
                spawn.IsBoss,
                spawn.IsNinja,
                spawn.NinjaOrdinal));
        }

        private Vector3 ResolveEnemyEntryPosition(
            Vector3 readyPosition,
            float readyLaneOffset,
            Vector3 formationRight,
            Vector3 formationForward,
            int unitIndex)
        {
            var scatterOffset = ExpeditionStageRules.GetEntryScatterOffset(unitIndex);
            if (TryResolveEnemyEntryPoint(readyLaneOffset, formationRight, out var entryPosition))
            {
                return entryPosition +
                       formationRight * scatterOffset.x +
                       formationForward * scatterOffset.y;
            }

            return readyPosition +
                   formationForward * (profile.EnemyEntryDistance + scatterOffset.y) +
                   formationRight * scatterOffset.x;
        }

        private Vector3 ResolveEnemyEntryCuePosition(Vector3 formationForward)
        {
            var total = Vector3.zero;
            var count = 0;
            if (enemyEntryPoints != null)
            {
                for (var index = 0; index < enemyEntryPoints.Length; index++)
                {
                    if (enemyEntryPoints[index] == null)
                    {
                        continue;
                    }

                    total += enemyEntryPoints[index].position;
                    count++;
                }
            }

            if (count > 0)
            {
                return total / count;
            }

            return enemySpawnAnchor == null
                ? transform.position
                : enemySpawnAnchor.position + formationForward * profile.EnemyEntryDistance;
        }

        private bool TryResolveEnemyEntryPoint(
            float readyLaneOffset,
            Vector3 formationRight,
            out Vector3 position)
        {
            var anchorPosition = enemySpawnAnchor == null ? transform.position : enemySpawnAnchor.position;
            var sideThreshold = ExpeditionStageRules.FormationSpacing * 0.75f;
            var bestScore = float.PositiveInfinity;
            position = default;
            var found = false;
            if (enemyEntryPoints != null)
            {
                for (var index = 0; index < enemyEntryPoints.Length; index++)
                {
                    var entryPoint = enemyEntryPoints[index];
                    if (entryPoint == null)
                    {
                        continue;
                    }

                    var entryLane = Vector3.Dot(entryPoint.position - anchorPosition, formationRight);
                    var score = readyLaneOffset < -sideThreshold
                        ? entryLane
                        : readyLaneOffset > sideThreshold
                            ? -entryLane
                            : Mathf.Abs(entryLane);
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    position = entryPoint.position;
                    found = true;
                }
            }

            return found; // 좌·중·우 Ready 열과 같은 입장선을 골라 행군선 교차 방지
        }

        private Vector3 ResolveEnemyFormationPosition(int wave, int index, int count)
        {
            var anchor = enemySpawnAnchor == null
                ? transform.position + new Vector3(4f, 0f, 4f)
                : enemySpawnAnchor.position;
            ResolveEnemyFormationAxes(out var formationRight, out var formationForward);
            var tuning = CombatImpactTuning.ActiveConfig;
            var spawnSpread = profile.EnemyFormationSpread *
                              (tuning == null ? 1f : tuning.MainBattleEnemySpawnSpreadMultiplier);
            var formationOffset = ExpeditionStageRules.GetFormationOffset(index, count) * spawnSpread;
            return anchor +
                   formationRight * formationOffset.x +
                   formationForward * (formationOffset.y + profile.GetWaveForwardOffset(currentStage, wave));
        }

        private void ResolveEnemyFormationAxes(out Vector3 formationRight, out Vector3 formationForward)
        {
            var fallbackForward = enemySpawnAnchor == null ? Vector3.forward : enemySpawnAnchor.forward;
            var anchorPosition = enemySpawnAnchor == null
                ? transform.position + new Vector3(4f, 0f, 4f)
                : enemySpawnAnchor.position;
            formationForward = ExpeditionStageRules.ResolveBattleForward(
                formationFrameConfigured ? formationOrigin : transform.position,
                anchorPosition,
                fallbackForward);
            formationRight = Vector3.Cross(Vector3.up, formationForward).normalized;
        }

        private void CompleteWaveArrival()
        {
            for (var index = 0; index < arrivingEnemies.Count; index++)
            {
                var arrival = arrivingEnemies[index];
                if (arrival.Actor == null || !arrival.Actor.IsAlive)
                {
                    continue;
                }

                arrival.Actor.transform.position = arrival.ReadyPosition;
                if (arrival.IsNinja)
                {
                    BeginNinjaFlank(arrival.Actor, arrival.NinjaOrdinal);
                }
                else
                {
                    arrival.Actor.SetCombatReady(true);
                    arrival.Actor.AnimationDriver?.PlayIdle(true);
                }
                if (arrival.IsBoss)
                {
                    bossHud?.Show(arrival.Actor, currentStage); // Ready 시점부터 보스 HUD 표시
                }
            }

            arrivingEnemies.Clear();
            waveArrivalActive = false;
            firstWaveReady = true;
            arrivalWave = 0;
            arrivalTotalCount = 0;
            arrivalNextSpawnIndex = 0;
            arrivalSpawnTimer = 0f;
            waveElapsed = 0f;
        }

        private void BeginNinjaFlank(UnitActor actor, int ninjaOrdinal)
        {
            if (actor == null)
            {
                return;
            }

            ResolveEnemyFormationAxes(out _, out var formationForward);
            UnitActor rearTarget = null;
            var rearProjection = float.PositiveInfinity;
            foreach (var pair in playerSlotByActor)
            {
                var candidate = pair.Key;
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                var projection = Vector3.Dot(candidate.transform.position - formationOrigin, formationForward);
                if (projection < rearProjection)
                {
                    rearProjection = projection;
                    rearTarget = candidate;
                }
            }

            if (rearTarget == null)
            {
                actor.SetCombatReady(true);
                return;
            }

            var flank = actor.gameObject.AddComponent<NinjaFlankController>();
            flank.Configure(actor, rearTarget, formationForward, Mathf.Max(0, ninjaOrdinal));
        }

        private static int CreateEnemyAppearanceSeed(int stage, int wave, int index, int runVersion)
        {
            unchecked
            {
                var seed = 2166136261u;
                seed = (seed ^ (uint)stage) * 16777619u;
                seed = (seed ^ (uint)wave) * 16777619u;
                seed = (seed ^ (uint)index) * 16777619u;
                seed = (seed ^ (uint)runVersion) * 16777619u;
                seed ^= seed >> 16;
                seed *= 0x7FEB352Du;
                seed ^= seed >> 15;
                seed *= 0x846CA68Bu;
                seed ^= seed >> 16; // 인접 슬롯 시드 동조 방지
                var positiveSeed = (int)(seed & int.MaxValue);
                return positiveSeed == 0 ? 1 : positiveSeed;
            }
        }

        private void TrackWaveEnemy(UnitActor actor, int wave)
        {
            if (actor == null || wave <= 0 || wave >= aliveEnemiesByWave.Length)
            {
                return;
            }

            enemyWaveByActor[actor] = wave;
            aliveEnemiesByWave[wave]++;
            actor.Died += HandleWaveEnemyDied;
        }

        private void HandleWaveEnemyDied(UnitActor actor)
        {
            if (actor == null || !enemyWaveByActor.TryGetValue(actor, out var wave))
            {
                return;
            }

            actor.Died -= HandleWaveEnemyDied;
            enemyWaveByActor.Remove(actor);
            aliveEnemiesByWave[wave] = Mathf.Max(0, aliveEnemiesByWave[wave] - 1);
            defeatedEnemyCount = Mathf.Min(runEnemyTotalCount, defeatedEnemyCount + 1);
            _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.MonsterKill, 1L); // 처치 1마리당 퀘스트 진행
            if (running && profile != null &&
                profile.CreateEnemyWorldDrops(currentStage, wave, actor.transform.position, worldDropBuffer) > 0)
            {
                for (var index = 0; index < worldDropBuffer.Count; index++)
                {
                    worldItemDrops?.TrySpawn(worldDropBuffer[index]); // 보상 원본과 분리된 표시 요청
                }
            }

            TrySpawnNormalEnemyEquipment(actor.transform.position);

            if (!running || aliveEnemiesByWave[wave] != 0 || climaxPlayedByWave[wave])
            {
                return;
            }

            climaxPlayedByWave[wave] = true;
            combatWorld?.PlayClimax(actor.transform.position, CombatClimaxStrength.Weak);
            _ = FlushWorldDropsCheckpointAsync(); // 이미 흡수한 항목만 웨이브 체크포인트 저장
        }

        private void ResetWaveTracking()
        {
            ResetArrivalState();
            foreach (var pair in enemyWaveByActor)
            {
                if (pair.Key != null)
                {
                    pair.Key.Died -= HandleWaveEnemyDied;
                }
            }

            enemyWaveByActor.Clear();
            Array.Clear(aliveEnemiesByWave, 0, aliveEnemiesByWave.Length);
            Array.Clear(climaxPlayedByWave, 0, climaxPlayedByWave.Length);
            runEnemyTotalCount = 0;
            defeatedEnemyCount = 0;
        }

        private void ResetArrivalState()
        {
            arrivingEnemies.Clear();
            waveArrivalActive = false;
            firstWaveReady = false;
            arrivalWave = 0;
            arrivalTotalCount = 0;
            arrivalNextSpawnIndex = 0;
            arrivalSpawnTimer = 0f;
            reinforcementWarningActive = false;
            reinforcementWarningWave = 0;
            reinforcementWarningRemaining = 0f;
            reinforcementNoticeRemaining = 0f;
            ShowReinforcementWarning(false);
        }
    }
}
