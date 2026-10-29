using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    public static class TripoCharacterBootstrap
    {
        public const string Folder = "Assets/Art/Characters/Common/LittleMungMaster";
        public const string LibraryPath = Folder + "/Library.asset";
        private const string ApprovedVisual =
            "Assets/Art/Characters/Tripo/LittleMung_Casual_Male_41b6e942_Review/MasterRig 2/Visual.prefab";

        public static void Build()
        {
            var library = TripoCharacterLibraryBuilder.Build(
                AssetDatabase.LoadAssetAtPath<GameObject>(ApprovedVisual),
                Folder
            );
            // 사용자가 확인한 남성 보정본의 면·가중치를 그대로 분리한 기본 부품이다.
            foreach (var part in new[] { library.head, library.dressedBody })
            {
                part.approvedFingerprint = part.CurrentFingerprint;
                EditorUtility.SetDirty(part);
            }
            AssetDatabase.SaveAssets();
            var male = TripoCharacterAssembly.Build(
                library,
                library.head,
                library.dressedBody,
                Folder + "/Variants/MaleCasual"
            );
            var female = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterWorkflow.VisualTemplate)
            );
            GameObject rest;
            try
            {
                var animator = female.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = library
                    .referenceVisual.GetComponentInChildren<Animator>()
                    .runtimeAnimatorController;
                var source = TripoMasterWorkflow.RequireMaster().GetComponentInChildren<Animator>();
                var bones = animator
                    .GetBoneTransform(HumanBodyBones.Hips)
                    .GetComponentsInChildren<Transform>()
                    .ToDictionary(b => b.name);
                foreach (
                    var bone in source
                        .GetBoneTransform(HumanBodyBones.Hips)
                        .GetComponentsInChildren<Transform>()
                )
                {
                    var target = bones[bone.name];
                    target.localPosition = bone.localPosition;
                    target.localRotation = bone.localRotation;
                    target.localScale = bone.localScale;
                }
                rest = PrefabUtility.SaveAsPrefabAsset(
                    female,
                    Folder + "/FemaleSourceAtRest.prefab"
                );
            }
            finally
            {
                Object.DestroyImmediate(female);
            }
            var femaleHead = TripoCharacterLibraryBuilder.PreparePart(
                library,
                rest,
                TripoCharacterRegion.Head,
                Folder + "/Heads/Female"
            );
            femaleHead = TripoCharacterHeadFitBuilder.Prepare(
                library,
                femaleHead,
                Folder + "/Heads/FemaleFitted"
            );
            var femaleRecipe = TripoCharacterAssembly.Build(
                library,
                femaleHead,
                library.dressedBody,
                Folder + "/Variants/FemaleCasual"
            );
            var maleReport = TripoCharacterAssembly.Validate(male);
            var femaleReport = TripoCharacterAssembly.Validate(femaleRecipe);
            string malePath = AssetDatabase.GetAssetPath(male),
                femalePath = AssetDatabase.GetAssetPath(femaleRecipe);
            string maleScene = BuildScene(male);
            string femaleScene = BuildScene(
                AssetDatabase.LoadAssetAtPath<TripoCharacterComposition>(femalePath)
            );
            string report = Environment.GetEnvironmentVariable("TRIPO_COMMON_REPORT");
            if (!string.IsNullOrEmpty(report))
                File.WriteAllText(
                    report,
                    JsonUtility.ToJson(
                        new Result
                        {
                            library = LibraryPath,
                            male = malePath,
                            female = femalePath,
                            maleScene = maleScene,
                            femaleScene = femaleScene,
                            maleErrors = maleReport.Errors.ToArray(),
                            femaleErrors = femaleReport.Errors.ToArray(),
                        },
                        true
                    )
                );
        }

        public static string BuildScene(TripoCharacterComposition composition)
        {
            var report = TripoCharacterAssembly.Validate(composition);
            if (!report.Passed)
                throw new InvalidOperationException(string.Join("\n", report.Errors));
            var record = ScriptableObject.CreateInstance<TripoAssetRecord>();
            try
            {
                record.sourceModel = composition.preview;
                record.commonBody = TripoMasterWorkflow.RequireMaster();
                record.revision.candidatePath = AssetDatabase.GetAssetPath(composition.preview);
                return TripoMasterSandbox.Build(record);
            }
            finally
            {
                Object.DestroyImmediate(record);
            }
        }

        [Serializable]
        private class Result
        {
            public string library,
                male,
                female,
                maleScene,
                femaleScene;
            public string[] maleErrors,
                femaleErrors;
        }
    }
}
