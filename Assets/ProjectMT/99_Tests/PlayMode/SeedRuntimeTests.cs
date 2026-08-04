using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Contents.CastleRaid;
using ProjectMT.Contents.FoodRiot;
using ProjectMT.Features.Expedition;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Pooling;
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
