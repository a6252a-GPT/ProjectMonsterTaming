using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class WorldHealthBarPresenter : MonoBehaviour // 피격 일반 유닛 HP바 풀·좌표 갱신
    {
        public const float DefaultVisibleSeconds = 1.35f;
        public static readonly Color FriendlyColor = new Color32(70, 218, 116, 255);
        public static readonly Color HostileColor = new Color32(239, 79, 72, 255);

        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform container;
        [SerializeField] private WorldHealthBarView viewPrefab;
        [SerializeField] private Camera worldCamera;
        [SerializeField, Min(0.1f)] private float visibleSeconds = DefaultVisibleSeconds;
        [SerializeField, Range(1, 64)] private int maxActiveBars = 24;

        private readonly Dictionary<int, ActiveBar> active = new Dictionary<int, ActiveBar>();
        private readonly Stack<WorldHealthBarView> available = new Stack<WorldHealthBarView>();
        private readonly List<int> releaseKeys = new List<int>();
        private bool visible = true;

        public int ActiveCount => active.Count;
        public bool IsVisible => visible;

        private void LateUpdate()
        {
            RefreshActiveBars();
        }

        private void OnDisable()
        {
            ReleaseAll();
        }

        public void ShowDamage(UnitActor target)
        {
            if (!visible || target == null || target.IsBoss || target.Health == null || !target.IsAlive ||
                container == null || viewPrefab == null || !isActiveAndEnabled)
            {
                return;
            }

            var key = target.GetInstanceID();
            var ratio = ResolveHealthRatio(target.Health);
            if (active.TryGetValue(key, out var current))
            {
                current.HideAt = Time.unscaledTime + visibleSeconds;
                current.View.SetHealthRatio(ratio);
                active[key] = current;
                return;
            }

            if (active.Count >= Mathf.Max(1, maxActiveBars))
            {
                return; // 모바일 화면의 동시 표시 예산 밖 요청은 생략
            }

            var view = RentView();
            if (view == null)
            {
                return;
            }

            view.Bind(target.Team == UnitTeam.Player ? FriendlyColor : HostileColor, ratio);
            active.Add(key, new ActiveBar(target, view, Time.unscaledTime + visibleSeconds));
            UpdatePosition(target, view);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (!visible)
            {
                ReleaseAll();
            }
        }

        private void RefreshActiveBars()
        {
            if (active.Count == 0)
            {
                return;
            }

            releaseKeys.Clear();
            var now = Time.unscaledTime;
            foreach (var pair in active)
            {
                var entry = pair.Value;
                if (!visible || entry.Target == null || !entry.Target.gameObject.activeInHierarchy ||
                    entry.Target.Health == null ||
                    !entry.Target.IsAlive || now >= entry.HideAt)
                {
                    releaseKeys.Add(pair.Key);
                    continue;
                }

                entry.View.SetHealthRatio(ResolveHealthRatio(entry.Target.Health));
                UpdatePosition(entry.Target, entry.View);
            }

            for (var index = 0; index < releaseKeys.Count; index++)
            {
                Release(releaseKeys[index]);
            }
        }

        private void UpdatePosition(UnitActor target, WorldHealthBarView view)
        {
            if (target == null || view == null || container == null)
            {
                return;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (worldCamera == null)
            {
                view.SetScreenVisible(false);
                return;
            }

            var height = target.RuntimeAssetSet?.BodyProfile?.HpBarHeight ?? 1.2f;
            var screen = worldCamera.WorldToScreenPoint(target.transform.position + Vector3.up * height);
            var onScreen = screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width &&
                           screen.y >= 0f && screen.y <= Screen.height;
            view.SetScreenVisible(onScreen);
            if (!onScreen)
            {
                return;
            }

            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    container,
                    screen,
                    eventCamera,
                    out var localPoint))
            {
                // 반 픽셀에 걸쳐 흐려지지 않도록 화면 좌표를 정수 픽셀에 맞춘다.
                view.RectTransform.anchoredPosition = new Vector2(
                    Mathf.Round(localPoint.x),
                    Mathf.Round(localPoint.y));
            }
        }

        private WorldHealthBarView RentView()
        {
            WorldHealthBarView view;
            if (available.Count > 0)
            {
                view = available.Pop();
            }
            else
            {
                view = Instantiate(viewPrefab, container, false);
                view.name = viewPrefab.name + "(Runtime)";
            }

            view.gameObject.SetActive(true);
            return view;
        }

        private void Release(int key)
        {
            if (!active.TryGetValue(key, out var entry))
            {
                return;
            }

            active.Remove(key);
            entry.View.SetScreenVisible(false);
            entry.View.gameObject.SetActive(false);
            available.Push(entry.View);
        }

        private void ReleaseAll()
        {
            releaseKeys.Clear();
            foreach (var pair in active)
            {
                releaseKeys.Add(pair.Key);
            }

            for (var index = 0; index < releaseKeys.Count; index++)
            {
                Release(releaseKeys[index]);
            }

            releaseKeys.Clear();
        }

        private static float ResolveHealthRatio(HealthComponent health)
        {
            return health != null && health.MaxHealth > 0f
                ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth)
                : 0f;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Canvas targetCanvas,
            RectTransform targetContainer,
            WorldHealthBarView prefab,
            Camera camera = null)
        {
            canvas = targetCanvas;
            container = targetContainer;
            viewPrefab = prefab;
            worldCamera = camera;
        }
#endif

        private struct ActiveBar
        {
            public ActiveBar(UnitActor target, WorldHealthBarView view, float hideAt)
            {
                Target = target;
                View = view;
                HideAt = hideAt;
            }

            public UnitActor Target;
            public WorldHealthBarView View;
            public float HideAt;
        }
    }
}
