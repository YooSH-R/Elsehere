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
        };

        public void SetPose(CharacterFacePose pose, float intensity = 1f)
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
                            IsActive(pose, i) ? Mathf.Clamp01(intensity) * 100 : 0
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
                _ => false,
            };
    }
}
