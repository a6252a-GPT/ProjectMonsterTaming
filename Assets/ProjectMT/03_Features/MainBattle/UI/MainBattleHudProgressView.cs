using ProjectMT.Shared.GameData;
using TMPro;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleHudProgressView : MonoBehaviour // 계정·재화 HUD 표시 전담
    {
        [SerializeField] private TMP_Text commanderMetaText;
        [SerializeField] private TMP_Text goldValueText;
        [SerializeField] private TMP_Text diamondValueText;
        [SerializeField] private TMP_Text ascensionValueText;

        private IGameProgressService progress;

        public void Configure(IGameProgressService progressService)
        {
            if (progress == progressService)
            {
                RefreshView();
                return;
            }

            Shutdown();
            progress = progressService;
            if (progress != null)
            {
                progress.Changed += RefreshView;
            }

            RefreshView();
        }

        public void Shutdown()
        {
            if (progress != null)
            {
                progress.Changed -= RefreshView;
            }

            progress = null;
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void RefreshView()
        {
            if (progress == null)
            {
                SetText(commanderMetaText, "Lv. — · 다음 도전 —");
                SetText(goldValueText, "—");
                SetText(diamondValueText, "—");
                SetText(ascensionValueText, "—");
                return;
            }

            var view = progress.View;
            SetText(commanderMetaText, $"Lv. {view.Commander.Level} · 다음 도전 {view.CurrentChallengeStage}");
            SetText(goldValueText, $"{view.Gold:N0}");
            SetText(diamondValueText, $"{view.Diamond:N0}");
            SetText(ascensionValueText, $"{view.AscensionCurrency:N0}");
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            TMP_Text commanderMeta,
            TMP_Text goldValue,
            TMP_Text diamondValue,
            TMP_Text ascensionValue)
        {
            commanderMetaText = commanderMeta;
            goldValueText = goldValue;
            diamondValueText = diamondValue;
            ascensionValueText = ascensionValue;
        }
#endif
    }
}
