using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoCharacterPart : ScriptableObject
    {
        public TripoCharacterRegion region;
        public GameObject model;
        public string referenceFingerprint;
        public string approvedFingerprint;
        public string CurrentFingerprint =>
            Hash128
                .Compute(referenceFingerprint + region + TripoCharacterFingerprint.Of(model))
                .ToString();
        public bool IsApproved => model != null && approvedFingerprint == CurrentFingerprint;
    }
}
