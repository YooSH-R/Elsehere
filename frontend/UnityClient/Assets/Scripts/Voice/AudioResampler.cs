using System;

namespace A305.Voice
{
    /// <summary>
    /// 녹음을 음성 인식에 보낼 샘플레이트로 줄인다. 출력 한 칸에 해당하는 입력 구간을 평균해, 건너뛰며 고를 때 생기는 잡음 접힘을 줄인다.
    /// </summary>
    public static class AudioResampler
    {
        /// <param name="sourceRate">녹음된 샘플레이트. 브라우저는 보통 44.1kHz·48kHz이며 <paramref name="targetRate"/>보다 낮으면 안 된다.</param>
        public static float[] Resample(float[] samples, int sourceRate, int targetRate)
        {
            if (sourceRate < targetRate)
            {
                throw new ArgumentException(
                    $"녹음 샘플레이트({sourceRate})가 목표({targetRate})보다 낮습니다.",
                    nameof(sourceRate)
                );
            }

            if (sourceRate == targetRate)
            {
                return (float[])samples.Clone();
            }

            double step = (double)sourceRate / targetRate;
            int outputLength = (int)(samples.Length / step);
            var output = new float[outputLength];
            for (int index = 0; index < outputLength; index++)
            {
                int start = (int)(index * step);
                int end = Math.Min(samples.Length, (int)((index + 1) * step));
                float sum = 0f;
                for (int source = start; source < end; source++)
                {
                    sum += samples[source];
                }

                output[index] = sum / Math.Max(1, end - start);
            }

            return output;
        }
    }
}
