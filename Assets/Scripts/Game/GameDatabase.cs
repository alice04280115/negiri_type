using System.Collections.Generic;
using Negiri.Core.Data;
using UnityEngine;

namespace Negiri.Game
{
    /// <summary>Resources/Data の JSON をまとめて読み、不備をチェックする。</summary>
    public sealed class GameDatabase
    {
        public ItemList Items { get; }
        public SentenceList Sentences { get; }
        public BalanceData Balance { get; }
        public StoreList Stores { get; }
        /// <summary>段階3でも店舗は1つだけ。データがなければ特売なしの仮の店舗。</summary>
        public StoreData Store => Stores?.stores != null && Stores.stores.Length > 0 ? Stores.stores[0] : new StoreData { id = "none", name = "お店" };
        public IReadOnlyDictionary<string, ItemData> ItemById => itemById;
        /// <summary>データの不備（画面の左下に出す）。なければ空文字。</summary>
        public string Errors { get; }

        readonly Dictionary<string, ItemData> itemById = new Dictionary<string, ItemData>();

        public GameDatabase()
        {
            Items = Load<ItemList>("Data/items");
            Sentences = Load<SentenceList>("Data/sentences");
            Balance = Load<BalanceData>("Data/balance");
            Stores = Load<StoreList>("Data/stores");
            foreach (var i in Items.items) itemById[i.id] = i;

            var errors = new List<string>();
            errors.AddRange(DataValidator.Validate(Items, Sentences));
            errors.AddRange(DataValidator.ValidateStores(Stores, Items));
            errors.AddRange(DataValidator.ValidateShopping(Balance.shopping, Difficulties.All.Length));
            errors.AddRange(DataValidator.ValidateLife(Balance.life, Balance.shopping));
            foreach (var e in errors) Debug.LogError("[データ] " + e);
            Errors = string.Join("\n", errors);
        }

        static T Load<T>(string path)
        {
            return JsonUtility.FromJson<T>(Resources.Load<TextAsset>(path).text);
        }

        /// <summary>商品ごとに、出した文章のID。全部出し切るまで同じ文章は出さない（出し切ったら空にしてもう一周）。</summary>
        readonly Dictionary<string, List<string>> usedByItem = new Dictionary<string, List<string>>();

        /// <summary>商品ごとに、前に出した商品名の食材名（同じ食材名が続かないようにする）。</summary>
        readonly Dictionary<string, string> lastFoodByItem = new Dictionary<string, string>();

        /// <summary>その商品の、難易度に合う文章（打つ商品名）を選ぶ。候補を一通り出し切るまで同じ文章は出さない。</summary>
        public SentenceData PickSentence(string itemId, int level)
        {
            var key = itemId + "#" + level;
            if (!usedByItem.TryGetValue(key, out var used)) usedByItem[key] = used = new List<string>();
            var foods = ItemById.TryGetValue(itemId, out var item) ? item.foods : null;
            lastFoodByItem.TryGetValue(itemId, out var lastFood);
            var s = SentenceCatalog.Pick(Sentences, itemId, level, n => Random.Range(0, n), used, foods, lastFood);
            if (s == null) return null;
            lastFoodByItem[itemId] = SentenceCatalog.FoodKey(s.text, foods);
            // 出し切っていたら（選ばれたものがもう使用済みなら）一周したので、記録をやり直す
            if (used.Contains(s.id)) used.Clear();
            used.Add(s.id);
            return s;
        }
    }
}
