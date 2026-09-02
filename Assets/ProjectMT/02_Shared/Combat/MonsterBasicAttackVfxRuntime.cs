using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public static class MonsterBasicAttackVfxPlayback // Pool·Preview가 같은 내부 시작점·속도를 사용
    {
        public const float DefaultMainBattleBrightnessScale = 0.6f;

        public static void ApplyBrightnessScale(GameObject root, float brightnessScale)
        {
            if (root == null)
            {
                return;
            }

            var resolvedScale = float.IsNaN(brightnessScale) || float.IsInfinity(brightnessScale)
                ? 1f
                : Mathf.Clamp01(brightnessScale);
            var state = root.GetComponent<MonsterVfxBrightnessState>();
            if (resolvedScale >= 0.9999f)
            {
                state?.Restore();
                return;
            }

            state ??= root.AddComponent<MonsterVfxBrightnessState>();
            state.Apply(resolvedScale);
        }

        public static void RestoreBrightness(GameObject root)
        {
            root?.GetComponent<MonsterVfxBrightnessState>()?.Restore();
        }

        public static void ApplyInstanceScale(GameObject root, Vector3 localScale)
        {
            if (root == null)
            {
                return;
            }

            root.transform.localScale = localScale;
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                var main = particles[index].main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        public static void RestartAtOffset(
            GameObject root,
            float playbackOffset,
            bool continuePlaying = true,
            float playbackSpeed = 1f)
        {
            if (root == null)
            {
                return;
            }

            var trails = root.GetComponentsInChildren<TrailRenderer>(true);
            for (var index = 0; index < trails.Length; index++)
            {
                trails[index].Clear();
            }

            var offset = Mathf.Max(0f, playbackOffset);
            var speed = SanitizePlaybackSpeed(playbackSpeed);
            var playbackState = root.GetComponent<MonsterBasicAttackVfxPlaybackState>() ??
                                root.AddComponent<MonsterBasicAttackVfxPlaybackState>();
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                var particle = particles[index];
                var main = particle.main;
                var authoredSpeed = playbackState.ResolveAuthoredSpeed(particle);
                main.simulationSpeed = authoredSpeed; // 시작점은 Vendor 원본 시간축으로 탐색
            }

            var roots = ResolveRootParticleSystems(particles);
            for (var index = 0; index < roots.Count; index++)
            {
                roots[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                roots[index].Simulate(offset, true, true, true);
            }

            for (var index = 0; index < particles.Length; index++)
            {
                var particle = particles[index];
                var main = particle.main;
                var authoredSpeed = playbackState.ResolveAuthoredSpeed(particle);
                main.simulationSpeed = authoredSpeed * speed;
            }
            for (var index = 0; index < roots.Count; index++)
            {
                if (continuePlaying) roots[index].Play(true);
                else roots[index].Pause(true);
            }
        }

        private static float SanitizePlaybackSpeed(float speed)
        {
            return float.IsNaN(speed) || float.IsInfinity(speed)
                ? 1f
                : Mathf.Max(0.01f, speed);
        }

        public static void Simulate(GameObject root, float deltaTime)
        {
            if (root == null || deltaTime <= 0f)
            {
                return;
            }

            var roots = ResolveRootParticleSystems(root.GetComponentsInChildren<ParticleSystem>(true));
            // Editor가 잠깐 바쁠 때 0.05~0.1초를 한 번에 넘기면 Trail·SubEmitter가
            // 큰 간격으로 튀어 잔상처럼 보일 수 있다. 총 시간은 유지하고 60Hz 이하로 쪼갠다.
            var remaining = Mathf.Max(0f, deltaTime);
            const float maximumStep = 1f / 60f;
            while (remaining > 0.000001f)
            {
                var step = Mathf.Min(maximumStep, remaining);
                for (var index = 0; index < roots.Count; index++)
                {
                    // fixedTimeStep을 켜면 작은 수동 Tick의 나머지가 버려져 파티클이 멈출 수 있다.
                    roots[index].Simulate(step, true, false, false);
                }
                remaining -= step;
            }
        }

        public static void StopAndClear(GameObject root)
        {
            if (root == null) return;
            var trails = root.GetComponentsInChildren<TrailRenderer>(true);
            for (var index = 0; index < trails.Length; index++) trails[index].Clear();
            var roots = ResolveRootParticleSystems(root.GetComponentsInChildren<ParticleSystem>(true));
            for (var index = 0; index < roots.Count; index++)
                roots[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public static void SimulateAtTime(
            GameObject root,
            float elapsed,
            float playbackOffset = 0f,
            float playbackSpeed = 1f)
        {
            if (root == null) return;
            var time = Mathf.Max(0f, elapsed);
            var offset = Mathf.Max(0f, playbackOffset);
            var speed = SanitizePlaybackSpeed(playbackSpeed);
            var playbackState = root.GetComponent<MonsterBasicAttackVfxPlaybackState>() ??
                                root.AddComponent<MonsterBasicAttackVfxPlaybackState>();
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                var particle = particles[index];
                var main = particle.main;
                var authoredSpeed = playbackState.ResolveAuthoredSpeed(particle);
                main.simulationSpeed = authoredSpeed;
            }

            var roots = ResolveRootParticleSystems(particles);
            for (var index = 0; index < roots.Count; index++)
            {
                roots[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                roots[index].Simulate(offset, true, true, false);
            }

            for (var index = 0; index < particles.Length; index++)
            {
                var particle = particles[index];
                var main = particle.main;
                var authoredSpeed = playbackState.ResolveAuthoredSpeed(particle);
                main.simulationSpeed = authoredSpeed * speed;
            }
            for (var index = 0; index < roots.Count; index++)
            {
                roots[index].Simulate(time, true, false, false);
                roots[index].Pause(true);
            }
        }

        private static List<ParticleSystem> ResolveRootParticleSystems(ParticleSystem[] particles)
        {
            var roots = new List<ParticleSystem>();
            if (particles == null) return roots;
            for (var index = 0; index < particles.Length; index++)
            {
                var particle = particles[index];
                if (particle == null) continue;
                var parent = particle.transform.parent;
                var hasParticleParent = false;
                while (parent != null)
                {
                    if (parent.GetComponent<ParticleSystem>() != null)
                    {
                        hasParticleParent = true;
                        break;
                    }
                    parent = parent.parent;
                }
                if (!hasParticleParent) roots.Add(particle);
            }
            return roots;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class MonsterVfxBrightnessState : MonoBehaviour // VFX 인스턴스 색·광원 원본을 보존해 Pool 누적 방지
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_Emissive_Color");

        private readonly List<ParticleSystem> particles = new List<ParticleSystem>();
        private readonly List<ParticleSystem.MinMaxGradient> particleStartColors =
            new List<ParticleSystem.MinMaxGradient>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<int> rendererMaterialIndexes = new List<int>();
        private readonly List<MaterialPropertyBlock> rendererPropertyBlocks =
            new List<MaterialPropertyBlock>();
        private readonly List<Light> lights = new List<Light>();
        private readonly List<float> lightIntensities = new List<float>();
        private bool captured;

        public void Apply(float scale)
        {
            EnsureCaptured();
            Restore();

            var resolvedScale = Mathf.Clamp01(scale);
            for (var index = 0; index < particles.Count; index++)
            {
                var particle = particles[index];
                if (particle == null)
                {
                    continue;
                }

                var main = particle.main;
                main.startColor = ScaleGradient(particleStartColors[index], resolvedScale);
            }

            for (var index = 0; index < renderers.Count; index++)
            {
                ApplyRendererScale(
                    renderers[index],
                    rendererMaterialIndexes[index],
                    rendererPropertyBlocks[index],
                    resolvedScale);
            }

            for (var index = 0; index < lights.Count; index++)
            {
                if (lights[index] != null)
                {
                    lights[index].intensity = lightIntensities[index] * resolvedScale;
                }
            }
        }

        public void Restore()
        {
            if (!captured)
            {
                return;
            }

            for (var index = 0; index < particles.Count; index++)
            {
                var particle = particles[index];
                if (particle == null)
                {
                    continue;
                }

                var main = particle.main;
                main.startColor = particleStartColors[index];
            }

            for (var index = 0; index < renderers.Count; index++)
            {
                var renderer = renderers[index];
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(
                        rendererPropertyBlocks[index],
                        rendererMaterialIndexes[index]);
                }
            }

            for (var index = 0; index < lights.Count; index++)
            {
                if (lights[index] != null)
                {
                    lights[index].intensity = lightIntensities[index];
                }
            }
        }

        private void EnsureCaptured()
        {
            if (captured)
            {
                return;
            }

            captured = true;
            var foundParticles = GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < foundParticles.Length; index++)
            {
                var particle = foundParticles[index];
                particles.Add(particle);
                particleStartColors.Add(particle.main.startColor);
            }

            var foundRenderers = GetComponentsInChildren<Renderer>(true);
            for (var rendererIndex = 0; rendererIndex < foundRenderers.Length; rendererIndex++)
            {
                var renderer = foundRenderers[rendererIndex];
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue; // 파티클은 Start Color 한 곳에서만 보정해 중복 감쇠를 피함
                }

                var materials = renderer.sharedMaterials;
                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    var material = materials[materialIndex];
                    if (!HasBrightnessColor(material))
                    {
                        continue;
                    }

                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, materialIndex);
                    renderers.Add(renderer);
                    rendererMaterialIndexes.Add(materialIndex);
                    rendererPropertyBlocks.Add(block);
                }
            }

            var foundLights = GetComponentsInChildren<Light>(true);
            for (var index = 0; index < foundLights.Length; index++)
            {
                lights.Add(foundLights[index]);
                lightIntensities.Add(foundLights[index].intensity);
            }
        }

        private static void ApplyRendererScale(
            Renderer renderer,
            int materialIndex,
            MaterialPropertyBlock originalBlock,
            float scale)
        {
            if (renderer == null)
            {
                return;
            }

            var materials = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= materials.Length || materials[materialIndex] == null)
            {
                return;
            }

            var material = materials[materialIndex];
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, materialIndex);
            var hasEmission = false;
            if (material.HasProperty(EmissionColorId))
            {
                block.SetColor(
                    EmissionColorId,
                    ScaleRgb(ResolveColor(originalBlock, material, EmissionColorId), scale));
                hasEmission = true;
            }
            if (material.HasProperty(EmissiveColorId))
            {
                block.SetColor(
                    EmissiveColorId,
                    ScaleRgb(ResolveColor(originalBlock, material, EmissiveColorId), scale));
                hasEmission = true;
            }

            if (!hasEmission)
            {
                var fallbackColorId = ResolveFallbackColorId(material);
                if (fallbackColorId != 0)
                {
                    block.SetColor(
                        fallbackColorId,
                        ScaleRgb(ResolveColor(originalBlock, material, fallbackColorId), scale));
                }
            }

            renderer.SetPropertyBlock(block, materialIndex);
        }

        private static bool HasBrightnessColor(Material material)
        {
            return material != null &&
                   (material.HasProperty(EmissionColorId) ||
                    material.HasProperty(EmissiveColorId) ||
                    ResolveFallbackColorId(material) != 0);
        }

        private static int ResolveFallbackColorId(Material material)
        {
            if (material == null)
            {
                return 0;
            }
            if (material.HasProperty(BaseColorId)) return BaseColorId;
            if (material.HasProperty(TintColorId)) return TintColorId;
            return material.HasProperty(ColorId) ? ColorId : 0;
        }

        private static Color ResolveColor(
            MaterialPropertyBlock block,
            Material material,
            int propertyId)
        {
            return block != null && block.HasColor(propertyId)
                ? block.GetColor(propertyId)
                : material.GetColor(propertyId);
        }

        private static ParticleSystem.MinMaxGradient ScaleGradient(
            ParticleSystem.MinMaxGradient source,
            float scale)
        {
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(ScaleRgb(source.color, scale));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(
                        ScaleRgb(source.colorMin, scale),
                        ScaleRgb(source.colorMax, scale));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(ScaleGradient(source.gradient, scale));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(
                        ScaleGradient(source.gradientMin, scale),
                        ScaleGradient(source.gradientMax, scale));
                case ParticleSystemGradientMode.RandomColor:
                {
                    var random = new ParticleSystem.MinMaxGradient(ScaleGradient(source.gradient, scale));
                    random.mode = ParticleSystemGradientMode.RandomColor;
                    return random;
                }
                default:
                    return source;
            }
        }

        private static Gradient ScaleGradient(Gradient source, float scale)
        {
            if (source == null)
            {
                return null;
            }

            var colors = source.colorKeys;
            for (var index = 0; index < colors.Length; index++)
            {
                colors[index].color = ScaleRgb(colors[index].color, scale);
            }

            var result = new Gradient { mode = source.mode };
            result.SetKeys(colors, source.alphaKeys);
            return result;
        }

        private static Color ScaleRgb(Color color, float scale)
        {
            color.r *= scale;
            color.g *= scale;
            color.b *= scale;
            return color;
        }
    }

    internal sealed class MonsterBasicAttackVfxPlaybackState : MonoBehaviour // Pool 재사용에도 Vendor 원본 속도를 보존
    {
        [SerializeField] private List<ParticleSystem> authoredParticles = new List<ParticleSystem>();
        [SerializeField] private List<float> authoredSpeeds = new List<float>();

        public float ResolveAuthoredSpeed(ParticleSystem particle)
        {
            if (particle == null)
            {
                return 1f;
            }

            var count = Mathf.Min(authoredParticles.Count, authoredSpeeds.Count);
            for (var index = 0; index < count; index++)
            {
                if (authoredParticles[index] == particle)
                {
                    return authoredSpeeds[index];
                }
            }

            var speed = Mathf.Max(0f, particle.main.simulationSpeed);
            authoredParticles.Add(particle);
            authoredSpeeds.Add(speed);
            return speed;
        }
    }

    public readonly struct MonsterBasicAttackVfxContext // 고정 이벤트에 필요한 최소 공간 정보
    {
        public MonsterBasicAttackVfxContext(
            CombatWorld world,
            MonsterBasicAttackProfile profile,
            MonsterFeedbackProfile feedback,
            UnitActor source,
            IDamageable target,
            MonsterAnimationDriver driver,
            Transform projectile,
            string socketOverride,
            Vector3 origin,
            Vector3 hitPoint,
            Vector3 areaCenter,
            Quaternion rotation,
            int damageStage = 0,
            IReadOnlyList<MonsterBasicAttackVfxBinding> bindings = null,
            string motionIdOverride = null,
            int? sequenceIdOverride = null,
            float playbackSpeed = 1f)
        {
            World = world;
            Profile = profile;
            Feedback = feedback;
            Source = source;
            Target = target;
            Driver = driver;
            Projectile = projectile;
            SocketOverride = socketOverride;
            Origin = origin;
            HitPoint = hitPoint;
            AreaCenter = areaCenter;
            Rotation = rotation;
            DamageStage = damageStage;
            Bindings = bindings ?? feedback?.BasicAttackVfxBindings;
            SequenceId = sequenceIdOverride ?? driver?.ActionSequenceId ?? 0;
            MotionId = motionIdOverride ?? driver?.CurrentMotionId ?? string.Empty;
            PlaybackSpeed = float.IsNaN(playbackSpeed) || float.IsInfinity(playbackSpeed)
                ? 1f
                : Mathf.Max(0.05f, playbackSpeed);
        }

        public CombatWorld World { get; }
        public MonsterBasicAttackProfile Profile { get; }
        public MonsterFeedbackProfile Feedback { get; }
        public UnitActor Source { get; }
        public IDamageable Target { get; }
        public MonsterAnimationDriver Driver { get; }
        public Transform Projectile { get; }
        public string SocketOverride { get; }
        public Vector3 Origin { get; }
        public Vector3 HitPoint { get; }
        public Vector3 AreaCenter { get; }
        public Quaternion Rotation { get; }
        public int DamageStage { get; }
        public IReadOnlyList<MonsterBasicAttackVfxBinding> Bindings { get; }
        public int SequenceId { get; }
        public string MotionId { get; }
        public float PlaybackSpeed { get; }
    }

    public static class MonsterBasicAttackVfxRuntime // Preview와 같은 슬롯 선택 규칙을 실행
    {
        public static bool Dispatch(
            MonsterBasicAttackVfxEvent eventType,
            in MonsterBasicAttackVfxContext context)
        {
            if (context.World == null || context.Profile == null || context.Bindings == null ||
                context.Source == null)
            {
                return false;
            }

            var registry = ResolveRegistry(context.Source);
            var played = false;
            var slots = context.Profile.VfxSlots;
            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                if (slot == null || slot.EventType != eventType ||
                    !MonsterBasicAttackVfxResolver.TryResolvePresentation(
                        context.Bindings,
                        context.Profile.AttackId,
                        slot,
                        context.MotionId,
                        out var binding))
                {
                    continue;
                }

                if (binding.HasSound && binding.Sfx != null &&
                    registry.TryClaim(slot, context, "sfx"))
                {
                    ResolveAnchor(
                        slot.Anchor,
                        context,
                        out _,
                        out var soundPosition,
                        out var soundRotation);
                    context.World.PlayMonsterSfx(
                        binding.Sfx,
                        soundPosition + soundRotation * binding.LocalPosition);
                    played = true;
                }

                // 이동체 외형은 Projectile Actor가 직접 소유하므로 여기서는 SFX만 재생한다.
                if (slot.IsDeliveryVisual || !binding.IsAssigned)
                {
                    continue;
                }

                var timingOffset = slot.ClampTimingOffset(binding.EventTimingOffset) /
                                   context.PlaybackSpeed;
                if (timingOffset < 0f &&
                    eventType == MonsterBasicAttackVfxEvent.RecipeExecute &&
                    registry.HasClaim(slot, context, "vfx"))
                {
                    continue;
                }
                if (!registry.TryClaim(slot, context, "vfx"))
                {
                    continue;
                }

                if (timingOffset > 0f)
                {
                    var scheduledContext = context;
                    registry.Schedule(
                        timingOffset,
                        () => PlayVfxNow(slot, binding, scheduledContext, registry),
                        slot.EndPolicy,
                        context.SequenceId,
                        context.Projectile);
                    played = true;
                    continue;
                }

                played |= PlayVfxNow(slot, binding, context, registry);
            }
            return played;
        }

        public static void BeginMotion(in MonsterBasicAttackVfxContext context)
        {
            var registry = ResolveRegistry(context.Source);
            registry?.BeginSequence(context.SequenceId);
            Dispatch(MonsterBasicAttackVfxEvent.MotionStart, context);
            ScheduleRecipeLeadVfx(context, registry);
            Dispatch(MonsterBasicAttackVfxEvent.Telegraph, context);
        }

        public static void EndMotion(in MonsterBasicAttackVfxContext context)
        {
            Dispatch(MonsterBasicAttackVfxEvent.MotionEnd, context);
            ResolveRegistry(context.Source)?.Release(
                MonsterBasicAttackVfxEndPolicy.MotionEnd,
                context.SequenceId,
                null);
        }

        public static void EndDelivery(in MonsterBasicAttackVfxContext context)
        {
            Dispatch(MonsterBasicAttackVfxEvent.DeliveryEnd, context);
            ResolveRegistry(context.Source)?.Release(
                MonsterBasicAttackVfxEndPolicy.DeliveryEnd,
                context.SequenceId,
                context.Projectile);
        }

        private static MonsterBasicAttackVfxRegistry ResolveRegistry(UnitActor source)
        {
            if (source == null)
            {
                return null;
            }
            return source.GetComponent<MonsterBasicAttackVfxRegistry>() ??
                   source.gameObject.AddComponent<MonsterBasicAttackVfxRegistry>();
        }

        private static void ScheduleRecipeLeadVfx(
            in MonsterBasicAttackVfxContext context,
            MonsterBasicAttackVfxRegistry registry)
        {
            if (registry == null || context.Profile == null || context.Bindings == null ||
                context.Driver == null ||
                !context.Driver.TryGetNextAttackMarkerDelay(out var markerDelay))
            {
                return;
            }

            var slots = context.Profile?.VfxSlots;
            if (slots == null)
            {
                return;
            }

            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                if (slot == null ||
                    slot.EventType != MonsterBasicAttackVfxEvent.RecipeExecute ||
                    slot.IsDeliveryVisual ||
                    !MonsterBasicAttackVfxResolver.TryResolvePresentation(
                        context.Bindings,
                        context.Profile.AttackId,
                        slot,
                        context.MotionId,
                        out var binding) ||
                    !binding.IsAssigned)
                {
                    continue;
                }

                var timingOffset = slot.ClampTimingOffset(binding.EventTimingOffset) /
                                   context.PlaybackSpeed;
                if (timingOffset >= 0f || !registry.TryClaim(slot, context, "vfx"))
                {
                    continue;
                }

                var delay = Mathf.Max(0f, markerDelay + timingOffset);
                if (delay <= 0.0001f)
                {
                    PlayVfxNow(slot, binding, context, registry);
                    continue;
                }

                var scheduledContext = context;
                registry.Schedule(
                    delay,
                    () => PlayVfxNow(slot, binding, scheduledContext, registry),
                    slot.EndPolicy,
                    context.SequenceId,
                    context.Projectile);
            }
        }

        private static bool PlayVfxNow(
            MonsterBasicAttackVfxSlot slot,
            MonsterBasicAttackVfxBinding binding,
            in MonsterBasicAttackVfxContext context,
            MonsterBasicAttackVfxRegistry registry)
        {
            if (slot == null || binding == null || !binding.IsAssigned ||
                context.World == null || context.Source == null)
            {
                return false;
            }

            ResolveAnchor(
                slot.Anchor,
                context,
                out var anchor,
                out var position,
                out var rotation);
            var parent = slot.Attachment == MonsterBasicAttackVfxAttachment.FollowAnchor
                ? anchor
                : null;
            var instance = context.World.SpawnBasicAttackVfx(
                binding,
                position,
                rotation,
                parent,
                context.Source.RuntimeAssetSet?.BodyProfile?.VfxScale ?? 1f,
                context.PlaybackSpeed);
            if (instance == null)
            {
                return false;
            }

            if (slot.EndPolicy is MonsterBasicAttackVfxEndPolicy.Timed or
                MonsterBasicAttackVfxEndPolicy.ParticleDuration)
            {
                context.World.ScheduleMonsterObjectReturn(
                    instance,
                    binding.Lifetime / context.PlaybackSpeed);
            }
            else
            {
                registry?.Track(
                    context.World,
                    instance,
                    slot.EndPolicy,
                    context.SequenceId,
                    context.Projectile);
            }
            return true;
        }

        private static void ResolveAnchor(
            MonsterBasicAttackVfxAnchor anchorKind,
            in MonsterBasicAttackVfxContext context,
            out Transform anchor,
            out Vector3 position,
            out Quaternion rotation)
        {
            anchor = null;
            position = context.Origin;
            rotation = context.Rotation;
            switch (anchorKind)
            {
                case MonsterBasicAttackVfxAnchor.SourceRoot:
                    anchor = context.Source?.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.AttackOrigin:
                    anchor = context.Driver?.AttackOrigin ?? context.Source?.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.MarkerSocket:
                    anchor = context.Driver?.ResolveSocket(context.SocketOverride) ??
                             context.Source?.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.ProjectileRoot:
                    anchor = context.Projectile;
                    break;
                case MonsterBasicAttackVfxAnchor.TargetRoot:
                    anchor = (context.Target as Component)?.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.HitPoint:
                    position = context.HitPoint;
                    return;
                case MonsterBasicAttackVfxAnchor.AreaCenter:
                    position = context.AreaCenter;
                    return;
                case MonsterBasicAttackVfxAnchor.TrajectoryOrigin:
                    anchor = context.Driver?.AttackOrigin ?? context.Source?.transform;
                    position = context.Origin;
                    return;
            }

            if (anchor != null)
            {
                position = anchor.position;
                rotation = anchor.rotation;
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class MonsterBasicAttackVfxRegistry : MonoBehaviour // 반복 억제와 종료 수명 소유
    {
        private readonly HashSet<string> claims = new HashSet<string>();
        private readonly List<TrackedVfx> tracked = new List<TrackedVfx>();
        private readonly List<PendingAction> pending = new List<PendingAction>();

        public void BeginSequence(int sequenceId)
        {
            claims.Clear();
            ReleaseAll(MonsterBasicAttackVfxEndPolicy.MotionEnd);
        }

        public bool TryClaim(
            MonsterBasicAttackVfxSlot slot,
            in MonsterBasicAttackVfxContext context,
            string channel)
        {
            return claims.Add(BuildClaimKey(slot, context, channel));
        }

        public bool HasClaim(
            MonsterBasicAttackVfxSlot slot,
            in MonsterBasicAttackVfxContext context,
            string channel)
        {
            return claims.Contains(BuildClaimKey(slot, context, channel));
        }

        public void Schedule(
            float delay,
            Action action,
            MonsterBasicAttackVfxEndPolicy endPolicy,
            int sequenceId,
            Transform delivery)
        {
            if (action == null)
            {
                return;
            }
            if (delay <= 0f)
            {
                action.Invoke();
                return;
            }
            pending.Add(new PendingAction(
                Time.time + delay,
                action,
                endPolicy,
                sequenceId,
                delivery));
        }

        private static string BuildClaimKey(
            MonsterBasicAttackVfxSlot slot,
            in MonsterBasicAttackVfxContext context,
            string channel)
        {
            var suffix = slot.Multiplicity switch
            {
                MonsterBasicAttackVfxMultiplicity.PerProjectile =>
                    context.Projectile == null ? "none" : context.Projectile.GetInstanceID().ToString(),
                MonsterBasicAttackVfxMultiplicity.PerTargetHit =>
                    $"{ResolveTargetId(context.Target)}:{context.DamageStage}",
                MonsterBasicAttackVfxMultiplicity.PerDamageStage => context.DamageStage.ToString(),
                _ => "once"
            };
            return $"{context.SequenceId}|{slot.SlotId}|{channel}|{suffix}";
        }

        private void Update()
        {
            for (var index = pending.Count - 1; index >= 0; index--)
            {
                var item = pending[index];
                if (Time.time + 0.0001f < item.ExecuteAt)
                {
                    continue;
                }
                pending.RemoveAt(index);
                item.Action?.Invoke();
            }
        }

        public void Track(
            CombatWorld world,
            GameObject instance,
            MonsterBasicAttackVfxEndPolicy policy,
            int sequenceId,
            Transform delivery)
        {
            tracked.Add(new TrackedVfx(world, instance, policy, sequenceId, delivery));
        }

        public void Release(
            MonsterBasicAttackVfxEndPolicy policy,
            int sequenceId,
            Transform delivery)
        {
            CancelPending(policy, sequenceId, delivery, false);
            for (var index = tracked.Count - 1; index >= 0; index--)
            {
                var item = tracked[index];
                var deliveryMatches = policy != MonsterBasicAttackVfxEndPolicy.DeliveryEnd ||
                                      item.Delivery == delivery;
                if (item.Policy != policy || item.SequenceId != sequenceId || !deliveryMatches)
                {
                    continue;
                }
                item.World?.ReturnMonsterObject(item.Instance);
                tracked.RemoveAt(index);
            }
        }

        private void ReleaseAll(MonsterBasicAttackVfxEndPolicy policy)
        {
            CancelPending(policy, 0, null, true);
            for (var index = tracked.Count - 1; index >= 0; index--)
            {
                var item = tracked[index];
                if (item.Policy != policy)
                {
                    continue;
                }
                item.World?.ReturnMonsterObject(item.Instance);
                tracked.RemoveAt(index);
            }
        }

        private void CancelPending(
            MonsterBasicAttackVfxEndPolicy policy,
            int sequenceId,
            Transform delivery,
            bool everySequence)
        {
            for (var index = pending.Count - 1; index >= 0; index--)
            {
                var item = pending[index];
                var deliveryMatches = policy != MonsterBasicAttackVfxEndPolicy.DeliveryEnd ||
                                      item.Delivery == delivery;
                if (item.EndPolicy != policy ||
                    !everySequence && item.SequenceId != sequenceId ||
                    !deliveryMatches)
                {
                    continue;
                }
                pending.RemoveAt(index);
            }
        }

        private void OnDisable()
        {
            for (var index = tracked.Count - 1; index >= 0; index--)
            {
                tracked[index].World?.ReturnMonsterObject(tracked[index].Instance);
            }
            tracked.Clear();
            pending.Clear();
            claims.Clear();
        }

        private static int ResolveTargetId(IDamageable target)
        {
            return target is Component component ? component.GetInstanceID() : 0;
        }

        private readonly struct TrackedVfx
        {
            public TrackedVfx(
                CombatWorld world,
                GameObject instance,
                MonsterBasicAttackVfxEndPolicy policy,
                int sequenceId,
                Transform delivery)
            {
                World = world;
                Instance = instance;
                Policy = policy;
                SequenceId = sequenceId;
                Delivery = delivery;
            }

            public CombatWorld World { get; }
            public GameObject Instance { get; }
            public MonsterBasicAttackVfxEndPolicy Policy { get; }
            public int SequenceId { get; }
            public Transform Delivery { get; }
        }

        private readonly struct PendingAction
        {
            public PendingAction(
                float executeAt,
                Action action,
                MonsterBasicAttackVfxEndPolicy endPolicy,
                int sequenceId,
                Transform delivery)
            {
                ExecuteAt = executeAt;
                Action = action;
                EndPolicy = endPolicy;
                SequenceId = sequenceId;
                Delivery = delivery;
            }

            public float ExecuteAt { get; }
            public Action Action { get; }
            public MonsterBasicAttackVfxEndPolicy EndPolicy { get; }
            public int SequenceId { get; }
            public Transform Delivery { get; }
        }
    }
}
