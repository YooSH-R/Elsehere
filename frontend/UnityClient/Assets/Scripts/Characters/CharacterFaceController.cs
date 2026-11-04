using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Characters
{
    public enum CharacterFacePose
    {
        Neutral,
        Smile,
        Blink,
        OpenMouth,
        FV,
        TH,
        ClosedLips,
        E,
        OH,
        OU,
        WideI,
        FlatEu,
    }

    public sealed class CharacterFaceController : MonoBehaviour
    {
        private static readonly string[] EmotionFamilies =
        {
            "browInnerUp",
            "browOuterUp",
            "browDown",
            "eyeSquint",
            "eyeWide",
            "cheekRaise",
            "mouthFrown",
        };
        private static readonly string[] Channels =
        {
            "mouthSmileLeft",
            "mouthSmileRight",
            "eyeBlinkLeft",
            "eyeBlinkRight",
            "viseme_AA",
            "viseme_FF",
            "viseme_TH",
            "viseme_PP",
            "viseme_E",
            "viseme_OH",
            "viseme_OU",
            "viseme_I",
            "viseme_EU",
        };

        public bool SupportsPose(CharacterFacePose pose)
        {
            var skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < Channels.Length; i++)
            {
                if (!IsActive(pose, i))
                    continue;
                bool found = false;
                foreach (var skin in skins)
                    found |=
                        skin.sharedMesh != null
                        && skin.sharedMesh.GetBlendShapeIndex(Channels[i]) >= 0;
                if (!found)
                    return false;
            }
            return true;
        }

        public static string[] MissingChannels(Transform root, bool includeInactive = false)
        {
            var missing = new List<string>();
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive);
            foreach (string channel in Channels)
            {
                bool found = false;
                foreach (var skin in skins)
                    found |=
                        skin.sharedMesh != null && skin.sharedMesh.GetBlendShapeIndex(channel) >= 0;
                if (!found)
                    missing.Add(channel);
            }
            return missing.ToArray();
        }

        public void ApplyBlink(float strength)
        {
            if (!SupportsPose(CharacterFacePose.Blink))
                throw new InvalidOperationException("Missing eyelid shapes.");
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null)
                    continue;
                foreach (string channel in new[] { "eyeBlinkLeft", "eyeBlinkRight" })
                {
                    int index = skin.sharedMesh.GetBlendShapeIndex(channel);
                    if (index >= 0)
                        skin.SetBlendShapeWeight(index, Mathf.Clamp01(strength) * 100);
                }
            }
        }

        public void SetPose(CharacterFacePose pose, float intensity = 1f) =>
            BlendPoses(pose, pose, 0, intensity);

        public void BlendPoses(
            CharacterFacePose from,
            CharacterFacePose to,
            float progress,
            float intensity = 1f
        )
        {
            float blend = Mathf.Clamp01(progress);
            float strength = Mathf.Clamp01(intensity) * 100f;
            var skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < Channels.Length; i++)
            {
                if (!IsActive(from, i) && !IsActive(to, i))
                    continue;
                bool found = false;
                foreach (var skin in skins)
                    found |=
                        skin.sharedMesh != null
                        && skin.sharedMesh.GetBlendShapeIndex(Channels[i]) >= 0;
                if (!found)
                    throw new InvalidOperationException("Missing face shape: " + Channels[i]);
            }
            foreach (var skin in skins)
            {
                if (skin.sharedMesh == null)
                    continue;
                ClearEmotion(skin);
                for (int i = 0; i < Channels.Length; i++)
                {
                    int index = skin.sharedMesh.GetBlendShapeIndex(Channels[i]);
                    if (index >= 0)
                        skin.SetBlendShapeWeight(
                            index,
                            Mathf.Lerp(
                                IsActive(from, i) ? 1f : 0f,
                                IsActive(to, i) ? 1f : 0f,
                                blend
                            ) * strength
                        );
                }
            }
        }

        public void ApplySpeech(SpeechMouthSample mouth, float smile, float blink)
        {
            var poses = new[]
            {
                CharacterFacePose.OpenMouth,
                CharacterFacePose.FV,
                CharacterFacePose.TH,
                CharacterFacePose.ClosedLips,
                CharacterFacePose.E,
                CharacterFacePose.OH,
                CharacterFacePose.OU,
                CharacterFacePose.WideI,
                CharacterFacePose.FlatEu,
            };
            var weights = new float[Channels.Length];
            float contact =
                mouth.Weight(CharacterFacePose.ClosedLips)
                + mouth.Weight(CharacterFacePose.FV)
                + mouth.Weight(CharacterFacePose.TH);
            float rounding =
                mouth.Weight(CharacterFacePose.OU) + mouth.Weight(CharacterFacePose.OH);
            float opening =
                mouth.Weight(CharacterFacePose.OpenMouth)
                + mouth.Weight(CharacterFacePose.E)
                + mouth.Weight(CharacterFacePose.TH);
            // Lip contact and rounding take priority over corner lift. Eyelids
            // remain independent, so one complete facial state is applied once.
            float smileWeight =
                Mathf.Clamp01(smile)
                * (1 - Mathf.Clamp01(contact))
                * (1 - .75f * Mathf.Clamp01(rounding))
                * (1 - .65f * Mathf.Clamp01(opening));
            weights[0] = weights[1] = smileWeight;
            weights[2] = weights[3] = Mathf.Clamp01(blink);
            for (int i = 0; i < poses.Length; i++)
                weights[i + 4] = mouth.Weight(poses[i]);
            var skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0)
                    continue;
                bool found = false;
                foreach (var skin in skins)
                    found |=
                        skin.sharedMesh != null
                        && skin.sharedMesh.GetBlendShapeIndex(Channels[i]) >= 0;
                if (!found)
                    throw new InvalidOperationException("Missing face shape: " + Channels[i]);
            }
            foreach (var skin in skins)
            {
                if (skin.sharedMesh == null)
                    continue;
                ClearEmotion(skin);
                for (int i = 0; i < weights.Length; i++)
                {
                    int index = skin.sharedMesh.GetBlendShapeIndex(Channels[i]);
                    if (index >= 0)
                        skin.SetBlendShapeWeight(index, weights[i] * 100);
                }
            }
        }

        public void ApplySpeech(SpeechMouthSample mouth, FaceEmotionSample emotion, float blink)
        {
            ApplySpeech(mouth, 0, blink);
            float contact = Mathf.Clamp01(
                mouth.Weight(CharacterFacePose.ClosedLips)
                    + mouth.Weight(CharacterFacePose.FV)
                    + mouth.Weight(CharacterFacePose.TH)
            );
            float rounding = Mathf.Clamp01(
                mouth.Weight(CharacterFacePose.OH) + mouth.Weight(CharacterFacePose.OU)
            );
            float opening = Mathf.Clamp01(
                mouth.Weight(CharacterFacePose.OpenMouth)
                    + mouth.Weight(CharacterFacePose.E)
                    + mouth.Weight(CharacterFacePose.TH)
            );
            float cornerFreedom = (1 - contact) * (1 - .75f * rounding) * (1 - .65f * opening);
            float openEyes = 1 - Mathf.Clamp01(blink);
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null)
                    continue;
                SetPair(skin, "mouthSmile", emotion.smile * cornerFreedom);
                SetPair(
                    skin,
                    "mouthFrown",
                    new Vector2(
                        emotion.mouthFrown.x * (1 - Mathf.Clamp01(emotion.smile.x)),
                        emotion.mouthFrown.y * (1 - Mathf.Clamp01(emotion.smile.y))
                    ) * cornerFreedom
                );
                SetPair(skin, "browInnerUp", emotion.browInnerUp);
                SetPair(skin, "browOuterUp", emotion.browOuterUp);
                SetPair(skin, "browDown", emotion.browDown);
                ApplyEyeClosure(skin, "Left", emotion.eyeSquint.x, blink);
                ApplyEyeClosure(skin, "Right", emotion.eyeSquint.y, blink);
                SetPair(skin, "eyeSquint", emotion.eyeSquint * openEyes);
                SetPair(
                    skin,
                    "eyeWide",
                    new Vector2(
                        emotion.eyeWide.x * (1 - Mathf.Clamp01(emotion.eyeSquint.x)),
                        emotion.eyeWide.y * (1 - Mathf.Clamp01(emotion.eyeSquint.y))
                    ) * openEyes
                );
                SetPair(skin, "cheekRaise", emotion.cheekRaise * openEyes);
            }
        }

        private static void ApplyEyeClosure(
            SkinnedMeshRenderer skin,
            string side,
            float squint,
            float blink
        )
        {
            // Authored eyeSquint is a curve correction, not a second closing
            // trajectory. Share closure with blink so the convex eye stays covered.
            if (skin.sharedMesh.GetBlendShapeIndex("eyeSquint" + side) < 0)
                return;
            int index = skin.sharedMesh.GetBlendShapeIndex("eyeBlink" + side);
            if (index >= 0)
                skin.SetBlendShapeWeight(index, Mathf.Clamp01(Mathf.Max(squint, blink)) * 100);
        }

        public static string[] MissingEmotionChannels(Transform root)
        {
            var missing = new List<string>();
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (string family in EmotionFamilies)
            foreach (string side in new[] { "Left", "Right" })
            {
                string channel = family + side;
                bool found = false;
                foreach (var skin in skins)
                    found |=
                        skin.sharedMesh != null && skin.sharedMesh.GetBlendShapeIndex(channel) >= 0;
                if (!found)
                    missing.Add(channel);
            }
            return missing.ToArray();
        }

        private static void ClearEmotion(SkinnedMeshRenderer skin)
        {
            foreach (string family in EmotionFamilies)
                SetPair(skin, family, Vector2.zero);
        }

        private static void SetPair(SkinnedMeshRenderer skin, string family, Vector2 weights)
        {
            int left = skin.sharedMesh.GetBlendShapeIndex(family + "Left");
            int right = skin.sharedMesh.GetBlendShapeIndex(family + "Right");
            if (left >= 0)
                skin.SetBlendShapeWeight(left, Mathf.Clamp01(weights.x) * 100);
            if (right >= 0)
                skin.SetBlendShapeWeight(right, Mathf.Clamp01(weights.y) * 100);
        }

        private static bool IsActive(CharacterFacePose pose, int channel) =>
            pose switch
            {
                CharacterFacePose.Smile => channel == 0 || channel == 1,
                CharacterFacePose.Blink => channel == 2 || channel == 3,
                CharacterFacePose.OpenMouth => channel == 4,
                CharacterFacePose.FV => channel == 5,
                CharacterFacePose.TH => channel == 6,
                CharacterFacePose.ClosedLips => channel == 7,
                CharacterFacePose.E => channel == 8,
                CharacterFacePose.OH => channel == 9,
                CharacterFacePose.OU => channel == 10,
                CharacterFacePose.WideI => channel == 11,
                CharacterFacePose.FlatEu => channel == 12,
                _ => false,
            };
    }
}
