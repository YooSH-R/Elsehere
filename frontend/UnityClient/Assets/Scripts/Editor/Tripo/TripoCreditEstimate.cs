using System;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoCreditEstimate
    {
        // https://developers.tripo3d.ai/en/pricing 및 /en/models/p2의 표준 텍스처·삼각형 요금.
        public const string PricingDate = "2026-10-22";

        public int Generation { get; }
        public int Rigging { get; }
        public int Conversion { get; }
        public int Total => Generation + Rigging + Conversion;

        private TripoCreditEstimate(int generation, int rigging, int conversion)
        {
            Generation = generation;
            Rigging = rigging;
            Conversion = conversion;
        }

        public static TripoCreditEstimate ForNewAsset(string model, bool autoRig)
        {
            int generation;
            switch (model)
            {
                case TripoJob.H31Model:
                    generation = 30;
                    break;
                case TripoJob.P2Model:
                    generation = 110;
                    break;
                default:
                    throw new ArgumentException(
                        "이 모델의 예상 크레딧 정보가 없습니다.",
                        nameof(model)
                    );
            }
            // 리깅 결과는 FBX로 직접 받는다. 리깅이 없을 때만 PNG 텍스처 FBX 변환(Advanced)을 요청한다.
            return new TripoCreditEstimate(generation, autoRig ? 25 : 0, autoRig ? 0 : 10);
        }
    }
}
