using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MoreMountains.Feedbacks;
using ProjectMT.Shared.Unit;
using ProjectMT.Shared.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class GachaRevealSequence : MonoBehaviour // 저장된 결과의 표시만 담당한다.
    {
        private readonly List<CanvasGroup> groups = new List<CanvasGroup>();
        private readonly List<MMF_Player> players = new List<MMF_Player>();
        private readonly List<Vector3> scales = new List<Vector3>();
        private readonly List<Quaternion> rotations = new List<Quaternion>();
        private readonly List<GachaResultItemView> resultViews = new List<GachaResultItemView>();
        private TaskCompletionSource<bool> completion;
        private TMP_Text title;
        private TMP_Text closeLabel;
        private string finalTitle;
        private string originalCloseLabel;
        [SerializeField] private GameObject seal;
        private Color originalTitleColor;
        private MonsterRarity highestRarity;
        [SerializeField] private GameObject tapSurface;
        [SerializeField] private SfxPool soundPool;
        private bool sealPoseCached;
        private Vector3 sealRestPosition;
        private Vector2 sealRestSize;
        private Vector3 sealRestScale;
        private Quaternion sealRestRotation;
        private int sealRestSibling;
        private Color sealRestColor;
        private int currentCard = -1;
        private float lastTapTime = -1f;
        public int CurrentCardIndex => currentCard;
        private static readonly Color LegendaryColor = new Color(1f, 0.76f, 0.25f);
        private static readonly Color MythicColor = new Color(1f, 0.22f, 0.38f);
        public bool IsPlaying => completion != null;

        public Task Play(IReadOnlyList<GachaResultItemView> items, RectTransform root, TMP_Text heading, Button closeButton, bool isTenPull = false)
        {
            CacheAuthoredSealPose();
            Finish();
            if (!isActiveAndEnabled || items.Count == 0) return Task.CompletedTask;
            title = heading;
            originalTitleColor = title != null ? title.color : Color.white;
            highestRarity = MonsterRarity.Common;
            for (var i = 0; i < items.Count; i++)
                if (items[i].Rarity > highestRarity) highestRarity = items[i].Rarity;
            finalTitle = title != null ? title.text : string.Empty;
            closeLabel = closeButton != null ? closeButton.GetComponentInChildren<TMP_Text>(true) : null;
            originalCloseLabel = closeLabel != null ? closeLabel.text : string.Empty;
            if (closeLabel != null) closeLabel.text = "한 장 공개";
            try
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var group = items[i].GetComponent<CanvasGroup>();
                    if (group == null) throw new System.InvalidOperationException("The result card needs an authored CanvasGroup.");
                    groups.Add(group);
                    scales.Add(items[i].transform.localScale);
                    rotations.Add(items[i].transform.localRotation);
                    resultViews.Add(items[i]);
                    items[i].PrepareBack(Resources.Load<Sprite>("UI/GachaCardBack"), isTenPull);
                    group.alpha = 0f;
                    var player = items[i].GetComponent<MMF_Player>();
                    if (player == null) throw new System.InvalidOperationException("The result card needs its authored reveal feedback.");
                    players.Add(player);
                    player.Initialization();
                }
                if (seal == null || tapSurface == null || soundPool == null || seal.transform.parent != root)
                    throw new System.InvalidOperationException("The reveal overlay needs its authored seal, input surface and sound pool.");
                seal.transform.SetSiblingIndex(sealRestSibling);
                seal.SetActive(true);
                currentCard = -1;
                lastTapTime = Time.unscaledTime; // 소환 버튼을 누른 입력은 결과에 재사용하지 않는다.
                var tapButton = tapSurface.GetComponent<Button>();
                tapButton.onClick.RemoveListener(SkipCurrentCard);
                tapButton.onClick.AddListener(SkipCurrentCard);
                tapSurface.SetActive(true);
                completion = new TaskCompletionSource<bool>();
                var task = completion.Task;
                StartCoroutine(Reveal(items));
                return task;
            }
            catch
            {
                Finish();
                throw;
            }
        }

        private IEnumerator Reveal(IReadOnlyList<GachaResultItemView> items)
        {
            var mythic = highestRarity == MonsterRarity.Mythic;
            var special = highestRarity >= MonsterRarity.Legendary;
            var tint = mythic ? MythicColor : special ? LegendaryColor : new Color(0.48f, 0.85f, 1f);
            var chargeDuration = mythic ? 1.15f : special ? 0.9f : 0.85f;
            PlaySound("Charge");
            var graphic = seal.GetComponent<GachaSummonSealGraphic>();
            graphic.IsMythic = mythic;
            graphic.color = tint;
            if (title != null)
            {
                title.text = mythic ? "신화의 기운이 깨어납니다…" : special ? "전설의 기운이 모입니다…" : "소환의 문을 여는 중…";
                title.color = special ? tint : originalTitleColor;
            }
            var started = Time.unscaledTime;
            while (Time.unscaledTime - started < chargeDuration)
            {
                var t = (Time.unscaledTime - started) / chargeDuration;
                var size = 0.75f + 0.3f * Mathf.SmoothStep(0f, 1f, t) + Mathf.Sin(t * Mathf.PI * 4f) * 0.055f;
                if (mythic && t > 0.68f) size = Mathf.Lerp(1.02f, 0.57f, (t - 0.68f) / 0.32f);
                seal.transform.localRotation = Quaternion.Euler(0f, 0f, (mythic ? 260f : -150f) * t * t);
                seal.transform.localScale = Vector3.one * size;
                graphic.color = Color.Lerp(tint, Color.white, special ? t * 0.45f : 0f);
                yield return null;
            }
            seal.SetActive(false);
            seal.transform.SetAsFirstSibling(); // 결과 카드 뒤에서만 빛을 펼친다.
            if (title != null) { title.text = "소환 카드가 도착했습니다…"; title.color = originalTitleColor; }
            for (var i = 0; i < groups.Count; i++)
            {
                groups[i].alpha = 1f;
                players[i].PlayFeedbacks();
                PlaySound("Enter");
                yield return new WaitForSecondsRealtime(0.12f);
            }
            yield return new WaitForSecondsRealtime(0.7f);
            yield return RevealCards(0);
        }

        private IEnumerator RevealCards(int first)
        {
            var items = resultViews;
            var graphic = seal.GetComponent<GachaSummonSealGraphic>();
            float started;
            for (var i = first; i < groups.Count; i++)
            {
                currentCard = i;
                var rarity = items[i].Rarity;
                var isMythic = rarity == MonsterRarity.Mythic;
                var isSpecial = rarity >= MonsterRarity.Legendary;
                yield return FlipCard(i, items[i], isMythic ? 2f : isSpecial ? 1f : 0f);
                if (title != null)
                {
                    title.text = isMythic ? "신화 몬스터 등장!" : isSpecial ? "전설 몬스터 등장!" : $"몬스터 소환 · {i + 1} / {groups.Count}장 공개";
                    title.color = isMythic ? MythicColor : isSpecial ? LegendaryColor : originalTitleColor;
                }
                if (isSpecial)
                {
                    var burstColor = isMythic ? MythicColor : LegendaryColor;
                    var rect = (RectTransform)seal.transform;
                    rect.sizeDelta = Vector2.one * (isMythic ? 400f : 340f);
                    rect.position = items[i].transform.position;
                    rect.localRotation = Quaternion.identity;
                    graphic.IsBurst = true;
                    graphic.IsMythic = isMythic;
                    seal.SetActive(true);
                    var duration = isMythic ? 0.65f : 0.45f;
                    started = Time.unscaledTime;
                    while (Time.unscaledTime - started < duration)
                    {
                        var t = (Time.unscaledTime - started) / duration;
                        ApplyRevealBounce(i, t, isMythic ? 0.3f : 0.22f);
                        var expansion = 1f - Mathf.Pow(1f - t, 3f);
                        rect.localScale = Vector3.one * Mathf.Lerp(0.6f, isMythic ? 1.3f : 1.1f, expansion);
                        rect.localRotation = Quaternion.Euler(0f, 0f, isMythic ? 24f * t : -10f * t);
                        graphic.Pulse = expansion;
                        var c = Color.Lerp(Color.white, burstColor, Mathf.Min(1f, t * 4f));
                        c.a = (1f - t) * 0.85f;
                        graphic.color = c;
                        yield return null;
                    }
                    seal.SetActive(false);
                }
                else
                {
                    started = Time.unscaledTime;
                    while (Time.unscaledTime - started < 0.12f)
                    {
                        ApplyRevealBounce(i, (Time.unscaledTime - started) / 0.12f, 0.14f);
                        yield return null;
                    }
                }
                items[i].transform.localScale = scales[i];
                items[i].transform.localRotation = rotations[i];
            }
            yield return new WaitForSecondsRealtime(0.4f);
            Finish();
        }


        private IEnumerator FlipCard(int index, GachaResultItemView item, float strength)
        {
            var target = item.transform;
            var original = rotations[index];
            var special = strength > 0f;
            var mythic = strength > 1f;
            var duration = mythic ? 1.2f : special ? 0.95f : 0.24f;
            PlaySound("Flip");
            var graphic = seal.GetComponent<GachaSummonSealGraphic>();
            var halo = (RectTransform)seal.transform;
            if (special)
            {
                halo.position = target.position;
                halo.sizeDelta = Vector2.one * (mythic ? 390f : 330f);
                graphic.IsBurst = true;
                graphic.IsMythic = mythic;
                seal.SetActive(true);
            }
            var start = Time.unscaledTime;
            var shown = false;
            while (Time.unscaledTime - start < duration)
            {
                var t = (Time.unscaledTime - start) / duration;
                float angle;
                float tilt;
                float size;
                if (special)
                {
                    var spin = Mathf.Clamp01((t - 0.18f) / 0.82f);
                    var eased = Mathf.SmoothStep(0f, 1f, spin);
                    var fullAngle = (mythic ? 1260f : 900f) * eased;
                    var finalAngle = mythic ? 1260f : 900f;
                    if (!shown && fullAngle >= finalAngle - 90f) { RevealFront(item); shown = true; }
                    angle = shown ? fullAngle - finalAngle : fullAngle;
                    var vibration = Mathf.Sin(t * Mathf.PI * (mythic ? 24f : 18f));
                    tilt = vibration * (t < 0.18f ? 9f : 13f * (1f - spin));
                    size = t < 0.18f
                        ? 1f - 0.13f * Mathf.Sin(t / 0.18f * Mathf.PI)
                        : 1f + Mathf.Sin(spin * Mathf.PI) * (mythic ? 0.3f : 0.22f);
                    halo.localRotation = Quaternion.Euler(0f, 0f, -fullAngle * 0.35f);
                    halo.localScale = Vector3.one * (0.85f + 0.14f * Mathf.Sin(t * Mathf.PI * 8f));
                    graphic.Pulse = 0.4f + 0.22f * Mathf.Sin(t * Mathf.PI * 6f);
                    var glow = Color.Lerp(mythic ? MythicColor : LegendaryColor, Color.white, Mathf.Pow(Mathf.Abs(vibration), 4f) * 0.8f);
                    glow.a = 0.35f + 0.35f * Mathf.Abs(vibration);
                    graphic.color = glow;
                }
                else
                {
                    if (t < 0.22f)
                        angle = -18f * Mathf.Sin(t / 0.22f * Mathf.PI * 0.5f);
                    else if (t < 0.6f)
                        angle = Mathf.Lerp(-18f, 90f, Mathf.Pow((t - 0.22f) / 0.38f, 2f));
                    else
                    {
                        if (!shown) { RevealFront(item); shown = true; }
                        angle = Mathf.Lerp(-90f, 0f, 1f - Mathf.Pow(1f - (t - 0.6f) / 0.4f, 3f));
                    }
                    tilt = Mathf.Sin(t * Mathf.PI * 2f) * 4f;
                    size = 1f + Mathf.Sin(t * Mathf.PI) * 0.09f;
                }
                target.localRotation = original * Quaternion.Euler(0f, angle, tilt);
                target.localScale = scales[index] * size;
                yield return null;
            }
            if (!shown) RevealFront(item);
            target.localRotation = original;
            target.localScale = scales[index];
            seal.SetActive(false);
        }

        public void SkipCurrentCard()
        {
            if (!IsPlaying || Time.unscaledTime - lastTapTime < 0.08f) return;
            lastTapTime = Time.unscaledTime;
            StopAllCoroutines();
            soundPool?.StopAll();
            var index = Mathf.Max(0, currentCard);
            for (var i = 0; i < groups.Count; i++)
            {
                players[i].StopFeedbacks();
                groups[i].alpha = 1f;
                groups[i].transform.localScale = scales[i];
                groups[i].transform.localRotation = rotations[i];
            }
            if (resultViews[index].IsBackVisible) RevealFront(resultViews[index]);
            seal.SetActive(false);
            if (title != null) { title.text = $"몬스터 소환 · {index + 1} / {groups.Count}장 공개"; title.color = originalTitleColor; }
            currentCard = index + 1;
            if (currentCard >= groups.Count) { Finish(); return; }
            StartCoroutine(RevealCards(currentCard));
        }

        private void RevealFront(GachaResultItemView item)
        {
            item.ShowFront();
            PlaySound(item.Rarity == MonsterRarity.Mythic ? "Mythic" : item.Rarity == MonsterRarity.Legendary ? "Legendary" : "Reveal");
        }

        private void PlaySound(string cueName)
        {
            var id = "EVENT-Gacha" + cueName;
            if (SfxEvents.TryResolve(id, out _)) SfxEvents.Play2D(id);
            else if (soundPool != null) soundPool.Play(Resources.Load<SfxCue>("GachaAudio/" + cueName), Vector3.zero);
        }

        private void ApplyRevealBounce(int index, float t, float strength)
        {
            var bounce = Mathf.Sin(t * Mathf.PI * 4f) * Mathf.Exp(-5f * t) * strength;
            groups[index].transform.localScale = Vector3.Scale(scales[index], new Vector3(1f + bounce, 1f - bounce * 0.6f, 1f));
            groups[index].transform.localRotation = rotations[index] * Quaternion.Euler(0f, 0f, -bounce * 20f);
        }

        public void Finish()
        {
            StopAllCoroutines();
            currentCard = -1;
            if (tapSurface != null)
            {
                tapSurface.SetActive(false);
                tapSurface.GetComponent<Button>().onClick.RemoveListener(SkipCurrentCard);
            }
            for (var i = 0; i < groups.Count; i++)
            {
                if (i < players.Count && players[i] != null)
                {
                    players[i].StopFeedbacks();
                }
                if (groups[i] == null) continue;
                groups[i].alpha = 1f;
                groups[i].transform.localScale = scales[i];
                groups[i].transform.localRotation = rotations[i];
                if (resultViews[i] != null) resultViews[i].RestoreFront();
            }
            groups.Clear(); players.Clear(); scales.Clear(); rotations.Clear(); resultViews.Clear();
            RestoreAuthoredSealPose();
            if (title != null) { title.text = finalTitle; title.color = originalTitleColor; }
            if (closeLabel != null) closeLabel.text = originalCloseLabel;
            title = null; closeLabel = null;
            var pending = completion;
            completion = null;
            pending?.TrySetResult(true);
        }
        private void CacheAuthoredSealPose()
        {
            if (sealPoseCached || seal == null) return;
            var rect = (RectTransform)seal.transform;
            sealRestPosition = rect.anchoredPosition3D;
            sealRestSize = rect.sizeDelta;
            sealRestScale = rect.localScale;
            sealRestRotation = rect.localRotation;
            sealRestSibling = rect.GetSiblingIndex();
            sealRestColor = seal.GetComponent<GachaSummonSealGraphic>().color;
            sealPoseCached = true;
        }

        private void RestoreAuthoredSealPose()
        {
            if (seal == null) return;
            seal.SetActive(false);
            if (!sealPoseCached) return;
            var rect = (RectTransform)seal.transform;
            rect.anchoredPosition3D = sealRestPosition;
            rect.sizeDelta = sealRestSize;
            rect.localScale = sealRestScale;
            rect.localRotation = sealRestRotation;
            var graphic = seal.GetComponent<GachaSummonSealGraphic>();
            graphic.color = sealRestColor;
            graphic.IsBurst = false;
            graphic.IsMythic = false;
            graphic.Pulse = 0f;
        }

        private void OnDisable() { Finish(); soundPool?.StopAll(); }
    }
}
