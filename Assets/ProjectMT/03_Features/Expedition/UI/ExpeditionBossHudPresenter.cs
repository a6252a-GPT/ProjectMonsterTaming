using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Expedition
{
    [DisallowMultipleComponent]
    public sealed class ExpeditionBossHudPresenter : MonoBehaviour // 10단위 원정대 보스 상단 고정 바
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text healthText;
        [SerializeField] private Image healthFill;
        [SerializeField] private Slider healthSlider;

        private UnitActor boss;

        public bool IsOpen => gameObject.activeSelf;
        public UnitActor Boss => boss;

        private void OnDisable()
        {
            Unbind();
        }

        public void Show(UnitActor target, int stage)
        {
            Unbind();
            if (target == null || target.Health == null || !target.IsBoss)
            {
                gameObject.SetActive(false);
                return;
            }

            boss = target;
            boss.Health.Damaged += HandleDamaged;
            boss.Died += HandleDied;
            if (titleText != null)
            {
                titleText.text = $"원정대 보스 · {Mathf.Max(1, stage)}단계";
            }

            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            Unbind();
            gameObject.SetActive(false);
        }

        private void HandleDamaged(DamageReport report)
        {
            Refresh();
        }

        private void HandleDied(UnitActor actor)
        {
            Hide();
        }

        private void Refresh()
        {
            var health = boss?.Health;
            var maximum = health == null ? 0f : Mathf.Max(0f, health.MaxHealth);
            var current = health == null ? 0f : Mathf.Clamp(health.CurrentHealth, 0f, maximum);
            var ratio = maximum > 0f ? current / maximum : 0f;
            if (healthSlider != null)
            {
                healthSlider.SetValueWithoutNotify(ratio);
            }

            if (healthFill != null)
            {
                healthFill.fillAmount = ratio;
            }

            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(current):N0} / {Mathf.CeilToInt(maximum):N0}";
            }
        }

        private void Unbind()
        {
            if (boss != null)
            {
                if (boss.Health != null)
                {
                    boss.Health.Damaged -= HandleDamaged;
                }

                boss.Died -= HandleDied;
            }

            boss = null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(Text title, Text value, Image fill, Slider slider = null)
        {
            titleText = title;
            healthText = value;
            healthFill = fill;
            healthSlider = slider;
        }
#endif
    }
}
