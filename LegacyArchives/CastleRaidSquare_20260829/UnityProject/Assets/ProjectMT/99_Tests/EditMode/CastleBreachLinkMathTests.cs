using NUnit.Framework;
using ProjectMT.Contents.CastleRaid;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class CastleBreachLinkMathTests
    {
        [Test]
        public void MoveAtConstantSpeed_UsesConfiguredWorldSpeed()
        {
            var result = CastleBreachLinkMath.MoveAtConstantSpeed(
                Vector3.zero,
                new Vector3(10f, 0f, 0f),
                2.5f,
                0.4f);

            Assert.That(result, Is.EqualTo(new Vector3(1f, 0f, 0f)));
        }

        [Test]
        public void MoveAtConstantSpeed_DoesNotOvershootDestination()
        {
            var destination = new Vector3(0.3f, 0f, 0f);
            var result = CastleBreachLinkMath.MoveAtConstantSpeed(
                Vector3.zero,
                destination,
                3f,
                1f);

            Assert.That(result, Is.EqualTo(destination));
        }

        [TestCase(-1f, 0.5f)]
        [TestCase(2f, -0.5f)]
        public void MoveAtConstantSpeed_NegativeInputsDoNotMove(float speed, float deltaTime)
        {
            var result = CastleBreachLinkMath.MoveAtConstantSpeed(
                Vector3.zero,
                Vector3.right,
                speed,
                deltaTime);

            Assert.That(result, Is.EqualTo(Vector3.zero));
        }
    }
}
