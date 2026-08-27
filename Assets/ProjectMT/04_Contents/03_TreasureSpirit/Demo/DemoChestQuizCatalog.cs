using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal enum DemoDungeonDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    internal readonly struct DemoChestQuizQuestion
    {
        public readonly string Prompt;
        public readonly string[] Choices;
        public readonly int CorrectIndex;
        public readonly string MemorizeText;

        public DemoChestQuizQuestion(string prompt, string[] choices, int correctIndex)
            : this(prompt, choices, correctIndex, null)
        {
        }

        public DemoChestQuizQuestion(string prompt, string[] choices, int correctIndex, string memorizeText)
        {
            Prompt = prompt;
            Choices = choices;
            CorrectIndex = correctIndex;
            MemorizeText = memorizeText;
        }

        public bool HasMemorizePhase => !string.IsNullOrEmpty(MemorizeText);
    }

    internal static class DemoDungeonDifficultyUtil
    {
        public static DemoDungeonDifficulty Resolve(GameObject mapInstance)
        {
            string mapName = mapInstance != null ? mapInstance.name : string.Empty;
            if (mapName.IndexOf("Hard", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DemoDungeonDifficulty.Hard;
            }

            if (mapName.IndexOf("Easy", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DemoDungeonDifficulty.Easy;
            }

            return DemoDungeonDifficulty.Normal;
        }
    }

    internal static class DemoChestQuizCatalog
    {
        private const int MemoryDigitCount = 5;
        private const int MemoryChoiceCount = 3;

        private static readonly DemoChestQuizQuestion[] EasyQuestions =
        {
            new DemoChestQuizQuestion("8 + 7 = ?", new[] { "14", "15", "16" }, 1),
            new DemoChestQuizQuestion("16 - 9 = ?", new[] { "6", "7", "8" }, 1),
            new DemoChestQuizQuestion("6 × 4 = ?", new[] { "20", "24", "28" }, 1),
            new DemoChestQuizQuestion("18 ÷ 3 = ?", new[] { "5", "6", "7" }, 1),
            new DemoChestQuizQuestion("12 + 9 = ?", new[] { "20", "21", "23" }, 1),
            new DemoChestQuizQuestion("25 - 8 = ?", new[] { "16", "17", "18" }, 1),
            new DemoChestQuizQuestion("7 × 6 = ?", new[] { "36", "42", "48" }, 1),
            new DemoChestQuizQuestion("9 × 5 = ?", new[] { "40", "45", "54" }, 1),
            new DemoChestQuizQuestion("30 + 15 = ?", new[] { "35", "45", "50" }, 1),
            new DemoChestQuizQuestion("100 - 40 = ?", new[] { "50", "60", "70" }, 1)
        };

        private static readonly DemoChestQuizQuestion[] NormalQuestions =
        {
            new DemoChestQuizQuestion("3x + 5 = 20 일 때 x = ?", new[] { "4", "5", "6" }, 1),
            new DemoChestQuizQuestion("(-3) + 8 = ?", new[] { "5", "11", "-5" }, 0),
            new DemoChestQuizQuestion("2의 3제곱은?", new[] { "6", "8", "9" }, 1),
            new DemoChestQuizQuestion("1/2 + 1/4 = ?", new[] { "1/2", "2/4", "3/4" }, 2),
            new DemoChestQuizQuestion("3의 제곱 + 4의 제곱 = ?", new[] { "12", "25", "7" }, 1),
            new DemoChestQuizQuestion("12x = 36 일 때 x = ?", new[] { "2", "3", "4" }, 1),
            new DemoChestQuizQuestion("루트 49 = ?", new[] { "6", "7", "8" }, 1),
            new DemoChestQuizQuestion("2(x + 3) = 14 일 때 x = ?", new[] { "4", "5", "7" }, 0),
            new DemoChestQuizQuestion("0.2 × 45 = ?", new[] { "9", "8", "11" }, 0),
            new DemoChestQuizQuestion("20의 3/5 는?", new[] { "10", "12", "15" }, 1)
        };

        public static List<DemoChestQuizQuestion> CreateRound(DemoDungeonDifficulty difficulty)
        {
            if (difficulty == DemoDungeonDifficulty.Hard)
            {
                return new List<DemoChestQuizQuestion> { ShuffleChoices(CreateMemoryQuestion()) };
            }

            DemoChestQuizQuestion[] pool = difficulty == DemoDungeonDifficulty.Normal
                ? NormalQuestions
                : EasyQuestions;

            int pick = Random.Range(0, pool.Length);
            return new List<DemoChestQuizQuestion> { ShuffleChoices(pool[pick]) };
        }

        private static DemoChestQuizQuestion CreateMemoryQuestion()
        {
            int[] digits = new int[MemoryDigitCount];
            for (int i = 0; i < digits.Length; i++)
            {
                digits[i] = Random.Range(0, 10);
            }

            string memorizeText = JoinDigits(digits, "   ");
            int variant = Random.Range(0, 3);
            if (variant == 0)
            {
                return CreateSequenceQuestion(digits, memorizeText);
            }

            if (variant == 1)
            {
                return CreateNthDigitQuestion(digits, memorizeText);
            }

            return CreateSumQuestion(digits, memorizeText);
        }

        private static DemoChestQuizQuestion CreateSequenceQuestion(int[] digits, string memorizeText)
        {
            string correct = JoinDigits(digits, " ");
            string[] choices = new string[MemoryChoiceCount];
            choices[0] = correct;
            choices[1] = JoinDigits(MutateDigits(digits), " ");
            choices[2] = JoinDigits(SwapDigits(digits), " ");
            EnsureUniqueChoices(choices, 0);
            return new DemoChestQuizQuestion("방금 본 숫자의 순서는?", choices, 0, memorizeText);
        }

        private static DemoChestQuizQuestion CreateNthDigitQuestion(int[] digits, string memorizeText)
        {
            int index = Random.Range(0, digits.Length);
            int correct = digits[index];
            int wrongA = (correct + 1 + Random.Range(0, 8)) % 10;
            int wrongB = (correct + 2 + Random.Range(0, 7)) % 10;
            if (wrongB == wrongA)
            {
                wrongB = (wrongA + 1) % 10;
            }

            return new DemoChestQuizQuestion(
                $"{index + 1}번째 숫자는?",
                new[] { correct.ToString(), wrongA.ToString(), wrongB.ToString() },
                0,
                memorizeText);
        }

        private static DemoChestQuizQuestion CreateSumQuestion(int[] digits, string memorizeText)
        {
            int sum = 0;
            for (int i = 0; i < digits.Length; i++)
            {
                sum += digits[i];
            }

            int wrongA = sum + Random.Range(1, 5);
            int wrongB = Mathf.Max(0, sum - Random.Range(1, 5));
            if (wrongB == wrongA)
            {
                wrongB = sum + 6;
            }

            return new DemoChestQuizQuestion(
                "방금 본 숫자를 모두 더한 값은?",
                new[] { sum.ToString(), wrongA.ToString(), wrongB.ToString() },
                0,
                memorizeText);
        }

        private static int[] MutateDigits(int[] source)
        {
            int[] copy = (int[])source.Clone();
            int index = Random.Range(0, copy.Length);
            copy[index] = (copy[index] + 1 + Random.Range(0, 8)) % 10;
            return copy;
        }

        private static int[] SwapDigits(int[] source)
        {
            int[] copy = (int[])source.Clone();
            int a = Random.Range(0, copy.Length);
            int b = (a + 1 + Random.Range(0, copy.Length - 1)) % copy.Length;
            int temp = copy[a];
            copy[a] = copy[b];
            copy[b] = temp;
            return copy;
        }

        private static void EnsureUniqueChoices(string[] choices, int correctIndex)
        {
            for (int i = 0; i < choices.Length; i++)
            {
                if (i == correctIndex)
                {
                    continue;
                }

                int guard = 0;
                while (choices[i] == choices[correctIndex] && guard < 8)
                {
                    choices[i] = choices[i] + "'";
                    guard++;
                }
            }
        }

        private static string JoinDigits(int[] digits, string separator)
        {
            string[] parts = new string[digits.Length];
            for (int i = 0; i < digits.Length; i++)
            {
                parts[i] = digits[i].ToString();
            }

            return string.Join(separator, parts);
        }

        private static DemoChestQuizQuestion ShuffleChoices(DemoChestQuizQuestion source)
        {
            string[] shuffled = (string[])source.Choices.Clone();
            int correctIndex = source.CorrectIndex;
            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                string choice = shuffled[i];
                shuffled[i] = shuffled[swap];
                shuffled[swap] = choice;
                if (correctIndex == i)
                {
                    correctIndex = swap;
                }
                else if (correctIndex == swap)
                {
                    correctIndex = i;
                }
            }

            return new DemoChestQuizQuestion(source.Prompt, shuffled, correctIndex, source.MemorizeText);
        }
    }
}
