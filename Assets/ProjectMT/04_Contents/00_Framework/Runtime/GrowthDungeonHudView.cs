using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.Framework
{
    [DisallowMultipleComponent]
    public sealed class GrowthDungeonHudView : MonoBehaviour // 성장 던전 공통 표시만 담당
    {
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text auxiliaryText;
        [SerializeField] private Image[] hearts;
        [SerializeField] private TMP_Text bossHealthText;
        [SerializeField] private Image bossHealthFill;
        [SerializeField] private TMP_Text breakText;
        [SerializeField] private Image breakFill;
        [SerializeField] private GameObject warningRoot;
        [SerializeField] private TMP_Text warningTitle;
        [SerializeField] private TMP_Text warningBody;
        [SerializeField] private Image[] structureFills;
        [SerializeField] private TMP_Text structuresText;

        public Image[] Hearts => hearts;
        public void SetStage(int stage) { if (stageText != null) stageText.SetText("{0}단계", Mathf.Max(1, stage)); }
        public void SetTimer(float remaining)
        {
            var seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
            if (timerText != null) timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
        }
        public void SetObjective(string value) { if (objectiveText != null && objectiveText.text != value) objectiveText.text = value; }
        public void SetAuxiliary(string value) { if (auxiliaryText != null && auxiliaryText.text != value) auxiliaryText.text = value; }
        public void SetHearts(int current, int maximum)
        {
            if (hearts == null) return;
            for (var i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null) continue;
                hearts[i].gameObject.SetActive(i < maximum);
                hearts[i].color = i < current ? Color.white : new Color(0.4f, 0.45f, 0.43f, 0.6f);
            }
        }
        public void SetBoss(float health, float maximum, float breakRatio, bool broken, float breakSeconds)
        {
            if (bossHealthFill != null) bossHealthFill.fillAmount = maximum > 0f ? Mathf.Clamp01(health / maximum) : 0f;
            if (bossHealthText != null) bossHealthText.text = $"{Mathf.CeilToInt(health):N0} / {Mathf.CeilToInt(maximum):N0}";
            if (breakFill != null) breakFill.fillAmount = Mathf.Clamp01(breakRatio);
            if (breakText != null) breakText.text = broken ? $"{breakSeconds:0.0}s" : $"{Mathf.RoundToInt(Mathf.Clamp01(breakRatio) * 100f)}%";
        }
        public void SetWarning(string title, string body)
        {
            var visible = !string.IsNullOrWhiteSpace(title);
            if (warningRoot != null && warningRoot.activeSelf != visible) warningRoot.SetActive(visible);
            if (warningTitle != null && warningTitle.text != title) warningTitle.text = title;
            if (warningBody != null && warningBody.text != body) warningBody.text = body;
        }
        public void SetStructure(int index, float ratio)
        {
            if (structureFills != null && index >= 0 && index < structureFills.Length && structureFills[index] != null)
                structureFills[index].fillAmount = Mathf.Clamp01(ratio);
        }
        public void SetStructuresRemaining(int alive, int total) { if (structuresText != null) structuresText.text = $"{alive} / {total} 남음"; }
    }
}
