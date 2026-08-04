using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace ProjectMT.Tests.PlayMode
{
    public sealed class MainBattleMonsterDragInputTests : InputTestFixture // 마우스·터치 직접 재배치 회귀
    {
        [UnityTest]
        public IEnumerator MouseHold_DragsDamageableMonsterAndDropsAtGroundPosition()
        {
            var context = CreateContext();
            var mouse = InputSystem.AddDevice<Mouse>();
            var start = context.Camera.WorldToScreenPoint(context.Actor.GetComponent<Renderer>().bounds.center);
            var destination = new Vector3(2f, context.OriginalPosition.y, 1.5f);
            var destinationScreen = context.Camera.WorldToScreenPoint(destination);

            Move(mouse.position, start);
            Press(mouse.leftButton);
            yield return null;
            Assert.That(context.Controller.IsHolding, Is.True, "마우스를 움직이기 전에 누른 즉시 잡혀야 한다.");
            Assert.That(context.Actor.transform.position.y, Is.GreaterThan(context.OriginalPosition.y + 0.001f),
                "누른 프레임부터 공중 이동이 시작되어야 한다.");
            yield return new WaitForSecondsRealtime(0.1f);

            Assert.That(context.Actor.transform.position.y, Is.GreaterThan(context.OriginalPosition.y + 0.25f));
            Assert.That(context.Actor.Health.ApplyDamage(
                new DamageRequest(null, 2f, context.Actor.transform.position)), Is.True);
            Assert.That(context.Actor.Health.CurrentHealth, Is.EqualTo(8f));

            Move(mouse.position, destinationScreen);
            yield return null;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(context.Actor.transform.position.x, Is.GreaterThan(1.4f));
            Assert.That(context.Actor.transform.position.z, Is.GreaterThan(0.9f));

            Release(mouse.leftButton);
            yield return null;
            yield return new WaitForSecondsRealtime(0.15f);

            Assert.That(context.Controller.IsHolding, Is.False);
            Assert.That(context.Actor.IsManuallyHeld, Is.False);
            Assert.That(context.Actor.transform.position.x, Is.EqualTo(destination.x).Within(0.08f));
            Assert.That(context.Actor.transform.position.z, Is.EqualTo(destination.z).Within(0.08f));
            Assert.That(context.Actor.transform.position.y, Is.EqualTo(context.OriginalPosition.y).Within(0.02f));

            context.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TouchHold_DragsAndDropsMonsterThroughSameMainBattleFlow()
        {
            var context = CreateContext();
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            var start = context.Camera.WorldToScreenPoint(context.Actor.GetComponent<Renderer>().bounds.center);
            var destination = new Vector3(-1.7f, context.OriginalPosition.y, 1.2f);
            var destinationScreen = context.Camera.WorldToScreenPoint(destination);

            BeginTouch(7, start, screen: touchscreen);
            yield return null;
            Assert.That(context.Controller.IsHolding, Is.True, "터치를 움직이기 전에 누른 즉시 잡혀야 한다.");
            Assert.That(context.Actor.transform.position.y, Is.GreaterThan(context.OriginalPosition.y + 0.001f),
                "누른 프레임부터 공중 이동이 시작되어야 한다.");
            yield return new WaitForSecondsRealtime(0.1f);

            MoveTouch(7, destinationScreen, screen: touchscreen);
            yield return null;
            yield return new WaitForSecondsRealtime(0.1f);
            EndTouch(7, destinationScreen, screen: touchscreen);
            yield return null;
            yield return new WaitForSecondsRealtime(0.15f);

            Assert.That(context.Actor.IsManuallyHeld, Is.False);
            Assert.That(context.Actor.transform.position.x, Is.EqualTo(destination.x).Within(0.08f));
            Assert.That(context.Actor.transform.position.z, Is.EqualTo(destination.z).Within(0.08f));

            context.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeldMonsterDeath_CancelsPointerAndCompletesDrop()
        {
            var context = CreateContext();
            var mouse = InputSystem.AddDevice<Mouse>();
            var start = context.Camera.WorldToScreenPoint(context.Actor.GetComponent<Renderer>().bounds.center);

            Move(mouse.position, start);
            Press(mouse.leftButton);
            yield return null;
            Assert.That(context.Controller.IsHolding, Is.True, "누른 즉시 잡힌 뒤 사망 처리까지 이어져야 한다.");
            yield return new WaitForSecondsRealtime(0.1f);

            Assert.That(context.Actor.Health.ApplyDamage(
                new DamageRequest(null, 99f, context.Actor.transform.position)), Is.True);
            Assert.That(context.Actor.IsAlive, Is.False);
            yield return new WaitForSecondsRealtime(0.15f);

            Assert.That(context.Controller.IsHolding, Is.False);
            Assert.That(context.Actor.IsManuallyHeld, Is.False);
            Assert.That(context.Actor.transform.position.x, Is.EqualTo(context.OriginalPosition.x).Within(0.02f));
            Assert.That(context.Actor.transform.position.z, Is.EqualTo(context.OriginalPosition.z).Within(0.02f));
            Assert.That(context.Actor.transform.position.y, Is.EqualTo(context.OriginalPosition.y).Within(0.02f));

            Release(mouse.leftButton);
            context.Destroy();
            yield return null;
        }

        private static DragTestContext CreateContext()
        {
            var cameraObject = new GameObject("MonsterDragTestCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 10f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "MonsterDragTestGround";
            ground.transform.position = new Vector3(0f, -0.1f, 0f);
            ground.transform.localScale = new Vector3(10f, 0.2f, 10f);

            var actorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            actorObject.name = "MonsterDragTestActor";
            actorObject.transform.position = new Vector3(0f, 0.5f, 0f);
            var actor = actorObject.AddComponent<UnitActor>();
            actor.Initialize(new UnitSpawnRequest(
                "drag_test",
                new UnitStatsSnapshot
                {
                    maxHealth = 10f,
                    damage = 1f,
                    moveSpeed = 2f,
                    attackRange = 1f,
                    attackInterval = 1f
                },
                UnitTeam.Player), null, null);

            var controllerObject = new GameObject("MonsterDragTestController");
            var controller = controllerObject.AddComponent<MainBattleMonsterDragController>();
            SetPrivateField(controller, "dropDurationSeconds", 0.06f);
            SetPrivateField(controller, "followSharpness", 40f);
            controller.Configure(camera, ground.GetComponent<Collider>(), () => true);
            return new DragTestContext(cameraObject, ground, actorObject, controllerObject, camera, actor, controller);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class DragTestContext
        {
            private readonly GameObject cameraObject;
            private readonly GameObject groundObject;
            private readonly GameObject actorObject;
            private readonly GameObject controllerObject;

            public DragTestContext(
                GameObject cameraRoot,
                GameObject ground,
                GameObject actorRoot,
                GameObject controllerRoot,
                Camera camera,
                UnitActor actor,
                MainBattleMonsterDragController controller)
            {
                cameraObject = cameraRoot;
                groundObject = ground;
                actorObject = actorRoot;
                controllerObject = controllerRoot;
                Camera = camera;
                Actor = actor;
                Controller = controller;
                OriginalPosition = actor.transform.position;
            }

            public Camera Camera { get; }
            public UnitActor Actor { get; }
            public MainBattleMonsterDragController Controller { get; }
            public Vector3 OriginalPosition { get; }

            public void Destroy()
            {
                Controller.Shutdown();
                Object.Destroy(controllerObject);
                Object.Destroy(actorObject);
                Object.Destroy(groundObject);
                Object.Destroy(cameraObject);
            }
        }
    }
}
