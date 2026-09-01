using System.Collections.Generic;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public enum FloatingNumberStyle // 시드에서 필요한 최소 숫자 스타일
    {
        EnemyDamage,
        PlayerDamage,
        Critical,
        Heal
    }

    [DisallowMultipleComponent]
    public sealed class FloatingNumberPresenter : MonoBehaviour // 피해 숫자 합산·예산·풀 재생
    {
        public const float DefaultHeightOffset = 1.15f;
        public const float DefaultMergeWindow = 0.08f;
        public const float DefaultDisplayDuration = 0.72f;
        public const float DefaultRiseDistance = 0.85f;
        public const float DefaultHorizontalDrift = 0.52f;
        public const float DefaultArcHeight = 0.16f;
        public const float DefaultStartTilt = 7f;
        public const float StatusQueueInterval = 0.28f;
        public const float StatusDuplicateWindow = 0.35f;
        public const float StatusMaximumQueueDelay = 1.5f;

        [SerializeField] private ScenePoolScope poolScope; // 현재 전투 수명 풀
        [SerializeField] private GameObject numberPrefab; // 정식 월드 TMP Prefab
        [SerializeField] private Camera worldCamera; // 비어 있으면 현재 MainCamera 사용
        [SerializeField, Min(0f)] private float heightOffset = DefaultHeightOffset; // 대상 위 표시 높이
        [SerializeField, Min(0f)] private float mergeWindow = DefaultMergeWindow; // 같은 대상 다단 히트 합산 시간
        [SerializeField, Min(1)] private int maxNumbersPerFrame = 6; // 한 프레임 생성 상한
        [SerializeField, Min(1)] private int maxActiveNumbers = 24; // 화면 동시 표시 상한
        [SerializeField, Min(0.1f)] private float displayDuration = DefaultDisplayDuration; // 숫자 유지 시간
        [SerializeField, Min(0f)] private float riseDistance = DefaultRiseDistance; // 위로 이동할 거리
        [SerializeField, Min(0f)] private float horizontalDrift = DefaultHorizontalDrift; // 좌우 팬 이동 거리
        [SerializeField, Min(0f)] private float arcHeight = DefaultArcHeight; // 곡선 상단 높이
        [SerializeField, Range(0f, 15f)] private float startTilt = DefaultStartTilt; // 시작 기울기

        private readonly Dictionary<int, PendingNumber> pending = new Dictionary<int, PendingNumber>(); // 대상별 합산 요청
        private readonly List<int> readyKeys = new List<int>(); // 순회 중 Dictionary 변경 방지
        private readonly List<PendingText> pendingTexts = new List<PendingText>();
        private readonly Dictionary<int, float> nextTextReleaseAt = new Dictionary<int, float>();
        private readonly Dictionary<int, RecentText> recentTexts = new Dictionary<int, RecentText>();
        private int activeNumberCount; // 현재 풀에서 재생 중인 숫자
        private int uniqueKeySeed = int.MinValue; // 합산하지 않을 요청 식별자
        private uint presentationSequence; // 반복 숫자의 좌우 방향 교대
        private bool visible = true; // 환경설정 연결 전 기본 표시

        public int ActiveNumberCount => activeNumberCount;
        public int PendingNumberCount => pending.Count;
        public int PendingTextCount => pendingTexts.Count;
        public bool IsVisible => visible;

        private void LateUpdate()
        {
            FlushReady();
        }

        private void OnDisable()
        {
            pending.Clear();
            pendingTexts.Clear();
            nextTextReleaseAt.Clear();
            recentTexts.Clear();
            readyKeys.Clear();
            activeNumberCount = 0;
            presentationSequence = 0u;
        }

        public void ShowDamage(UnitActor target, DamageReport report)
        {
            if (target == null)
            {
                return;
            }

            var style = report.Request.IsCritical
                ? FloatingNumberStyle.Critical
                : target.Team == UnitTeam.Player
                    ? FloatingNumberStyle.PlayerDamage
                    : FloatingNumberStyle.EnemyDamage;
            var passiveScale =
                (report.Request.FeedbackFlags & DamageFeedbackFlags.PassiveEnhancedNumber) != 0
                    ? 1.2f
                    : 1f;
            var mergeKey =
                (report.Request.FeedbackFlags & DamageFeedbackFlags.SeparateFloatingNumber) != 0
                    ? 0
                    : target.GetInstanceID();
            Queue(report.Request.HitPoint, report.AppliedDamage, style, mergeKey, passiveScale);
        }

        public void Queue(
            Vector3 position,
            float amount,
            FloatingNumberStyle style,
            int mergeKey = 0,
            float sizeMultiplier = 1f)
        {
            if (!visible || amount <= 0f || poolScope == null || numberPrefab == null || !isActiveAndEnabled)
            {
                return;
            }

            if (mergeKey == 0)
            {
                mergeKey = uniqueKeySeed++;
                if (uniqueKeySeed >= 0)
                {
                    uniqueKeySeed = int.MinValue; // 양수 Unity InstanceID와 충돌 방지
                }
            }

            var now = Time.unscaledTime;
            if (pending.TryGetValue(mergeKey, out var current))
            {
                current.Amount += amount;
                current.Position = position;
                current.ReleaseAt = now + mergeWindow;
                if (style == FloatingNumberStyle.Critical || current.Style != FloatingNumberStyle.Critical)
                {
                    current.Style = style;
                }
                current.SizeMultiplier = Mathf.Max(current.SizeMultiplier, sizeMultiplier);

                pending[mergeKey] = current;
                return;
            }

            pending.Add(mergeKey, new PendingNumber
            {
                Amount = amount,
                Position = position,
                ReleaseAt = now + mergeWindow,
                Style = style,
                SizeMultiplier = Mathf.Max(0.1f, sizeMultiplier)
            });
        }

        public void QueueText(
            Vector3 position,
            string value,
            CombatStatusTextStyle style,
            int queueKey = 0)
        {
            value = value?.Trim();
            if (!visible || string.IsNullOrWhiteSpace(value) || poolScope == null || numberPrefab == null ||
                !isActiveAndEnabled)
            {
                return;
            }

            if (queueKey == 0)
            {
                queueKey = uniqueKeySeed++;
                if (uniqueKeySeed >= 0)
                {
                    uniqueKeySeed = int.MinValue;
                }
            }

            var now = Time.unscaledTime;
            if (recentTexts.TryGetValue(queueKey, out var recent) &&
                recent.Value == value &&
                now - recent.QueuedAt < StatusDuplicateWindow)
            {
                return;
            }

            var releaseAt = nextTextReleaseAt.TryGetValue(queueKey, out var next)
                ? Mathf.Max(now, next)
                : now;
            if (releaseAt - now > StatusMaximumQueueDelay)
            {
                return; // 전투가 진행된 뒤 오래된 시작 문구가 뒤늦게 나오지 않게 한다
            }

            pendingTexts.Add(new PendingText
            {
                Position = position,
                Value = value,
                Style = style,
                ReleaseAt = releaseAt,
                QueuedAt = now,
                QueueKey = queueKey
            });
            nextTextReleaseAt[queueKey] = releaseAt + StatusQueueInterval;
            recentTexts[queueKey] = new RecentText(value, now);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (!visible)
            {
                pending.Clear(); // 비표시 전환 뒤 늦게 숫자가 뜨지 않게 한다
                pendingTexts.Clear();
                nextTextReleaseAt.Clear();
                recentTexts.Clear();
                readyKeys.Clear();
            }
        }

        private void FlushReady()
        {
            if (pending.Count == 0 && pendingTexts.Count == 0)
            {
                return;
            }

            var now = Time.unscaledTime;
            var spawned = FlushReadyTexts(now);
            if (spawned >= maxNumbersPerFrame || activeNumberCount >= maxActiveNumbers || pending.Count == 0)
            {
                return;
            }

            readyKeys.Clear();
            foreach (var pair in pending)
            {
                if (pair.Value.ReleaseAt <= now)
                {
                    readyKeys.Add(pair.Key);
                }
            }

            for (var i = 0; i < readyKeys.Count; i++)
            {
                var key = readyKeys[i];
                if (!pending.TryGetValue(key, out var request))
                {
                    continue;
                }

                pending.Remove(key);
                if (spawned >= maxNumbersPerFrame || activeNumberCount >= maxActiveNumbers)
                {
                    continue; // 오래 지난 숫자를 뒤늦게 표시하지 않고 예산 밖 요청 폐기
                }

                if (SpawnNumber(key, request))
                {
                    spawned++;
                }
            }
        }

        private int FlushReadyTexts(float now)
        {
            var spawned = 0;
            for (var index = pendingTexts.Count - 1; index >= 0; index--)
            {
                var request = pendingTexts[index];
                if (request.ReleaseAt > now)
                {
                    continue;
                }

                pendingTexts.RemoveAt(index);
                if (now - request.QueuedAt > StatusMaximumQueueDelay ||
                    spawned >= maxNumbersPerFrame ||
                    activeNumberCount >= maxActiveNumbers)
                {
                    continue;
                }

                if (SpawnText(request))
                {
                    spawned++;
                }
            }

            return spawned;
        }

        private bool SpawnNumber(int key, PendingNumber request)
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            presentationSequence = unchecked(presentationSequence + 1u);
            var signedDrift = ResolveHorizontalDrift(key, presentationSequence, horizontalDrift);
            var side = signedDrift < 0f ? -1f : 1f;
            var cameraRight = worldCamera != null ? worldCamera.transform.right : Vector3.right;
            var position = request.Position
                + Vector3.up * heightOffset
                + cameraRight * (signedDrift * 0.12f); // 시작점도 살짝 벌려 겹침 완화
            var instance = poolScope.Rent(numberPrefab, position, Quaternion.identity, poolScope.transform); // 활성 숫자도 PoolScope 아래 유지
            var view = instance == null ? null : instance.GetComponent<FloatingNumberView>();
            if (view == null)
            {
                Debug.LogError("Floating number prefab has no FloatingNumberView.");
                if (instance != null)
                {
                    poolScope.Return(instance);
                }

                return false;
            }

            activeNumberCount++;
            view.Play(
                poolScope,
                FormatValue(request.Amount, request.Style),
                ResolveColor(request.Style),
                displayDuration,
                riseDistance,
                signedDrift,
                arcHeight,
                startTilt * side,
                (request.Style == FloatingNumberStyle.Critical ? 1.25f : 1f) *
                Mathf.Max(0.1f, request.SizeMultiplier),
                worldCamera,
                HandleViewReleased);
            return true;
        }

        private bool SpawnText(PendingText request)
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            presentationSequence = unchecked(presentationSequence + 1u);
            var signedDrift = ResolveHorizontalDrift(request.QueueKey, presentationSequence, horizontalDrift * 0.14f);
            var cameraRight = worldCamera != null ? worldCamera.transform.right : Vector3.right;
            var position = request.Position + Vector3.up * heightOffset;
            var instance = poolScope.Rent(numberPrefab, position, Quaternion.identity, poolScope.transform);
            var view = instance == null ? null : instance.GetComponent<FloatingNumberView>();
            if (view == null)
            {
                Debug.LogError("Floating number prefab has no FloatingNumberView.");
                if (instance != null)
                {
                    poolScope.Return(instance);
                }
                return false;
            }

            activeNumberCount++;
            view.Play(
                poolScope,
                request.Value,
                ResolveStatusColor(request.Style),
                0.72f,
                0.58f,
                signedDrift,
                0.08f,
                0f,
                0.86f,
                worldCamera,
                HandleViewReleased);
            return true;
        }

        private void HandleViewReleased()
        {
            activeNumberCount = Mathf.Max(0, activeNumberCount - 1);
        }

        public static string FormatValue(float amount, FloatingNumberStyle style)
        {
            var value = Mathf.Max(1, Mathf.RoundToInt(amount));
            return style == FloatingNumberStyle.Heal ? $"+{value:N0}" : value.ToString("N0");
        }

        public static Color ResolveColor(FloatingNumberStyle style)
        {
            switch (style)
            {
                case FloatingNumberStyle.PlayerDamage:
                    return new Color(1f, 0.32f, 0.28f, 1f);
                case FloatingNumberStyle.Critical:
                    return new Color(1f, 0.55f, 0.08f, 1f);
                case FloatingNumberStyle.Heal:
                    return new Color(0.3f, 1f, 0.48f, 1f);
                default:
                    return new Color(1f, 0.9f, 0.35f, 1f);
            }
        }

        public static Color ResolveStatusColor(CombatStatusTextStyle style)
        {
            return style switch
            {
                CombatStatusTextStyle.Enhanced => new Color32(0xFF, 0xD4, 0x5C, 0xFF),
                CombatStatusTextStyle.AttackUp => new Color32(0xFF, 0x9F, 0x45, 0xFF),
                CombatStatusTextStyle.Haste => new Color32(0x5F, 0xE0, 0xC1, 0xFF),
                CombatStatusTextStyle.DamageReduction => new Color32(0x72, 0xA7, 0xFF, 0xFF),
                CombatStatusTextStyle.Shield => new Color32(0x65, 0xD9, 0xFF, 0xFF),
                CombatStatusTextStyle.Impact => new Color32(0xFF, 0x88, 0x48, 0xFF),
                _ => Color.white
            };
        }

        public static float ResolveHorizontalDrift(int key, uint sequence, float maximumDrift)
        {
            var side = (sequence & 1u) == 0u ? -1f : 1f;
            var hash = unchecked((uint)key * 747796405u + sequence * 2891336453u);
            var distanceVariation = Mathf.Lerp(0.78f, 1f, (hash & 255u) / 255f); // 기계적인 좌우 대칭 완화
            return Mathf.Max(0f, maximumDrift) * distanceVariation * side;
        }

#if UNITY_EDITOR
        public void EditorConfigure(ScenePoolScope pool, GameObject prefab, Camera camera = null)
        {
            poolScope = pool;
            numberPrefab = prefab;
            worldCamera = camera;
        }
#endif

        private struct PendingNumber
        {
            public Vector3 Position;
            public float Amount;
            public float ReleaseAt;
            public FloatingNumberStyle Style;
            public float SizeMultiplier;
        }

        private struct PendingText
        {
            public Vector3 Position;
            public string Value;
            public CombatStatusTextStyle Style;
            public float ReleaseAt;
            public float QueuedAt;
            public int QueueKey;
        }

        private readonly struct RecentText
        {
            public RecentText(string value, float queuedAt)
            {
                Value = value;
                QueuedAt = queuedAt;
            }

            public string Value { get; }
            public float QueuedAt { get; }
        }
    }
}
