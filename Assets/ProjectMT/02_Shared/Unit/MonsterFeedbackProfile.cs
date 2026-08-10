using System;
using ProjectMT.Shared.Audio;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [Serializable]
    public sealed class MonsterFeedbackCue // 한 동작 시점의 선택 사운드와 선택 VFX 묶음
    {
        [SerializeField] private SfxCue sfx;
        [SerializeField] private GameObject vfxPrefab;
        [SerializeField, Min(0.01f)] private float vfxLifetime = 1f;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField, Min(0.01f)] private float scale = 1f;

        public SfxCue Sfx => sfx;
        public GameObject VfxPrefab => vfxPrefab;
        public float VfxLifetime => Mathf.Max(0.01f, vfxLifetime);
        public Vector3 LocalPosition => localPosition;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public float Scale => Mathf.Max(0.01f, scale);
        public bool HasAnyFeedback => sfx != null || vfxPrefab != null;

        public bool TryValidate(out string error)
        {
            if (sfx != null && !sfx.HasPlayableClip)
            {
                error = $"Assigned SFX Cue has no playable AudioClip. Cue={sfx.name}";
                return false;
            }

            if (vfxPrefab != null && (vfxLifetime <= 0f || scale <= 0f))
            {
                error = $"Assigned VFX requires positive lifetime and scale. VFX={vfxPrefab.name}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            SfxCue cue,
            GameObject vfx,
            float lifetime = 1f,
            Vector3 position = default,
            Vector3 eulerAngles = default,
            float scaleMultiplier = 1f)
        {
            sfx = cue;
            vfxPrefab = vfx;
            vfxLifetime = Mathf.Max(0.01f, lifetime);
            localPosition = position;
            localEulerAngles = eulerAngles;
            scale = Mathf.Max(0.01f, scaleMultiplier);
        }
#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Feedback Profile", fileName = "MF_Monster")]
    public sealed class MonsterFeedbackProfile : ScriptableObject // 기존 데이터 호환을 포함한 Monster 선택 피드백 원본
    {
        [SerializeField] private MonsterFeedbackCue spawn;
        [SerializeField] private MonsterFeedbackCue attackStart;
        [SerializeField] private MonsterFeedbackCue attackMarker;
        [SerializeField] private MonsterFeedbackCue hitReceived;
        [SerializeField] private MonsterFeedbackCue death;
        [SerializeField] private MonsterFeedbackCue special;

        public MonsterFeedbackCue Spawn => spawn;
        public MonsterFeedbackCue AttackStart => attackStart;
        public MonsterFeedbackCue AttackMarker => attackMarker;
        public MonsterFeedbackCue HitReceived => hitReceived;
        public MonsterFeedbackCue Death => death;
        public MonsterFeedbackCue Special => special;

        public bool TryValidate(out string error)
        {
            var cues = new[] { spawn, attackStart, attackMarker, hitReceived, death, special };
            var roles = new[] { "Spawn", "AttackStart", "AttackMarker", "HitReceived", "Death", "Special" };
            for (var index = 0; index < cues.Length; index++)
            {
                if (cues[index] != null && !cues[index].TryValidate(out var cueError))
                {
                    error = $"Monster Feedback is invalid. Role={roles[index]}, Detail={cueError}";
                    return false;
                }
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            MonsterFeedbackCue spawnCue,
            MonsterFeedbackCue attackStartCue,
            MonsterFeedbackCue attackMarkerCue,
            MonsterFeedbackCue hitReceivedCue,
            MonsterFeedbackCue deathCue,
            MonsterFeedbackCue specialCue)
        {
            spawn = spawnCue;
            attackStart = attackStartCue;
            attackMarker = attackMarkerCue;
            hitReceived = hitReceivedCue;
            death = deathCue;
            special = specialCue;
        }
#endif
    }
}
