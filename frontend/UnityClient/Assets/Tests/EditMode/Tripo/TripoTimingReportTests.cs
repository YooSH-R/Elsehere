using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoTimingReportTests
    {
        [Test]
        public void Timing_UsesAbsoluteP95AndMaximum_RejectsBothExceededLimits()
        {
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            var errors = new float[20];
            try
            {
                errors[19] = -50f;
                var report = TripoTimingReport.Measure(errors, profile);
                Assert.That(report.Passed, Is.True);
                Assert.That(report.Percentile95, Is.Zero);
                Assert.That(report.Maximum, Is.EqualTo(50f));
                errors[18] = 41f;
                Assert.That(TripoTimingReport.Measure(errors, profile).Passed, Is.False);
                errors[18] = 0f;
                errors[19] = 81f;
                Assert.That(TripoTimingReport.Measure(errors, profile).Passed, Is.False);
                Assert.That(TripoTimingReport.Measure(new float[0], profile).Passed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
