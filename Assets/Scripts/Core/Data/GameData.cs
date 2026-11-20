using System;
using System.Collections.Generic;
using Negiri.Core.Typing;

namespace Negiri.Core.Data
{
    // JSON の形をそのまま表すクラス。Unity の JsonUtility で読めるよう、public フィールドと配列で持つ。

    [Serializable]
    public class ItemData
    {
        public string id;
        public string name;
        public string category;
        /// <summary>"essential"（必須）または "free"（自由）。</summary>
        public string kind;
        public int basePrice;
        /// <summary>割引上限（ベーシスポイント）。省略時は 5000 = 50%。</summary>
        public int maxDiscountBp = 5000;
        public int typingLevel = 1;
    }

    [Serializable]
    public class SentenceData
    {
        public string id;
        public string[] itemIds;
        public string text;
        public string reading;
        public int level = 1;
    }

    [Serializable]
    public class ItemList
    {
        public ItemData[] items;
    }

    [Serializable]
    public class SentenceList
    {
        public SentenceData[] sentences;
    }

    [Serializable]
    public class BalanceData
    {
        public TypingBalance typing = new TypingBalance();
    }

    /// <summary>データの不備を起動時に見つけるためのチェック。</summary>
    public static class DataValidator
    {
        public static List<string> Validate(ItemList items, SentenceList sentences)
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();

            foreach (var item in items?.items ?? Array.Empty<ItemData>())
            {
                if (string.IsNullOrEmpty(item.id)) { errors.Add($"商品IDが空です: {item.name}"); continue; }
                if (!ids.Add(item.id)) errors.Add($"商品IDが重複しています: {item.id}");
                if (item.basePrice <= 0) errors.Add($"価格が0以下です: {item.id}");
                if (item.kind != "essential" && item.kind != "free") errors.Add($"kind は essential か free にしてください: {item.id}");
            }

            foreach (var s in sentences?.sentences ?? Array.Empty<SentenceData>())
            {
                if (string.IsNullOrEmpty(s.reading)) { errors.Add($"読みがながありません: {s.id}"); continue; }
                try { RomajiMatcher.Tokenize(s.reading); }
                catch (ArgumentException e) { errors.Add($"{s.id}: {e.Message}"); }
                foreach (var itemId in s.itemIds ?? Array.Empty<string>())
                {
                    if (!ids.Contains(itemId)) errors.Add($"存在しない商品IDです: {s.id} → {itemId}");
                }
            }

            foreach (var id in ids)
            {
                bool hasSentence = false;
                foreach (var s in sentences?.sentences ?? Array.Empty<SentenceData>())
                {
                    if (Array.IndexOf(s.itemIds ?? Array.Empty<string>(), id) >= 0) { hasSentence = true; break; }
                }
                if (!hasSentence) errors.Add($"文章が1つもない商品です: {id}");
            }

            return errors;
        }
    }
}
