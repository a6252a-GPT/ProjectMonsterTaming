using System.Collections;
using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.CommanderSkill
{
    [DisallowMultipleComponent]
    public sealed class SkillInscriptionSequence : MonoBehaviour
    {
        [SerializeField] private Button tapSurface;
        [SerializeField] private SfxPool soundPool;
        private readonly List<CommanderSkillSummonResultItemView> items = new();
        private readonly List<Vector3> restScales = new();
        private TMP_Text heading;
        private TMP_Text buttonLabel;
        private string finalHeading;
        private string finalButton;
        [SerializeField] private SkillInscriptionGraphic massSeal;
        private bool massReveal;
        private System.Action completed;
        private int current;
        private bool skipCurrent;
        private float lastTap = -1;
        public bool IsPlaying { get; private set; }
        public int CurrentIndex => current;

        public void Play(IReadOnlyList<CommanderSkillSummonResultItemView> cards, TMP_Text title, Button close, bool mass = false, System.Action onComplete = null)
        {
            Finish();
            soundPool?.StopAll();
            if (!isActiveAndEnabled || cards.Count == 0) return;
            if (tapSurface == null || soundPool == null) throw new System.InvalidOperationException("Skill inscription needs authored input and audio references.");
            heading = title;
            finalHeading = title != null ? title.text : string.Empty;
            buttonLabel = close != null ? close.GetComponentInChildren<TMP_Text>(true) : null;
            finalButton = buttonLabel != null ? buttonLabel.text : string.Empty;
            for (int i = 0; i < cards.Count; i++)
            {
                items.Add(cards[i]);
                restScales.Add(cards[i].transform.localScale);
                cards[i].SetInscription(0, i * .7f);
            }
            massReveal = mass;
            completed = onComplete;
            current = 0;
            IsPlaying = true;
            lastTap = -1;
            tapSurface.onClick.RemoveListener(SkipCurrent);
            tapSurface.onClick.AddListener(SkipCurrent);
            tapSurface.gameObject.SetActive(true);
            if (buttonLabel != null) buttonLabel.text = mass ? "결과 바로 보기" : "한 장 각인";
            StartCoroutine(Reveal());
        }

        public void SkipCurrent()
        {
            if (!IsPlaying || Time.unscaledTime - lastTap < .10f) return;
            lastTap = Time.unscaledTime;
            if (massReveal) { var callback = completed; Finish(); callback?.Invoke(); return; }
            skipCurrent = true;
        }

        private IEnumerator Reveal()
        {
            if (massReveal)
            {
                if (massSeal == null) throw new System.InvalidOperationException("Missing mass inscription seal.");
                int highest = 0;
                foreach (var card in items)
                {
                    card.transform.localScale = Vector3.zero;
                    highest = Mathf.Max(highest, (int)card.Rarity);
                }
                massSeal.gameObject.SetActive(true);
                massSeal.Tier = highest;
                var accent = CommanderSkillSummonResultItemView.ResolveAccent((CommanderSkillRarity)highest);
                if (heading != null) heading.text = "30개의 마력 · 응축 중";
                float charge = 0;
                bool sounded = false;
                while (charge < 3.7f)
                {
                    charge += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(charge / 3.7f);
                    massSeal.Progress = progress;
                    massSeal.color = Color.Lerp(new Color(.94f,.73f,.44f,1), accent, Mathf.SmoothStep(0,1,progress));
                    massSeal.Clock = Time.unscaledTime * 2;
                    massSeal.Redraw();
                    if (!sounded && progress >= .67f) { PlaySound(highest); sounded = true; }
                    yield return null;
                }
                massSeal.gameObject.SetActive(false);
                if (heading != null) heading.text = "30회 소환 · 전체 결과 각인";
                float reveal = 0;
                while (reveal < 1.2f)
                {
                    reveal += Time.unscaledDeltaTime;
                    for (int i = 0; i < items.Count; i++)
                    {
                        float p = Mathf.Clamp01((reveal - (i % 5) * .035f) / .9f);
                        items[i].SetInscription(.67f + .33f * p, Time.unscaledTime);
                        items[i].transform.localScale = restScales[i] * (1f - Mathf.Pow(1f-p,3f) + Mathf.Sin(p*Mathf.PI)*.06f);
                    }
                    yield return null;
                }
                var finished = completed;
                Finish();
                finished?.Invoke();
                yield break;
            }
            float intro = 0;
            while (intro < .5f && !skipCurrent)
            {
                intro += Time.unscaledDeltaTime;
                for (int i = 0; i < items.Count; i++)
                {
                    float p = Mathf.Clamp01((intro - i * .025f) / .24f);
                    float scale = 1f - Mathf.Pow(1f-p, 3f) + Mathf.Sin(p*Mathf.PI)*.09f;
                    items[i].transform.localScale = restScales[i]*scale;
                    items[i].SetInscription(0, Time.unscaledTime + i);
                }
                yield return null;
            }
            for (int i = 0; i < items.Count; i++) items[i].transform.localScale = restScales[i];
            for (current = 0; current < items.Count; current++)
            {
                var card = items[current];
                int tier = Mathf.Clamp((int)card.Rarity, 0, 4);
                float duration = tier switch { 4 => 1.65f, 3 => 1.25f, 2 => .72f, 1 => .52f, _ => .40f };
                if (heading != null) heading.text = $"{finalHeading} · {current + 1} / {items.Count}";
                float elapsed = 0;
                bool sounded = false;
                while (elapsed < duration && !skipCurrent)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(elapsed / duration);
                    // 상위 등급은 응축 순간에 잠깐 머물렀다가 터진다.
                    float stage = tier >= 3 && p > .44f && p < .62f ? .56f : p < .44f && tier >= 3 ? p * 1.2727f : p;
                    card.SetInscription(stage, Time.unscaledTime);
                    float recoil = p < .67f ? -Mathf.Sin(p/.67f*Mathf.PI)*.06f : Mathf.Sin((p-.67f)/.33f*Mathf.PI*2)*.09f*(1-p)/.33f;
                    card.transform.localScale = restScales[current]*(1f+recoil);
                    if (!sounded && p >= .67f) { PlaySound(tier); sounded = true; }
                    for (int i = current + 1; i < items.Count; i++) items[i].SetInscription(0, Time.unscaledTime + i);
                    yield return null;
                }
                if (!sounded) PlaySound(tier);
                card.SetInscription(1, Time.unscaledTime);
                card.transform.localScale = restScales[current];
                skipCurrent = false;
                yield return null;
            }
            if (completed != null) yield return new WaitForSecondsRealtime(.45f);
            var callback = completed;
            Finish();
            callback?.Invoke();
        }

        private void PlaySound(int tier)
        {
            var cue = Resources.Load<SfxCue>("GachaAudio/" + (tier >= 4 ? "Mythic" : tier >= 3 ? "Legendary" : "Reveal"));
            if (cue != null) soundPool.Play(cue, Vector3.zero);
        }

        public void Finish()
        {
            StopAllCoroutines();
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null) { items[i].SetInscription(1, 0); items[i].transform.localScale = restScales[i]; }
            items.Clear(); restScales.Clear();
            if (heading != null) heading.text = finalHeading;
            if (buttonLabel != null) buttonLabel.text = finalButton;
            heading = null; buttonLabel = null;
            if (tapSurface != null) { tapSurface.onClick.RemoveListener(SkipCurrent); tapSurface.gameObject.SetActive(false); }
            if (massSeal != null) massSeal.gameObject.SetActive(false);
            completed = null; massReveal = false;
            IsPlaying = false; skipCurrent = false;
        }
        private void OnDisable() { Finish(); soundPool?.StopAll(); }
    }
}
