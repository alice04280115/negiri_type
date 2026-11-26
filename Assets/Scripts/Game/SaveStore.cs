using System;
using Negiri.Core.Save;
using UnityEngine;

namespace Negiri.Game
{
    /// <summary>
    /// セーブの読み書き。JSON にして PlayerPrefs に1つだけ保存する（WebGL ではブラウザの IndexedDB に残る）。
    /// unityroom では同じドメインに他のゲームもあるので、キー名に negiri_type を付けてぶつからないようにする。
    /// </summary>
    public static class SaveStore
    {
        const string Key = "negiri_type.save";

        public static bool Exists => PlayerPrefs.HasKey(Key);

        /// <summary>読めなければ null（壊れたセーブはエラーを出して無視する）。</summary>
        public static SaveData Load()
        {
            if (!Exists) return null;
            try
            {
                return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
            }
            catch (Exception e)
            {
                Debug.LogError("[セーブ] 読み込めませんでした: " + e.Message);
                return null;
            }
        }

        public static void Write(SaveData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            // WebGL ではすぐ書き出さないと、ブラウザを閉じたときに消えることがある
            PlayerPrefs.Save();
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
