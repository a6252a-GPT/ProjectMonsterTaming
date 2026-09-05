using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleDestructionVisual : MonoBehaviour // 세 공용 잔해의 튕김·착지·잔류·회수를 담당한다
    {
        public const string ResourceFolder = "CastleRaidDestruction/";
        public const int MaximumVisibleEffects = 32;

        [SerializeField] private Transform[] fragments;
        [SerializeField] private ParticleSystem dust;
        [SerializeField, Min(0.1f)] private float collapseDuration = 0.85f;
        [SerializeField, Min(0f)] private float debrisDuration = 4f;
        [SerializeField, Min(0.1f)] private float sinkDuration = 0.7f;
        [SerializeField, Range(0f, 1f)] private float spread = 0.3f;
        [SerializeField, Min(0f)] private float hopHeight = 0.15f;

        private static readonly LinkedList<HexCastleDestructionVisual> activeEffects =
            new LinkedList<HexCastleDestructionVisual>();
        private LinkedListNode<HexCastleDestructionVisual> activeNode;
        private Vector3[] origins;
        private Vector3[] scales;
        private Vector3[] destinations;
        private Quaternion[] rotations;
        private Quaternion[] restingRotations;
        private float age;
        private bool settled;
        private const float RestingScale = 0.76f;

        public int FragmentCount => fragments == null ? 0 : fragments.Length;
        public float Lifetime => collapseDuration + debrisDuration + sinkDuration;
        public float Elapsed => age;

        public static string ResolveResourceName(HexCastleCellKind kind)
        {
            if (kind == HexCastleCellKind.Palace) return "PF_CRHex_Debris_Palace";
            return kind == HexCastleCellKind.Wall || kind == HexCastleCellKind.Gate ||
                   kind == HexCastleCellKind.Tower
                ? "PF_CRHex_Debris_Wall"
                : "PF_CRHex_Debris_Building";
        }

        public static Transform CreateFor(HexCastleCellRuntime cell)
        {
            if (!Application.isPlaying || cell.ContentVisualRoot == null) return null;
            var renderers = cell.ContentVisualRoot.GetComponentsInChildren<MeshRenderer>(false);
            if (renderers.Length == 0) return null; // 왕궁 주변의 빈 점유 Cell에는 생성하지 않는다
            var prefab = Resources.Load<GameObject>(ResourceFolder + ResolveResourceName(cell.Kind));
            if (prefab == null) return null;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var instance = Instantiate(prefab, cell.transform, false);
            instance.name = "DestroyedVisualRoot";
            instance.SetActive(false);
            instance.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            instance.transform.rotation = Quaternion.identity;
            var parentScale = cell.transform.lossyScale;
            instance.transform.localScale = new Vector3(
                Mathf.Max(0.1f, bounds.size.x) / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                Mathf.Max(0.1f, bounds.size.y) / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
                Mathf.Max(0.1f, bounds.size.z) / Mathf.Max(0.001f, Mathf.Abs(parentScale.z)));
            return instance.transform;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            PrepareFragments();
            age = 0f;
            settled = false;
            Sample(0f);
            activeNode = activeEffects.AddLast(this);
            while (activeEffects.Count > MaximumVisibleEffects)
            {
                var oldest = activeEffects.First.Value;
                if (oldest != null) oldest.gameObject.SetActive(false);
                else activeEffects.RemoveFirst();
            }
            if (dust != null) dust.Play(true);
        }

        private void OnDisable()
        {
            if (activeNode != null)
            {
                activeEffects.Remove(activeNode);
                activeNode = null;
            }
            if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void PrepareFragments()
        {
            if (origins != null) return;
            var count = FragmentCount;
            origins = new Vector3[count];
            scales = new Vector3[count];
            destinations = new Vector3[count];
            rotations = new Quaternion[count];
            restingRotations = new Quaternion[count];
            var random = new System.Random(1783 + count);
            for (var i = 0; i < count; i++)
            {
                var fragment = fragments[i];
                origins[i] = fragment.localPosition;
                scales[i] = fragment.localScale;
                rotations[i] = fragment.localRotation;
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                restingRotations[i] = Quaternion.Euler(
                    30f + (float)random.NextDouble() * 65f,
                    (float)random.NextDouble() * 360f,
                    -25f + (float)random.NextDouble() * 50f);
                var offset = direction * spread * (0.45f + (float)random.NextDouble() * 0.55f);
                var mesh = fragment.GetComponent<MeshFilter>().sharedMesh;
                var extents = mesh.bounds.extents;
                var pose = Matrix4x4.TRS(Vector3.zero, restingRotations[i], scales[i] * RestingScale);
                var height = Mathf.Abs(pose.m10) * extents.x + Mathf.Abs(pose.m11) * extents.y +
                             Mathf.Abs(pose.m12) * extents.z;
                destinations[i] = new Vector3(origins[i].x + offset.x, height + 0.015f, origins[i].z + offset.z);
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Lifetime)
            {
                gameObject.SetActive(false);
                return;
            }
            if (age >= collapseDuration && age < collapseDuration + debrisDuration)
            {
                if (!settled) Sample(collapseDuration);
                settled = true;
                return;
            }
            Sample(age);
        }

        public void Sample(float elapsed) // Editor 시간 샘플과 Runtime은 같은 궤적을 사용한다
        {
            PrepareFragments();
            var progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, collapseDuration));
            var settle = 1f - Mathf.Pow(1f - progress, 2f);
            var sink = Mathf.SmoothStep(0f, 1f,
                (elapsed - collapseDuration - debrisDuration) / Mathf.Max(0.1f, sinkDuration));
            for (var i = 0; i < FragmentCount; i++)
            {
                var position = Vector3.Lerp(origins[i], destinations[i], settle);
                position.y += Mathf.Sin(progress * Mathf.PI) * hopHeight * (1f + i % 3 * 0.18f);
                position.y += Mathf.Abs(Mathf.Sin(progress * Mathf.PI * 3f)) * 0.025f * progress * (1f - progress);
                fragments[i].localPosition = position - Vector3.up * sink * 0.2f;
                fragments[i].localRotation = Quaternion.Slerp(rotations[i], restingRotations[i], settle);
                fragments[i].localScale = scales[i] * Mathf.Lerp(1f, RestingScale, settle) * (1f - sink);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(Transform[] pieces, ParticleSystem dustSystem, float duration, float distance)
        {
            fragments = pieces;
            dust = dustSystem;
            collapseDuration = duration;
            spread = distance;
            origins = null;
        }
#endif
    }
}
