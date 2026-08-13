using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.GuardianTrial
{
    // 화면 밖에 있는(카메라 뷰포트 밖) 방어 건물 쪽으로 화면 가장자리에 빨간 화살표를 표시한다.
    // 부서진 건물이나 이미 화면에 보이는 건물은 표시하지 않고, 1초 주기로 깜빡인다.
    // GuardiansTowerController가 판마다 Initialize(structures)/Shutdown()을 호출해준다.
    [DisallowMultipleComponent]
    public sealed class GuardiansTowerOffscreenIndicator : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera; // 비워두면 Camera.main(=GuardiansTowerCamera) 사용
        [SerializeField] private RectTransform indicatorParent; // 비워두면 가장 가까운 상위 Canvas의 RectTransform 사용
        [SerializeField, Range(0f, 0.49f)] private float edgeMarginRatio = 0.08f; // 화면 가장자리에서 안쪽으로 들어오는 여유(캔버스 반너비/반높이 비율)
        [SerializeField, Min(0.1f)] private float blinkPeriodSeconds = 0.5f; // 깜빡임 전체 주기(켜짐+꺼짐 합쳐서 0.5초)
        [SerializeField, Min(1f)] private float indicatorSize = 40f;
        [SerializeField] private Color indicatorColor = new Color(1f, 0.16f, 0.16f, 1f);

        private static Sprite cachedTriangleSprite;

        private readonly List<RectTransform> indicatorIcons = new List<RectTransform>();
        private GuardiansTowerStructure[] structures = System.Array.Empty<GuardiansTowerStructure>();
        private float blinkTimer;

        public void Initialize(GuardiansTowerStructure[] structureList)
        {
            structures = structureList ?? System.Array.Empty<GuardiansTowerStructure>();
            blinkTimer = 0f;
            ResolveReferences();
            EnsureIconPool(structures.Length);
            for (var i = 0; i < indicatorIcons.Count; i++)
            {
                indicatorIcons[i].gameObject.SetActive(false);
            }
        }

        public void Shutdown()
        {
            structures = System.Array.Empty<GuardiansTowerStructure>();
            for (var i = 0; i < indicatorIcons.Count; i++)
            {
                if (indicatorIcons[i] != null)
                {
                    indicatorIcons[i].gameObject.SetActive(false);
                }
            }
        }

#if UNITY_EDITOR
        // 플레이 모드 종료/씬 언로드로 런타임 생성 아이콘들이 파괴될 때, 하이라키에서 그 아이콘이
        // 선택되어 있으면 인스펙터가 파괴된 오브젝트를 참조하다 콘솔에 MissingReferenceException을 낸다.
        // 파괴되기 직전에 선택을 미리 해제해서 이 에디터 전용 에러가 뜨지 않게 방어한다.
        private void OnDestroy()
        {
            var selected = UnityEditor.Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            for (var i = 0; i < indicatorIcons.Count; i++)
            {
                if (indicatorIcons[i] != null && indicatorIcons[i].gameObject == selected)
                {
                    UnityEditor.Selection.activeGameObject = null;
                    break;
                }
            }
        }
#endif

        private void ResolveReferences()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (indicatorParent == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    indicatorParent = canvas.transform as RectTransform;
                }
            }
        }

        private void LateUpdate()
        {
            if (worldCamera == null || indicatorParent == null || structures.Length == 0)
            {
                return;
            }

            blinkTimer += Time.deltaTime;
            if (blinkTimer >= blinkPeriodSeconds)
            {
                blinkTimer -= blinkPeriodSeconds;
            }

            var blinkOn = blinkTimer < blinkPeriodSeconds * 0.5f;
            var canvasSize = indicatorParent.rect.size;
            var halfWidth = canvasSize.x * (0.5f - edgeMarginRatio);
            var halfHeight = canvasSize.y * (0.5f - edgeMarginRatio);

            for (var i = 0; i < indicatorIcons.Count; i++)
            {
                var icon = indicatorIcons[i];
                if (icon == null)
                {
                    continue;
                }

                var structure = i < structures.Length ? structures[i] : null;
                if (structure == null || !structure.IsAlive)
                {
                    icon.gameObject.SetActive(false); // 부서진 건물은 표시하지 않음
                    continue;
                }

                var viewportPoint = worldCamera.WorldToViewportPoint(structure.transform.position);
                var behindCamera = viewportPoint.z <= 0f;
                if (behindCamera)
                {
                    // 카메라 뒤에 있으면 중심을 기준으로 뒤집어 대략적인 방향만 사용
                    viewportPoint.x = 1f - viewportPoint.x;
                    viewportPoint.y = 1f - viewportPoint.y;
                }

                var isOnScreen = !behindCamera &&
                    viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
                    viewportPoint.y >= 0f && viewportPoint.y <= 1f;
                if (isOnScreen || !blinkOn)
                {
                    icon.gameObject.SetActive(false); // 화면에 보이거나, 깜빡임 꺼짐 구간이면 숨김
                    continue;
                }

                // 뷰포트(0~1) 상의 편차를 캔버스 실제 픽셀 비율로 환산해야 화면 비율(예: 16:9)에 상관없이
                // 실제 화면에서 보이는 방향과 화살표 각도가 일치한다. (뷰포트 단위 그대로 쓰면 가로/세로 비율 왜곡이 생김)
                var direction = new Vector2(
                    (viewportPoint.x - 0.5f) * canvasSize.x,
                    (viewportPoint.y - 0.5f) * canvasSize.y);
                if (direction.sqrMagnitude < 0.0001f)
                {
                    direction = Vector2.up;
                }

                var scale = Mathf.Min(
                    halfWidth / Mathf.Max(Mathf.Abs(direction.x), 0.0001f),
                    halfHeight / Mathf.Max(Mathf.Abs(direction.y), 0.0001f));

                icon.gameObject.SetActive(true);
                icon.anchoredPosition = direction * scale;
                var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f; // ▲(위 방향) 기준 회전
                icon.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void EnsureIconPool(int count)
        {
            for (var i = indicatorIcons.Count; i < count; i++)
            {
                indicatorIcons.Add(CreateIcon(i));
            }
        }

        private RectTransform CreateIcon(int index)
        {
            var iconObject = new GameObject($"OffscreenIndicator_{index}", typeof(RectTransform), typeof(Image));
            var rectTransform = (RectTransform)iconObject.transform;
            rectTransform.SetParent(indicatorParent, worldPositionStays: false);
            rectTransform.sizeDelta = new Vector2(indicatorSize, indicatorSize);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var image = iconObject.GetComponent<Image>();
            image.sprite = GetOrCreateTriangleSprite();
            image.color = indicatorColor;
            image.raycastTarget = false;

            iconObject.SetActive(false);
            return rectTransform;
        }

        // 별도 스프라이트 에셋 없이 위쪽을 향한 삼각형 텍스처를 코드로 한 번만 생성해 공유한다.
        private static Sprite GetOrCreateTriangleSprite()
        {
            if (cachedTriangleSprite != null)
            {
                return cachedTriangleSprite;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                var normalizedHeight = y / (float)(size - 1); // 0(아래) ~ 1(위 꼭짓점)
                var allowedHalfWidth = half * (1f - normalizedHeight);
                for (var x = 0; x < size; x++)
                {
                    var distanceFromCenterX = Mathf.Abs(x + 0.5f - half);
                    var inside = distanceFromCenterX <= allowedHalfWidth;
                    texture.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
                }
            }

            texture.Apply();
            cachedTriangleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            return cachedTriangleSprite;
        }

#if UNITY_EDITOR
        public void EditorConfigure(Camera camera, RectTransform parent)
        {
            worldCamera = camera;
            indicatorParent = parent;
        }
#endif
    }
}
