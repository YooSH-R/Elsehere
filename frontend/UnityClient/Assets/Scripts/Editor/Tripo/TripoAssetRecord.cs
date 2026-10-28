using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoAssetRecord : ScriptableObject
    {
        public System.Collections.Generic.List<TripoPartSlot> parts =
            new System.Collections.Generic.List<TripoPartSlot>();
        public System.Collections.Generic.List<TripoFaceBinding> faceBindings =
            new System.Collections.Generic.List<TripoFaceBinding>();
        public System.Collections.Generic.List<TripoContactSample> contacts =
            new System.Collections.Generic.List<TripoContactSample>();
        public string contactTopologyFingerprint;
        public bool requireFace;
        public bool requireHumanoid;
        public GameObject commonBody;
        public string masterPreparedPath;
        public TripoMasterBindingStamp masterBinding;
        public string registeredPrefabPath;
        public GameObject sourceModel;
        public TripoValidationProfile profile;
        public TripoAssetKind kind;
        public TripoAttachmentKind attachment;
        public float referenceHeightMeters;
        public float uniformScale = 1f;
        public string blenderSourcePath;
        public TripoRevision revision = new TripoRevision();
        public System.Collections.Generic.List<string> validationErrors =
            new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> manualChecks =
            new System.Collections.Generic.List<string>();

        public string ModelPath =>
            string.IsNullOrEmpty(revision.candidatePath)
                ? revision.activePath
                : revision.candidatePath;

        [System.Serializable]
        private sealed class AppearanceState
        {
            public System.Collections.Generic.List<TripoPartSlot> parts;
            public System.Collections.Generic.List<TripoFaceBinding> faces;
            public System.Collections.Generic.List<TripoContactSample> contacts =
                new System.Collections.Generic.List<TripoContactSample>();
            public string contactTopologyFingerprint;
            public bool requireFace;
            public bool requireHumanoid;
        }

        private string AppearanceFingerprint()
        {
            string settings = JsonUtility.ToJson(
                new AppearanceState
                {
                    parts = parts,
                    faces = faceBindings,
                    contacts = contacts,
                    contactTopologyFingerprint = contactTopologyFingerprint,
                    requireFace = requireFace,
                    requireHumanoid = requireHumanoid,
                }
            );
            foreach (var part in parts)
                if (part.model != null)
                    settings += AssetDatabase
                        .GetAssetDependencyHash(AssetDatabase.GetAssetPath(part.model))
                        .ToString();
            if (commonBody != null)
                settings += AssetDatabase
                    .GetAssetDependencyHash(AssetDatabase.GetAssetPath(commonBody))
                    .ToString();
            if (kind == TripoAssetKind.Character)
                settings += AssetDatabase
                    .GetAssetDependencyHash(TripoMasterWorkflow.VisualTemplate)
                    .ToString();
            return settings;
        }

        public string GetFingerprint()
        {
            string modelPath = ModelPath ?? "";
            string modelHash = string.IsNullOrEmpty(modelPath)
                ? ""
                : AssetDatabase.GetAssetDependencyHash(modelPath).ToString();
            string settings = profile == null ? "missing-profile" : JsonUtility.ToJson(profile);
            return Hash128
                .Compute(
                    "validation:master-rig-v3\n"
                        + modelPath
                        + "\n"
                        + modelHash
                        + "\n"
                        + settings
                        + "\n"
                        + JsonUtility.ToJson(masterBinding)
                        + masterPreparedPath
                        + kind
                        + "\n"
                        + attachment
                        + "\n"
                        + uniformScale.ToString("R", CultureInfo.InvariantCulture)
                        + "\n"
                        + referenceHeightMeters.ToString("R", CultureInfo.InvariantCulture)
                        + AppearanceFingerprint()
                )
                .ToString();
        }
    }
}
