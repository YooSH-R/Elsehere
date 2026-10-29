using System;
using System.Collections.Generic;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoTimingReport
    {
        public float Percentile95;
        public float Maximum;
        public bool Passed;

        public static TripoTimingReport Measure(
            IReadOnlyList<float> errorsMilliseconds,
            TripoValidationProfile profile
        )
        {
            var report = new TripoTimingReport();
            if (
                errorsMilliseconds == null
                || errorsMilliseconds.Count == 0
                || profile == null
                || !profile.IsValid
            )
                return report;
            var absolute = new float[errorsMilliseconds.Count];
            for (int i = 0; i < absolute.Length; i++)
            {
                float value = errorsMilliseconds[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return report;
                absolute[i] = Math.Abs(value);
            }
            Array.Sort(absolute);
            report.Percentile95 = absolute[(int)Math.Ceiling(absolute.Length * 0.95) - 1];
            report.Maximum = absolute[absolute.Length - 1];
            report.Passed =
                report.Percentile95 <= profile.LipsyncPercentileMilliseconds
                && report.Maximum <= profile.LipsyncMaximumMilliseconds;
            return report;
        }
    }
}
