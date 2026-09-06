using System;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public enum CommanderMarkTriggerType { HitCount, StackReached, Expire, Death, MarkTriggered }
    public enum CommanderMarkFeedbackAnchor { TargetRoot, TargetCenter, TargetFeet, HitPoint, WorldPosition, CasterRoot }

    [Serializable]
    public sealed class CommanderMarkFeedbackSlot
    {
        [SerializeField] private GameObject vfxPrefab;
        [SerializeField, Min(0.05f)] private float lifetime = 1f;
        [SerializeField] private Vector3 localOffset;
        [SerializeField] private Vector3 localEuler;
        [SerializeField, Min(0.01f)] private float scale = 1f;
        [SerializeField] private SfxCue sfx;
        [SerializeField] private CommanderMarkFeedbackAnchor anchor = CommanderMarkFeedbackAnchor.TargetCenter;
        public GameObject VfxPrefab => vfxPrefab;
        public float Lifetime => Mathf.Max(0.05f, lifetime);
        public Vector3 LocalOffset => localOffset;
        public Vector3 LocalEuler => localEuler;
        public float Scale => Mathf.Max(0.01f, scale);
        public SfxCue Sfx => sfx;
        public CommanderMarkFeedbackAnchor Anchor => anchor;
#if UNITY_EDITOR
        public void EditorConfigure(GameObject prefab, float seconds, Vector3 offset, Vector3 euler,
            float localScale, SfxCue cue, CommanderMarkFeedbackAnchor feedbackAnchor)
        {
            vfxPrefab = prefab;
            lifetime = Mathf.Max(0.05f, seconds);
            localOffset = offset;
            localEuler = euler;
            scale = Mathf.Max(0.01f, localScale);
            sfx = cue;
            anchor = feedbackAnchor;
        }
#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Commander Skill/Effects/Commander Mark", fileName = "CSEffect_CommanderMark")]
    public sealed class CommanderMarkEffectDefinition : CommanderSkillEffectDefinition
    {
        [SerializeField] private string markId;
        [SerializeField, Min(0.01f)] private float duration = 6f;
        [SerializeField] private CommanderSkillEffectScope scope;
        [SerializeField, Min(0.1f)] private float radius = 3f;
        [SerializeField, Min(1)] private int maxTargets = 8;
        [SerializeField] private CommanderMarkTriggerType triggerType = CommanderMarkTriggerType.HitCount;
        [SerializeField, Min(1)] private int requiredHits = 1;
        [SerializeField, Min(1)] private int requiredStacks = 1;
        [SerializeField, Min(1)] private int maxStacks = 1;
        [SerializeField] private bool consumeOnTrigger = true;
        [SerializeField] private bool refreshDurationOnApply = true;
        [SerializeField, Min(0f)] private float triggerCooldown;
        [SerializeField] private bool countBasicAttack = true;
        [SerializeField] private bool countMonsterSkill = true;
        [SerializeField] private bool countCommanderSkill = true;
        [SerializeField] private bool countCommanderMarkTrigger;
        [SerializeField] private bool recordHitCount;
        [SerializeField] private CommanderSkillEffectDefinition[] effectsOnTrigger = Array.Empty<CommanderSkillEffectDefinition>();
        [SerializeField] private CommanderMarkFeedbackSlot onApply = new CommanderMarkFeedbackSlot();
        [SerializeField] private CommanderMarkFeedbackSlot loop = new CommanderMarkFeedbackSlot();
        [SerializeField] private CommanderMarkFeedbackSlot onStack = new CommanderMarkFeedbackSlot();
        [SerializeField] private CommanderMarkFeedbackSlot onTrigger = new CommanderMarkFeedbackSlot();
        [SerializeField] private CommanderMarkFeedbackSlot onRemove = new CommanderMarkFeedbackSlot();
        public string MarkId => markId?.Trim() ?? string.Empty;
        public float Duration => Mathf.Max(0.01f, duration);
        public CommanderSkillEffectScope Scope => scope;
        public float Radius => Mathf.Max(0.1f, radius);
        public int MaxTargets => Mathf.Max(1, maxTargets);
        public CommanderMarkTriggerType TriggerType => triggerType;
        public int RequiredHits => Mathf.Max(1, requiredHits);
        public int RequiredStacks => Mathf.Max(1, requiredStacks);
        public int MaxStacks => Mathf.Max(1, maxStacks);
        public bool ConsumeOnTrigger => consumeOnTrigger;
        public bool RefreshDurationOnApply => refreshDurationOnApply;
        public float TriggerCooldown => Mathf.Max(0f, triggerCooldown);
        public bool RecordHitCount => recordHitCount;
        public bool CountBasicAttack => countBasicAttack;
        public bool CountMonsterSkill => countMonsterSkill;
        public bool CountCommanderSkill => countCommanderSkill;
        public bool CountCommanderMarkTrigger => countCommanderMarkTrigger;
        public System.Collections.Generic.IReadOnlyList<CommanderSkillEffectDefinition> EffectsOnTrigger => effectsOnTrigger ?? Array.Empty<CommanderSkillEffectDefinition>();
        public CommanderMarkFeedbackSlot OnApply => onApply;
        public CommanderMarkFeedbackSlot Loop => loop;
        public CommanderMarkFeedbackSlot OnStack => onStack;
        public CommanderMarkFeedbackSlot OnTrigger => onTrigger;
        public CommanderMarkFeedbackSlot OnRemove => onRemove;
        public bool Counts(CombatDamageOrigin origin) => origin switch
        {
            CombatDamageOrigin.MonsterSkill => countMonsterSkill,
            CombatDamageOrigin.CommanderSkill or CombatDamageOrigin.CommanderPeriodic => countCommanderSkill,
            CombatDamageOrigin.CommanderMarkTrigger => countCommanderMarkTrigger,
            _ => countBasicAttack
        };
        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error)) return false;
            if (string.IsNullOrWhiteSpace(MarkId) || duration <= 0f || requiredHits < 1 || requiredStacks < 1 || maxStacks < 1 || requiredStacks > maxStacks || triggerCooldown < 0f)
            { error = $"{EffectId}: commander mark values are invalid."; return false; }
            for (var index = 0; index < EffectsOnTrigger.Count; index++)
                if (EffectsOnTrigger[index] == null || !EffectsOnTrigger[index].TryValidate(out error))
                { error = $"{EffectId}: trigger effect {index} is invalid. {error}"; return false; }
            error = string.Empty; return true;
        }
#if UNITY_EDITOR
        public void EditorConfigure(string id, string runtimeMarkId, float seconds, CommanderSkillEffectScope targetScope,
            float effectRadius, int targetCount, CommanderMarkTriggerType trigger, int hits, int stacks,
            int stackLimit, bool consume, bool refresh, float cooldown, CommanderSkillEffectDefinition[] triggerEffects)
        {
            EditorConfigureId(id); markId = runtimeMarkId?.Trim() ?? string.Empty; duration = Mathf.Max(0.01f, seconds);
            scope = targetScope; radius = Mathf.Max(0.1f, effectRadius); maxTargets = Mathf.Max(1, targetCount);
            triggerType = trigger; requiredHits = Mathf.Max(1, hits); requiredStacks = Mathf.Max(1, stacks);
            maxStacks = Mathf.Max(requiredStacks, stackLimit); consumeOnTrigger = consume;
            refreshDurationOnApply = refresh; triggerCooldown = Mathf.Max(0f, cooldown);
            effectsOnTrigger = triggerEffects ?? Array.Empty<CommanderSkillEffectDefinition>();
        }

        public void EditorConfigureRecording(bool shouldRecordHits)
        {
            recordHitCount = shouldRecordHits;
        }

        public void EditorConfigureDamageOriginFilter(bool basicAttack, bool monsterSkill,
            bool commanderSkill, bool commanderMarkTrigger)
        {
            countBasicAttack = basicAttack;
            countMonsterSkill = monsterSkill;
            countCommanderSkill = commanderSkill;
            countCommanderMarkTrigger = commanderMarkTrigger;
        }

        public void EditorConfigureFeedback(CommanderMarkFeedbackSlot apply, CommanderMarkFeedbackSlot persistentLoop,
            CommanderMarkFeedbackSlot stack, CommanderMarkFeedbackSlot trigger, CommanderMarkFeedbackSlot remove)
        {
            onApply = apply ?? new CommanderMarkFeedbackSlot();
            loop = persistentLoop ?? new CommanderMarkFeedbackSlot();
            onStack = stack ?? new CommanderMarkFeedbackSlot();
            onTrigger = trigger ?? new CommanderMarkFeedbackSlot();
            onRemove = remove ?? new CommanderMarkFeedbackSlot();
        }
#endif
    }
}
