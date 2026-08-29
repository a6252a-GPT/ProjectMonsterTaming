using NUnit.Framework;
using ProjectMT.Contents.CastleRaid;
using ProjectMT.Shared.Combat;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class CastleRaidCameraControllerTests
    {
        [Test]
        public void PinchZoom_ClampsSizeAndSuppressesBothDeploymentClicks()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Controller.BeginPointer(1, new Vector2(200f, 256f));
                fixture.Controller.BeginPointer(2, new Vector2(312f, 256f));
                fixture.Controller.MovePointer(2, new Vector2(400f, 256f));
                fixture.Controller.EndPointer(2);
                fixture.Controller.EndPointer(1);

                Assert.That(fixture.Controller.TargetOrthographicSize, Is.EqualTo(5f).Within(0.001f));
                Assert.That(fixture.Camera.orthographicSize, Is.GreaterThan(5f));
                Advance(fixture.Controller);
                Assert.That(fixture.Camera.orthographicSize, Is.EqualTo(5f).Within(0.001f));
                Assert.That(fixture.Controller.ConsumeClickSuppression(1), Is.True);
                Assert.That(fixture.Controller.ConsumeClickSuppression(2), Is.True);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void TwoFingerTranslation_PansWithoutChangingFinalZoom()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Controller.BeginPointer(1, new Vector2(200f, 256f));
                fixture.Controller.BeginPointer(2, new Vector2(312f, 256f));
                fixture.Controller.MovePointer(1, new Vector2(250f, 256f));
                fixture.Controller.MovePointer(2, new Vector2(362f, 256f));
                fixture.Controller.EndPointer(2);
                fixture.Controller.EndPointer(1);

                Assert.That(fixture.Controller.TargetOrthographicSize, Is.EqualTo(8.5f).Within(0.001f));
                Assert.That(fixture.Controller.TargetGroundCenter, Is.Not.EqualTo(Vector2.zero));
                Advance(fixture.Controller);
                Assert.That(fixture.Camera.orthographicSize, Is.EqualTo(8.5f).Within(0.001f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void DragPan_MovesCameraButShortTapKeepsDeploymentClick()
        {
            var fixture = CreateFixture();
            try
            {
                var initialPosition = fixture.Camera.transform.position;
                fixture.Controller.BeginPointer(-1, new Vector2(256f, 256f));
                fixture.Controller.MovePointer(-1, new Vector2(356f, 256f));
                fixture.Controller.EndPointer(-1);

                Advance(fixture.Controller);
                Assert.That(fixture.Camera.transform.position, Is.Not.EqualTo(initialPosition));
                Assert.That(fixture.Controller.ConsumeClickSuppression(-1), Is.True);

                var positionAfterDrag = fixture.Camera.transform.position;
                fixture.Controller.BeginPointer(-1, new Vector2(256f, 256f));
                fixture.Controller.MovePointer(-1, new Vector2(260f, 256f));
                fixture.Controller.EndPointer(-1);
                fixture.Controller.EditorStep(1f / 60f);
                Assert.That(fixture.Camera.transform.position, Is.EqualTo(positionAfterDrag));
                Assert.That(fixture.Controller.ConsumeClickSuppression(-1), Is.False);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void WheelZoom_UsesConfiguredMinimumAndMaximumRange()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Controller.ZoomByScroll(new Vector2(256f, 256f), 1f);
                Assert.That(fixture.Controller.TargetOrthographicSize, Is.LessThan(8.5f));
                Advance(fixture.Controller);
                Assert.That(fixture.Camera.orthographicSize, Is.LessThan(8.5f));

                fixture.Controller.ZoomByScroll(new Vector2(256f, 256f), -100f);
                Assert.That(fixture.Controller.TargetOrthographicSize, Is.EqualTo(28.75f).Within(0.001f));
                Advance(fixture.Controller);
                Assert.That(fixture.Camera.orthographicSize, Is.EqualTo(28.75f).Within(0.001f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void WheelZoom_KeepsScreenAnchorOnSameGroundPoint()
        {
            var fixture = CreateFixture();
            try
            {
                var anchor = new Vector2(380f, 330f);
                var before = ResolveGroundPoint(fixture.Camera, anchor);
                fixture.Controller.ZoomByScroll(anchor, 2f);
                Advance(fixture.Controller);
                var after = ResolveGroundPoint(fixture.Camera, anchor);

                Assert.That(Vector3.Distance(before, after), Is.LessThan(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ReleasedDrag_AddsBoundedInertiaAndSettles()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Controller.BeginPointer(-1, new Vector2(256f, 256f));
                fixture.Controller.MovePointer(-1, new Vector2(306f, 256f));
                var targetAtRelease = fixture.Controller.TargetGroundCenter;
                fixture.Controller.EndPointer(-1);
                fixture.Controller.EditorStep(1f / 60f);

                Assert.That(fixture.Controller.TargetGroundCenter, Is.Not.EqualTo(targetAtRelease));
                Advance(fixture.Controller, 240);
                var settledTarget = fixture.Controller.TargetGroundCenter;
                Advance(fixture.Controller, 60);
                Assert.That(Vector2.Distance(settledTarget, fixture.Controller.TargetGroundCenter),
                    Is.LessThan(0.001f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void Awake_DisablesImpulseThatWouldOverwriteMovingCameraTransform()
        {
            var cameraObject = new GameObject("CastleRaidCamera_ImpulseConflict_Test");
            try
            {
                cameraObject.AddComponent<Camera>();
                var impulse = cameraObject.AddComponent<CameraImpulseRig>();
                var controller = cameraObject.AddComponent<CastleRaidCameraController>();
                typeof(CastleRaidCameraController)
                    .GetMethod("Awake", System.Reflection.BindingFlags.Instance |
                                        System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(controller, null);

                Assert.That(impulse.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void Advance(CastleRaidCameraController controller, int frameCount = 180)
        {
            for (var frame = 0; frame < frameCount; frame++)
            {
                controller.EditorStep(1f / 120f);
            }
        }

        private static Vector3 ResolveGroundPoint(Camera camera, Vector2 screenPosition)
        {
            var ray = camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            Assert.That(plane.Raycast(ray, out var distance), Is.True);
            return ray.GetPoint(distance);
        }

        private static CameraFixture CreateFixture()
        {
            var cameraObject = new GameObject("CastleRaidCamera_Test");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.SetPositionAndRotation(
                new Vector3(15f, 18f, -15f),
                Quaternion.Euler(40.32f, 315f, 0f));
            var targetTexture = new RenderTexture(512, 512, 16);
            targetTexture.Create();
            camera.targetTexture = targetTexture;
            var controller = cameraObject.AddComponent<CastleRaidCameraController>();
            controller.EditorConfigure(
                camera,
                8.5f,
                5f,
                28.75f,
                Vector2.zero,
                new Vector2(50f, 50f));
            return new CameraFixture(cameraObject, camera, controller, targetTexture);
        }

        private sealed class CameraFixture
        {
            public CameraFixture(
                GameObject root,
                Camera camera,
                CastleRaidCameraController controller,
                RenderTexture targetTexture)
            {
                Root = root;
                Camera = camera;
                Controller = controller;
                TargetTexture = targetTexture;
            }

            public GameObject Root { get; }
            public Camera Camera { get; }
            public CastleRaidCameraController Controller { get; }
            public RenderTexture TargetTexture { get; }

            public void Dispose()
            {
                Camera.targetTexture = null;
                TargetTexture.Release();
                Object.DestroyImmediate(TargetTexture);
                Object.DestroyImmediate(Root);
            }
        }
    }
}
