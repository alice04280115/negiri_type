using System;
using System.Collections.Generic;

namespace Negiri.Core.Economy
{
    /// <summary>借入先1つの数値（balance.json の life.debt.lenders に並べる）。</summary>
    [Serializable]
    public class LenderData
    {
        public string id = "card";
        /// <summary>画面とレシートに出す名前。</summary>
        public string name = "カードローン";
        /// <summary>借りられる残高の上限（利息で超えることはある）。</summary>
        public int limit = 10000;
        /// <summary>月末に残高へ足す利息（ベーシスポイント。150 = 1.5%）。円未満は切り上げ。</summary>
        public int interestBp = 150;
    }

    /// <summary>借金の数値。</summary>
    [Serializable]
    public class DebtBalance
    {
        /// <summary>返済の単位（全額返済はこの単位でなくてもよい）。</summary>
        public int repayStep = 1000;
        /// <summary>借入先。買い物で借りるのは ShoppingRule.borrowFrom で指定した借入先。</summary>
        public LenderData[] lenders = { new LenderData() };

        public LenderData Find(string id)
        {
            foreach (var l in lenders ?? Array.Empty<LenderData>())
            {
                if (l != null && l.id == id) return l;
            }
            return null;
        }
    }

    /// <summary>借入先1つ分の借金。セーブにもこの形で入る。</summary>
    [Serializable]
    public class DebtAccount
    {
        public string lender;
        /// <summary>初めからある借金（借金を持って始まるキャラ用。今は0）。財布には入っていない。</summary>
        public int initial;
        public int borrowed;
        public int interest;
        public int repaid;

        /// <summary>借金残高 ＝ 初めからある借金 ＋ 借入合計 ＋ 利息合計 － 返済合計。</summary>
        public int Balance => initial + borrowed + interest - repaid;
    }

    /// <summary>借金の記録の種類。</summary>
    public enum DebtEventKind { Borrow, Repay, Interest }

    /// <summary>借金の記録1行（家計簿に出す）。セーブにもこの形で入る。</summary>
    [Serializable]
    public class DebtEvent
    {
        public int day;
        public string lender;
        /// <summary>DebtEventKind の名前（Borrow / Repay / Interest）。</summary>
        public string kind;
        public int amount;

        public DebtEventKind Kind => (DebtEventKind)Enum.Parse(typeof(DebtEventKind), kind, true);
    }

    /// <summary>
    /// 借入先ごとの借金。借りる・返す・利息はかならずこのクラスを通す。
    /// 借りたお金と返したお金は Wallet の借入合計・返済合計にも入れて、お金の式を保つ（MonthState がそろえて呼ぶ）。
    /// </summary>
    public sealed class Debt
    {
        readonly DebtBalance balance;
        readonly List<DebtAccount> accounts = new List<DebtAccount>();
        readonly List<DebtEvent> log = new List<DebtEvent>();

        public Debt(DebtBalance balance)
        {
            this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public IReadOnlyList<DebtAccount> Accounts => accounts;
        public IReadOnlyList<DebtEvent> Log => log;
        public int RepayStep => Math.Max(1, balance.repayStep);

        /// <summary>全部の借入先の借金残高の合計。</summary>
        public int Total
        {
            get
            {
                int sum = 0;
                foreach (var a in accounts) sum += a.Balance;
                return sum;
            }
        }

        public int TotalBorrowed => Sum(a => a.borrowed);
        public int TotalInterest => Sum(a => a.interest);
        public int TotalRepaid => Sum(a => a.repaid);

        int Sum(Func<DebtAccount, int> f)
        {
            int sum = 0;
            foreach (var a in accounts) sum += f(a);
            return sum;
        }

        public LenderData Lender(string id) => balance.Find(id);

        public string LenderName(string id) => Lender(id)?.name ?? id;

        /// <summary>借入先の口座。まだなければ作る。</summary>
        DebtAccount Account(string lender)
        {
            foreach (var a in accounts) if (a.lender == lender) return a;
            if (Lender(lender) == null) throw new ArgumentException($"借入先がありません: {lender}");
            var acc = new DebtAccount { lender = lender };
            accounts.Add(acc);
            return acc;
        }

        public int BalanceOf(string lender)
        {
            foreach (var a in accounts) if (a.lender == lender) return a.Balance;
            return 0;
        }

        /// <summary>この借入先からあといくら借りられるか（限度額 － 残高）。</summary>
        public int Room(string lender)
        {
            var l = Lender(lender);
            if (l == null) return 0;
            return Math.Max(0, l.limit - BalanceOf(lender));
        }

        /// <summary>初めからある借金を置く（借金を持って始まるキャラ用）。財布は増えない。</summary>
        public void SetInitial(string lender, int amount)
        {
            if (amount < 0) throw new ArgumentException("借金は0以上にしてください");
            Account(lender).initial = amount;
        }

        /// <summary>借りる（Wallet.Borrow と一緒に呼ぶ）。限度額を超えるなら例外にする。</summary>
        public void Borrow(string lender, int amount, int day)
        {
            if (amount <= 0) throw new ArgumentException("借りる額は1以上にしてください");
            if (amount > Room(lender)) throw new InvalidOperationException($"限度額を{amount - Room(lender)}円超えています");
            Account(lender).borrowed += amount;
            log.Add(new DebtEvent { day = day, lender = lender, kind = nameof(DebtEventKind.Borrow), amount = amount });
        }

        /// <summary>返す（Wallet.Repay と一緒に呼ぶ）。残高を超えては返せない。</summary>
        public void Repay(string lender, int amount, int day)
        {
            if (amount <= 0) throw new ArgumentException("返す額は1以上にしてください");
            if (amount > BalanceOf(lender)) throw new InvalidOperationException("借金より多くは返せません");
            Account(lender).repaid += amount;
            log.Add(new DebtEvent { day = day, lender = lender, kind = nameof(DebtEventKind.Repay), amount = amount });
        }

        /// <summary>利息を足す（月末）。借金だけが増え、財布は変わらない。足した合計を返す。</summary>
        public int AddInterest(int day)
        {
            int total = 0;
            foreach (var a in accounts)
            {
                var l = Lender(a.lender);
                int bal = a.Balance;
                if (l == null || bal <= 0 || l.interestBp <= 0) continue;
                int add = (int)((bal * (long)l.interestBp + 9999) / 10000);
                a.interest += add;
                total += add;
                log.Add(new DebtEvent { day = day, lender = a.lender, kind = nameof(DebtEventKind.Interest), amount = add });
            }
            return total;
        }

        /// <summary>セーブから戻す。</summary>
        public static Debt Restore(DebtBalance balance, IEnumerable<DebtAccount> accounts, IEnumerable<DebtEvent> log)
        {
            var d = new Debt(balance);
            foreach (var a in accounts ?? Array.Empty<DebtAccount>())
            {
                if (a == null) continue;
                if (a.initial < 0 || a.borrowed < 0 || a.interest < 0 || a.repaid < 0 || a.Balance < 0) throw new ArgumentException("セーブの借金の値がおかしいです");
                d.accounts.Add(new DebtAccount { lender = a.lender, initial = a.initial, borrowed = a.borrowed, interest = a.interest, repaid = a.repaid });
            }
            foreach (var e in log ?? Array.Empty<DebtEvent>()) if (e != null) d.log.Add(e);
            return d;
        }
    }
}
