using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Npc
{
    public static class BenRigPreview
    {
        public const string SandboxPath = "Assets/Scenes/Sandbox/BenWalkSandbox.unity";

        [MenuItem("Tools/A305/Npc/Preview Ben Walk")]
        public static void BuildSandboxAndCapture()
        {
            if (!BuilderTools.CanBuild())
                return;
            var previousScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.NewScene(
                    NewSceneSetup.DefaultGameObjects,
                    NewSceneMode.Single
                );
                var groups = BuilderTools.EnsureGroups(scene);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BenRigBuilder.VisualPath);
                GameObject front = BuilderTools.InstantiatePrefab(
                    prefab,
                    groups.Characters,
                    new Vector3(-.9f, 0, 0),
                    Quaternion.identity
                );
                GameObject side = BuilderTools.InstantiatePrefab(
                    prefab,
                    groups.Characters,
                    new Vector3(.9f, 0, 0),
                    Quaternion.Euler(0, 90, 0)
                );
                front.name = "BenFront";
                side.name = "BenSide";
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Ground";
                floor.transform.SetParent(groups.Environment, false);
                floor.transform.localScale = Vector3.one * 2;
                var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
                camera.transform.SetParent(groups.Systems, false);
                camera.transform.position = new Vector3(0, 1.15f, 5);
                camera.transform.LookAt(new Vector3(0, .83f, 0));
                camera.orthographic = true;
                camera.orthographicSize = 1.25f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.18f, .22f, .28f);
                EditorSceneManager.SaveScene(scene, SandboxPath);

                string folder = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "../../../artifacts/ben-rig")
                );
                Directory.CreateDirectory(folder);
                Animator[] animators = new[] { front, side }
                    .Select(model => model.GetComponentInChildren<Animator>())
                    .ToArray();
                foreach (Animator animator in animators)
                {
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Rebind();
                    animator.Update(0);
                }
                Capture(camera, Path.Combine(folder, "idle.png"));
                foreach (Animator animator in animators)
                    animator.SetFloat("Speed", 1.1f);
                for (int frame = 0; frame < 33; frame++)
                {
                    foreach (Animator animator in animators)
                        animator.Update(.55f / 33);
                    Capture(camera, Path.Combine(folder, $"walk-{frame:D2}.png"));
                }
                Debug.Log("Ben walk preview captured at " + folder);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1000, 600, 24);
            var pixels = new Texture2D(1000, 600, TextureFormat.RGB24, false);
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1000, 600), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousTarget;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }
    }
}
