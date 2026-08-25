using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleTrapRuntime : MonoBehaviour
    {
        private readonly List<Renderer> visualRenderers = new List<Renderer>();
        private readonly List<Color> baseColors = new List<Color>();
        private readonly List<Animator> visualAnimators = new List<Animator>();
        private readonly List<LineRenderer> crackleLines = new List<LineRenderer>();
        private HexCastleTrapBalance balance;
        private float cooldownRemaining;
        private float pulseRemaining;
        private float warningRemaining;
        private float warningDuration;
        private float explosionRemaining;
        private string animationStateName = string.Empty;
        private LineRenderer explosionRing;

        public HexCastleTrapPlacement Placement { get; private set; }
        public HexCastleTrapType TrapType => Placement?.TrapType ?? HexCastleTrapType.Snare;
        public HexCoordinates Coordinates => Placement?.Coordinates ?? default;
        public int RemainingCharges { get; private set; }
        public int MaximumCharges => balance.MaximumCharges;
        public float CooldownRemaining => cooldownRemaining;
        public bool IsArmed => RemainingCharges > 0 && cooldownRemaining <= 0f;
        public bool IsWarning => warningRemaining > 0f;
        public float WarningRemaining => warningRemaining;
        public HexCastleTrapBalance Balance => balance;
        public bool UsesImportedVisual { get; private set; }
        public string VisualVariantId { get; private set; } = string.Empty;

        internal void Configure(
            HexCastleTrapPlacement placement,
            HexCastleTrapBalance trapBalance,
            IEnumerable<Renderer> renderers,
            HexCastleTrapVisualEntry visualEntry,
            Material warningMaterial)
        {
            Placement = placement ?? throw new ArgumentNullException(nameof(placement));
            balance = trapBalance;
            RemainingCharges = trapBalance.MaximumCharges;
            cooldownRemaining = 0f;
            pulseRemaining = 0f;
            warningRemaining = 0f;
            warningDuration = 0f;
            explosionRemaining = 0f;
            UsesImportedVisual = visualEntry != null;
            VisualVariantId = visualEntry?.VisualVariantId ?? string.Empty;
            animationStateName = visualEntry?.AnimationStateName ?? string.Empty;
            visualRenderers.Clear();
            baseColors.Clear();
            visualAnimators.Clear();
            foreach (var renderer in renderers?.Where(value => value != null) ??
                                     Enumerable.Empty<Renderer>())
            {
                visualRenderers.Add(renderer);
                baseColors.Add(ResolveMaterialColor(renderer.sharedMaterial));
            }
            ConfigureAnimation();
            ConfigureMineTelegraph(warningMaterial);
            ApplyVisualState();
        }

        internal bool TryConsumeTrigger()
        {
            if (!IsArmed)
            {
                return false;
            }

            RemainingCharges--;
            cooldownRemaining = RemainingCharges > 0 ? balance.RearmSeconds : 0f;
            pulseRemaining = 0.16f;
            if (balance.TriggerDelaySeconds > 0f)
            {
                warningDuration = balance.TriggerDelaySeconds;
                warningRemaining = warningDuration;
            }
            PlayTriggerAnimation();
            ApplyVisualState();
            return true;
        }

        internal bool Tick(float deltaTime)
        {
            var previousCooldown = cooldownRemaining;
            var previousPulse = pulseRemaining;
            var previousWarning = warningRemaining;
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Mathf.Max(0f, deltaTime));
            pulseRemaining = Mathf.Max(0f, pulseRemaining - Mathf.Max(0f, deltaTime));
            warningRemaining = Mathf.Max(0f, warningRemaining - Mathf.Max(0f, deltaTime));
            UpdateMineTelegraph(deltaTime);
            if (previousCooldown > 0f && cooldownRemaining <= 0f && RemainingCharges > 0)
            {
                ResetAnimationToArmed();
            }
            if (!Mathf.Approximately(previousCooldown, cooldownRemaining) ||
                !Mathf.Approximately(previousPulse, pulseRemaining) ||
                !Mathf.Approximately(previousWarning, warningRemaining))
            {
                ApplyVisualState();
            }

            if (previousWarning > 0f && warningRemaining <= 0f)
            {
                PlayExplosionVisual();
                return true;
            }

            return false;
        }

        private void ApplyVisualState()
        {
            var intensity = IsWarning
                ? Mathf.Lerp(0.55f, 1.85f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 38f))
                : RemainingCharges <= 0
                ? 0.24f
                : pulseRemaining > 0f
                    ? 1.35f
                    : cooldownRemaining > 0f
                        ? 0.55f
                        : 1f;
            var propertyBlock = new MaterialPropertyBlock();
            for (var index = 0; index < visualRenderers.Count; index++)
            {
                var renderer = visualRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                var color = baseColors[index] * intensity;
                color.a = baseColors[index].a;
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    propertyBlock.SetColor("_BaseColor", color);
                }
                else
                {
                    propertyBlock.SetColor("_Color", color);
                }
                renderer.SetPropertyBlock(propertyBlock);
                propertyBlock.Clear();
            }
        }

        private void ConfigureAnimation()
        {
            foreach (var animator in GetComponentsInChildren<Animator>(true))
            {
                if (animator == null || animator.runtimeAnimatorController == null)
                {
                    continue;
                }

                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                visualAnimators.Add(animator);
            }
            ResetAnimationToArmed();
        }

        private void ResetAnimationToArmed()
        {
            foreach (var animator in visualAnimators)
            {
                if (animator == null)
                {
                    continue;
                }

                animator.enabled = true;
                animator.speed = 1f;
                animator.Rebind();
                animator.Update(0f);
                if (!string.IsNullOrWhiteSpace(animationStateName))
                {
                    animator.Play(animationStateName, 0, 0f);
                    animator.Update(0f);
                }
                animator.speed = 0f;
            }
        }

        private void PlayTriggerAnimation()
        {
            foreach (var animator in visualAnimators)
            {
                if (animator == null)
                {
                    continue;
                }

                animator.enabled = true;
                animator.speed = 1f;
                animator.Rebind();
                animator.Update(0f);
                if (!string.IsNullOrWhiteSpace(animationStateName))
                {
                    animator.Play(animationStateName, 0, 0f);
                    animator.Update(0f);
                }
            }
        }

        private void ConfigureMineTelegraph(Material warningMaterial)
        {
            crackleLines.Clear();
            explosionRing = null;
            if (TrapType != HexCastleTrapType.BlastMine || warningMaterial == null)
            {
                return;
            }

            for (var index = 0; index < 3; index++)
            {
                var lineRoot = new GameObject($"Crackle_{index:00}");
                lineRoot.transform.SetParent(transform, false);
                var line = lineRoot.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 3;
                line.widthMultiplier = 0.018f;
                line.numCornerVertices = 2;
                line.sharedMaterial = warningMaterial;
                line.startColor = new Color(1f, 0.75f, 0.10f, 1f);
                line.endColor = new Color(1f, 0.18f, 0.04f, 1f);
                line.enabled = false;
                crackleLines.Add(line);
            }

            var ringRoot = new GameObject("ExplosionRing");
            ringRoot.transform.SetParent(transform, false);
            explosionRing = ringRoot.AddComponent<LineRenderer>();
            explosionRing.useWorldSpace = false;
            explosionRing.loop = true;
            explosionRing.positionCount = 30;
            explosionRing.widthMultiplier = 0.055f;
            explosionRing.numCornerVertices = 2;
            explosionRing.sharedMaterial = warningMaterial;
            explosionRing.startColor = new Color(1f, 0.62f, 0.08f, 1f);
            explosionRing.endColor = explosionRing.startColor;
            explosionRing.enabled = false;
        }

        private void UpdateMineTelegraph(float deltaTime)
        {
            if (crackleLines.Count > 0)
            {
                var warningPhase = warningDuration <= 0f
                    ? 1f
                    : 1f - Mathf.Clamp01(warningRemaining / warningDuration);
                for (var index = 0; index < crackleLines.Count; index++)
                {
                    var line = crackleLines[index];
                    if (line == null)
                    {
                        continue;
                    }

                    var visible = warningRemaining > 0f &&
                                  ((Mathf.FloorToInt(warningPhase * 18f) + index) & 1) == 0;
                    line.enabled = visible;
                    if (!visible)
                    {
                        continue;
                    }

                    var angle = Time.unscaledTime * (11f + index * 2f) + index * 2.1f;
                    var endRadius = 0.26f + 0.07f * Mathf.Sin(angle * 1.7f);
                    var end = new Vector3(Mathf.Cos(angle) * endRadius, 0.08f, Mathf.Sin(angle) * endRadius);
                    var middle = Vector3.Lerp(new Vector3(0f, 0.24f, 0f), end, 0.5f) +
                                 new Vector3(Mathf.Sin(angle * 2.3f), 0.06f, Mathf.Cos(angle * 1.9f)) * 0.07f;
                    line.SetPosition(0, new Vector3(0f, 0.24f, 0f));
                    line.SetPosition(1, middle);
                    line.SetPosition(2, end);
                }
            }

            if (explosionRemaining <= 0f || explosionRing == null)
            {
                return;
            }

            explosionRemaining = Mathf.Max(0f, explosionRemaining - Mathf.Max(0f, deltaTime));
            var phase = 1f - explosionRemaining / 0.28f;
            var radius = Mathf.Lerp(0.18f, 0.95f, phase);
            explosionRing.widthMultiplier = Mathf.Lerp(0.085f, 0.015f, phase);
            for (var index = 0; index < explosionRing.positionCount; index++)
            {
                var angle = index / (float)explosionRing.positionCount * Mathf.PI * 2f;
                explosionRing.SetPosition(
                    index,
                    new Vector3(Mathf.Cos(angle) * radius, 0.09f, Mathf.Sin(angle) * radius));
            }
            explosionRing.enabled = explosionRemaining > 0f;
        }

        private void PlayExplosionVisual()
        {
            foreach (var line in crackleLines)
            {
                if (line != null)
                {
                    line.enabled = false;
                }
            }

            explosionRemaining = 0.28f;
            if (explosionRing != null)
            {
                explosionRing.enabled = true;
            }
        }

        private static Color ResolveMaterialColor(Material material)
        {
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        }
    }

    [DisallowMultipleComponent]
    public sealed class HexCastleTrapWorld : MonoBehaviour // 공격 유닛의 Cell 진입을 함정 효과로 연결한다
    {
        private readonly Dictionary<HexCoordinates, HexCastleTrapRuntime> trapsByCell =
            new Dictionary<HexCoordinates, HexCastleTrapRuntime>();
        private readonly HashSet<HexCastleAssaultUnit> registeredUnits =
            new HashSet<HexCastleAssaultUnit>();
        private readonly Dictionary<HexCastleTrapRuntime, HexCastleAssaultUnit> pendingBlastTriggers =
            new Dictionary<HexCastleTrapRuntime, HexCastleAssaultUnit>();
        private HexCastleAssaultWorld assaultWorld;

        public int TrapCount => trapsByCell.Count;
        public int ArmedTrapCount => trapsByCell.Values.Count(value => value != null && value.IsArmed);

        public event Action<HexCastleAssaultUnit, HexCastleTrapRuntime> TrapTriggered;

        internal void Configure(
            HexCastleLayout layout,
            Transform trapsRoot,
            HexCastleVisualSet visualSet,
            ICollection<Object> generatedAssets)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }
            if (trapsRoot == null)
            {
                throw new ArgumentNullException(nameof(trapsRoot));
            }
            if (visualSet == null || visualSet.KayKitMaterial == null)
            {
                throw new ArgumentNullException(nameof(visualSet));
            }

            trapsByCell.Clear();
            pendingBlastTriggers.Clear();
            var baseMaterial = visualSet.KayKitMaterial;
            var wood = CreateMaterial(baseMaterial, "MAT_CRHex_TrapWood_Runtime", new Color(0.30f, 0.16f, 0.07f));
            var metal = CreateMaterial(baseMaterial, "MAT_CRHex_TrapMetal_Runtime", new Color(0.25f, 0.28f, 0.31f));
            var explosive = CreateMaterial(baseMaterial, "MAT_CRHex_TrapMine_Runtime", new Color(0.10f, 0.11f, 0.12f));
            var warning = CreateMaterial(baseMaterial, "MAT_CRHex_TrapWarning_Runtime", new Color(0.78f, 0.16f, 0.06f));
            generatedAssets?.Add(wood);
            generatedAssets?.Add(metal);
            generatedAssets?.Add(explosive);
            generatedAssets?.Add(warning);

            foreach (var placement in layout.TrapPlacements.OrderBy(value => value.PlacementId, StringComparer.Ordinal))
            {
                var trapRoot = new GameObject($"Trap_{placement.PlacementId}");
                trapRoot.transform.SetParent(trapsRoot, false);
                trapRoot.transform.localPosition =
                    HexSpatialContract.ToWorld(placement.Coordinates) + Vector3.up * 0.025f;
                trapRoot.transform.localRotation = Quaternion.Euler(0f, (placement.RegionId - 1) * 60f, 0f);
                var visualEntry = visualSet.ResolveTrapVisual(placement.TrapType, placement.PlacementId);
                if (visualEntry != null)
                {
                    InstantiateImportedVisual(visualEntry, trapRoot.transform);
                }
                else
                {
                    BuildTemporaryVisual(placement.TrapType, trapRoot.transform, wood, metal, explosive, warning);
                }
                var runtime = trapRoot.AddComponent<HexCastleTrapRuntime>();
                runtime.Configure(
                    placement,
                    HexCastleTrapBalance.Resolve(placement.TrapType, layout.DifficultyLevel),
                    trapRoot.GetComponentsInChildren<Renderer>(true),
                    visualEntry,
                    warning);
                trapsByCell.Add(placement.Coordinates, runtime);
            }
        }

        public void Bind(HexCastleAssaultWorld targetWorld)
        {
            if (targetWorld == null)
            {
                throw new ArgumentNullException(nameof(targetWorld));
            }

            Unbind();
            assaultWorld = targetWorld;
            assaultWorld.UnitRegistered += HandleUnitRegistered;
            assaultWorld.UnitUnregistered += HandleUnitUnregistered;
            foreach (var unit in assaultWorld.RegisteredUnits)
            {
                HandleUnitRegistered(unit);
            }
        }

        public void Shutdown()
        {
            Unbind();
        }

        public bool TryTriggerAt(HexCastleAssaultUnit unit, HexCoordinates coordinates)
        {
            if (unit == null || !unit.IsAlive ||
                !trapsByCell.TryGetValue(coordinates, out var trap) || trap == null ||
                !trap.TryConsumeTrigger())
            {
                return false;
            }

            var balance = trap.Balance;
            TrapTriggered?.Invoke(unit, trap); // 지연 폭발도 밟은 순간 한 번만 알린다
            switch (trap.TrapType)
            {
                case HexCastleTrapType.Snare:
                    ApplyDamageRatio(unit, balance.DamageRatio);
                    if (unit.IsAlive)
                    {
                        unit.ApplyTrapMovementLock(balance.EffectDuration);
                    }
                    break;
                case HexCastleTrapType.SpikePlate:
                    ApplyDamageRatio(unit, balance.DamageRatio);
                    if (unit.IsAlive)
                    {
                        unit.ApplyTrapSlow(balance.MovementSpeedMultiplier, balance.EffectDuration);
                    }
                    break;
                case HexCastleTrapType.BlastMine:
                    pendingBlastTriggers[trap] = unit;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return true;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            foreach (var trap in trapsByCell.Values.ToArray())
            {
                if (trap != null && trap.Tick(Time.deltaTime))
                {
                    DetonateBlastMine(trap);
                }
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleUnitRegistered(HexCastleAssaultUnit unit)
        {
            if (unit == null || !registeredUnits.Add(unit))
            {
                return;
            }

            unit.EnteredCell -= HandleUnitEnteredCell;
            unit.EnteredCell += HandleUnitEnteredCell;
        }

        private void HandleUnitUnregistered(HexCastleAssaultUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            unit.EnteredCell -= HandleUnitEnteredCell;
            registeredUnits.Remove(unit);
        }

        private void HandleUnitEnteredCell(HexCastleAssaultUnit unit, HexCoordinates coordinates)
        {
            TryTriggerAt(unit, coordinates);
        }

        private void Unbind()
        {
            if (assaultWorld != null)
            {
                assaultWorld.UnitRegistered -= HandleUnitRegistered;
                assaultWorld.UnitUnregistered -= HandleUnitUnregistered;
            }

            foreach (var unit in registeredUnits.ToArray())
            {
                if (unit != null)
                {
                    unit.EnteredCell -= HandleUnitEnteredCell;
                }
            }
            registeredUnits.Clear();
            pendingBlastTriggers.Clear();
            assaultWorld = null;
        }

        private static void ApplyDamageRatio(HexCastleAssaultUnit unit, float ratio)
        {
            if (unit != null && unit.IsAlive && ratio > 0f)
            {
                unit.ApplyDamage(unit.MaxHealth * ratio, unit.transform.position);
            }
        }

        private void DetonateBlastMine(HexCastleTrapRuntime trap)
        {
            if (trap == null || trap.TrapType != HexCastleTrapType.BlastMine)
            {
                return;
            }

            var balance = trap.Balance;
            pendingBlastTriggers.TryGetValue(trap, out var triggeringUnit);
            pendingBlastTriggers.Remove(trap);
            foreach (var target in registeredUnits
                         .Where(value => value != null && value.IsAlive &&
                                         value.CurrentCoordinates.DistanceTo(trap.Coordinates) <=
                                         balance.BlastRadiusCells)
                         .Concat(triggeringUnit != null && triggeringUnit.IsAlive
                             ? new[] { triggeringUnit }
                             : Array.Empty<HexCastleAssaultUnit>())
                         .Distinct()
                         .ToArray())
            {
                var ratio = target == triggeringUnit || target.CurrentCoordinates == trap.Coordinates
                    ? balance.DamageRatio
                    : balance.SplashDamageRatio;
                ApplyDamageRatio(target, ratio);
                if (target.IsAlive)
                {
                    target.ApplyTrapMovementLock(balance.EffectDuration);
                }
            }
        }

        private static Material CreateMaterial(Material source, string materialName, Color color)
        {
            var material = new Material(source)
            {
                name = materialName,
                color = color
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.08f);
            }
            return material;
        }

        private static void InstantiateImportedVisual(
            HexCastleTrapVisualEntry visualEntry,
            Transform parent)
        {
            if (visualEntry == null || visualEntry.Prefab == null || visualEntry.MaterialOverride == null)
            {
                throw new ArgumentException("함정 Visual Entry가 불완전합니다.", nameof(visualEntry));
            }

            var instance = Object.Instantiate(visualEntry.Prefab, parent, false);
            instance.name = $"Model_{visualEntry.VisualVariantId}";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
            foreach (var obstacle in instance.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
            {
                Object.DestroyImmediate(obstacle);
            }
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(
                    visualEntry.MaterialOverride,
                    Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
            }

            FitImportedVisual(instance.transform, parent.position);
        }

        private static void FitImportedVisual(Transform visual, Vector3 groundPosition)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException($"함정 Visual {visual.name}의 Renderer가 비었습니다.");
            }

            var bounds = ResolveRendererBounds(renderers);
            var footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            if (footprint <= 0.001f)
            {
                throw new InvalidOperationException($"함정 Visual {visual.name}의 Bounds가 비었습니다.");
            }

            visual.localScale = Vector3.one * Mathf.Clamp(0.84f / footprint, 0.35f, 1.25f);
            var seatingRenderers = renderers
                .Where(value => value != null &&
                                value.name.IndexOf("osnova", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            bounds = ResolveRendererBounds(seatingRenderers.Length > 0 ? seatingRenderers : renderers);
            visual.position += Vector3.up * (groundPosition.y - bounds.min.y);
        }

        private static Bounds ResolveRendererBounds(IReadOnlyList<Renderer> renderers)
        {
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Count; index++)
            {
                if (renderers[index] != null)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }
            }

            return bounds;
        }

        private static void BuildTemporaryVisual(
            HexCastleTrapType trapType,
            Transform parent,
            Material wood,
            Material metal,
            Material explosive,
            Material warning)
        {
            switch (trapType)
            {
                case HexCastleTrapType.Snare:
                    CreatePrimitiveVisual(
                        PrimitiveType.Cylinder,
                        parent,
                        "PressurePlate",
                        new Vector3(0f, 0.025f, 0f),
                        new Vector3(0.22f, 0.025f, 0.22f),
                        metal);
                    for (var index = 0; index < 8; index++)
                    {
                        var angle = index * 45f;
                        var radians = angle * Mathf.Deg2Rad;
                        CreatePrimitiveVisual(
                            PrimitiveType.Cube,
                            parent,
                            $"Jaw_{index:00}",
                            new Vector3(Mathf.Sin(radians) * 0.32f, 0.07f, Mathf.Cos(radians) * 0.32f),
                            new Vector3(0.10f, 0.10f, 0.23f),
                            metal,
                            Quaternion.Euler(-18f, angle, 0f));
                    }
                    break;
                case HexCastleTrapType.SpikePlate:
                    CreatePrimitiveVisual(
                        PrimitiveType.Cube,
                        parent,
                        "WoodPlate",
                        new Vector3(0f, 0.035f, 0f),
                        new Vector3(0.76f, 0.07f, 0.62f),
                        wood);
                    for (var row = -1; row <= 1; row++)
                    {
                        for (var column = -1; column <= 1; column++)
                        {
                            CreatePrimitiveVisual(
                                PrimitiveType.Cylinder,
                                parent,
                                $"Spike_{row + 1}_{column + 1}",
                                new Vector3(column * 0.22f, 0.18f, row * 0.17f),
                                new Vector3(0.035f, 0.14f, 0.035f),
                                metal);
                        }
                    }
                    break;
                case HexCastleTrapType.BlastMine:
                    CreatePrimitiveVisual(
                        PrimitiveType.Cylinder,
                        parent,
                        "MineBody",
                        new Vector3(0f, 0.075f, 0f),
                        new Vector3(0.38f, 0.075f, 0.38f),
                        explosive);
                    CreatePrimitiveVisual(
                        PrimitiveType.Sphere,
                        parent,
                        "MineDome",
                        new Vector3(0f, 0.14f, 0f),
                        new Vector3(0.48f, 0.19f, 0.48f),
                        explosive);
                    CreatePrimitiveVisual(
                        PrimitiveType.Cylinder,
                        parent,
                        "WarningCap",
                        new Vector3(0f, 0.245f, 0f),
                        new Vector3(0.09f, 0.035f, 0.09f),
                        warning);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(trapType), trapType, null);
            }
        }

        private static GameObject CreatePrimitiveVisual(
            PrimitiveType primitiveType,
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Quaternion? localRotation = null)
        {
            var instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation ?? Quaternion.identity;
            instance.transform.localScale = localScale;
            var collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            instance.GetComponent<Renderer>().sharedMaterial = material;
            return instance;
        }
    }
}
