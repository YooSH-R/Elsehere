using System.IO;
using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Player
{
    public static class SeatedPosePreview
    {
        [MenuItem("Tools/A305/Player/Capture Seated Pose")]
        public static void Capture()
        {
            if (!BuilderTools.CanBuild())
                return;
            var previousScenes = EditorSceneManager.GetSceneManagerSetup();
            string output =
                System.Environment.GetEnvironmentVariable("SEATED_POSE_OUTPUT")
                ?? Path.GetFullPath(
                    Path.Combine(Application.dataPath, "../../../artifacts/seated-pose")
                );
            Directory.CreateDirectory(output);
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
                var light = Object.FindFirstObjectByType<Light>();
                light.transform.rotation = Quaternion.Euler(35, 180, 0);
                light.intensity = 1.3f;
                var camera = Camera.main;
                camera.orthographic = true;
                camera.orthographicSize = 1.15f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.28f, .32f, .38f);
                foreach (string id in new[] { "F", "M" })
                {
                    var definition = AssetDatabase.LoadAssetAtPath<PlayerCharacterDefinition>(
                        "Assets/Data/Player/Characters/" + id + ".asset"
                    );
                    var root = new GameObject("Seated " + id, typeof(CharacterController));
                    var visual = Object.Instantiate(definition.VisualPrefab, root.transform);
                    foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
                        behaviour.enabled = false;
                    var animator = visual.GetComponentInChildren<Animator>();
                    animator.Rebind();
                    animator.Update(0);
                    var seat = new GameObject("Seat");
                    var cushion = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cushion.transform.position = new Vector3(0, .4f, -.1f);
                    cushion.transform.localScale = new Vector3(.9f, .1f, .65f);
                    var arms = SeatedArms.Find(animator);
                    SeatedLegs
                        .Find(animator)
                        .Apply(
                            visual.transform,
                            visual.transform.localPosition,
                            seat.transform,
                            .45f,
                            .2f
                        );
                    arms.Apply(seat.transform);
                    foreach (
                        var view in new[]
                        {
                            ("front", new Vector3(0, 1.05f, 4)),
                            ("side", new Vector3(4, 1.05f, 0)),
                            ("angle", new Vector3(3, 1.6f, 4)),
                        }
                    )
                    {
                        camera.transform.position = view.Item2;
                        camera.transform.LookAt(new Vector3(0, .95f, 0));
                        Save(camera, Path.Combine(output, id + "-" + view.Item1 + ".png"));
                    }
                    foreach (
                        var bone in new[]
                        {
                            HumanBodyBones.Hips,
                            HumanBodyBones.LeftUpperArm,
                            HumanBodyBones.LeftLowerArm,
                            HumanBodyBones.LeftHand,
                            HumanBodyBones.LeftUpperLeg,
                            HumanBodyBones.LeftLowerLeg,
                        }
                    )
                        Debug.Log(
                            id
                                + " "
                                + bone
                                + " "
                                + animator.GetBoneTransform(bone).position.ToString("F3")
                        );
                    Object.DestroyImmediate(root);
                    Object.DestroyImmediate(seat);
                    Object.DestroyImmediate(cushion);
                }
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (System.Array.Exists(previousScenes, setup => setup.isLoaded))
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }

        private static void Save(Camera camera, string path)
        {
            var target = new RenderTexture(800, 800, 24);
            var pixels = new Texture2D(800, 800, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                // 첫 렌더에서 재질을 준비해 검수 이미지의 색이 빠지지 않게 한다.
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }
    }
}
