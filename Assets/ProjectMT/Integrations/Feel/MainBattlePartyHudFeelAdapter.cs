using MoreMountains.Feedbacks;
using ProjectMT.Features.MainBattle;
using UnityEngine;

namespace ProjectMT.Integrations.Feel
{
    [DisallowMultipleComponent]
    public sealed class MainBattlePartyHudFeelAdapter : MonoBehaviour, IMainBattlePartyHudDamageFeedback
    {
        [SerializeField] private MMF_Player damagePlayer;
        [SerializeField] private MMPositionShaker positionShaker;
        [SerializeField] private RectTransform visualRoot;

        private Vector2 restingPosition;
        private bool hasRestingPosition;
        private bool initialized;

        public bool IsConfigured => damagePlayer != null && positionShaker != null && visualRoot != null;
        public int PlayCount { get; private set; }

        private void Awake()
        {
            CaptureRestingPosition();
        }

        public void PlayDamageFeedback()
        {
            if (!IsConfigured)
            {
                return;
            }

            ResetDamageFeedback();
            CaptureRestingPosition();
            positionShaker.Mode = MMPositionShaker.Modes.RectTransform;
            positionShaker.TargetRectTransform = visualRoot;
            damagePlayer.Initialization(true);
            initialized = true;
            damagePlayer.PlayFeedbacks();
            PlayCount++;
        }

        public void ResetDamageFeedback()
        {
            if (damagePlayer != null && initialized)
            {
                damagePlayer.StopFeedbacks();
                damagePlayer.RestoreInitialValues();
                damagePlayer.ResetFeedbacks();
            }
            initialized = false;

            if (visualRoot != null && hasRestingPosition)
            {
                visualRoot.anchoredPosition = restingPosition;
            }
        }

        private void OnDisable()
        {
            ResetDamageFeedback();
        }

        private void CaptureRestingPosition()
        {
            if (visualRoot == null)
            {
                return;
            }

            restingPosition = visualRoot.anchoredPosition;
            hasRestingPosition = true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(MMF_Player player, MMPositionShaker shaker, RectTransform target)
        {
            damagePlayer = player;
            positionShaker = shaker;
            visualRoot = target;
            CaptureRestingPosition();
        }
#endif
    }
}
