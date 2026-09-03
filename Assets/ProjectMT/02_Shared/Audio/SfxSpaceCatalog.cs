using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Shared.Audio
{
    public enum SfxSpacePriority
    {
        P0,
        P1,
        P2
    }

    public enum SfxSpaceCoverageState // 전수조사 시점의 실제 연결 상태
    {
        Connected,
        Partial,
        EmptySlot,
        MissingHook,
        FollowUp
    }

    public enum SfxSpaceAssignmentState // 사운드 담당자가 내리는 결정
    {
        Undecided,
        Disabled,
        Assigned
    }

    [Serializable]
    public sealed class SfxSpaceDefinition // 전수조사 원본에서 동기화하는 변경 불가 메타데이터
    {
        [SerializeField] private string id;
        [SerializeField] private SfxSpacePriority priority;
        [SerializeField] private string categoryId;
        [SerializeField] private string area;
        [SerializeField] private string eventName;
        [SerializeField] private SfxSpaceCoverageState coverageState;
        [SerializeField, TextArea] private string evidence;
        [SerializeField, TextArea] private string completionCriteria;

        public string Id => id;
        public SfxSpacePriority Priority => priority;
        public string CategoryId => categoryId;
        public string Area => area;
        public string EventName => eventName;
        public SfxSpaceCoverageState CoverageState => coverageState;
        public string Evidence => evidence;
        public string CompletionCriteria => completionCriteria;

#if UNITY_EDITOR
        public SfxSpaceDefinition(
            string spaceId,
            SfxSpacePriority spacePriority,
            string spaceCategoryId,
            string spaceArea,
            string spaceEventName,
            SfxSpaceCoverageState spaceCoverageState,
            string spaceEvidence,
            string spaceCompletionCriteria)
        {
            id = spaceId;
            priority = spacePriority;
            categoryId = spaceCategoryId;
            area = spaceArea;
            eventName = spaceEventName;
            coverageState = spaceCoverageState;
            evidence = spaceEvidence;
            completionCriteria = spaceCompletionCriteria;
        }
#endif
    }

    [Serializable]
    public sealed class SfxSpaceEntry // 공간 정의와 사용자의 Cue 결정만 보존
    {
        [SerializeField] private string id;
        [SerializeField] private SfxSpacePriority priority;
        [SerializeField] private string categoryId;
        [SerializeField] private string area;
        [SerializeField] private string eventName;
        [SerializeField] private SfxSpaceCoverageState coverageState;
        [SerializeField, TextArea] private string evidence;
        [SerializeField, TextArea] private string completionCriteria;
        [SerializeField] private SfxSpaceAssignmentState assignmentState;
        [SerializeField] private SfxCue cue;
        [SerializeField, TextArea] private string note;

        public string Id => id;
        public SfxSpacePriority Priority => priority;
        public string CategoryId => categoryId;
        public string Area => area;
        public string EventName => eventName;
        public SfxSpaceCoverageState CoverageState => coverageState;
        public string Evidence => evidence;
        public string CompletionCriteria => completionCriteria;
        public SfxSpaceAssignmentState AssignmentState => assignmentState;
        public SfxCue Cue => cue;
        public string Note => note;
        public bool HasExistingOwner => coverageState == SfxSpaceCoverageState.Connected ||
                                        coverageState == SfxSpaceCoverageState.Partial ||
                                        coverageState == SfxSpaceCoverageState.EmptySlot;
        public bool IsDecisionClosed => coverageState == SfxSpaceCoverageState.Connected ||
                                        assignmentState == SfxSpaceAssignmentState.Disabled ||
                                        (assignmentState == SfxSpaceAssignmentState.Assigned && cue != null);

#if UNITY_EDITOR
        public SfxSpaceEntry(SfxSpaceDefinition definition)
        {
            EditorApplyDefinition(definition);
        }

        public bool EditorApplyDefinition(SfxSpaceDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            var changed = id != definition.Id ||
                          priority != definition.Priority ||
                          categoryId != definition.CategoryId ||
                          area != definition.Area ||
                          eventName != definition.EventName ||
                          coverageState != definition.CoverageState ||
                          evidence != definition.Evidence ||
                          completionCriteria != definition.CompletionCriteria;
            id = definition.Id;
            priority = definition.Priority;
            categoryId = definition.CategoryId;
            area = definition.Area;
            eventName = definition.EventName;
            coverageState = definition.CoverageState;
            evidence = definition.Evidence;
            completionCriteria = definition.CompletionCriteria;
            return changed;
        }

        public bool EditorSetAssignment(SfxSpaceAssignmentState state, SfxCue sourceCue)
        {
            if (sourceCue != null)
            {
                state = SfxSpaceAssignmentState.Assigned;
            }

            var nextCue = state == SfxSpaceAssignmentState.Assigned ? sourceCue : null;
            if (assignmentState == state && cue == nextCue)
            {
                return false;
            }

            assignmentState = state;
            cue = nextCue;
            return true;
        }

        public bool EditorSetNote(string value)
        {
            value ??= string.Empty;
            if (note == value)
            {
                return false;
            }

            note = value;
            return true;
        }
#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Audio/SFX Space Catalog", fileName = "SfxSpaceCatalog")]
    public sealed class SfxSpaceCatalog : ScriptableObject // 110개 사건의 결정 상태를 보존하는 Editor 기준 자산
    {
        [SerializeField] private List<SfxSpaceEntry> entries = new List<SfxSpaceEntry>();

        public IReadOnlyList<SfxSpaceEntry> Entries => entries;

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (entries == null || entries.Count == 0)
            {
                error = "SFX 공간이 없습니다.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                {
                    error = $"SFX 공간 {index + 1}의 ID가 비어 있습니다.";
                    return false;
                }

                if (!ids.Add(entry.Id))
                {
                    error = $"SFX 공간 ID가 중복되었습니다: {entry.Id}";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(entry.CategoryId))
                {
                    error = $"{entry.Id}의 영역 ID가 비어 있습니다.";
                    return false;
                }

            }

            return true;
        }

#if UNITY_EDITOR
        public bool EditorSynchronize(IEnumerable<SfxSpaceDefinition> definitions)
        {
            entries ??= new List<SfxSpaceEntry>();
            var definitionList = definitions?
                .Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id))
                .GroupBy(definition => definition.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList() ?? new List<SfxSpaceDefinition>();

            var existingById = entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Id))
                .GroupBy(entry => entry.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var ordered = new List<SfxSpaceEntry>(Math.Max(entries.Count, definitionList.Count));
            var changed = false;

            foreach (var definition in definitionList)
            {
                if (existingById.TryGetValue(definition.Id, out var existing))
                {
                    changed |= existing.EditorApplyDefinition(definition);
                    ordered.Add(existing);
                    existingById.Remove(definition.Id);
                }
                else
                {
                    ordered.Add(new SfxSpaceEntry(definition));
                    changed = true;
                }
            }

            foreach (var customEntry in entries.Where(entry => entry != null && existingById.ContainsKey(entry.Id)))
            {
                ordered.Add(customEntry);
                existingById.Remove(customEntry.Id);
            }

            if (entries.Count != ordered.Count || !entries.SequenceEqual(ordered))
            {
                changed = true;
            }

            entries = ordered;
            return changed;
        }

        public SfxSpaceEntry EditorFindEntry(string spaceId)
        {
            return entries?.FirstOrDefault(entry => entry != null && entry.Id == spaceId);
        }

        public bool EditorSetAssignment(string spaceId, SfxSpaceAssignmentState state, SfxCue cue)
        {
            return EditorFindEntry(spaceId)?.EditorSetAssignment(state, cue) == true;
        }

        public bool EditorSetNote(string spaceId, string note)
        {
            return EditorFindEntry(spaceId)?.EditorSetNote(note) == true;
        }
#endif
    }
}
