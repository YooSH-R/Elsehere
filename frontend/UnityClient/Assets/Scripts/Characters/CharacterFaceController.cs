using System;
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
