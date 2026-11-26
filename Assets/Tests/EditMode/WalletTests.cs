using System;
using Negiri.Core.Economy;
using NUnit.Framework;

namespace Negiri.Core.Tests
{
    public class WalletTests
    {
        [Test]
        public void 最初の金額は収入として数える()
        {
            var w = new Wallet(3000);
            Assert.AreEqual(3000, w.Cash);
            Assert.AreEqual(3000, w.TotalIncome);
            Assert.IsTrue(w.IsBalanced);
        }

        [Test]
        public void 支払いとロック貯金のあともお金の式が成り立つ()
        {
            var w = new Wallet(3000);
            w.Pay(1200);
            w.Lock(500);
            w.Receive(1000);
            w.Unlock(200);
            w.Pay(300);
            Assert.AreEqual(3000 + 1000 - 1200 - 500 + 200 - 300, w.Cash);
            Assert.AreEqual(300, w.Locked);
            Assert.AreEqual(1500, w.TotalSpent);
            Assert.IsTrue(w.IsBalanced);
        }

        [Test]
        public void 財布より多くは払えない()
        {
            var w = new Wallet(100);
            Assert.IsFalse(w.CanPay(101));
            Assert.Throws<InvalidOperationException>(() => w.Pay(101));
            Assert.AreEqual(100, w.Cash);
            Assert.IsTrue(w.IsBalanced);
        }

        [Test]
        public void 財布ちょうどなら払える()
        {
            var w = new Wallet(100);
            w.Pay(100);
            Assert.AreEqual(0, w.Cash);
            Assert.IsTrue(w.IsBalanced);
        }

        [Test]
        public void マイナスの金額は受け付けない()
        {
            var w = new Wallet(100);
            Assert.Throws<ArgumentException>(() => w.Pay(-1));
            Assert.Throws<ArgumentException>(() => w.Receive(-1));
            Assert.Throws<ArgumentException>(() => w.Lock(101));
            Assert.Throws<ArgumentException>(() => w.Unlock(1));
        }
    }
}
