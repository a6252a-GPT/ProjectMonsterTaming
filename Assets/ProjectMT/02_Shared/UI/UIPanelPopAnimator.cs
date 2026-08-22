using System;
using DG.Tweening;
using UnityEngine;

namespace ProjectMT.Shared.UI
{
    // Mobile_UI_Panel_Animation_Patterns.md에서 정한 통일 연출 스타일.
    // 프로젝트 전체 패널이 같은 느낌을 갖도록 세 가지 패턴만 사용한다.
    public enum UIPanelPopStyle
    {
        Standard,      // 일반 관리 패널: 딤 페이드 + 0.96→1.0 스케일 + 16px 위로 이동, OutCubic
        RewardPopup,   // 보상/완료 팝업: 0.85→1.0 스케일 중심, 약한 OutBack
        FullScreenPage // 상점처럼 화면 전체를 채우는 페이지: 스케일 없이 페이드 + 아래에서 위로 슬라이드
    }

    // 패널 오브젝트가 SetActive(true)로 켜질 때마다(OnEnable) 등장 연출을 자동 재생하고,
    // RequestOpen/RequestClose를 통해 호출하면 짧은 퇴장 연출 후 실제로 비활성화한다.
    // 각 컨트롤러는 기존 SetActive(true/false) 호출을 이 클래스의 정적 메서드로 바꾸기만 하면 된다.
    //
    // - pivot이 (0.5, 0.5)가 아닌 패널도 항상 "중앙 기준"으로 확대/이동하도록 pivot을 정규화한다.
    //   (패널마다 pivot이 달라 서로 다른 방향에서 펼쳐지는 것처럼 보이는 문제 방지)
    // - 루트 아래에 딤(InputBlocker)과 콘텐츠 프레임(PF_UIStandard_Popup*)이 형제로 있는 구조라면
    //   딤은 애니메이션 없이 즉시 표시/숨김되고, 콘텐츠 프레임만 등장/퇴장 연출 대상이 된다.
    [DisallowMultipleComponent]
    public sealed class UIPanelPopAnimator : MonoBehaviour
    {
        private const string ContentFrameNamePrefix = "PF_UIStandard_Popup";

        private const float StandardOpenDuration = 0.28f;
        private const float StandardFadeDuration = 0.16f;
        private const float StandardMoveOffsetY = -16f;
        private const float StandardStartScale = 0.96f;

        private const float RewardOpenDuration = 0.24f;
        private const float RewardFadeDuration = 0.16f;
        private const float RewardStartScale = 0.85f;

        private const float FullScreenOpenDuration = 0.30f;
        private const float FullScreenFadeDuration = 0.18f;
        private const float FullScreenMoveOffsetY = -60f;
        private const float FullScreenCloseMoveDuration = 0.20f;

        private const float CloseFadeDuration = 0.14f;
        private const float CloseMoveDuration = 0.18f;

        public UIPanelPopStyle Style { get; set; } = UIPanelPopStyle.Standard;

        private Transform animationTarget;
        private RectTransform panelRect;
        private CanvasGroup canvasGroup;
        private Vector2 restingAnchoredPosition;
        private Sequence sequence;
        private bool isClosing;

        private void Awake()
        {
            animationTarget = ResolveAnimationTarget(transform);

            panelRect = animationTarget as RectTransform;
            canvasGroup = animationTarget.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = animationTarget.gameObject.AddComponent<CanvasGroup>();
            }

            if (panelRect != null)
            {
                NormalizePivotToCenter(panelRect);
                restingAnchoredPosition = panelRect.anchoredPosition;
            }
        }

        private void OnEnable()
        {
            isClosing = false;
            PlayOpen();
            UIButtonClickPunch.ApplyToAllButtonsUnder(transform);
        }

        private void OnDisable()
        {
            sequence?.Kill();
            sequence = null;
        }

        // 딤(InputBlocker) 자식과 실제 콘텐츠 프레임(PF_UIStandard_Popup*) 자식이 형제로 있는 구조에서는
        // 딤을 애니메이션 대상에서 제외하고 콘텐츠 프레임만 등장/퇴장 연출한다.
        // 그런 자식이 없으면(딤이 없는 일반 패널) 자기 자신을 그대로 연출 대상으로 쓴다.
        private static Transform ResolveAnimationTarget(Transform root)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.StartsWith(ContentFrameNamePrefix, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return root;
        }

        // 패널 프리팹마다 pivot이 달라(0,0 / 0.5,0.5 등) 스케일 연출이 서로 다른 방향에서
        // 확대되는 것처럼 보이는 문제를 막기 위해 pivot을 (0.5, 0.5)로 정규화한다.
        // 위치를 함께 보정하므로 화면상 위치는 그대로 유지된다.
        //
        // 주의: 앵커가 늘어난(stretch, anchorMin != anchorMax) 축에서는 앵커 보간 항과 pivot 항이
        // 서로 상쇄되어 실제 위치는 sizeDelta에만 좌우된다(상점처럼 sizeDelta가 0인 완전 풀스크린
        // 패널은 pivot이 바뀌어도 화면상 위치가 변하지 않아야 한다). 그래서 rect.rect.size(실제 해석된
        // 크기)가 아니라 sizeDelta를 기준으로 보정해야 한다. rect.rect.size를 쓰면 풀스크린 패널이
        // 절반만큼 옆으로 밀려버리는 버그가 생긴다.
        private static void NormalizePivotToCenter(RectTransform rect)
        {
            var desiredPivot = new Vector2(0.5f, 0.5f);
            var currentPivot = rect.pivot;
            if ((currentPivot - desiredPivot).sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var sizeDelta = rect.sizeDelta;
            var offset = new Vector2(
                (desiredPivot.x - currentPivot.x) * sizeDelta.x,
                (desiredPivot.y - currentPivot.y) * sizeDelta.y);

            rect.pivot = desiredPivot;
            rect.anchoredPosition += offset;
        }

        private void PlayOpen()
        {
            sequence?.Kill();

            var target = animationTarget;
            float startScale;
            float openDuration;
            float fadeDuration;
            float moveOffsetY;
            var scaleEase = Ease.OutCubic;

            switch (Style)
            {
                case UIPanelPopStyle.RewardPopup:
                    startScale = RewardStartScale;
                    openDuration = RewardOpenDuration;
                    fadeDuration = RewardFadeDuration;
                    moveOffsetY = 0f;
                    scaleEase = Ease.OutBack;
                    break;
                case UIPanelPopStyle.FullScreenPage:
                    startScale = 1f; // 전체화면 페이지는 스케일 팝 없이 슬라이드+페이드만 사용
                    openDuration = FullScreenOpenDuration;
                    fadeDuration = FullScreenFadeDuration;
                    moveOffsetY = FullScreenMoveOffsetY;
                    break;
                default:
                    startScale = StandardStartScale;
                    openDuration = StandardOpenDuration;
                    fadeDuration = StandardFadeDuration;
                    moveOffsetY = StandardMoveOffsetY;
                    break;
            }

            canvasGroup.alpha = 0f;
            target.localScale = Vector3.one * startScale;

            if (panelRect != null && moveOffsetY != 0f)
            {
                panelRect.anchoredPosition = restingAnchoredPosition + new Vector2(0f, moveOffsetY);
            }

            sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 1f, fadeDuration).SetEase(Ease.OutCubic));
            if (!Mathf.Approximately(startScale, 1f))
            {
                sequence.Join(target.DOScale(Vector3.one, openDuration).SetEase(scaleEase));
            }
            if (panelRect != null && moveOffsetY != 0f)
            {
                // RectTransform.DOAnchorPos는 DOTween의 UI 모듈(느슨한 스크립트, asmdef 미적용)에 정의되어
                // 커스텀 어셈블리(ProjectMT.Shared)에서 보이지 않으므로, 코어 DLL의 DOTween.To로 대체한다.
                sequence.Join(DOTween.To(() => panelRect.anchoredPosition, x => panelRect.anchoredPosition = x,
                    restingAnchoredPosition, openDuration).SetEase(Ease.OutCubic));
            }
        }

        // Close() 계열 메서드에서 직접 호출하기보다는 정적 RequestClose(...)를 통해 사용한다.
        public void PlayClose(Action onFullyClosed)
        {
            if (isClosing || !isActiveAndEnabled)
            {
                isClosing = false;
                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }

                onFullyClosed?.Invoke();
                return;
            }

            isClosing = true;
            sequence?.Kill();

            var target = animationTarget;
            float endScale;
            float closeMoveDuration;
            float moveOffsetY;

            switch (Style)
            {
                case UIPanelPopStyle.RewardPopup:
                    endScale = RewardStartScale;
                    closeMoveDuration = CloseMoveDuration;
                    moveOffsetY = 0f;
                    break;
                case UIPanelPopStyle.FullScreenPage:
                    endScale = 1f;
                    closeMoveDuration = FullScreenCloseMoveDuration;
                    moveOffsetY = FullScreenMoveOffsetY;
                    break;
                default:
                    endScale = StandardStartScale;
                    closeMoveDuration = CloseMoveDuration;
                    moveOffsetY = StandardMoveOffsetY;
                    break;
            }

            sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 0f, CloseFadeDuration).SetEase(Ease.InCubic));
            if (!Mathf.Approximately(endScale, 1f))
            {
                sequence.Join(target.DOScale(Vector3.one * endScale, closeMoveDuration).SetEase(Ease.InCubic));
            }
            if (panelRect != null && moveOffsetY != 0f)
            {
                sequence.Join(DOTween.To(() => panelRect.anchoredPosition, x => panelRect.anchoredPosition = x,
                    restingAnchoredPosition + new Vector2(0f, moveOffsetY),
                    closeMoveDuration).SetEase(Ease.InCubic));
            }

            sequence.OnComplete(() =>
            {
                sequence = null;
                isClosing = false;
                if (this != null && gameObject != null)
                {
                    gameObject.SetActive(false);
                }

                onFullyClosed?.Invoke();
            });
        }

        // 닫히는 애니메이션 도중 다시 열기가 요청된 경우: 닫기를 취소하고 즉시 등장 연출을 다시 재생한다.
        private void CancelCloseAndReopen()
        {
            if (!isClosing)
            {
                return;
            }

            isClosing = false;
            PlayOpen();
        }

        // 대상에 애니메이터를 붙이고(없으면 추가) SetActive(true) 대신 호출한다.
        public static UIPanelPopAnimator RequestOpen(GameObject target, UIPanelPopStyle style = UIPanelPopStyle.Standard)
        {
            if (target == null)
            {
                return null;
            }

            var animator = target.GetComponent<UIPanelPopAnimator>();
            if (animator == null)
            {
                animator = target.AddComponent<UIPanelPopAnimator>();
            }

            animator.Style = style;

            if (target.activeSelf)
            {
                animator.CancelCloseAndReopen();
            }
            else
            {
                target.SetActive(true);
            }

            return animator;
        }

        // gameObject.SetActive(false) 대신 호출한다. 애니메이터가 없는 오브젝트는 즉시 비활성화된다.
        public static void RequestClose(GameObject target, Action onFullyClosed = null)
        {
            if (target == null)
            {
                onFullyClosed?.Invoke();
                return;
            }

            var animator = target.GetComponent<UIPanelPopAnimator>();
            if (animator != null && target.activeSelf)
            {
                animator.PlayClose(onFullyClosed);
                return;
            }

            if (target.activeSelf)
            {
                target.SetActive(false);
            }

            onFullyClosed?.Invoke();
        }
    }
}
