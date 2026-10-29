using System;
using A305.EditorTools.Tripo;
using NUnit.Framework;

namespace A305.Tripo.Tests
{
    public class TripoCreditEstimateTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("future-model")]
        public void ForNewAsset_WhenModelHasNoPrice_RejectsInsteadOfQuotingHPrice(string model)
        {
            Assert.Throws<ArgumentException>(() => TripoCreditEstimate.ForNewAsset(model, true));
        }

        [TestCase("v3.1-20260211", true, 30, 25, 0, 55)]
        [TestCase("v3.1-20260211", false, 30, 0, 10, 40)]
        [TestCase("P2-20260801", true, 110, 25, 0, 135)]
        [TestCase("P2-20260801", false, 110, 0, 10, 120)]
        public void ForNewAsset_QuotesGenerationAndOnlyTheChosenFbxRoute(
            string model,
            bool autoRig,
            int generation,
            int rigging,
            int conversion,
            int total
        )
        {
            TripoCreditEstimate estimate = TripoCreditEstimate.ForNewAsset(model, autoRig);
            Assert.That(estimate.Generation, Is.EqualTo(generation));
            Assert.That(estimate.Rigging, Is.EqualTo(rigging));
            Assert.That(estimate.Conversion, Is.EqualTo(conversion));
            Assert.That(estimate.Total, Is.EqualTo(total));
        }
    }
}
