using UnityEngine;
using UnityEngine.UI;

namespace Negiri.Game.Ui
{
    /// <summary>列の位置と揃え方。x は行の左端からの位置。</summary>
    public struct Column
    {
        public float X;
        public float Width;
        public TextAnchor Align;

        public Column(float x, float width, TextAnchor align = TextAnchor.MiddleLeft)
        {
            X = x;
            Width = width;
            Align = align;
        }
    }

    /// <summary>
    /// 表のような一覧（商品棚・カート・返品・レシート）。行ごとに選択中の帯と、列ごとの文字を持つ。
    /// 行の数は最初に決めておき、使わない行は隠す。
    /// </summary>
    public sealed class RowList
    {
        readonly GameObject[] rows;
        readonly Image[] highlights;
        readonly Text[,] cells;

        public int Capacity => rows.Length;

        /// <param name="topLeft">親の左上からの位置（y は下向きにマイナス）。</param>
        public RowList(RectTransform parent, Vector2 topLeft, float width, float rowHeight, int capacity, int fontSize, params Column[] columns)
        {
            rows = new GameObject[capacity];
            highlights = new Image[capacity];
            cells = new Text[capacity, columns.Length];
            for (int r = 0; r < capacity; r++)
            {
                var row = UiFactory.NewRect("Row", parent);
                row.anchorMin = row.anchorMax = row.pivot = new Vector2(0, 1);
                row.anchoredPosition = topLeft + new Vector2(0, -rowHeight * r);
                row.sizeDelta = new Vector2(width, rowHeight);
                highlights[r] = row.gameObject.AddComponent<Image>();
                highlights[r].color = Color.clear;
                for (int c = 0; c < columns.Length; c++)
                {
                    var col = columns[c];
                    var t = UiFactory.MakeText(row, "", fontSize, UiFactory.Ink, new Vector2(0, 0.5f), new Vector2(col.X, 0), new Vector2(col.Width, rowHeight), col.Align);
                    cells[r, c] = t;
                }
                rows[r] = row.gameObject;
            }
        }

        /// <summary>count 行だけ出し、残りは隠す。</summary>
        public void SetCount(int count)
        {
            for (int r = 0; r < rows.Length; r++) rows[r].SetActive(r < count);
        }

        public void Set(int row, Color color, bool highlighted, params string[] values)
        {
            if (row < 0 || row >= rows.Length) return;
            highlights[row].color = highlighted ? new Color(1, 1, 1, 0.14f) : Color.clear;
            for (int c = 0; c < cells.GetLength(1); c++)
            {
                cells[row, c].text = c < values.Length ? values[c] : "";
                cells[row, c].color = color;
            }
        }
    }
}
