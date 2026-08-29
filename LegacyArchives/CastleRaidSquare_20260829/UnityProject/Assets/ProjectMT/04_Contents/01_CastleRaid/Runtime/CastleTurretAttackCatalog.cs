using System;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [CreateAssetMenu(menuName = "ProjectMT/Castle Raid/Turret Attack Catalog", fileName = "CastleTurretAttackCatalog")]
    public sealed class CastleTurretAttackCatalog : ScriptableObject // 3종 x 3레벨 포탑 프로필 표
    {
        [SerializeField] private CastleTurretAttackProfile[] profiles = new CastleTurretAttackProfile[9];

        public bool IsComplete
        {
            get
            {
                for (var family = 0; family < 3; family++)
                {
                    for (var level = 1; level <= 3; level++)
                    {
                        if (Resolve((CastleTurretFamily)family, level) == null)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        public CastleTurretAttackProfile Resolve(CastleTurretFamily family, int level)
        {
            if (profiles == null)
            {
                return null;
            }

            level = Mathf.Clamp(level, 1, 3);
            for (var index = 0; index < profiles.Length; index++)
            {
                var profile = profiles[index];
                if (profile != null && profile.IsValid && profile.Family == family && profile.Level == level)
                {
                    return profile;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(CastleTurretAttackProfile[] source)
        {
            if (source == null || source.Length != 9)
            {
                throw new ArgumentException("포탑 공격 프로필은 3종 x 3레벨 아홉 개가 필요합니다.", nameof(source));
            }

            profiles = new CastleTurretAttackProfile[source.Length];
            Array.Copy(source, profiles, source.Length);
            if (!IsComplete)
            {
                throw new ArgumentException("포탑 공격 프로필 종류·레벨 계약이 완성되지 않았습니다.", nameof(source));
            }
        }
#endif
    }
}
