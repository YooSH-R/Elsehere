using System;
using System.IO;
using System.Linq;
using A305.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    // Offline Base-eye fitting on the existing review prefab. Generated FBX and Base assets are read-only.
    public static class BusinessBlinkBuilder
    {
        private static string CharacterName =>
            Environment.GetEnvironmentVariable("NPC_BASE_EYE_NAME") ?? "LittleMung_Business_Male";
        public static string Prefab => "Assets/Prefabs/Npc/Review/" + CharacterName + ".prefab";
        private static string Folder =>
            "Assets/Art/Characters/ReviewCorrections/ServiceNpcs/" + CharacterName + "/Face/Blink";
        private static string Output =>
            Environment.GetEnvironmentVariable("BUSINESS_BLINK_OUTPUT")
            ?? throw new InvalidOperationException("Set BUSINESS_BLINK_OUTPUT.");

        [Serializable]
        private sealed class Frame
        {
            public float weight;
            public Vector3[] vertices,
                normals;
        }

        [Serializable]
        private sealed class Part
        {
            public string name,
                channel,
                texture;
            public Vector3[] vertices,
                normals;
            public Vector2[] uv;
            public int[] triangles;
            public Frame[] frames;
        }

        [Serializable]
        private sealed class Data
        {
            public Part[] parts;
            public int[] bodyTriangles;
        }

        [Serializable]
        private sealed class Surface
        {
            public Vector3[] vertices,
                normals;
            public Vector2[] uv;
            public int[] triangles;
            public Vector3 headMin,
                headMax;
            public string texture;
        }

        public static void ExportRemaining() => ForRemaining(Export);

        public static void ApplyRemaining() => ForRemaining(Apply);

        private static void ForRemaining(Action action)
        {
            string output =
                Environment.GetEnvironmentVariable("NPC_BASE_EYES_OUTPUT")
                ?? throw new InvalidOperationException("Set NPC_BASE_EYES_OUTPUT.");
            foreach (string name in ServiceNpcReviewBuilder.Names.Skip(1))
            {
                Environment.SetEnvironmentVariable("NPC_BASE_EYE_NAME", name);
                Environment.SetEnvironmentVariable(
                    "BUSINESS_BLINK_OUTPUT",
                    Path.Combine(output, name)
                );
                action();
            }
        }

        public static void Export()
        {
            Directory.CreateDirectory(Output);
            var root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var skin = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(s => s.sharedMesh.vertexCount)
                    .First();
                if (File.Exists(Path.Combine(Output, "Surface.json")))
                    throw new IOException(
                        "Preserve the existing eye fitting source: " + CharacterName
                    );
                var mesh = skin.sharedMesh;
                var head = root.GetComponentInChildren<Animator>()
                    .GetBoneTransform(HumanBodyBones.Head);
                int index = Array.IndexOf(skin.bones, head);
                var weights = mesh.boneWeights;
                var points = mesh
                    .vertices.Where((v, i) => Weight(weights[i], index) > .8f)
                    .ToArray();
                var bounds = new Bounds(points[0], Vector3.zero);
                foreach (var point in points)
                    bounds.Encapsulate(point);
                var texture = skin.sharedMaterial.GetTexture("_BaseMap");
                File.WriteAllText(
                    Path.Combine(Output, "Surface.json"),
                    JsonUtility.ToJson(
                        new Surface
                        {
                            vertices = mesh.vertices,
                            normals = mesh.normals,
                            uv = mesh.uv,
                            triangles = mesh.triangles,
                            headMin = bounds.min,
                            headMax = bounds.max,
                            texture = AssetDatabase.GetAssetPath(texture),
                        }
                    )
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Render();
        }

        private static float Weight(BoneWeight w, int i) =>
            (w.boneIndex0 == i ? w.weight0 : 0)
            + (w.boneIndex1 == i ? w.weight1 : 0)
            + (w.boneIndex2 == i ? w.weight2 : 0)
            + (w.boneIndex3 == i ? w.weight3 : 0);

        public static void Apply()
        {
            var data = JsonUtility.FromJson<Data>(
                File.ReadAllText(Path.Combine(Output, "Blink.json"))
            );
            if (
                AssetDatabase.IsValidFolder(Folder)
                && Environment.GetEnvironmentVariable("BUSINESS_BLINK_REFRESH") != "1"
            )
                throw new IOException(
                    "Existing authored lids are preserved; set BUSINESS_BLINK_REFRESH=1 for an explicit refresh."
                );
            A305.EditorTools.Common.BuilderTools.EnsureFolder(Folder);
            foreach (string texture in data.parts.Select(p => p.texture).Distinct())
            {
                string path = Folder + "/" + texture;
                File.Copy(Path.Combine(Output, texture), path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var body = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(s => s.sharedMesh.vertexCount)
                    .First();
                int head = Array.FindIndex(body.bones, b => b.name == "Head");
                if (head < 0 || data.bodyTriangles == null || data.bodyTriangles.Length == 0)
                    throw new InvalidDataException(
                        "Base eye fitting requires a Head bone and a cut face surface."
                    );
                var surface = JsonUtility.FromJson<Surface>(
                    File.ReadAllText(Path.Combine(Output, "Surface.json"))
                );
                if (
                    body.sharedMesh.vertexCount != surface.vertices.Length
                    || body.sharedMesh.vertices.Zip(
                            surface.vertices,
                            (a, b) => (a - b).sqrMagnitude
                        )
                        .Max() > 1e-10f
                )
                    throw new InvalidDataException(
                        "The reviewed body has changed. Refit the Base eyes before applying."
                    );
                string bodyPath = Folder + "/FaceWithBaseEyes.asset";
                var cutBody = AssetDatabase.LoadAssetAtPath<Mesh>(bodyPath);
                if (cutBody == null)
                {
                    cutBody = Object.Instantiate(body.sharedMesh);
                    cutBody.name = "FaceWithBaseEyes";
                    AssetDatabase.CreateAsset(cutBody, bodyPath);
                }
                cutBody.triangles = data.bodyTriangles;
                EditorUtility.SetDirty(cutBody);
                body.sharedMesh = cutBody;
                foreach (var part in data.parts)
                {
                    string meshPath = Folder + "/" + part.name + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null)
                    {
                        mesh = new Mesh { name = part.name };
                        AssetDatabase.CreateAsset(mesh, meshPath);
                    }
                    // Modify native mesh buffers directly. CopySerialized does not reliably
                    // replace vertex/shape buffers on a previously imported Mesh asset.
                    mesh.Clear();
                    mesh.ClearBlendShapes();
                    mesh.vertices = part.vertices;
                    mesh.triangles = part.triangles;
                    mesh.uv = part.uv;
                    mesh.normals = part.normals;
                    mesh.boneWeights = Enumerable
                        .Repeat(new BoneWeight { boneIndex0 = head, weight0 = 1 }, mesh.vertexCount)
                        .ToArray();
                    mesh.bindposes = body.sharedMesh.bindposes;
                    foreach (var frame in part.frames)
                        mesh.AddBlendShapeFrame(
                            part.channel,
                            frame.weight,
                            frame.vertices.Zip(part.vertices, (a, b) => a - b).ToArray(),
                            frame.normals.Zip(part.normals, (a, b) => a - b).ToArray(),
                            null
                        );
                    mesh.RecalculateBounds();
                    EditorUtility.SetDirty(mesh);
                    string texPath = Folder + "/" + part.texture;
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    {
                        name = part.name,
                    };
                    mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
                    mat.SetFloat("_Smoothness", 0);
                    mat.SetFloat("_Cull", 0);
                    string matPath = Folder + "/" + part.name + ".mat";
                    var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (existingMaterial == null)
                        AssetDatabase.CreateAsset(mat, matPath);
                    else
                    {
                        EditorUtility.CopySerialized(mat, existingMaterial);
                        EditorUtility.SetDirty(existingMaterial);
                        Object.DestroyImmediate(mat);
                        mat = existingMaterial;
                    }
                    var found = body.transform.Find(part.name);
                    var child = found != null ? found.gameObject : new GameObject(part.name);
                    child.transform.SetParent(body.transform, false);
                    var skin = child.GetComponent<SkinnedMeshRenderer>();
                    if (skin == null)
                        skin = child.AddComponent<SkinnedMeshRenderer>();
                    skin.sharedMesh = mesh;
                    skin.sharedMaterial = mat;
                    skin.bones = body.bones;
                    skin.rootBone = body.rootBone;
                    skin.localBounds = body.localBounds;
                    skin.updateWhenOffscreen = true;
                    MatchFaceLighting(skin, body);
                    EditorUtility.SetDirty(mat);
                }
                if (root.GetComponent<CharacterIdleBlink>() == null)
                    root.AddComponent<CharacterIdleBlink>();
                root.GetComponent<CharacterFaceController>().SetPose(CharacterFacePose.Blink);
                root.GetComponent<CharacterFaceController>().SetPose(CharacterFacePose.Neutral);
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Render();
        }

        private static void MatchFaceLighting(SkinnedMeshRenderer skin, SkinnedMeshRenderer body)
        {
            // The inserted skin must darken with the face under hair and scene shadows.
            bool receives =
                body.receiveShadows
                && !body.sharedMaterial.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF");
            skin.receiveShadows = receives;
            skin.shadowCastingMode = body.shadowCastingMode;
            skin.sharedMaterial.SetFloat("_ReceiveShadows", receives ? 1 : 0);
            if (receives)
                skin.sharedMaterial.DisableKeyword("_RECEIVE_SHADOWS_OFF");
            else
                skin.sharedMaterial.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        }

        public static void RepairEyeLighting()
        {
            string output = Output;
            string previousName = Environment.GetEnvironmentVariable("NPC_BASE_EYE_NAME");
            try
            {
                foreach (string name in ServiceNpcReviewBuilder.Names)
                {
                    Environment.SetEnvironmentVariable("NPC_BASE_EYE_NAME", name);
                    Environment.SetEnvironmentVariable(
                        "BUSINESS_BLINK_OUTPUT",
                        Path.Combine(output, name)
                    );
                    RenderReview("LightingBefore", true);
                    var root = PrefabUtility.LoadPrefabContents(Prefab);
                    try
                    {
                        var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                        var body = skins.OrderByDescending(s => s.sharedMesh.vertexCount).First();
                        foreach (
                            var skin in skins.Where(s =>
                                s != body
                                && AssetDatabase
                                    .GetAssetPath(s.sharedMesh)
                                    .StartsWith(Folder + "/", StringComparison.Ordinal)
                            )
                        )
                        {
                            MatchFaceLighting(skin, body);
                            EditorUtility.SetDirty(skin.sharedMaterial);
                        }
                        PrefabUtility.SaveAsPrefabAsset(root, Prefab);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                    AssetDatabase.SaveAssets();
                    RenderReview("LightingAfter", true);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("NPC_BASE_EYE_NAME", previousName);
                Environment.SetEnvironmentVariable("BUSINESS_BLINK_OUTPUT", output);
            }
        }

        public static void Render() => RenderReview(null, false);

        private static void RenderReview(string stageOverride, bool shadowCheck)
        {
            EditorSceneManager.OpenScene(
                "Assets/Scenes/Sandbox/NpcReview/" + CharacterName + "_Face.unity"
            );
            var face = Object.FindFirstObjectByType<CharacterFaceController>();
            var camera = Object.FindFirstObjectByType<Camera>();
            string stage =
                stageOverride ?? (face.SupportsPose(CharacterFacePose.Blink) ? "After" : "Before");
            Directory.CreateDirectory(Path.Combine(Output, stage));
            var rt = new RenderTexture(640, 640, 24);
            camera.targetTexture = rt;
            var previous = RenderTexture.active;
            var origin = camera.transform.position;
            var focus = CharacterHeadProportions.HeadBounds(face.gameObject).center;
            var timing = ScriptableObject.CreateInstance<SpeechExpressionProfile>();
            GameObject shadowCaster = null;
            if (shadowCheck)
            {
                // Reproduce a tree or building shadow without saving objects into the scene.
                var light = Object
                    .FindObjectsByType<Light>(FindObjectsSortMode.None)
                    .First(l => l.type == LightType.Directional);
                light.shadows = LightShadows.Hard;
                light.shadowBias = .02f;
                light.shadowNormalBias = .1f;
                light.transform.rotation = Quaternion.Euler(30, 70, 0);
                shadowCaster = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shadowCaster.name = "눈 접합부 그림자 검수";
                shadowCaster.transform.position = focus - light.transform.forward * .6f;
                shadowCaster.transform.rotation = light.transform.rotation;
                shadowCaster.transform.localScale = new Vector3(.35f, 1.5f, .05f);
                shadowCaster.GetComponent<Renderer>().shadowCastingMode = UnityEngine
                    .Rendering
                    .ShadowCastingMode
                    .ShadowsOnly;
            }
            try
            {
                for (int i = 0; i < 120; i++)
                {
                    int frame = i % 40;
                    int view = i / 40;
                    camera.transform.position =
                        focus
                        + Quaternion.AngleAxis(
                            view == 0 ? 0
                                : view == 1 ? -30
                                : 30,
                            Vector3.up
                        ) * (origin - focus);
                    camera.transform.LookAt(focus);
                    float blink = timing.Sample(1.1f + frame * .0125f, 10f).Blink;
                    face.SetPose(CharacterFacePose.Neutral);
                    if (face.SupportsPose(CharacterFacePose.Blink))
                        face.ApplySpeech(
                            SpeechMouthSample.Pose(
                                CharacterFacePose.OpenMouth,
                                .25f + .2f * Mathf.Sin(frame * .25f)
                            ),
                            .15f,
                            blink
                        );
                    var skins = face.GetComponentsInChildren<SkinnedMeshRenderer>();
                    if (i == 19)
                        File.WriteAllLines(
                            Path.Combine(Output, "RenderAudit.txt"),
                            skins.Select(s =>
                                s.name
                                + " | "
                                + AssetDatabase.GetAssetPath(s.sharedMesh)
                                + " | vertices "
                                + s.sharedMesh.vertexCount
                                + " | "
                                + string.Join(
                                    ",",
                                    Enumerable
                                        .Range(0, s.sharedMesh.blendShapeCount)
                                        .Select(b =>
                                            s.sharedMesh.GetBlendShapeName(b)
                                            + "="
                                            + s.GetBlendShapeWeight(b)
                                        )
                                )
                            )
                        );
                    var previews = new System.Collections.Generic.List<GameObject>();
                    var meshes = new System.Collections.Generic.List<Mesh>();
                    foreach (var skin in skins)
                    {
                        var baked = new Mesh();
                        skin.BakeMesh(baked, true);
                        meshes.Add(baked);
                        var preview = new GameObject("Blink render");
                        preview.transform.SetParent(skin.transform, false);
                        preview.AddComponent<MeshFilter>().sharedMesh = baked;
                        var renderer = preview.AddComponent<MeshRenderer>();
                        renderer.sharedMaterials = skin.sharedMaterials;
                        renderer.shadowCastingMode = skin.shadowCastingMode;
                        renderer.receiveShadows = skin.receiveShadows;
                        previews.Add(preview);
                        skin.enabled = false;
                    }
                    camera.Render();
                    foreach (var preview in previews)
                        Object.DestroyImmediate(preview);
                    foreach (var mesh in meshes)
                        Object.DestroyImmediate(mesh);
                    foreach (var skin in skins)
                        skin.enabled = true;
                    RenderTexture.active = rt;
                    var texture = new Texture2D(640, 640, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(
                        Path.Combine(Output, stage, i.ToString("D3") + ".png"),
                        texture.EncodeToPNG()
                    );
                    Object.DestroyImmediate(texture);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(timing);
                if (shadowCaster != null)
                    Object.DestroyImmediate(shadowCaster);
            }
        }
    }
}
