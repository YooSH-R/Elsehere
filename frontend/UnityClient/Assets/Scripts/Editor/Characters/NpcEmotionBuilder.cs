using System;
using System.IO;
using System.Linq;
using A305.Characters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static partial class NpcEmotionBuilder
    {
        public static readonly string[] Names =
        {
            "LittleMung_Business_Male",
            "LittleMung_MiddleAge_Male",
            "LittleMung_MiddleAge_Female",
            "LittleMung_BlackYoung_Male",
            "LittleMung_Grandmother",
            "LittleMung_HotelStaff_male",
        };

        public static string PrefabPath(string name) =>
            name == "LittleMung_Cafe_Female" ? CafeDialogueBuilder.PrefabPath
            : name == "LittleMung_HotelStaff_male" ? HotelierFaceBuilder.PrefabPath
            : "Assets/Prefabs/Npc/Review/" + name + ".prefab";

        private static string Output =>
            Environment.GetEnvironmentVariable("NPC_EMOTION_BATCH_OUTPUT")
            ?? throw new InvalidOperationException("NPC_EMOTION_BATCH_OUTPUT을 지정하세요.");

        [Serializable]
        public sealed class Shape
        {
            public string name;
            public float weight;
            public Vector3[] deltaVertices,
                deltaNormals;
        }

        [Serializable]
        public sealed class Surface
        {
            public string name,
                path,
                meshPath,
                texture;
            public Vector3[] vertices,
                normals;
            public Vector2[] uv;
            public int[] triangles;
            public Shape[] shapes;
        }

        [Serializable]
        public sealed class Source
        {
            public string name,
                prefab,
                body;
            public Surface[] meshes;
        }

        [Serializable]
        private sealed class EyeSource
        {
            public Vector3[] vertices,
                normals;
            public Vector2[] uv;
            public int[] triangles;
            public Vector3 headMin,
                headMax;
            public string texture;
        }

        public static void Export()
        {
            foreach (string name in Names)
            {
                string folder = Path.Combine(Output, name);
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "Source.json");
                if (File.Exists(path))
                    throw new IOException("이미 저장한 원본 검수 자료를 보존합니다: " + path);
                var root = PrefabUtility.LoadPrefabContents(PrefabPath(name));
                try
                {
                    var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var body = skins.OrderByDescending(s => s.sharedMesh.vertexCount).First();
                    var source = new Source
                    {
                        name = name,
                        prefab = PrefabPath(name),
                        body = AnimationUtility.CalculateTransformPath(
                            body.transform,
                            root.transform
                        ),
                        meshes = skins.Select(s => ExportSurface(root, body, s)).ToArray(),
                    };
                    File.WriteAllText(path, JsonUtility.ToJson(source));
                    if (skins.All(s => s.name != "LeftEye"))
                    {
                        var mesh = body.sharedMesh;
                        int head = Array.FindIndex(body.bones, b => b.name == "Head");
                        if (head < 0)
                            throw new InvalidOperationException("Head 본이 없습니다: " + name);
                        var weights = mesh.boneWeights;
                        var points = mesh
                            .vertices.Where((v, i) => HeadWeight(weights[i], head) > .8f)
                            .ToArray();
                        var bounds = new Bounds(points[0], Vector3.zero);
                        foreach (var point in points)
                            bounds.Encapsulate(point);
                        string eyeFolder = Path.Combine(Output, "Eyes", name);
                        Directory.CreateDirectory(eyeFolder);
                        File.WriteAllText(
                            Path.Combine(eyeFolder, "Surface.json"),
                            JsonUtility.ToJson(
                                new EyeSource
                                {
                                    vertices = mesh.vertices,
                                    normals = mesh.normals,
                                    uv = mesh.uv,
                                    triangles = mesh.triangles,
                                    headMin = bounds.min,
                                    headMax = bounds.max,
                                    texture = Path.GetFullPath(
                                        AssetDatabase.GetAssetPath(
                                            body.sharedMaterial.GetTexture("_BaseMap")
                                        )
                                    ),
                                }
                            )
                        );
                    }
                    Debug.Log(name + ": 원본 표면·표정 채널 내보내기 완료.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static float HeadWeight(BoneWeight weight, int head) =>
            (weight.boneIndex0 == head ? weight.weight0 : 0)
            + (weight.boneIndex1 == head ? weight.weight1 : 0)
            + (weight.boneIndex2 == head ? weight.weight2 : 0)
            + (weight.boneIndex3 == head ? weight.weight3 : 0);

        private static Surface ExportSurface(
            GameObject root,
            SkinnedMeshRenderer body,
            SkinnedMeshRenderer skin
        )
        {
            if (
                Vector3.Distance(body.transform.position, skin.transform.position) > .00001f
                || Quaternion.Angle(body.transform.rotation, skin.transform.rotation) > .001f
                || Vector3.Distance(body.transform.lossyScale, skin.transform.lossyScale) > .00001f
            )
                throw new InvalidOperationException(
                    "얼굴 부품의 로컬 좌표계가 다릅니다: " + skin.name
                );
            var mesh = skin.sharedMesh;
            var shapes = new System.Collections.Generic.List<Shape>();
            for (int i = 0; i < mesh.blendShapeCount; i++)
            for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(i); frame++)
            {
                var delta = new Vector3[mesh.vertexCount];
                var normals = new Vector3[mesh.vertexCount];
                mesh.GetBlendShapeFrameVertices(i, frame, delta, normals, null);
                shapes.Add(
                    new Shape
                    {
                        name = mesh.GetBlendShapeName(i),
                        weight = mesh.GetBlendShapeFrameWeight(i, frame),
                        deltaVertices = delta,
                        deltaNormals = normals,
                    }
                );
            }
            var texture = skin.sharedMaterial.GetTexture("_BaseMap");
            return new Surface
            {
                name = skin.name,
                path = AnimationUtility.CalculateTransformPath(skin.transform, root.transform),
                meshPath = AssetDatabase.GetAssetPath(mesh),
                vertices = mesh.vertices,
                normals = mesh.normals,
                uv = mesh.uv,
                triangles = mesh.triangles,
                shapes = shapes.ToArray(),
                texture =
                    texture != null ? Path.GetFullPath(AssetDatabase.GetAssetPath(texture)) : "",
            };
        }
    }
}
