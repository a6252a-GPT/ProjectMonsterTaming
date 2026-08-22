using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class MonsterAttackAreaIndicator : MonoBehaviour // 실제 XZ 판정 외곽선을 짧게 표시
    {
        private const int CircleSegments = 36;
        private const int FanSegments = 20;
        private static Material sharedLineMaterial;

        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private readonly List<Color> baseColors = new List<Color>();
        private float remaining;
        private float duration;
        private bool automaticTick;

        public static MonsterAttackAreaIndicator Create(
            Transform parent,
            MonsterBasicAttackProfile profile,
            Vector3 origin,
            Vector3 forward,
            Vector3 primaryTarget,
            float attackRange,
            Color color,
            bool autoTick = true)
        {
            if (profile == null)
            {
                return null;
            }

            var root = new GameObject($"[Basic Attack Area] {profile.AttackId}");
            root.hideFlags = HideFlags.DontSave;
            root.transform.SetParent(parent, true);
            var indicator = root.AddComponent<MonsterAttackAreaIndicator>();
            indicator.Build(
                profile,
                origin,
                forward,
                primaryTarget,
                attackRange,
                color,
                autoTick);
            return indicator;
        }

        public bool Tick(float deltaTime)
        {
            remaining = Mathf.Max(0f, remaining - Mathf.Max(0f, deltaTime));
            var alpha = duration <= 0f ? 0f : Mathf.Clamp01(remaining / duration);
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                if (line == null)
                {
                    continue;
                }

                var baseColor = index < baseColors.Count ? baseColors[index] : Color.white;
                var start = baseColor;
                var end = baseColor;
                start.a *= alpha;
                end.a *= alpha;
                line.startColor = start;
                line.endColor = end;
            }

            return remaining > 0f;
        }

        private void Update()
        {
            if (!automaticTick || Tick(Time.unscaledDeltaTime))
            {
                return;
            }

            Destroy(gameObject);
        }

        private void Build(
            MonsterBasicAttackProfile profile,
            Vector3 origin,
            Vector3 forward,
            Vector3 primaryTarget,
            float attackRange,
            Color color,
            bool autoTick)
        {
            automaticTick = autoTick;
            duration = profile.HitAreaVisibleDuration;
            remaining = duration;
            origin.y += 0.08f;
            primaryTarget.y = origin.y;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            if (profile.Delivery == MonsterBasicAttackDelivery.Dash)
            {
                var dashEnd = origin + forward * Mathf.Min(profile.DashDistance, profile.ResolveRange(attackRange));
                AddLine(new[] { origin, dashEnd }, color, 0.055f, false);
                AddCircle(dashEnd, profile.Radius, color);
                return;
            }

            switch (profile.Shape)
            {
                case MonsterBasicAttackShape.Fan:
                    AddFan(origin, forward, profile.ResolveRange(attackRange), profile.Angle, color);
                    break;
                case MonsterBasicAttackShape.Line:
                    AddLineArea(origin, forward, profile.ResolveRange(attackRange), profile.LineWidth, color);
                    break;
                case MonsterBasicAttackShape.Circle:
                    AddCircle(
                        profile.Center == MonsterBasicAttackCenter.Source ? origin : primaryTarget,
                        profile.Radius,
                        color);
                    break;
                default:
                    AddCircle(primaryTarget, profile.Radius, color);
                    break;
            }
        }

        private void AddCircle(Vector3 center, float radius, Color color)
        {
            var points = new Vector3[CircleSegments + 1];
            for (var index = 0; index <= CircleSegments; index++)
            {
                var angle = index / (float)CircleSegments * Mathf.PI * 2f;
                points[index] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }

            AddLine(points, color, 0.045f, true);
        }

        private void AddFan(Vector3 origin, Vector3 forward, float range, float angle, Color color)
        {
            var points = new Vector3[FanSegments + 3];
            points[0] = origin;
            for (var index = 0; index <= FanSegments; index++)
            {
                var ratio = index / (float)FanSegments;
                var yaw = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, ratio);
                points[index + 1] = origin + Quaternion.Euler(0f, yaw, 0f) * forward * range;
            }
            points[points.Length - 1] = origin;
            AddLine(points, color, 0.045f, true);
        }

        private void AddLineArea(Vector3 origin, Vector3 forward, float length, float width, Color color)
        {
            var right = Vector3.Cross(Vector3.up, forward).normalized * (width * 0.5f);
            var end = origin + forward * length;
            AddLine(
                new[]
                {
                    origin - right,
                    origin + right,
                    end + right,
                    end - right,
                    origin - right
                },
                color,
                0.045f,
                true);
        }

        private void AddLine(Vector3[] points, Color color, float width, bool loop)
        {
            if (points == null || points.Length < 2)
            {
                return;
            }

            var lineObject = new GameObject("Outline");
            lineObject.hideFlags = HideFlags.DontSave;
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = ResolveMaterial();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            lines.Add(line);
            baseColors.Add(color);
        }

        private static Material ResolveMaterial()
        {
            if (sharedLineMaterial != null)
            {
                return sharedLineMaterial;
            }

            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return null;
            }

            sharedLineMaterial = new Material(shader)
            {
                name = "[Runtime] Basic Attack Area Line",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedLineMaterial;
        }
    }
}
