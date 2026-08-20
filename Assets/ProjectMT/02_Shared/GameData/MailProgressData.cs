using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Shared.GameData
{
    public enum MailCategory
    {
        System = 0,
        Event = 1,
        Combat = 2
    }

    [Serializable]
    public sealed class MailEntryData // 제목·본문·복수 첨부를 함께 보관
    {
        [SerializeField] private string mailId;
        [SerializeField] private string title;
        [SerializeField] private string body;
        [SerializeField] private MailCategory category;
        [SerializeField] private string sentAtUtc;
        [SerializeField] private string expiresAtUtc;
        [SerializeField] private List<ItemAmount> attachments = new List<ItemAmount>();

        public string MailId => mailId?.Trim() ?? string.Empty;
        public string Title => title?.Trim() ?? string.Empty;
        public string Body => body?.Trim() ?? string.Empty;
        public MailCategory Category => Enum.IsDefined(typeof(MailCategory), category) ? category : MailCategory.System;
        public string SentAtUtc => sentAtUtc?.Trim() ?? string.Empty;
        public string ExpiresAtUtc => expiresAtUtc?.Trim() ?? string.Empty;
        public IReadOnlyList<ItemAmount> Attachments => attachments ??= new List<ItemAmount>();

        public static MailEntryData Create(
            string id,
            string mailTitle,
            string mailBody,
            MailCategory mailCategory,
            DateTime sentUtc,
            DateTime expiresUtc,
            IEnumerable<ItemAmount> itemAttachments)
        {
            return new MailEntryData
            {
                mailId = id?.Trim(),
                title = mailTitle?.Trim(),
                body = mailBody?.Trim(),
                category = mailCategory,
                sentAtUtc = NormalizeUtc(sentUtc).ToString("O", CultureInfo.InvariantCulture),
                expiresAtUtc = NormalizeUtc(expiresUtc).ToString("O", CultureInfo.InvariantCulture),
                attachments = itemAttachments == null
                    ? new List<ItemAmount>()
                    : new List<ItemAmount>(itemAttachments)
            };
        }

        public MailEntryData Clone()
        {
            return new MailEntryData
            {
                mailId = MailId,
                title = Title,
                body = Body,
                category = Category,
                sentAtUtc = SentAtUtc,
                expiresAtUtc = ExpiresAtUtc,
                attachments = new List<ItemAmount>(Attachments)
            };
        }

        public bool TryGetSentUtc(out DateTime value)
        {
            return TryParseUtc(SentAtUtc, out value);
        }

        public bool TryGetExpiresUtc(out DateTime value)
        {
            return TryParseUtc(ExpiresAtUtc, out value);
        }

        public bool IsExpired(DateTime utcNow)
        {
            return !TryGetExpiresUtc(out var expires) || expires <= NormalizeUtc(utcNow);
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(MailId) || string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Body))
            {
                error = "Mail id, title, and body are required.";
                return false;
            }

            if (!TryGetSentUtc(out var sent) || !TryGetExpiresUtc(out var expires) || expires <= sent ||
                expires - sent > TimeSpan.FromDays(30))
            {
                error = "Mail timestamps must define a valid period up to 30 days.";
                return false;
            }

            if (Attachments.Count is < 1 or > 8)
            {
                error = "Mail must contain between 1 and 8 attachments.";
                return false;
            }

            for (var index = 0; index < Attachments.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(Attachments[index].ItemId) || Attachments[index].Amount <= 0L)
                {
                    error = "Mail attachments must use a valid item id and positive amount.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryParseUtc(string value, out DateTime utc)
        {
            if (DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                utc = NormalizeUtc(parsed);
                return true;
            }

            utc = default;
            return false;
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }
    }

    [Serializable]
    public sealed class MailProgressData // 미수령 우편만 저장
    {
        public const int MaximumStoredMail = 100;

        [SerializeField] private List<MailEntryData> entries = new List<MailEntryData>();

        public IReadOnlyList<MailEntryData> Entries => entries ??= new List<MailEntryData>();
        public int Count => Entries.Count;

        public static MailProgressData CreateDefault()
        {
            return new MailProgressData();
        }

        public MailProgressData Clone()
        {
            var clone = new MailProgressData();
            for (var index = 0; index < Entries.Count; index++)
            {
                if (Entries[index] != null)
                {
                    clone.entries.Add(Entries[index].Clone());
                }
            }

            return clone;
        }

        internal bool TryAdd(MailEntryData mail)
        {
            if (mail == null || !mail.TryValidate(out _) || Count >= MaximumStoredMail ||
                Entries.Any(entry => string.Equals(entry?.MailId, mail.MailId, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            entries.Add(mail.Clone());
            SortNewestFirst();
            return true;
        }

        internal bool HasExpired(DateTime utcNow)
        {
            return Entries.Any(entry => entry == null || entry.IsExpired(utcNow));
        }

        internal int RemoveExpired(DateTime utcNow)
        {
            return entries.RemoveAll(entry => entry == null || entry.IsExpired(utcNow));
        }

        internal bool TryCreateClaim(
            IReadOnlyList<string> mailIds,
            DateTime utcNow,
            out RewardBundle rewards,
            out string[] normalizedIds)
        {
            rewards = RewardBundle.Empty;
            normalizedIds = Array.Empty<string>();
            if (mailIds == null || mailIds.Count == 0)
            {
                return false;
            }

            var ids = mailIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (ids.Length == 0 || ids.Length != mailIds.Count)
            {
                return false;
            }

            var merged = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            for (var idIndex = 0; idIndex < ids.Length; idIndex++)
            {
                var entry = Entries.FirstOrDefault(candidate =>
                    string.Equals(candidate?.MailId, ids[idIndex], StringComparison.OrdinalIgnoreCase));
                if (entry == null || entry.IsExpired(utcNow))
                {
                    return false;
                }

                for (var attachmentIndex = 0; attachmentIndex < entry.Attachments.Count; attachmentIndex++)
                {
                    var attachment = entry.Attachments[attachmentIndex];
                    if (string.IsNullOrWhiteSpace(attachment.ItemId) || attachment.Amount <= 0L)
                    {
                        return false;
                    }

                    var itemId = attachment.ItemId.Trim();
                    merged.TryGetValue(itemId, out var current);
                    try
                    {
                        merged[itemId] = checked(current + attachment.Amount);
                    }
                    catch (OverflowException)
                    {
                        return false;
                    }
                }
            }

            normalizedIds = ids;
            rewards = RewardBundle.FromItems(merged.Select(pair => new ItemAmount(pair.Key, pair.Value)).ToArray());
            return !rewards.IsEmpty;
        }

        internal bool TryRemoveClaimed(IReadOnlyList<string> mailIds)
        {
            if (mailIds == null || mailIds.Count == 0 ||
                mailIds.Any(id => !Entries.Any(entry =>
                    string.Equals(entry?.MailId, id, StringComparison.OrdinalIgnoreCase))))
            {
                return false;
            }

            var idSet = new HashSet<string>(mailIds, StringComparer.OrdinalIgnoreCase);
            entries.RemoveAll(entry => entry != null && idSet.Contains(entry.MailId));
            return true;
        }

        internal void Repair()
        {
            entries ??= new List<MailEntryData>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            entries = entries
                .Where(entry => entry != null && entry.TryValidate(out _) && seen.Add(entry.MailId))
                .Take(MaximumStoredMail)
                .Select(entry => entry.Clone())
                .ToList();
            SortNewestFirst();
        }

        public MailProgressView CreateView()
        {
            return new MailProgressView(this);
        }

        private void SortNewestFirst()
        {
            entries.Sort((left, right) => string.CompareOrdinal(right?.SentAtUtc, left?.SentAtUtc));
        }
    }

    public readonly struct MailEntryView
    {
        internal MailEntryView(MailEntryData data)
        {
            MailId = data?.MailId ?? string.Empty;
            Title = data?.Title ?? string.Empty;
            Body = data?.Body ?? string.Empty;
            Category = data?.Category ?? MailCategory.System;
            SentAtUtc = data?.SentAtUtc ?? string.Empty;
            ExpiresAtUtc = data?.ExpiresAtUtc ?? string.Empty;
            Attachments = data?.Attachments?.ToArray() ?? Array.Empty<ItemAmount>();
        }

        public string MailId { get; }
        public string Title { get; }
        public string Body { get; }
        public MailCategory Category { get; }
        public string SentAtUtc { get; }
        public string ExpiresAtUtc { get; }
        public IReadOnlyList<ItemAmount> Attachments { get; }

        public bool IsExpired(DateTime utcNow)
        {
            return !DateTime.TryParse(
                       ExpiresAtUtc,
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.RoundtripKind,
                       out var expires) ||
                   (expires.Kind == DateTimeKind.Utc ? expires : expires.ToUniversalTime()) <=
                   (utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime());
        }
    }

    public readonly struct MailProgressView
    {
        private readonly MailEntryView[] entries;

        internal MailProgressView(MailProgressData data)
        {
            entries = data?.Entries?.Select(entry => new MailEntryView(entry)).ToArray() ?? Array.Empty<MailEntryView>();
        }

        public IReadOnlyList<MailEntryView> Entries => entries ?? Array.Empty<MailEntryView>();
        public int Count => entries?.Length ?? 0;
        public bool HasExpired(DateTime utcNow) => Entries.Any(entry => entry.IsExpired(utcNow));
    }
}
