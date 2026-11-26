using System;

namespace Negiri.Core.Life
{
    /// <summary>満足度（0～上限）。増減はかならず Change を通し、範囲の外に出ないようにする。</summary>
    public sealed class Satisfaction
    {
        public int Value { get; private set; }
        public int Max { get; }

        public Satisfaction(int value, int max)
        {
            if (max <= 0) throw new ArgumentException("満足度の上限は1以上にしてください");
            Max = max;
            Value = Math.Max(0, Math.Min(max, value));
        }

        /// <summary>delta だけ動かし、実際に動いた量を返す（0や上限で止まった分は含まない）。</summary>
        public int Change(int delta)
        {
            int before = Value;
            Value = Math.Max(0, Math.Min(Max, Value + delta));
            return Value - before;
        }
    }
}
