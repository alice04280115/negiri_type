using System;
using System.Collections.Generic;
using Negiri.Core.Shopping;
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
        /// <summary>使ったときの効果。段階2では店内に表示するだけで、在庫には反映しない。</summary>
        public ItemEffect[] effects = Array.Empty<ItemEffect>();
    }

    [Serializable]
    public class ItemEffect
    {
        /// <summary>"food"（食料・日）、"daily"（日用品・日）、"satisfaction"（満足度）。</summary>
        public string stat;
        public int amount;

        public static readonly string[] Stats = { "food", "daily", "satisfaction" };

        /// <summary>画面に出す文字（例：食料+2日）。</summary>
        public string Label()
        {
            switch (stat)
            {
                case "food": return $"食料+{amount}日";
                case "daily": return $"日用品+{amount}日";
                case "satisfaction": return $"満足度+{amount}";
                default: return $"{stat}+{amount}";
            }
        }
    }

    [Serializable]
    public class SaleData
    {
        public string itemId;
        /// <summary>特売の割引（ベーシスポイント。1000 = 10%）。</summary>
        public int discountBp;
    }

    [Serializable]
    public class StoreData
    {
        public string id;
        public string name;
        public SaleData[] sales = Array.Empty<SaleData>();

        /// <summary>その商品の特売割引。特売でなければ 0。</summary>
        public int SaleBp(string itemId)
        {
            foreach (var s in sales ?? Array.Empty<SaleData>())
            {
                if (s.itemId == itemId) return s.discountBp;
            }
            return 0;
        }
    }

    [Serializable]
    public class StoreList
    {
        public StoreData[] stores;
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
        public ShoppingBalance shopping = new ShoppingBalance();
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
                foreach (var e in item.effects ?? Array.Empty<ItemEffect>())
                {
                    if (Array.IndexOf(ItemEffect.Stats, e.stat) < 0) errors.Add($"効果の種類が不明です: {item.id} → {e.stat}");
                    if (e.amount <= 0) errors.Add($"効果の量は1以上にしてください: {item.id} → {e.stat}");
                }
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

        /// <summary>店舗データ（特売）のチェック。</summary>
        public static List<string> ValidateStores(StoreList stores, ItemList items)
        {
            var errors = new List<string>();
            var itemIds = new HashSet<string>();
            foreach (var item in items?.items ?? Array.Empty<ItemData>()) itemIds.Add(item.id);

            var list = stores?.stores ?? Array.Empty<StoreData>();
            if (list.Length == 0) errors.Add("店舗が1つもありません");
            var storeIds = new HashSet<string>();
            foreach (var store in list)
            {
                if (string.IsNullOrEmpty(store.id)) { errors.Add($"店舗IDが空です: {store.name}"); continue; }
                if (!storeIds.Add(store.id)) errors.Add($"店舗IDが重複しています: {store.id}");
                var saleIds = new HashSet<string>();
                foreach (var sale in store.sales ?? Array.Empty<SaleData>())
                {
                    if (!itemIds.Contains(sale.itemId)) errors.Add($"特売に存在しない商品IDがあります: {store.id} → {sale.itemId}");
                    if (!saleIds.Add(sale.itemId)) errors.Add($"特売の商品が重複しています: {store.id} → {sale.itemId}");
                    if (sale.discountBp <= 0 || sale.discountBp >= 10000) errors.Add($"特売の割引は1〜9999にしてください: {store.id} → {sale.itemId}");
                }
            }
            return errors;
        }

        /// <summary>買い物の数値（財布・品数の上限・不足時の扱い）のチェック。</summary>
        public static List<string> ValidateShopping(ShoppingBalance shopping, int difficultyCount)
        {
            var errors = new List<string>();
            if (shopping == null) { errors.Add("shopping の設定がありません"); return errors; }
            if (shopping.startingCash <= 0) errors.Add("startingCash は1以上にしてください");
            var list = shopping.difficulties ?? Array.Empty<ShoppingRule>();
            if (list.Length != difficultyCount) errors.Add($"shopping.difficulties は難易度の数（{difficultyCount}）だけ並べてください");
            foreach (var d in list)
            {
                if (d.maxItems <= 0) errors.Add($"maxItems は1以上にしてください: {d.label}");
                if (d.satisfactionPerReturn < 0) errors.Add($"satisfactionPerReturn は0以上にしてください: {d.label}");
                if (!Enum.TryParse<ShortageRule>(d.shortage, true, out _)) errors.Add($"shortage が不明です: {d.label} → {d.shortage}");
            }
            return errors;
        }
    }

    /// <summary>商品に合う文章（打つ商品名）を難易度の level から選ぶ。</summary>
    public static class SentenceCatalog
    {
        /// <summary>
        /// itemId の文章のうち level が同じものから選ぶ。なければ level の近いもの（同じ差なら低い方）から選ぶ。
        /// pick は「候補の数を受け取り、0〜数−1 を返す」関数（画面ではランダム、テストでは固定）。
        /// </summary>
        public static SentenceData Pick(SentenceList sentences, string itemId, int level, Func<int, int> pick)
        {
            var candidates = new List<SentenceData>();
            int bestDistance = int.MaxValue;
            int bestLevel = int.MaxValue;
            foreach (var s in sentences?.sentences ?? Array.Empty<SentenceData>())
            {
                if (Array.IndexOf(s.itemIds ?? Array.Empty<string>(), itemId) < 0) continue;
                int distance = Math.Abs(s.level - level);
                bool better = distance < bestDistance || (distance == bestDistance && s.level < bestLevel);
                if (better)
                {
                    candidates.Clear();
                    bestDistance = distance;
                    bestLevel = s.level;
                }
                if (distance == bestDistance && s.level == bestLevel) candidates.Add(s);
            }
            if (candidates.Count == 0) return null;
            int index = pick(candidates.Count);
            if (index < 0 || index >= candidates.Count) index = 0;
            return candidates[index];
        }
    }
}
