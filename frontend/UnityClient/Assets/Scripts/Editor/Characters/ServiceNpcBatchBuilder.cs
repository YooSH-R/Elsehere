using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Tripo;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Characters
{
    // 로컬 저작 단계만 연결한다. 생성 API나 운영 NPC 등록은 실행하지 않는다.
    public static class ServiceNpcBatchBuilder
    {
        public static void Prepare()
        {
            ServiceNpcReviewBuilder.Prepare();
            ServiceNpcReviewBuilder.InspectPrepared();
        }

        public static void RenderBefore()
        {
            string output =
                Environment.GetEnvironmentVariable("NPC_RENDER_OUTPUT")
                ?? throw new InvalidOperationException("Set NPC_RENDER_OUTPUT.");
            foreach (string name in ServiceNpcReviewBuilder.Names)
            {
                var paths = ServiceNpcReviewBuilder.Paths(name);
                string source = File.Exists(paths.Prefab)
                    ? paths.Prefab
                    : ServiceNpcReviewBuilder.SourceVisual(name);
                ServiceNpcComparisonRenderer.RenderModel(
                    source,
                    Path.Combine(output, name, "Before")
                );
            }
        }

        public static void BuildMouthsAndExportEyes()
        {
            ServiceNpcReviewBuilder.BuildFaces();
            ForMissingEyes(BusinessBlinkBuilder.Export);
        }

        public static void ApplyEyes()
        {
            ForMissingEyes(BusinessBlinkBuilder.Apply);
            ConfigureSurfaceContact();
            ServiceNpcReviewBuilder.ValidateAndScenes();
            AuditFaces();
        }

        public static void RefreshSelectedEyes()
        {
            string names =
                Environment.GetEnvironmentVariable("NPC_BASE_EYE_NAMES")
                ?? throw new InvalidOperationException("Set NPC_BASE_EYE_NAMES.");
            string folder =
                Environment.GetEnvironmentVariable("NPC_BASE_EYES_OUTPUT")
                ?? throw new InvalidOperationException("Set NPC_BASE_EYES_OUTPUT.");
            string previousName = Environment.GetEnvironmentVariable("NPC_BASE_EYE_NAME");
            string previousOutput = Environment.GetEnvironmentVariable("BUSINESS_BLINK_OUTPUT");
            string previousRefresh = Environment.GetEnvironmentVariable("BUSINESS_BLINK_REFRESH");
            try
            {
                foreach (string name in names.Split(';'))
                {
                    if (!ServiceNpcReviewBuilder.Names.Contains(name))
                        throw new InvalidOperationException("검수 대상이 아닙니다: " + name);
                    Environment.SetEnvironmentVariable("NPC_BASE_EYE_NAME", name);
                    Environment.SetEnvironmentVariable(
                        "BUSINESS_BLINK_OUTPUT",
                        Path.Combine(folder, name)
                    );
                    Environment.SetEnvironmentVariable("BUSINESS_BLINK_REFRESH", "1");
                    BusinessBlinkBuilder.Apply();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("NPC_BASE_EYE_NAME", previousName);
                Environment.SetEnvironmentVariable("BUSINESS_BLINK_OUTPUT", previousOutput);
                Environment.SetEnvironmentVariable("BUSINESS_BLINK_REFRESH", previousRefresh);
            }
            ConfigureSurfaceContact();
            AuditFaces();
        }

        public static void RenderAfter()
        {
            string output =
                Environment.GetEnvironmentVariable("NPC_RENDER_OUTPUT")
                ?? throw new InvalidOperationException("Set NPC_RENDER_OUTPUT.");
            ServiceNpcComparisonRenderer.RenderModel(
                TripoMasterWorkflow.VisualTemplate,
                Path.Combine(output, "LittleMungReference")
            );
            foreach (string name in ServiceNpcReviewBuilder.Names)
                ServiceNpcComparisonRenderer.RenderModel(
                    ServiceNpcReviewBuilder.Paths(name).Prefab,
                    Path.Combine(output, name, "After")
                );
            AuditFaces();
        }

        public static void RenderCompleteReview()
        {
            RenderAfter();
            string output = Environment.GetEnvironmentVariable("NPC_RENDER_OUTPUT");
            string previousName = Environment.GetEnvironmentVariable("NPC_BASE_EYE_NAME");
            string previousOutput = Environment.GetEnvironmentVariable("BUSINESS_BLINK_OUTPUT");
            try
            {
                foreach (string name in ServiceNpcReviewBuilder.Names)
                {
                    Environment.SetEnvironmentVariable("NPC_BASE_EYE_NAME", name);
                    Environment.SetEnvironmentVariable(
                        "BUSINESS_BLINK_OUTPUT",
                        Path.Combine(output, "Faces", name)
                    );
                    BusinessBlinkBuilder.Render();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("NPC_BASE_EYE_NAME", previousName);
                Environment.SetEnvironmentVariable("BUSINESS_BLINK_OUTPUT", previousOutput);
            }
        }

        public static void ConfigureSurfaceContact()
        {
            foreach (string name in ServiceNpcReviewBuilder.Names)
            {
                string path = ServiceNpcReviewBuilder.Paths(name).Prefab;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (TripoMasterMotionAudit.Inspect(root).Passed)
                        continue;
                    var correction =
                        root.GetComponentInChildren<A305.Player.LittleMungArmClearance>();
                    var settings = new SerializedObject(correction);
                    settings.FindProperty("refineSurfaceContact").boolValue = true;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    var report = TripoMasterMotionAudit.Inspect(root);
                    if (!report.Passed)
                        throw new InvalidOperationException(
                            name + ": " + string.Join(" / ", report.Errors)
                        );
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
        }

        public static void ReviewPlayerArms()
        {
            ConfigureSurfaceContact();
            string output =
                Environment.GetEnvironmentVariable("NPC_RENDER_OUTPUT")
                ?? throw new InvalidOperationException("Set NPC_RENDER_OUTPUT.");
            ServiceNpcComparisonRenderer.RenderModel(
                TripoMasterWorkflow.VisualTemplate,
                Path.Combine(output, "FemalePlayer", "Before"),
                false
            );
            ServiceNpcComparisonRenderer.RenderModel(
                TripoMasterWorkflow.VisualTemplate,
                Path.Combine(output, "FemalePlayer", "WithSurfaceRefinement"),
                true
            );
            ServiceNpcComparisonRenderer.RenderModel(
                TripoMasterWorkflow.VisualTemplate,
                Path.Combine(output, "LittleMungReference")
            );
            AuditFaces();
        }

        private static void ForMissingEyes(Action action)
        {
            string folder =
                Environment.GetEnvironmentVariable("NPC_BASE_EYES_OUTPUT")
                ?? throw new InvalidOperationException("Set NPC_BASE_EYES_OUTPUT.");
            string previousName = Environment.GetEnvironmentVariable("NPC_BASE_EYE_NAME");
            string previousOutput = Environment.GetEnvironmentVariable("BUSINESS_BLINK_OUTPUT");
            try
            {
                foreach (string name in ServiceNpcReviewBuilder.Names)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        ServiceNpcReviewBuilder.Paths(name).Prefab
                    );
                    if (prefab == null)
                        throw new InvalidOperationException("입 적용 사본이 없습니다: " + name);
                    var face = prefab.GetComponent<CharacterFaceController>();
                    if (face != null && face.SupportsPose(CharacterFacePose.Blink))
                        continue;
                    Environment.SetEnvironmentVariable("NPC_BASE_EYE_NAME", name);
                    Environment.SetEnvironmentVariable(
                        "BUSINESS_BLINK_OUTPUT",
                        Path.Combine(folder, name)
                    );
                    action();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("NPC_BASE_EYE_NAME", previousName);
                Environment.SetEnvironmentVariable("BUSINESS_BLINK_OUTPUT", previousOutput);
            }
        }

        public static void AuditFaces()
        {
            foreach (string name in ServiceNpcReviewBuilder.Names)
            {
                var paths = ServiceNpcReviewBuilder.Paths(name);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths.Prefab);
                string[] missing = CharacterFaceController.MissingChannels(prefab.transform, true);
                var rig = TripoMasterRig.Inspect(prefab, TripoMasterWorkflow.RequireMaster());
                var motion = TripoMasterMotionAudit.Inspect(prefab);
                File.WriteAllLines(
                    Path.Combine(paths.Output, "CompleteReview.txt"),
                    new[]
                    {
                        "Prefab=" + paths.Prefab,
                        "Face=" + (missing.Length == 0),
                        "Missing=" + string.Join(",", missing),
                        "Rig=" + rig.Passed,
                        "Motion=" + motion.Passed,
                        "Appearance=Manual review required",
                    }
                        .Concat(rig.Errors)
                        .Concat(motion.Errors)
                        .Concat(motion.ManualChecks)
                );
            }
        }
    }
}
