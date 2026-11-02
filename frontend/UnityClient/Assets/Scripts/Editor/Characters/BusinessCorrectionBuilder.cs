using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using A305.EditorTools.Tripo;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static class BusinessCorrectionBuilder
    {
        public const string Folder =
            "Assets/Art/Characters/ReviewCorrections/LittleMung_Business_Male/MasterRig";
        public const string Visual = Folder + "/LittleMung_Business_Male_Visual.prefab";

        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Characters/Tripo/NPC/LittleMung_Business_Male/LittleMung_Business_Male.fbx"
            );
            var model = TripoMasterRig.Build(source, TripoMasterWorkflow.RequireMaster(), Folder);
            var visual = TripoMasterWorkflow.BuildVisual(model, Visual);
            var head = TripoMasterHeadAudit.Inspect(source, visual);
            var motion = TripoMasterMotionAudit.Inspect(visual);
            string output = Environment.GetEnvironmentVariable("BUSINESS_CORRECTION_OUTPUT");
            File.WriteAllText(
                Path.Combine(output, "CorrectedAudit.txt"),
                "Head passed: "
                    + head.Passed
                    + "\n"
                    + string.Join("\n", head.Errors)
                    + "\nMotion passed: "
                    + motion.Passed
                    + "\n"
                    + string.Join("\n", motion.Errors)
            );
            BusinessCharacterReview.ExportModel(Visual, output, "Master");
            if (!head.Passed || !motion.Passed)
                throw new InvalidOperationException("Correction audit failed; review copy only.");
            AssetDatabase.SaveAssets();
        }

        public static void BuildMotionScene()
        {
            const string path =
                "Assets/Scenes/Sandbox/Characters/LittleMung_Business_Male_MotionReview.unity";
            if (File.Exists(path))
                throw new IOException("Preserve the existing motion review scene.");
            if (
                !AssetDatabase.CopyAsset(
                    "Assets/Scenes/Sandbox/Mannequin/LittleMung_Base_Male_170_Motion.unity",
                    path
                )
            )
                throw new IOException("Missing mannequin motion review scene.");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            Object.DestroyImmediate(player.transform.Find("Visual").gameObject);
            var visual = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(BusinessFacePreviewBuilder.Prefab),
                    player.transform
                );
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            var skin = visual
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First();
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh, true);
                var points = mesh.vertices.Select(skin.transform.TransformPoint).ToArray();
                float low = points.Min(p => p.y) - player.transform.position.y;
                float height = points.Max(p => p.y) - points.Min(p => p.y);
                if (Mathf.Abs(height - 1.7f) > .001f)
                    throw new InvalidOperationException("Review height must remain 1.7m.");
                visual.transform.position -= Vector3.up * low;
                var capsule = player.GetComponent<CharacterController>();
                capsule.height = height;
                capsule.center = Vector3.up * height / 2;
                capsule.radius = Mathf.Min(
                    height * .45f,
                    Mathf.Max(
                        (points.Max(p => p.x) - points.Min(p => p.x)) / 2,
                        (points.Max(p => p.z) - points.Min(p => p.z)) / 2
                    )
                );
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
            BuilderTools.SetReference(
                player.GetComponent<CharacterAnimationDriver>(),
                "animator",
                visual.GetComponentInChildren<Animator>()
            );
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
        }

        public static void ExportFace()
        {
            string output = Environment.GetEnvironmentVariable("BUSINESS_CORRECTION_OUTPUT");
            BusinessCharacterReview.ExportModel(
                BusinessFacePreviewBuilder.Prefab,
                output,
                "Corrected"
            );
            var report = TripoMasterMotionAudit.Inspect(
                AssetDatabase.LoadAssetAtPath<GameObject>(BusinessFacePreviewBuilder.Prefab)
            );
            File.WriteAllText(
                Path.Combine(output, "CorrectedFaceAudit.txt"),
                "Passed: " + report.Passed + "\n" + string.Join("\n", report.Errors)
            );
            if (!report.Passed)
                throw new InvalidOperationException("Corrected face motion audit failed.");
        }
    }
}
