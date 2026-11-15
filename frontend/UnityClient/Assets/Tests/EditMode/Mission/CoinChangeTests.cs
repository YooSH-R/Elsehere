using NUnit.Framework;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 퀘스트 코인이 바뀌면 코인 칩 옆에 바뀐 만큼을 알린다. 줄면 무엇을 샀는지, 늘면 퀘스트 보상임을 함께 보인다.
    /// 처음 받은 코인이나 그대로인 코인은 알리지 않는다.
    /// </summary>
    public sealed class CoinChangeTests
    {
        [Test]
        public void Between_TellsTheAmountAndWhy()
        {
            Assert.That(CoinChange.Between(null, 100, null), Is.Null, "처음 받은 코인");
            Assert.That(CoinChange.Between(96, 96, "카페 음료"), Is.Null, "그대로");

            CoinChange paid = CoinChange.Between(96, 88, "우산").Value;
            Assert.That(paid.Delta, Is.EqualTo(-8));
            Assert.That(paid.Float, Is.EqualTo("−8"));
            Assert.That(paid.Receipt, Does.StartWith("우산").And.Contain("−8코인"));

            CoinChange reward = CoinChange.Between(88, 98, "우산").Value;
            Assert.That(reward.Float, Is.EqualTo("+10"));
            Assert.That(reward.Receipt, Does.StartWith("퀘스트 보상").And.Contain("+10코인"));
        }

        [Test]
        public void Between_NamesTheFavorWhoseRewardItIs()
        {
            CoinChange reward = CoinChange.Between(98, 104, "우산", "Jake의 부탁").Value;

            Assert.That(reward.Receipt, Does.StartWith("Jake의 부탁").And.Contain("+6코인"));
        }
    }
}
