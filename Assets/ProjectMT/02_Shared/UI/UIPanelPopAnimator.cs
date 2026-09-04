using System;
using DG.Tweening;
using ProjectMT.Shared.Audio;
using UnityEngine;

namespace ProjectMT.Shared.UI
{
    // Mobile_UI_Panel_Animation_Patterns.md에서 정한 통일 연출 스타일.
    // 프로젝트 전체 패널이 같은 느낌을 갖도록 네 가지 패턴만 사용한다.
    public enum UIPanelPopStyle
    {
        Standard,      // 일반 관리 패널: 딤 페이드 + 0.96→1.0 스케일 + 16px 위로 이동, OutCubic
        RewardPopup,   // 보상/완료 팝업: 0.85→1.0 스케일 중심, 약한 OutBack
        FullScreenPage, // 상점처럼 화면 전체를 채우는 페이지: 스케일 없이 페이드 + 아래에서 위로 슬라이드
        FadeOnly       // 스케일/위치 이동 없이 페이드만: 내부에 3D 프리뷰(군단장 모델 등)를 물고 있어
                       // 스케일·위치 트윈이 그 프리뷰의 IK/카메라 기준점을 흔들면 안 되는 패널 전용
    }

    // 패널 오브젝트가 SetActive(true)로 켜질 때마다(OnEnable) 등장 연출을 자동 재생하고,
    // RequestOpen/RequestClose를 통해 호출하면 짧은 퇴장 연출 후 실제로 비활성화한다.
    // 각 컨트롤러는 기존 SetActive(true/false) 호출을 이 클래스의 정적 메서드로 바꾸기만 하면 된다.
    //
    // - pivot이 (0.5, 0.5)가 아닌 패널도 항상 "중앙 기준"으로 확대/이동하도록 pivot을 정규화한다.
    // - 딤(InputBlocker)은 애니메이션 대상에서 제외하고 즉시 표시/숨김한다. 실제 연출 대상을 찾는
    //   방식은 ResolveAnimationTarget 참고.
    [DisallowMultipleComponent]
    public sealed class UIPanelPopAnimator : MonoBehaviour
    {
        private const string InputBlockerName = "InputBlocker";

        // 표준 팝업 틀(PF_UIStandard_PopupMedium/Vertical/Wide 등)의 공통 접두사.
        // ResolveAnimationTarget이 이 이름으로 실제 콘텐츠 틀을 찾는다.
        private const string ContentFramePrefix = "PF_UIStandard_";

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
            UIButtonClickSound.ApplyToAllButtonsUnder(transform);
        }

        private void OnDisable()
        {
            var wasClosing = isClosing;
            sequence?.Kill();
            sequence = null;
            isClosing = false;

            // 조상이 먼저 비활성화되면 닫힘 트윈이 중간에 끊겨 activeSelf가 true로 남을 수 있다.
            // 그대로 두면 조상이 다시 켜질 때 이 패널도 함께 되살아나므로, 닫히는 중이었다면 확실히 꺼준다.
            if (wasClosing && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        // "InputBlocker" 자식이 있으면 그 형제 중 실제 콘텐츠만 연출 대상으로 삼아 딤을 제외한다.
        // 형제가 여럿이면(OutsideCloseArea, DeleteConfirmRoot 등 부가 오브젝트 포함) "PF_UIStandard_"
        // 접두사로 콘텐츠 틀을 우선 찾고, 못 찾으면 형제가 정확히 하나일 때만 그것을 쓴다(그 외에는
        // 안전하게 현재 노드를 그대로 사용).
        // InputBlocker가 안 보이고 자식이 하나뿐인 통과용 래퍼라면 한 단계 더 내려가며 다시 찾는다
        // (표준 팝업 틀처럼 한 겹 더 안쪽에 InputBlocker+콘텐츠가 있는 구조 대응).
        // 끝까지 못 찾으면(원래 딤이 없는 패널) 원본 루트를 그대로 쓴다.
        private static Transform ResolveAnimationTarget(Transform root)
        {
            var current = root;
            while (true)
            {
                Transform otherChild = null;
                Transform namedContentChild = null;
                var hasInputBlocker = false;
                var otherChildCount = 0;

                for (var i = 0; i < current.childCount; i++)
                {
                    var child = current.GetChild(i);
                    if (string.Equals(child.name, InputBlockerName, StringComparison.Ordinal))
                    {
                        hasInputBlocker = true;
                        continue;
                    }

                    otherChildCount++;
                    otherChild = child;

                    if (namedContentChild == null &&
                        child.name.StartsWith(ContentFramePrefix, StringComparison.Ordinal))
                    {
                        namedContentChild = child;
                    }
                }

                if (hasInputBlocker)
                {
                    if (namedContentChild != null)
                    {
                        return namedContentChild;
                    }

                    return otherChildCount == 1 ? otherChild : current;
                }

                if (otherChildCount == 1 && current.childCount == 1)
                {
                    current = otherChild;
                    continue;
                }

                // 못 찾았으면 원본 루트를 그대로 쓴다(딤 없는 기존 패널 동작 유지).
                return root;
            }
        }

        // 패널마다 pivot이 달라(0,0 / 0.5,0.5 등) 스케일 연출이 서로 다른 방향에서 확대되는 것처럼
        // 보이는 문제를 막기 위해 pivot을 (0.5, 0.5)로 정규화한다. 위치도 함께 보정해 화면상 위치는
        // 그대로 유지된다.
        //
        // 주의: 앵커가 늘어난(stretch) 축에서는 앵커 보간 항과 pivot 항이 상쇄되어 실제 위치가
        // sizeDelta에만 좌우된다(풀스크린 패널은 sizeDelta가 0이라 pivot이 바뀌어도 위치가 변하면
        // 안 된다). 그래서 rect.rect.size가 아니라 sizeDelta로 보정해야 하며, rect.rect.size를 쓰면
        // 풀스크린 패널이 옆으로 밀리는 버그가 생긴다.
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
            AudioManager.PlayPopupOpen();
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
                case UIPanelPopStyle.FadeOnly:
                    // 3D 프리뷰 IK 보호용 - 이유는 위 enum 주석 참고.
                    startScale = 1f;
                    openDuration = FullScreenOpenDuration;
                    fadeDuration = FullScreenFadeDuration;
                    moveOffsetY = 0f;
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
            AudioManager.PlayPopupClose();
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
                case UIPanelPopStyle.FadeOnly:
                    endScale = 1f;
                    closeMoveDuration = FullScreenCloseMoveDuration;
                    moveOffsetY = 0f;
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
