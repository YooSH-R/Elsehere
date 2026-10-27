using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoFacePreview
    {
        public static readonly string[] Emotions =
        {
            "Neutral",
            "Smile",
            "Joy",
            "Concern",
            "Sadness",
            "Surprise",
            "Firm",
        };
        public static readonly string[] Visemes =
        {
            "SIL",
            "PP",
            "FF",
            "TH",
            "DD",
            "KK",
            "CH",
            "SS",
            "NN",
            "RR",
            "AA",
            "E",
            "IH",
            "OH",
            "OU",
        };
        public static readonly string[] EmotionShapes =
        {
            "eyeBlinkLeft",
            "eyeBlinkRight",
            "eyeWideLeft",
            "eyeWideRight",
            "eyeSquintLeft",
            "eyeSquintRight",
            "browInnerUp",
            "browDownLeft",
            "browDownRight",
            "browOuterUpLeft",
            "browOuterUpRight",
            "mouthSmileLeft",
            "mouthSmileRight",
            "mouthFrownLeft",
            "mouthFrownRight",
        };

        public static Dictionary<string, float> Mix(
            string emotion,
            string viseme,
            float speechWeight,
            float blink
        )
        {
            var weights = new Dictionary<string, float>();
            foreach (string shape in EmotionShapes)
                weights[shape] = 0f;
            foreach (string speech in Visemes)
                if (speech != "SIL")
                    weights["viseme_" + speech] = 0f;
            switch (emotion)
            {
                case "Smile":
                    Pair(weights, "mouthSmile", 40f);
                    break;
                case "Joy":
                    Pair(weights, "mouthSmile", 75f);
                    Pair(weights, "eyeSquint", 35f);
                    break;
                case "Concern":
                    weights["browInnerUp"] = 35f;
                    Pair(weights, "browDown", 15f);
                    break;
                case "Sadness":
                    weights["browInnerUp"] = 50f;
                    Pair(weights, "mouthFrown", 35f);
                    break;
                case "Surprise":
                    Pair(weights, "eyeWide", 55f);
                    Pair(weights, "browOuterUp", 50f);
                    break;
                case "Firm":
                    Pair(weights, "browDown", 25f);
                    Pair(weights, "mouthFrown", 15f);
                    break;
            }
            float articulation = viseme == "SIL" ? 0f : Mathf.Clamp01(speechWeight);
            string key = "viseme_" + viseme;
            if (weights.ContainsKey(key))
                weights[key] = articulation * 100f;
            foreach (string shape in EmotionShapes)
                if (shape.StartsWith("mouth", StringComparison.Ordinal))
                    weights[shape] *= 1f - articulation;
            Pair(weights, "eyeBlink", Mathf.Clamp01(blink) * 100f);
            return weights;
        }

        public static float ReleaseWeight(float elapsedMilliseconds, float releaseMilliseconds)
        {
            if (releaseMilliseconds <= 0f)
                return 0f;
            return 1f - Mathf.Clamp01(elapsedMilliseconds / releaseMilliseconds);
        }

        private static void Pair(Dictionary<string, float> weights, string prefix, float value)
        {
            weights[prefix + "Left"] = value;
            weights[prefix + "Right"] = value;
        }
    }
}
