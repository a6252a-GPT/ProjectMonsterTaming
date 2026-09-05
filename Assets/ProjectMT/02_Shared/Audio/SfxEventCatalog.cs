using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Audio
{
    [CreateAssetMenu(menuName = "ProjectMT/Audio/SFX Event Catalog")]
    public sealed class SfxEventCatalog : ScriptableObject // 편집 화면과 게임이 함께 읽는 공용 효과음 설정
    {
        public const string ResourceName = "SfxEvents_ProjectMT";
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public SfxSpaceAssignmentState state;
            public SfxCue cue;
        }
        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField] private bool managed;
        public bool IsManaged => managed;
        public IReadOnlyList<Entry> Entries => entries;

        public bool TryResolve(string id, out SfxCue cue)
        {
            cue = null;
            foreach (var entry in entries)
            {
                if (entry == null || entry.id != id) continue;
                if (entry.state == SfxSpaceAssignmentState.Undecided) return managed;
                cue = entry.state == SfxSpaceAssignmentState.Assigned ? entry.cue : null;
                return true; // 명시적 무음은 기존 설정으로 돌아가지 않음
            }
            return managed && SfxEvents.Supports(id);
        }
#if UNITY_EDITOR
        public void EditorSetManaged() => managed = true;
        public void EditorAdd(string id)
        {
            if (!SfxEvents.Supports(id) || entries.Exists(e => e != null && e.id == id)) return;
            entries.Add(new Entry { id = id, state = SfxSpaceAssignmentState.Assigned });
        }
        public void EditorRemove(string id) => entries.RemoveAll(e => e != null && e.id == id);
        public void EditorReplace(IEnumerable<Entry> values) => entries = new List<Entry>(values);
#endif
    }

    public static partial class SfxEvents // Monster Maker 개별 Sound는 이 경로를 사용하지 않음
    {
        public const string Button = "SYS-01", Close = "SYS-02", Open = "SYS-03";
        public const string Hit = "COM-01", Death = "COM-02", Weak = "COM-03", Strong = "COM-04";
        public const string Reward = "ECO-01";
        public const string BattleStart = "MB-01", Wave = "MB-02", Boss = "MB-03";
        public const string Victory = "MB-07", Defeat = "MB-08";
        public const string ContentVictory = Victory, ContentDefeat = Defeat;
        private static SfxEventCatalog catalog;
        private static bool loaded;
        private static SfxPool globalPool;

        public static bool Supports(string id)
        {
            foreach (var definition in Definitions)
                if (definition.Id == id) return true;
            return false;
        }

        public static bool TryResolve(string id, out SfxCue cue)
        {
            cue = null;
            if (!Supports(id)) return false;
            if (!loaded)
            {
                catalog = Resources.Load<SfxEventCatalog>(SfxEventCatalog.ResourceName);
                loaded = true;
            }
            return catalog != null && catalog.TryResolve(id, out cue);
        }
        public static SfxCue Resolve(string id, SfxCue fallback) =>
            TryResolve(id, out var cue) ? cue : fallback;

        public static SfxCue ResolveShared(string id, SfxCue fallback, bool makerOwnsSound)
        {
            if (!TryResolve(id, out var cue)) return fallback;
            if (cue == null) return null; // 공용 무음은 Maker 소리와 무관하게 적용
            return makerOwnsSound ? fallback : cue; // Maker에 새 기본음을 겹치지 않음
        }

        public static bool Play(string id, SfxPool pool, Vector3 position, SfxCue fallback = null)
        {
            var cue = Resolve(id, fallback);
            return cue != null && pool != null && pool.Play(cue, position);
        }
        public static void Play2D(string id)
        {
            if (!Application.isPlaying || !TryResolve(id, out var cue) || cue == null) return;
            if (globalPool == null)
            {
                var root = new GameObject("ProjectMT Common SFX");
                UnityEngine.Object.DontDestroyOnLoad(root);
                globalPool = root.AddComponent<SfxPool>();
            }
            globalPool.Play(cue, Vector3.zero);
        }
        public static void InvalidateCatalog()
        {
            catalog = null;
            loaded = false;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            InvalidateCatalog();
            globalPool = null;
        }
    }
}
