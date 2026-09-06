using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectMT.Contents.CastleRaidHex.PlayMode.Tests
{
    public sealed class HexCastleDestructionPlayModeTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
            owned.Clear();
        }

        [UnityTest]
        public IEnumerator ThreeKinds_OpenImmediately_RetainRubble_Expire_AndReplayOnReset()
        {
            var kinds = new[] { HexCastleCellKind.Wall, HexCastleCellKind.Building, HexCastleCellKind.Palace };
            var counts = new[] { 24, 36, 48 };
            var cells = new HexCastleCellRuntime[3];
            for (var i = 0; i < kinds.Length; i++)
            {
                cells[i] = CreateCell(kinds[i]);
                Assert.That(cells[i].DestroyedVisualRoot, Is.Null);
                Assert.That(cells[i].ApplyDamage(1000f, cells[i].transform.position), Is.True);
                Assert.That(cells[i].IsBlocked, Is.False);
                Assert.That(cells[i].FootprintCollider.enabled, Is.False);
                Assert.That(cells[i].ContentVisualRoot.gameObject.activeSelf, Is.False);
                var rubble = cells[i].DestroyedVisualRoot;
                Assert.That(rubble.gameObject.activeSelf, Is.True);
                Assert.That(rubble.GetComponent<HexCastleDestructionVisual>().FragmentCount, Is.EqualTo(counts[i]));
                Assert.That(rubble.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(rubble.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(cells[i].ApplyDamage(1000f, Vector3.zero), Is.False);
            }
            yield return new WaitForSeconds(1.2f);
            foreach (var cell in cells) Assert.That(cell.DestroyedVisualRoot.gameObject.activeSelf, Is.True);
            yield return new WaitForSeconds(3.4f);
            foreach (var cell in cells) Assert.That(cell.DestroyedVisualRoot.gameObject.activeSelf, Is.True);
            yield return new WaitForSeconds(1.3f);
            foreach (var cell in cells)
            {
                var rubble = cell.DestroyedVisualRoot;
                Assert.That(rubble.gameObject.activeSelf, Is.False);
                cell.InitializeState();
                Assert.That(cell.IsAlive, Is.True);
                Assert.That(cell.IsBlocked, Is.True);
                Assert.That(cell.ApplyDamage(1000f, Vector3.zero), Is.True);
                Assert.That(cell.DestroyedVisualRoot, Is.SameAs(rubble));
                Assert.That(rubble.gameObject.activeSelf, Is.True);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MassDestruction_CapsEffects_AndEmptyPalaceOccupancyHasNoRubble()
        {
            var cells = new List<HexCastleCellRuntime>();
            for (var i = 0; i < HexCastleDestructionVisual.MaximumVisibleEffects + 3; i++)
            {
                var cell = CreateCell(HexCastleCellKind.Wall);
                cells.Add(cell);
                cell.ApplyDamage(1000f, Vector3.zero);
            }
            var visible = 0;
            foreach (var cell in cells) if (cell.DestroyedVisualRoot.gameObject.activeSelf) visible++;
            Assert.That(visible, Is.EqualTo(HexCastleDestructionVisual.MaximumVisibleEffects));
            Assert.That(cells[0].DestroyedVisualRoot.gameObject.activeSelf, Is.False);
            var empty = CreateCell(HexCastleCellKind.Palace, false);
            empty.ApplyDamage(1000f, Vector3.zero);
            Assert.That(empty.IsDestroyed, Is.True);
            Assert.That(empty.DestroyedVisualRoot, Is.Null);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private HexCastleCellRuntime CreateCell(HexCastleCellKind kind, bool addVisual = true)
        {
            var root = new GameObject("DestructionTest_" + kind);
            owned.Add(root);
            var tile = new GameObject("Tile").transform;
            tile.SetParent(root.transform, false);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            if (addVisual)
            {
                var set = Resources.Load<HexCastleVisualSet>("HexCastleRuntimeVisualSet");
                var prefab = kind == HexCastleCellKind.Wall ? set.ResolveWall(HexCastleWallVisualKind.Straight) :
                    kind == HexCastleCellKind.Palace ? set.Palace : set.ResolveBuilding("building_barracks_blue");
                Object.Instantiate(prefab, visual, false);
            }
            var health = root.AddComponent<HealthComponent>();
            var collider = root.AddComponent<BoxCollider>();
            var cell = root.AddComponent<HexCastleCellRuntime>();
            cell.Configure(new HexCastleCell(new HexCoordinates(0, 0), kind, hitPoints: 100f, initialBlocked: true),
                health, collider, tile, visual);
            return cell;
        }
    }
}
