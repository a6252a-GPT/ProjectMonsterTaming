using System;
using ProjectMT.Contents.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.GiantSpellbook
{
    /*
     * DEV_03_GiantSpellbook Scene을 00_Entry나 실제 저장 데이터 없이 바로 실행하기 위한 개발 전용 진입점이다.
     * 정식 MainBattle에서는 AppRoot의 ContentFlow가 Context를 만들지만, DEV Scene에는 AppRoot가 없으므로
     * 이 컴포넌트가 단독 보스전 StartData와 DebugContentExit을 조립해 같은 GiantSpellbookController.Initialize()를 호출한다.
     * 따라서 팀원은 DEV Scene에서 먼저 작업해도 정식 MainBattle과 동일한 Runtime Prefab·Controller를 수정하게 된다.
     * DEV와 Hosted 차이는 Notion `04_1단계_현재시드구조_이해하기`, 확장 순서는
     * `05_2단계_현재시드에서_최종구조로_가는방법`의 성장 던전 부분을 참고한다.
     */
    [DisallowMultipleComponent]
    public sealed class GiantSpellbookDevBootstrap : MonoBehaviour // DEV 씬 단독 실행 진입점
    {
        [SerializeField] private GiantSpellbookController controller;
        [SerializeField] private GiantSpellbookStartDataFactory startDataFactory;

        private DebugContentExit debugExit;
        private GiantSpellbookDevOverlay overlay;

        private void Start()
        {
            if (controller == null || startDataFactory == null)
            {
                Debug.LogError("Giant Spellbook DEV references are missing.");
                return;
            }

            // DebugContentExit은 실제 저장이나 보상 처리를 하지 않고 종료 결과를 이벤트와 로그로만 돌려준다.
            debugExit = new DebugContentExit();
            debugExit.Exited += HandleExit;

            var startData = startDataFactory.Create(null); // 거대 마도서는 편성 몬스터를 사용하지 않는다.
            var context = new ContentContext(
                new ContentRunInfo(new ContentId("giant_spellbook"), "dev_seed", ContentRunMode.SeedTest),
                startData,
                debugExit);
            overlay = GiantSpellbookDevOverlay.Create();
            overlay.ShowEntry(() => controller.Initialize(context));
        }

        private void HandleExit(ContentOutcome outcome, IContentResultData result)
        {
            var resultScore = controller != null ? controller.CurrentScore : 0;
            controller.Shutdown();
            overlay ??= GiantSpellbookDevOverlay.Create();
            overlay.ShowResult(outcome, resultScore, () => overlay.gameObject.SetActive(false));
            Debug.Log($"Giant Spellbook DEV finished. Outcome={outcome}");
        }

        private void OnDestroy()
        {
            // Scene을 닫거나 PlayMode를 종료할 때 이벤트와 풀링 유닛을 남기지 않도록 정리한다.
            if (debugExit != null)
            {
                debugExit.Exited -= HandleExit;
            }

            if (controller != null)
            {
                controller.Shutdown();
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(GiantSpellbookController targetController, GiantSpellbookStartDataFactory factory)
        {
            controller = targetController;
            startDataFactory = factory;
        }
#endif
    }

    public sealed class GiantSpellbookDevOverlay : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject panel;
        private Text titleText;
        private Text bodyText;
        private Button actionButton;

        public static GiantSpellbookDevOverlay Create()
        {
            var root = new GameObject("GiantSpellbookDevOverlay_Runtime");
            var overlay = root.AddComponent<GiantSpellbookDevOverlay>();
            overlay.Build();
            return overlay;
        }

        public void ShowEntry(Action onEnter)
        {
            Show("거대 마도서", "성장 던전에 입장하시겠습니까?", "입장하기", () =>
            {
                panel.SetActive(false);
                onEnter?.Invoke();
            });
        }

        public void ShowResult(ContentOutcome outcome, int score, Action onExit)
        {
            var title = outcome == ContentOutcome.Complete ? "전투 결과" : "전투 종료";
            var body = outcome == ContentOutcome.Complete
                ? $"거대 마도서를 처치했습니다.\n최종 점수: {score}"
                : $"전투가 종료되었습니다.\n최종 점수: {score}";
            Show(title, body, "나가기", onExit);
        }

        private void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            gameObject.AddComponent<GraphicRaycaster>();

            panel = CreateObject("Panel", transform);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 360f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.035f, 0.05f, 0.075f, 0.96f);

            titleText = CreateText("Title", panel.transform, 48, TextAnchor.MiddleCenter);
            SetRect(titleText.rectTransform, new Vector2(0f, 70f), new Vector2(680f, 80f));

            bodyText = CreateText("Body", panel.transform, 30, TextAnchor.MiddleCenter);
            SetRect(bodyText.rectTransform, new Vector2(0f, -15f), new Vector2(680f, 90f));

            var buttonObject = CreateObject("ActionButton", panel.transform);
            var buttonRect = buttonObject.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(300f, 72f);
            buttonRect.anchoredPosition = new Vector2(0f, -115f);

            var buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(1f, 0.68f, 0.08f, 1f);
            actionButton = buttonObject.AddComponent<Button>();
            actionButton.targetGraphic = buttonImage;

            var label = CreateText("Label", buttonObject.transform, 28, TextAnchor.MiddleCenter);
            label.color = Color.black;
            SetRect(label.rectTransform, Vector2.zero, new Vector2(300f, 72f));
        }

        private void Show(string title, string body, string action, Action callback)
        {
            titleText.text = title;
            bodyText.text = body;
            actionButton.GetComponentInChildren<Text>().text = action;
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => callback?.Invoke());
            panel.SetActive(true);
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            var result = new GameObject(name);
            result.transform.SetParent(parent, false);
            return result;
        }

        private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
        {
            var result = CreateObject(name, parent).AddComponent<Text>();
            result.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            result.fontSize = fontSize;
            result.alignment = alignment;
            result.color = Color.white;
            return result;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
