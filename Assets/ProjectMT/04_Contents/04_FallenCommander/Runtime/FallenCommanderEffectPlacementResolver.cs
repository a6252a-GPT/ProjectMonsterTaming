using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public enum FallenCommanderEffectStage
    {
        Start,
        Resolve,
        Hit
    }

    public readonly struct FallenCommanderEffectPlacementContext
    {
        public FallenCommanderEffectPlacementContext(
            Vector3 attackPosition,
            Vector3 attackDirection,
            Vector3? bossPosition = null,
            Vector3? commanderPosition = null,
            Vector3? projectilePosition = null,
            Vector3? groundPosition = null,
            bool clampHeightToGround = false)
        {
            AttackPosition = attackPosition;
            AttackDirection = attackDirection;
            BossPosition = bossPosition;
            CommanderPosition = commanderPosition;
            ProjectilePosition = projectilePosition;
            GroundPosition = groundPosition;
            ClampHeightToGround = clampHeightToGround;
        }

        public Vector3 AttackPosition { get; }
        public Vector3 AttackDirection { get; }
        public Vector3? BossPosition { get; }
        public Vector3? CommanderPosition { get; }
        public Vector3? ProjectilePosition { get; }
        public Vector3? GroundPosition { get; }
        public bool ClampHeightToGround { get; }
    }

    public readonly struct FallenCommanderEffectPlacement
    {
        public FallenCommanderEffectPlacement(
            Vector3 anchorPosition,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            AnchorPosition = anchorPosition;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public Vector3 AnchorPosition { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
    }

    public static class FallenCommanderEffectPlacementResolver
    {
        // 동일한 공격 연출 데이터와 위치 문맥을 실제 Play와 에디터 미리보기용 배치값으로 변환한다.
        public static FallenCommanderEffectPlacement Resolve(
            FallenCommanderAttackEffectData effects,
            FallenCommanderEffectStage stage,
            in FallenCommanderEffectPlacementContext context)
        {
            var anchor = stage switch
            {
                FallenCommanderEffectStage.Start =>
                    effects?.StartVfxAnchor ?? FallenCommanderEffectAnchor.AttackPosition,
                FallenCommanderEffectStage.Hit => FallenCommanderEffectAnchor.AttackPosition,
                _ => effects?.ResolveVfxAnchor ?? FallenCommanderEffectAnchor.AttackPosition
            };
            var anchorPosition = ResolveAnchorPosition(anchor, context);
            if (context.ClampHeightToGround && context.GroundPosition.HasValue)
            {
                anchorPosition.y = context.GroundPosition.Value.y;
            }
            var baseRotation = context.AttackDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(context.AttackDirection.normalized, Vector3.up)
                : Quaternion.identity;
            var positionOffset = stage switch
            {
                FallenCommanderEffectStage.Start => effects?.StartVfxPositionOffset ?? Vector3.zero,
                FallenCommanderEffectStage.Hit => effects?.HitVfxPositionOffset ?? Vector3.zero,
                _ => effects?.ResolveVfxPositionOffset ?? Vector3.zero
            };
            var rotationOffset = stage switch
            {
                FallenCommanderEffectStage.Start => effects?.StartVfxRotationOffset ?? Vector3.zero,
                FallenCommanderEffectStage.Hit => effects?.HitVfxRotationOffset ?? Vector3.zero,
                _ => effects?.ResolveVfxRotationOffset ?? Vector3.zero
            };
            var scale = stage switch
            {
                FallenCommanderEffectStage.Start => effects?.StartVfxScale ?? Vector3.one,
                FallenCommanderEffectStage.Hit => effects?.HitVfxScale ?? Vector3.one,
                _ => effects?.ResolveVfxScale ?? Vector3.one
            };

            return new FallenCommanderEffectPlacement(
                anchorPosition,
                anchorPosition + baseRotation * positionOffset,
                baseRotation * Quaternion.Euler(rotationOffset),
                scale);
        }

        // 선택한 기준 위치가 없으면 기존 동작과 동일하게 공격 지점을 안전하게 사용한다.
        private static Vector3 ResolveAnchorPosition(
            FallenCommanderEffectAnchor anchor,
            in FallenCommanderEffectPlacementContext context)
        {
            return anchor switch
            {
                FallenCommanderEffectAnchor.Boss when context.BossPosition.HasValue =>
                    context.BossPosition.Value,
                FallenCommanderEffectAnchor.Commander when context.CommanderPosition.HasValue =>
                    context.CommanderPosition.Value,
                FallenCommanderEffectAnchor.Projectile when context.ProjectilePosition.HasValue =>
                    context.ProjectilePosition.Value,
                FallenCommanderEffectAnchor.Ground when context.GroundPosition.HasValue =>
                    new Vector3(
                        context.AttackPosition.x,
                        context.GroundPosition.Value.y,
                        context.AttackPosition.z),
                _ => context.AttackPosition
            };
        }
    }
}
