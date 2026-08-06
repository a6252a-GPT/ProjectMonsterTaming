using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Contents.CastleRaid;
using ProjectMT.Contents.FoodRiot;
using ProjectMT.Features.Expedition;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ProjectMT.Tests.PlayMode
{
    public sealed class SeedRuntimeTests // 실제 실행 중 시드 동작 회귀 검사
    {
        [UnityTest]
        public IEnumerator FixedDamageTarget_RequiresExactHitCount() // 고정 타격 횟수 처치 검사
        {
            var target = new GameObject("FixedDamageTarget");
            var health = target.AddComponent<HealthComponent>();
            health.Initialize(3f, 1f);

            Assert.That(health.ApplyDamage(new DamageRequest(null, 99f, Vector3.zero)), Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(2f));
            Assert.That(health.ApplyDamage(new DamageRequest(null, 99f, Vector3.zero)), Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(1f));
            Assert.That(health.ApplyDamage(new DamageRequest(null, 99f, Vector3.zero)), Is.True);
            Assert.That(health.IsAlive, Is.False);

            Object.Destroy(target);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScenePoolScope_ReturnsAndReusesInstance() // 반환 오브젝트 재사용 검사
        {
            var poolRoot = new GameObject("PoolRoot");
            var pool = poolRoot.AddComponent<ScenePoolScope>();
            var prefab = new GameObject("PoolPrefab");

            var first = pool.Rent(prefab, Vector3.zero, Quaternion.identity);
            Assert.That(first, Is.Not.Null);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            pool.Return(first);
            Assert.That(pool.ActiveCount, Is.Zero);

            var second = pool.Rent(prefab, Vector3.one, Quaternion.identity);
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.transform.position, Is.EqualTo(Vector3.one));

            pool.ReturnAll();
            Object.Destroy(poolRoot);
            Object.Destroy(prefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CastleTarget_DeathFeedbackRemainsVisibleUntilPulseCompletes() // 사망 펄스 표시 시간 검사
        {
            var targetRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer = targetRoot.GetComponent<Renderer>();
            var targetCollider = targetRoot.GetComponent<Collider>();
            targetRoot.AddComponent<UnitVisualFeedback>();
            var target = targetRoot.AddComponent<CastleTarget>();
            target.EditorConfigure(CastleTargetKind.Wall, 1f, null, null);
            target.Initialize();

            target.Health.ApplyDamage(new DamageRequest(null, 1f, Vector3.zero));

            Assert.That(renderer.enabled, Is.True);
            Assert.That(targetCollider.enabled, Is.False);
            yield return new WaitForSeconds(UnitVisualFeedback.DeathPulseDurationSeconds + 0.05f);
            Assert.That(renderer.enabled, Is.False);

            target.Shutdown();
            Object.Destroy(targetRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CastleRaid_DeadAssaultUnitReturnsToPoolAfterFeedback() // 사망 연출 뒤 풀 반환 검사
        {
            var poolRoot = new GameObject("PoolRoot");
            var pool = poolRoot.AddComponent<ScenePoolScope>();
            var unitPrefab = new GameObject("CastleAssaultUnitPrefab");
            unitPrefab.SetActive(false);
            unitPrefab.AddComponent<CastleAssaultUnit>();
            unitPrefab.GetComponent<NavMeshAgent>().enabled = false;
            var unitObject = pool.Rent(unitPrefab, Vector3.zero, Quaternion.identity);
            var unit = unitObject.GetComponent<CastleAssaultUnit>();
            var controllerRoot = new GameObject("CastleRaidController");
            var controller = controllerRoot.AddComponent<CastleRaidController>();
            var activeUnits = GetPrivateField<System.Collections.Generic.List<CastleAssaultUnit>>(
                controller,
                "activeUnits");
            activeUnits.Add(unit);
            SetPrivateField(controller, "poolScope", pool);
            SetPrivateField(
                controller,
                "startData",
                new CastleRaidStartData(SeedBattlePartySnapshotFactory.Create(), 5));

            InvokePrivateMethod(controller, "HandleUnitDied", unit);

            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            yield return new WaitForSeconds(UnitVisualFeedback.DeathPulseDurationSeconds + 0.1f);
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(unitObject.activeSelf, Is.False);
            Assert.That(activeUnits.Contains(unit), Is.False);

            controller.Shutdown();
            Object.Destroy(controllerRoot);
            Object.Destroy(poolRoot);
            Object.Destroy(unitPrefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CastleDeploymentZone_AllowsOnlySquareOuterRing() // 정사각형 외곽 배치 구역 검사
        {
            var zoneRoot = new GameObject("CastleDeploymentZone");
            var zone = zoneRoot.AddComponent<CastleDeploymentZone>();

            Assert.That(zone.ContainsWorldPosition(Vector3.zero), Is.False);
            Assert.That(zone.ContainsWorldPosition(new Vector3(8f, 0f, 0f)), Is.True);
            Assert.That(zone.ContainsWorldPosition(new Vector3(0f, 0f, -8f)), Is.True);
            Assert.That(zone.ContainsWorldPosition(new Vector3(9.3f, 0f, 0f)), Is.False);

            Object.Destroy(zoneRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CastleRaid_CornerBreachContinuesWithoutConsoleError() // 모서리 파괴 중 오류 방지
        {
            var wallRoot = new GameObject("AliveWall");
            var wall = wallRoot.AddComponent<CastleTarget>();
            wall.EditorConfigure(CastleTargetKind.Wall, 10f, null, null);
            wall.Initialize();

            var controllerRoot = new GameObject("CastleRaidController");
            var controller = controllerRoot.AddComponent<CastleRaidController>();
            var status = CreateText("CastleRaidStatus");
            controller.EditorConfigure(
                null,
                null,
                null,
                null,
                null,
                null,
                new[] { wall },
                null,
                status,
                null,
                null,
                null);
            SetPrivateField(controller, "<IsRunning>k__BackingField", true);

            yield return InvokePrivateCoroutine(controller, "VerifyInnerPath");

            Assert.That(controller.InnerPathOpen, Is.False);
            Assert.That(status.text, Is.EqualTo("모서리만으로는 진입할 수 없습니다 · 인접 성벽도 파괴하세요"));
            LogAssert.NoUnexpectedReceived();

            wall.Shutdown();
            Object.Destroy(status.gameObject);
            Object.Destroy(controllerRoot);
            Object.Destroy(wallRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CastleRaid_CornerBreachRequiresAnAdjacentWallTile() // 모서리 단독 진입 차단 검사
        {
            var wallPositions = new[]
            {
                new Vector3(-1f, 0f, -1f),
                new Vector3(-1f, 0f, 1f),
                new Vector3(1f, 0f, -1f),
                new Vector3(1f, 0f, 1f),
                new Vector3(0f, 0f, -1f)
            };
            var wallRoots = new GameObject[wallPositions.Length];
            var walls = new CastleTarget[wallPositions.Length];
            for (var i = 0; i < wallPositions.Length; i++)
            {
                wallRoots[i] = new GameObject($"Wall_{i}");
                wallRoots[i].transform.position = wallPositions[i];
                walls[i] = wallRoots[i].AddComponent<CastleTarget>();
                walls[i].EditorConfigure(CastleTargetKind.Wall, 1f, null, null);
                walls[i].Initialize();
            }

            var controllerRoot = new GameObject("CastleRaidController");
            var controller = controllerRoot.AddComponent<CastleRaidController>();
            controller.EditorConfigure(null, null, null, null, null, null, walls, null, null, null, null, null);

            walls[0].Health.ApplyDamage(new DamageRequest(null, 1f, wallPositions[0]));
            Assert.That((bool)InvokePrivateMethod(controller, "HasNonCornerDestroyedWall"), Is.False);

            walls[4].Health.ApplyDamage(new DamageRequest(null, 1f, wallPositions[4]));
            Assert.That((bool)InvokePrivateMethod(controller, "HasNonCornerDestroyedWall"), Is.True);

            for (var i = 0; i < walls.Length; i++)
            {
                walls[i].Shutdown();
                Object.Destroy(wallRoots[i]);
            }

            Object.Destroy(controllerRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SfxPool_PrewarmsOnlyConfiguredVoiceCount() // 모바일 Voice 사전 생성 상한 검사
        {
            var root = new GameObject("SfxPool");
            root.SetActive(false);
            var pool = root.AddComponent<SfxPool>();
            pool.EditorConfigure(4, 2);
            root.SetActive(true);

            yield return null;

            Assert.That(pool.VoiceCount, Is.EqualTo(2));
            Assert.That(pool.MaxVoices, Is.EqualTo(4));
            Assert.That(root.transform.childCount, Is.EqualTo(2));
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FloatingNumbers_MergeMoveOnCurveAndReturnToPool() // 합산·곡선 이동·반환 검사
        {
            var poolRoot = new GameObject("FloatingNumberPool");
            var pool = poolRoot.AddComponent<ScenePoolScope>();
            var numberPrefab = new GameObject("FloatingNumberPrefab", typeof(TextMeshPro), typeof(FloatingNumberView));
            numberPrefab.GetComponent<FloatingNumberView>().EditorConfigure(numberPrefab.GetComponent<TMP_Text>());
            numberPrefab.SetActive(false);
            var presenterRoot = new GameObject("FloatingNumberPresenter");
            presenterRoot.SetActive(false);
            var presenter = presenterRoot.AddComponent<FloatingNumberPresenter>();
            presenter.EditorConfigure(pool, numberPrefab);
            presenterRoot.SetActive(true);

            presenter.Queue(Vector3.zero, 2f, FloatingNumberStyle.EnemyDamage, 1001);
            presenter.Queue(Vector3.one, 3f, FloatingNumberStyle.EnemyDamage, 1001);
            yield return new WaitForSecondsRealtime(0.12f);
            yield return null;

            Assert.That(presenter.ActiveNumberCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            var active = GetPrivateField<HashSet<GameObject>>(pool, "active").Single();
            Assert.That(active.GetComponent<TMP_Text>().text, Is.EqualTo("5"));
            var curveStart = active.transform.position;

            yield return new WaitForSecondsRealtime(0.18f);
            Assert.That(active.transform.position.y, Is.GreaterThan(curveStart.y + 0.05f));
            Assert.That(Mathf.Abs(active.transform.position.x - curveStart.x), Is.GreaterThan(0.05f));

            yield return new WaitForSecondsRealtime(0.8f);
            Assert.That(presenter.ActiveNumberCount, Is.Zero);
            Assert.That(pool.ActiveCount, Is.Zero);

            Object.Destroy(presenterRoot);
            Object.Destroy(poolRoot);
            Object.Destroy(numberPrefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RewardAcquirePresenter_PlaysAndReturnsConfirmedItem() // 확정 보상 이동·반환 검사
        {
            var displayObject = new GameObject("RewardDisplay", typeof(RectTransform));
            displayObject.SetActive(false);
            var display = displayObject.GetComponent<RectTransform>();
            var pool = displayObject.AddComponent<ScenePoolScope>();
            var presenter = displayObject.AddComponent<RewardAcquirePresenter>();
            var spawn = new GameObject("Spawn", typeof(RectTransform)).GetComponent<RectTransform>();
            spawn.SetParent(display, false);
            var target = new GameObject("Target", typeof(RectTransform)).GetComponent<RectTransform>();
            target.SetParent(display, false);
            target.anchoredPosition = new Vector2(300f, 180f);
            var itemPrefab = new GameObject("RewardItem", typeof(RectTransform), typeof(CanvasGroup), typeof(RewardAcquireView));
            var label = CreateText("RewardLabel");
            label.transform.SetParent(itemPrefab.transform, false);
            itemPrefab.GetComponent<RewardAcquireView>().EditorConfigure(label);
            itemPrefab.SetActive(false);
            presenter.EditorConfigure(pool, itemPrefab, display, spawn, target, null);
            displayObject.SetActive(true);

            presenter.PlayConfirmed(RewardPresentationRequest.Gold(7));
            yield return null;

            Assert.That(presenter.ActiveItemCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            var active = GetPrivateField<HashSet<GameObject>>(pool, "active").Single();
            Assert.That(active.GetComponentInChildren<TMP_Text>().text, Does.Contain("골드 +7"));

            yield return new WaitForSecondsRealtime(1f);
            Assert.That(presenter.ActiveItemCount, Is.Zero);
            Assert.That(pool.ActiveCount, Is.Zero);

            Object.Destroy(displayObject);
            Object.Destroy(itemPrefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RewardAcquirePresenter_DisableOverlay_ReturnsItemWithoutReparenting() // 비활성화 중 부모 변경 방지
        {
            var overlayObject = new GameObject("RewardOverlay", typeof(RectTransform));
            overlayObject.SetActive(false);
            var pool = overlayObject.AddComponent<ScenePoolScope>();
            var presenter = overlayObject.AddComponent<RewardAcquirePresenter>();
            var display = new GameObject("DisplayRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            display.SetParent(overlayObject.transform, false);
            var spawn = new GameObject("Spawn", typeof(RectTransform)).GetComponent<RectTransform>();
            spawn.SetParent(display, false);
            var target = new GameObject("Target", typeof(RectTransform)).GetComponent<RectTransform>();
            target.SetParent(display, false);
            var itemPrefab = new GameObject("RewardItem", typeof(RectTransform), typeof(CanvasGroup), typeof(RewardAcquireView));
            var label = CreateText("RewardLabel");
            label.transform.SetParent(itemPrefab.transform, false);
            itemPrefab.GetComponent<RewardAcquireView>().EditorConfigure(label);
            itemPrefab.SetActive(false);
            presenter.EditorConfigure(pool, itemPrefab, display, spawn, target, null);
            overlayObject.SetActive(true);

            presenter.PlayConfirmed(RewardPresentationRequest.Gold(7));
            yield return null;

            var active = GetPrivateField<HashSet<GameObject>>(pool, "active").Single();
            Assert.That(active.transform.parent, Is.EqualTo(display));
            overlayObject.SetActive(false);

            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(active.activeSelf, Is.False);
            Assert.That(active.transform.parent, Is.EqualTo(display));

            overlayObject.SetActive(true);
            presenter.PlayConfirmed(RewardPresentationRequest.Gold(7));
            yield return null;

            var reused = GetPrivateField<HashSet<GameObject>>(pool, "active").Single();
            Assert.That(reused, Is.SameAs(active));

            Object.Destroy(overlayObject);
            Object.Destroy(itemPrefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Expedition_ClimaxPlaysOnlyForEachWavesLastEnemy() // 웨이브별 마지막 적 약한 연출 검사
        {
            var poolRoot = new GameObject("PoolRoot");
            var pool = poolRoot.AddComponent<ScenePoolScope>();
            var vfxPrefab = new GameObject("ClimaxVfxPrefab");
            vfxPrefab.SetActive(false);
            vfxPrefab.AddComponent<SeedFeedbackVfx>();
            var feedbackRoot = new GameObject("CombatFeedback");
            var feedback = feedbackRoot.AddComponent<CombatFeedbackPlayer>();
            feedback.EditorConfigure(pool, vfxPrefab, null);
            var worldRoot = new GameObject("CombatWorld");
            var world = worldRoot.AddComponent<CombatWorld>();
            world.EditorConfigure(pool, feedback, null);
            var expeditionRoot = new GameObject("ExpeditionController");
            var expedition = expeditionRoot.AddComponent<ExpeditionController>();
            SetPrivateField(expedition, "combatWorld", world);
            SetPrivateField(expedition, "running", true);

            var waveOneFirst = new GameObject("Wave1_First").AddComponent<UnitActor>();
            var waveOneLast = new GameObject("Wave1_Last").AddComponent<UnitActor>();
            var waveTwoLast = new GameObject("Wave2_Last").AddComponent<UnitActor>();
            InvokePrivateMethod(expedition, "TrackWaveEnemy", waveOneFirst, 1);
            InvokePrivateMethod(expedition, "TrackWaveEnemy", waveOneLast, 1);
            InvokePrivateMethod(expedition, "TrackWaveEnemy", waveTwoLast, 2);

            InvokePrivateMethod(expedition, "HandleWaveEnemyDied", waveOneFirst);
            Assert.That(pool.ActiveCount, Is.Zero);
            InvokePrivateMethod(expedition, "HandleWaveEnemyDied", waveOneLast);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(GetActiveFeedbackSize(pool), Is.EqualTo(0.38f).Within(0.001f));

            pool.ReturnAll();
            InvokePrivateMethod(expedition, "HandleWaveEnemyDied", waveTwoLast);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(GetActiveFeedbackSize(pool), Is.EqualTo(0.38f).Within(0.001f));

            expedition.Shutdown();
            Object.Destroy(waveOneFirst.gameObject);
            Object.Destroy(waveOneLast.gameObject);
            Object.Destroy(waveTwoLast.gameObject);
            Object.Destroy(expeditionRoot);
            Object.Destroy(worldRoot);
            Object.Destroy(feedbackRoot);
            Object.Destroy(poolRoot);
            Object.Destroy(vfxPrefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Expedition_DeploysReservesInOrderAtDeadUnitsPosition() // 사망 자리 예비 순차 투입
        {
            var poolRoot = new GameObject("ReservePool");
            var pool = poolRoot.AddComponent<ScenePoolScope>();
            var worldRoot = new GameObject("ReserveCombatWorld");
            var world = worldRoot.AddComponent<CombatWorld>();
            world.enabled = false;
            world.EditorConfigure(pool, null, null);
            var unitPrefab = new GameObject("ReserveUnitPrefab");
            unitPrefab.AddComponent<UnitActor>();
            unitPrefab.SetActive(false);
            var spawnRoot = new GameObject("MainPartySlot1");
            spawnRoot.transform.position = new Vector3(-2f, 0f, 1f);
            var expeditionRoot = new GameObject("ReserveExpedition");
            var expedition = expeditionRoot.AddComponent<ExpeditionController>();
            expedition.EditorConfigure(
                null,
                world,
                unitPrefab,
                null,
                new[] { spawnRoot.transform },
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            var stats = new UnitStatsSnapshot
            {
                maxHealth = 10f,
                damage = 1f,
                moveSpeed = 1f,
                attackRange = 1f,
                attackInterval = 1f
            };
            var party = new BattlePartySnapshot(
                new[] { new BattleUnitSnapshot("main", stats) },
                new[]
                {
                    new BattleUnitSnapshot("reserve_1", stats),
                    new BattleUnitSnapshot("reserve_2", stats)
                });
            SetPrivateField(expedition, "party", party);
            SetPrivateField(expedition, "activeRunParty", party);
            SetPrivateField(expedition, "running", true);

            InvokePrivateMethod(expedition, "SpawnParty");
            var tracked = GetPrivateField<Dictionary<UnitActor, int>>(expedition, "playerSlotByActor");
            var main = tracked.Keys.Single();
            expedition.SetPartyForNextRun(new BattlePartySnapshot(
                new[] { new BattleUnitSnapshot("next_main", stats) },
                new[] { new BattleUnitSnapshot("next_reserve", stats) }));
            var firstDeathPosition = new Vector3(2.5f, 0f, -1.5f);
            main.transform.position = firstDeathPosition;
            main.Health.ApplyDamage(new DamageRequest(null, 999f, firstDeathPosition));

            Assert.That(world.CountAlive(UnitTeam.Player), Is.EqualTo(1));
            var firstReserve = tracked.Keys.Single();
            Assert.That(firstReserve.UnitId, Is.EqualTo("reserve_1"));
            Assert.That(firstReserve.transform.position, Is.EqualTo(firstDeathPosition));

            var secondDeathPosition = new Vector3(3.5f, 0f, -0.5f);
            firstReserve.transform.position = secondDeathPosition;
            firstReserve.Health.ApplyDamage(new DamageRequest(null, 999f, secondDeathPosition));

            Assert.That(world.CountAlive(UnitTeam.Player), Is.EqualTo(1));
            var secondReserve = tracked.Keys.Single();
            Assert.That(secondReserve.UnitId, Is.EqualTo("reserve_2"));
            Assert.That(secondReserve.transform.position, Is.EqualTo(secondDeathPosition));

            secondReserve.Health.ApplyDamage(new DamageRequest(null, 999f, secondDeathPosition));
            Assert.That(world.CountAlive(UnitTeam.Player), Is.Zero);
            Assert.That(tracked, Is.Empty);
            Assert.That(GetPrivateField<int>(expedition, "nextReserveIndex"), Is.EqualTo(2));

            expedition.Shutdown();
            Object.Destroy(expeditionRoot);
            Object.Destroy(spawnRoot);
            Object.Destroy(worldRoot);
            Object.Destroy(poolRoot);
            Object.Destroy(unitPrefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CastleRaid_ClimaxPlaysOnlyForMainCastle() // 성벽 제외·최종 건물 강한 연출 검사
        {
            var poolRoot = new GameObject("PoolRoot");
            var pool = poolRoot.AddComponent<ScenePoolScope>();
            var vfxPrefab = new GameObject("ClimaxVfxPrefab");
            vfxPrefab.SetActive(false);
            vfxPrefab.AddComponent<SeedFeedbackVfx>();
            var feedbackRoot = new GameObject("CombatFeedback");
            var feedback = feedbackRoot.AddComponent<CombatFeedbackPlayer>();
            feedback.EditorConfigure(pool, vfxPrefab, null);
            var wallRoot = new GameObject("Wall");
            var wall = wallRoot.AddComponent<CastleTarget>();
            wall.EditorConfigure(CastleTargetKind.Wall, 1f, null, null);
            var mainCastleRoot = new GameObject("MainCastle");
            var mainCastle = mainCastleRoot.AddComponent<CastleTarget>();
            mainCastle.EditorConfigure(CastleTargetKind.MainCastle, 1f, null, null);
            var controllerRoot = new GameObject("CastleRaidController");
            var controller = controllerRoot.AddComponent<CastleRaidController>();
            controller.EditorConfigure(
                pool,
                feedback,
                null,
                null,
                null,
                null,
                new[] { wall, mainCastle },
                null,
                null,
                null,
                null,
                null);

            SetPrivateField(controller, "<IsRunning>k__BackingField", true);
            InvokePrivateMethod(controller, "HandleTargetDestroyed", wall);
            Assert.That(pool.ActiveCount, Is.Zero);

            SetPrivateField(controller, "<IsRunning>k__BackingField", true);
            InvokePrivateMethod(controller, "HandleTargetDestroyed", mainCastle);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(GetActiveFeedbackSize(pool), Is.EqualTo(0.8f).Within(0.001f));

            controller.Shutdown();
            Object.Destroy(controllerRoot);
            Object.Destroy(mainCastleRoot);
            Object.Destroy(wallRoot);
            Object.Destroy(feedbackRoot);
            Object.Destroy(poolRoot);
            Object.Destroy(vfxPrefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainBattle_ContentClickDuringSettlementShowsFeedback() // 저장 중 중복 진입 안내 검사
        {
            var expeditionRoot = new GameObject("ExpeditionController");
            var expedition = expeditionRoot.AddComponent<ExpeditionController>();
            SetPrivateField(expedition, "settling", true);

            var sceneRootObject = new GameObject("MainBattleSceneRoot");
            var sceneRoot = sceneRootObject.AddComponent<MainBattleSceneRoot>();
            var status = CreateText("MainBattleStatus");
            sceneRoot.EditorConfigure(expedition, null, null, null, status);
            SetPrivateField(sceneRoot, "<IsInitialized>k__BackingField", true);

            InvokePrivateMethod(sceneRoot, "OpenCastleRaid");

            Assert.That(status.text, Is.EqualTo("전투 결과 정산 중입니다. 잠시 후 다시 시도하세요."));

            Object.Destroy(status.gameObject);
            Object.Destroy(sceneRootObject);
            Object.Destroy(expeditionRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContentClearOverlay_ConfirmsOnlyOnce() // 결과 확인 중복 실행 방지
        {
            var overlayRoot = new GameObject("ContentClearOverlay", typeof(RectTransform));
            overlayRoot.SetActive(false);
            var title = CreateText("TitleText");
            var summary = CreateText("SummaryText");
            var reward = CreateText("RewardText");
            title.transform.SetParent(overlayRoot.transform, false);
            summary.transform.SetParent(overlayRoot.transform, false);
            reward.transform.SetParent(overlayRoot.transform, false);
            var buttonRoot = new GameObject("ConfirmButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            buttonRoot.transform.SetParent(overlayRoot.transform, false);
            var button = buttonRoot.AddComponent<Button>();
            var overlay = overlayRoot.AddComponent<ContentClearOverlay>();
            overlay.EditorConfigure(title, summary, reward, button);
            var confirmationCount = 0;

            Assert.That(overlay.TryShow("처치 12마리", "골드 +12", () => confirmationCount++), Is.True);
            yield return null;

            Assert.That(overlay.IsVisible, Is.True);
            Assert.That(title.text, Is.EqualTo("클리어"));
            Assert.That(summary.text, Is.EqualTo("처치 12마리"));
            Assert.That(reward.text, Is.EqualTo("골드 +12"));

            button.onClick.Invoke();
            button.onClick.Invoke();
            Assert.That(confirmationCount, Is.EqualTo(1));
            Assert.That(overlay.IsVisible, Is.False);

            Object.Destroy(overlayRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FoodRiotDevBootstrap_IgnoresDestroyedControllerOnDestroy() // 파괴 순서 예외 방지
        {
            var controllerRoot = new GameObject("FoodRiotController");
            var controller = controllerRoot.AddComponent<FoodRiotController>();
            var bootstrapRoot = new GameObject("FoodRiotDevBootstrap");
            bootstrapRoot.SetActive(false);
            var bootstrap = bootstrapRoot.AddComponent<FoodRiotDevBootstrap>();
            bootstrap.EditorConfigure(controller, null);

            Object.Destroy(controllerRoot);
            yield return null;
            Object.Destroy(bootstrapRoot);
            yield return null;

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator UnitActor_ManualHoldPausesSelfButRemainsTargetableAndDamageable() // 잡힌 중 피격·사망 유지
        {
            var worldRoot = new GameObject("ManualHoldWorld");
            var world = worldRoot.AddComponent<CombatWorld>();
            world.enabled = false;
            var heldRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var enemyRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            heldRoot.name = "HeldPlayer";
            enemyRoot.name = "EnemySeeker";
            heldRoot.transform.position = Vector3.zero;
            enemyRoot.transform.position = new Vector3(4f, 0f, 0f);
            var held = heldRoot.AddComponent<UnitActor>();
            var enemy = enemyRoot.AddComponent<UnitActor>();
            var stats = new UnitStatsSnapshot
            {
                maxHealth = 10f,
                damage = 1f,
                moveSpeed = 3f,
                attackRange = 0.5f,
                attackInterval = 1f
            };
            held.Initialize(new UnitSpawnRequest("held", stats, UnitTeam.Player), world, null);
            enemy.Initialize(new UnitSpawnRequest("enemy", stats, UnitTeam.Enemy), world, null);

            Assert.That(held.BeginManualReposition(), Is.True);
            var heldPosition = held.transform.position;
            held.Tick(1f);
            Assert.That(held.transform.position, Is.EqualTo(heldPosition));
            Assert.That(world.FindNearestOpponent(enemy, float.PositiveInfinity), Is.SameAs(held));

            Assert.That(held.Health.ApplyDamage(new DamageRequest(enemy, 3f, heldPosition)), Is.True);
            Assert.That(held.Health.CurrentHealth, Is.EqualTo(7f));
            var died = false;
            held.Died += _ => died = true;
            Assert.That(held.Health.ApplyDamage(new DamageRequest(enemy, 99f, heldPosition)), Is.True);
            Assert.That(held.IsAlive, Is.False);
            Assert.That(died, Is.True);

            Object.Destroy(worldRoot);
            Object.Destroy(heldRoot);
            Object.Destroy(enemyRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnitActor_ReinitializeReplacesMonsterTint() // 풀 재사용 색상 누수 방지
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer = root.GetComponent<Renderer>();
            var shader = Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.color = Color.white;
            renderer.sharedMaterial = material;
            root.AddComponent<UnitVisualFeedback>();
            var actor = root.AddComponent<UnitActor>();
            var stats = new UnitStatsSnapshot
            {
                maxHealth = 10f,
                damage = 1f,
                moveSpeed = 1f,
                attackRange = 1f,
                attackInterval = 1f
            };
            var tint = new Color(0.2f, 0.8f, 0.4f, 1f);

            actor.Initialize(new UnitSpawnRequest(
                "tinted",
                stats,
                UnitTeam.Player,
                visualTint: tint), null, null);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var tintedColor = block.GetColor(Shader.PropertyToID("_Color"));
            Assert.That(tintedColor.r, Is.EqualTo(tint.r).Within(0.01f));
            Assert.That(tintedColor.g, Is.EqualTo(tint.g).Within(0.01f));
            Assert.That(tintedColor.b, Is.EqualTo(tint.b).Within(0.01f));

            actor.Initialize(new UnitSpawnRequest("white", stats, UnitTeam.Player), null, null);
            renderer.GetPropertyBlock(block);
            var resetColor = block.GetColor(Shader.PropertyToID("_Color"));
            Assert.That(resetColor.r, Is.EqualTo(1f).Within(0.01f));
            Assert.That(resetColor.g, Is.EqualTo(1f).Within(0.01f));
            Assert.That(resetColor.b, Is.EqualTo(1f).Within(0.01f));

            Object.Destroy(root);
            Object.Destroy(material);
            yield return null;
        }

        private static TMP_Text CreateText(string name) // 테스트용 TMP 글자 생성
        {
            return new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
                .GetComponent<TMP_Text>();
        }

        private static float GetActiveFeedbackSize(ScenePoolScope pool) // 현재 클라이맥스 VFX 크기 읽기
        {
            var active = GetPrivateField<HashSet<GameObject>>(pool, "active");
            var feedback = active.Single().GetComponent<SeedFeedbackVfx>();
            Assert.That(feedback, Is.Not.Null);
            return GetPrivateField<float>(feedback, "size");
        }

        private static IEnumerator InvokePrivateCoroutine(object target, string methodName) // 비공개 코루틴 실행 보조
        {
            return (IEnumerator)InvokePrivateMethod(target, methodName);
        }

        private static object InvokePrivateMethod(object target, string methodName, params object[] arguments) // 비공개 메서드 호출 보조
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Private method was not found: {methodName}");
            return method.Invoke(target, arguments);
        }

        private static T GetPrivateField<T>(object target, string fieldName) // 비공개 필드 읽기 보조
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Private field was not found: {fieldName}");
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value) // 비공개 필드 설정 보조
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Private field was not found: {fieldName}");
            field.SetValue(target, value);
        }
    }
}
