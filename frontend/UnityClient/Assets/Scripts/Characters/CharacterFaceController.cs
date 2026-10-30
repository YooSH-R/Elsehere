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
                for (int i = 0; i < weights.Length; i++)
                {
                    int index = skin.sharedMesh.GetBlendShapeIndex(Channels[i]);
                    if (index >= 0)
                        skin.SetBlendShapeWeight(index, weights[i] * 100);
                }
            }
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
