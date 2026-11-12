using System;
using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using A305.Npc;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>햄버거 가게의 두 직원에게만 모자와 몸 움직임을 따르는 앞치마를 입힌다.</summary>
    public static class BurgerStaffUniformBuilder
    {
        private const string Folder = "Assets/Art/Characters/BurgerStaffUniform";
        private const string ScenePath = "Assets/Scenes/ShopInteriors/MapleDinerInterior.unity";
        private const string CapName = "BurgerStaffCap";
        private const string ApronName = "BurgerStaffApron";
        internal static readonly string[] StaffCodes = { "DINER_STAFF", "FIRE_CAPTAIN" };
        private static readonly HumanBodyBones[] ApronBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
        };

        [MenuItem("Tools/A305/Npc/Apply Burger Staff Uniforms")]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            foreach (string code in StaffCodes)
                PrepareAssets(code);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (NpcIdentity npc in BuilderTools.FindAllInScene<NpcIdentity>(scene))
                if (StaffCodes.Contains(npc.Code))
                    NpcCastDresser.Dress(npc.gameObject, NpcCast.Find(npc.Code));
            BuilderTools.SaveScene(scene, ScenePath);
            foreach (string code in StaffCodes)
            {
                string path = "Assets/Prefabs/Npc/Commuters/" + code + ".prefab";
                GameObject commuter = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    NpcCastDresser.Dress(commuter, NpcCast.Find(code));
                    PrefabUtility.SaveAsPrefabAsset(commuter, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(commuter);
                }
            }
            AssetDatabase.SaveAssets();
        }

        public static void Dress(GameObject npc, string code)
        {
            if (!StaffCodes.Contains(code))
                return;
            PrepareAssets(code);
            Animator animator = npc.GetComponentInChildren<Animator>(true);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform cap = FindOrCreate(head, CapName);
            BuilderTools.GetOrAddComponent<MeshFilter>(cap.gameObject).sharedMesh =
                AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/" + code + "_Cap.asset");
            BuilderTools.GetOrAddComponent<MeshRenderer>(cap.gameObject).sharedMaterials = new[]
            {
                Fabric("Red"),
                Fabric("Charcoal"),
                Fabric("Gold"),
                Fabric("SeamRed"),
            };
            var body = npc.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .OrderByDescending(renderer => renderer.sharedMesh.vertexCount)
                .First();
            body.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                Folder + "/" + code + "_BodyUnderCap.asset"
            );
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);

            Transform apron = FindOrCreate(animator.transform, ApronName);
            var skin = BuilderTools.GetOrAddComponent<SkinnedMeshRenderer>(apron.gameObject);
            skin.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                Folder + "/" + code + "_Apron.asset"
            );
            skin.bones = ApronBones.Select(animator.GetBoneTransform).ToArray();
            skin.rootBone = skin.bones[0];
            skin.sharedMaterials = new[] { Fabric("Charcoal"), Fabric("Gold") };
            skin.localBounds = skin.sharedMesh.bounds;
        }

        private static Transform FindOrCreate(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            return child;
        }

        private static Material Fabric(string name)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            Color color =
                name == "Red" ? new Color(.72f, .035f, .045f)
                : name == "SeamRed" ? new Color(.46f, .022f, .03f)
                : name == "Gold" ? new Color(1f, .69f, .18f)
                : new Color(.055f, .065f, .075f);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .16f);
            material.SetFloat("_Cull", 0);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        [MenuItem("Tools/A305/Npc/Refit Burger Staff Uniform Meshes")]
        public static void RefitMeshes()
        {
            BuilderTools.ThrowIfPlaying();
            foreach (string code in StaffCodes)
                PrepareAssets(code, true);
            AssetDatabase.SaveAssets();
        }

        private static void PrepareAssets(string code, bool refit = false)
        {
            string capPath = Folder + "/" + code + "_Cap.asset";
            string apronPath = Folder + "/" + code + "_Apron.asset";
            string bodyPath = Folder + "/" + code + "_BodyUnderCap.asset";
            if (
                !refit
                && AssetDatabase.LoadAssetAtPath<Mesh>(capPath) != null
                && AssetDatabase.LoadAssetAtPath<Mesh>(apronPath) != null
                && AssetDatabase.LoadAssetAtPath<Mesh>(bodyPath) != null
            )
                return;
            BuilderTools.EnsureFolder(Folder);
            var source = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(NpcCast.Find(code).Look.PrefabPath)
                );
            var baked = new Mesh();
            try
            {
                Animator animator = source.GetComponentInChildren<Animator>();
                var body = source
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(skin => skin.sharedMesh.vertexCount)
                    .First();
                body.BakeMesh(baked, true);
                Vector3 forward = BuilderTools.GetCharacterViewDirection(source, 0);
                forward.y = 0;
                Quaternion facing = Quaternion.LookRotation(forward.normalized);
                Quaternion inverse = Quaternion.Inverse(facing);
                Vector3[] points = baked
                    .vertices.Select(vertex => inverse * body.transform.TransformPoint(vertex))
                    .ToArray();
                baked.vertices = points;
                baked.RecalculateBounds();
                Bounds bounds = baked.bounds;
                var fit = new CapFit(bounds, points);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (refit || AssetDatabase.LoadAssetAtPath<Mesh>(capPath) == null)
                    SaveMesh(
                        CreateCap(fit, point => head.InverseTransformPoint(facing * point)),
                        capPath
                    );
                if (refit || AssetDatabase.LoadAssetAtPath<Mesh>(bodyPath) == null)
                    SaveMesh(FitHairUnderCap(body, points, facing, fit), bodyPath);
                if (refit || AssetDatabase.LoadAssetAtPath<Mesh>(apronPath) == null)
                    SaveMesh(CreateApron(animator, body, points, bounds, facing), apronPath);
            }
            finally
            {
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(source);
            }
        }

        private static void SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
                AssetDatabase.CreateAsset(mesh, path);
            else
            {
                // Mesh의 네이티브 정점 버퍼도 갱신한다. 직렬화 필드만 복사하면 이전 핏이 남는다.
                existing.Clear();
                existing.ClearBlendShapes();
                existing.indexFormat = mesh.indexFormat;
                existing.vertices = mesh.vertices;
                existing.normals = mesh.normals;
                existing.tangents = mesh.tangents;
                existing.uv = mesh.uv;
                existing.uv2 = mesh.uv2;
                existing.colors32 = mesh.colors32;
                existing.bindposes = mesh.bindposes;
                existing.boneWeights = mesh.boneWeights;
                existing.subMeshCount = mesh.subMeshCount;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    existing.SetTriangles(mesh.GetTriangles(i), i);
                var positionDeltas = new Vector3[mesh.vertexCount];
                var normalDeltas = new Vector3[mesh.vertexCount];
                var tangentDeltas = new Vector3[mesh.vertexCount];
                for (int shape = 0; shape < mesh.blendShapeCount; shape++)
                for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(shape); frame++)
                {
                    mesh.GetBlendShapeFrameVertices(
                        shape,
                        frame,
                        positionDeltas,
                        normalDeltas,
                        tangentDeltas
                    );
                    existing.AddBlendShapeFrame(
                        mesh.GetBlendShapeName(shape),
                        mesh.GetBlendShapeFrameWeight(shape, frame),
                        positionDeltas,
                        normalDeltas,
                        tangentDeltas
                    );
                }
                existing.RecalculateBounds();
                existing.UploadMeshData(false);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
            }
        }

        private sealed class CapFit
        {
            public readonly float Height;
            public readonly Vector3 Center;
            public readonly float RadiusX;
            public readonly float RadiusZ;
            public readonly float Top;
            private readonly float frontRim;
            private readonly float backRim;

            public CapFit(Bounds body, Vector3[] surface)
            {
                Height = body.size.y;
                Vector3[] upperHead = surface
                    .Where(point => point.y > body.min.y + Height * .72f)
                    .ToArray();
                var headBounds = new Bounds(upperHead[0], Vector3.zero);
                foreach (Vector3 point in upperHead)
                    headBounds.Encapsulate(point);
                Center = headBounds.center;
                RadiusX = headBounds.extents.x * .93f;
                RadiusZ = headBounds.extents.z * .94f;
                Top = body.max.y + Height * .008f;
                frontRim = body.min.y + Height * .80f;
                backRim = body.min.y + Height * .70f;
            }

            public float RimHeight(float angle) =>
                Mathf.Lerp(backRim, frontRim, (Mathf.Cos(angle) + 1) * .5f);

            public Vector3 CrownPoint(float angle, float down, float offset = 0)
            {
                return new Vector3(
                    Center.x + Mathf.Sin(angle) * Mathf.Sin(down) * (RadiusX + offset),
                    Mathf.Lerp(RimHeight(angle), Top + offset, Mathf.Cos(down)),
                    Center.z + Mathf.Cos(angle) * Mathf.Sin(down) * (RadiusZ + offset)
                );
            }

            public Vector3 FitHair(Vector3 point)
            {
                float x = (point.x - Center.x) / RadiusX;
                float z = (point.z - Center.z) / RadiusZ;
                float radius = Mathf.Sqrt(x * x + z * z);
                float angle = Mathf.Atan2(x, z);
                float rim = RimHeight(angle);
                // 긴 뒷머리는 넓은 구간에 걸쳐 눌러 급하게 꺾이는 경계를 만들지 않는다.
                // 얼굴과 귀가 있는 앞·옆은 모자 밑단 가까이에서만 맞춘다.
                float rear = Mathf.SmoothStep(
                    0,
                    1,
                    Mathf.InverseLerp(.15f, -.65f, Mathf.Cos(angle))
                );
                float fittedDepth = Height * Mathf.Lerp(.025f, .14f, rear);
                float blendDepth = fittedDepth + Height * Mathf.Lerp(.04f, .22f, rear);
                if (point.y < rim - blendDepth || radius < .001f)
                    return point;
                float elevation = Mathf.Clamp01((point.y - rim) / (Top - rim));
                float allowedRadius = Mathf.Sqrt(1 - elevation * elevation) * .96f;
                float compression = Mathf.Min(1, allowedRadius / radius);
                float blend = Mathf.SmoothStep(
                    0,
                    1,
                    Mathf.InverseLerp(rim - blendDepth, rim - fittedDepth, point.y)
                );
                compression = Mathf.Lerp(1, compression, blend);
                return new Vector3(
                    Center.x + (point.x - Center.x) * compression,
                    point.y,
                    Center.z + (point.z - Center.z) * compression
                );
            }
        }

        private static Mesh FitHairUnderCap(
            SkinnedMeshRenderer body,
            Vector3[] surface,
            Quaternion facing,
            CapFit fit
        )
        {
            // 원본 외형은 유지하고, 이 두 직원에게만 모자 안쪽 머리카락을 눌러 쓴 사본을 연결한다.
            var mesh = Object.Instantiate(body.sharedMesh);
            mesh.name = "BodyUnderCap";
            Vector3[] vertices = mesh.vertices;
            Vector3[] originalNormals = mesh.normals;
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4[] bindposes = mesh.bindposes;
            Transform[] bones = body.bones;
            var moved = new bool[vertices.Length];
            for (int vertex = 0; vertex < vertices.Length; vertex++)
            {
                Vector3 delta = facing * (fit.FitHair(surface[vertex]) - surface[vertex]);
                if (delta.sqrMagnitude < .00000001f)
                    continue;
                BoneWeight weight = weights[vertex];
                Matrix4x4 skin = Matrix4x4.zero;
                void AddBone(int index, float strength)
                {
                    if (strength <= 0)
                        return;
                    Matrix4x4 bone = bones[index].localToWorldMatrix * bindposes[index];
                    for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 4; column++)
                        skin[row, column] += bone[row, column] * strength;
                }
                AddBone(weight.boneIndex0, weight.weight0);
                AddBone(weight.boneIndex1, weight.weight1);
                AddBone(weight.boneIndex2, weight.weight2);
                AddBone(weight.boneIndex3, weight.weight3);
                vertices[vertex] += skin.inverse.MultiplyVector(delta);
                moved[vertex] = true;
            }
            mesh.vertices = vertices;
            var check = new Mesh();
            Mesh originalMesh = body.sharedMesh;
            body.sharedMesh = mesh;
            body.BakeMesh(check, true);
            Vector3[] checkVertices = check.vertices;
            float maximumError = 0;
            for (int vertex = 0; vertex < vertices.Length; vertex++)
                maximumError = Mathf.Max(
                    maximumError,
                    Vector3.Distance(
                        body.transform.TransformPoint(checkVertices[vertex]),
                        facing * fit.FitHair(surface[vertex])
                    )
                );
            body.sharedMesh = originalMesh;
            Object.DestroyImmediate(check);
            if (maximumError > .001f)
                throw new InvalidOperationException(
                    "모자 착용용 머리카락과 본의 배율이 맞지 않습니다."
                );
            mesh.RecalculateNormals();
            Vector3[] fittedNormals = mesh.normals;
            for (int vertex = 0; vertex < vertices.Length; vertex++)
                if (!moved[vertex])
                    fittedNormals[vertex] = originalNormals[vertex];
            mesh.normals = fittedNormals;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateCap(CapFit fit, Func<Vector3, Vector3> toLocal)
        {
            var cap = new SurfaceMesh(4, toLocal);
            const int crownSegments = 48;
            int[] rim = cap.Grid(
                crownSegments,
                24,
                0,
                (u, v) => fit.CrownPoint(u * Mathf.PI * 2, v * Mathf.PI * .5f)
            );

            for (int panel = 0; panel < 6; panel++)
            {
                float angle = (panel + .5f) * Mathf.PI / 3;
                cap.Grid(
                    2,
                    24,
                    3,
                    (u, v) =>
                        fit.CrownPoint(
                            angle + (u - .5f) * .006f,
                            Mathf.Lerp(.04f, Mathf.PI * .5f, v),
                            fit.Height * .0008f
                        )
                );
            }

            const int visorSegments = 16;
            var visorRoot = new int[visorSegments + 1];
            for (int column = 0; column <= visorSegments; column++)
                visorRoot[column] = rim[
                    (column - visorSegments / 2 + crownSegments) % crownSegments
                ];

            Vector3 VisorPoint(float u, float v)
            {
                float angle = Mathf.Lerp(-Mathf.PI / 3, Mathf.PI / 3, u);
                Vector3 root = fit.CrownPoint(angle, Mathf.PI * .5f);
                float distanceFromCenter = Mathf.Abs(u * 2 - 1);
                // 앞쪽 폭을 유지하고 끝부분에서만 좁혀 둥근 부채꼴처럼 보이지 않게 한다.
                float taper = Mathf.Sqrt(1 - Mathf.Pow(distanceFromCenter, 4));
                return root
                    + new Vector3(
                        (fit.Center.x - root.x) * v * .035f,
                        fit.Height * (.009f * Mathf.Sin(v * Mathf.PI) - .022f * v * v) * taper,
                        fit.Height * .19f * v * taper
                    );
            }
            // 챙 첫 줄과 크라운 밑단이 같은 정점을 쓰므로 연결부가 벌어지지 않는다.
            cap.Grid(visorSegments, 12, 0, VisorPoint, visorRoot);
            float thickness = fit.Height * .004f;
            cap.Grid(visorSegments, 12, 1, (u, v) => VisorPoint(1 - u, v) - Vector3.up * thickness);
            cap.Grid(
                visorSegments,
                2,
                0,
                (u, v) => VisorPoint(u, 1) - Vector3.up * (v * thickness)
            );

            cap.Grid(
                crownSegments,
                2,
                3,
                (u, v) =>
                {
                    Vector3 edge = fit.CrownPoint(u * Mathf.PI * 2, Mathf.PI * .5f);
                    return edge
                        + new Vector3(
                            (fit.Center.x - edge.x) * v * .014f,
                            -fit.Height * .012f * v,
                            (fit.Center.z - edge.z) * v * .014f
                        );
                },
                rim
            );
            cap.Ellipsoid(
                fit.CrownPoint(0, 0),
                new Vector3(fit.Height * .015f, fit.Height * .007f, fit.Height * .015f),
                0
            );
            // 표식도 크라운 곡면을 따라가게 하여 떠 있는 단추처럼 보이지 않게 한다.
            cap.Grid(
                32,
                5,
                2,
                (u, v) =>
                {
                    float angle = u * Mathf.PI * 2;
                    return fit.CrownPoint(
                        Mathf.Cos(angle) * v * fit.Height * .032f / fit.RadiusX,
                        Mathf.Acos(.48f) - Mathf.Sin(angle) * v * .07f,
                        fit.Height * .002f
                    );
                }
            );
            return cap.Build("BurgerStaffCap");
        }

        private static Mesh CreateApron(
            Animator animator,
            SkinnedMeshRenderer body,
            Vector3[] fittedVertices,
            Bounds bounds,
            Quaternion facing
        )
        {
            float height = bounds.size.y;
            Transform[] bones = ApronBones.Select(animator.GetBoneTransform).ToArray();
            var torsoBones = new HashSet<Transform>(bones)
            {
                animator.GetBoneTransform(HumanBodyBones.Neck),
            };
            Transform[] bodyBones = body.bones;
            bool IsTorsoBone(int index) => torsoBones.Contains(bodyBones[index]);
            BoneWeight[] bodyWeights = body.sharedMesh.boneWeights;
            // 허리 옆에 내려온 손과 팔은 옷 둘레 측정에서 제외한다.
            Vector3[] fittedBody = fittedVertices
                .Where(
                    (point, index) =>
                    {
                        BoneWeight weight = bodyWeights[index];
                        float torsoWeight =
                            (IsTorsoBone(weight.boneIndex0) ? weight.weight0 : 0)
                            + (IsTorsoBone(weight.boneIndex1) ? weight.weight1 : 0)
                            + (IsTorsoBone(weight.boneIndex2) ? weight.weight2 : 0)
                            + (IsTorsoBone(weight.boneIndex3) ? weight.weight3 : 0);
                        return torsoWeight > .5f;
                    }
                )
                .ToArray();
            Vector3 hips = Quaternion.Inverse(facing) * bones[0].position;
            float centerX = hips.x;
            float bottom = bounds.min.y + height * .155f;
            float waist = hips.y + height * .01f;
            float top = bones[3].position.y + height * .045f;
            float halfWidth = height * .092f;
            Vector3 neck =
                Quaternion.Inverse(facing)
                * animator.GetBoneTransform(HumanBodyBones.Neck).position;
            neck.y += height * .006f;
            var surface = new ApronFitSurface(
                fittedBody,
                height,
                centerX,
                halfWidth,
                bottom,
                neck.y
            );
            // 자유롭게 늘어지는 아랫단에는 보행 중 바지 주름을 덮을 여유를 둔다.
            float SurfaceZ(float x, float y) =>
                surface.Front(x, y) + height * .006f * Mathf.InverseLerp(waist, bottom, y);
            var apron = new SurfaceMesh(
                2,
                point => animator.transform.InverseTransformPoint(facing * point),
                reverseFaces: true
            );
            apron.Grid(
                18,
                24,
                0,
                (u, v) =>
                {
                    float y = Mathf.Lerp(bottom, top, v);
                    float width =
                        y > waist
                            ? Mathf.Lerp(halfWidth, halfWidth * .57f, (y - waist) / (top - waist))
                            : halfWidth * Mathf.Lerp(.94f, 1f, Mathf.Clamp01(v * 16));
                    float x = centerX + (u * 2 - 1) * width;
                    return new Vector3(x, y, SurfaceZ(x, y));
                }
            );
            var belt = new ApronBandFit(
                fittedBody,
                new Vector3(centerX, waist, hips.z),
                height * .13f,
                height * .012f,
                height * .004f
            );
            apron.Grid(
                64,
                2,
                0,
                (u, v) => belt.Point(u * Mathf.PI * 2) + Vector3.up * ((v - .5f) * height * .014f)
            );
            var collar = new ApronBandFit(
                fittedBody,
                neck,
                height * .075f,
                height * .008f,
                height * .003f,
                neck.z - height * .055f
            );
            const float collarEndAngle = Mathf.PI / 4;
            Vector3 CollarPoint(float angle, float width)
            {
                return collar.Point(angle) + Vector3.up * ((width - .5f) * height * .012f);
            }
            apron.Grid(
                48,
                2,
                0,
                (u, v) =>
                    CollarPoint(Mathf.Lerp(collarEndAngle, 2 * Mathf.PI - collarEndAngle, u), v)
            );
            foreach (float side in new[] { -1f, 1f })
                apron.Grid(
                    4,
                    12,
                    0,
                    (u, v) =>
                    {
                        float x = centerX + side * halfWidth * .52f + (u - .5f) * height * .012f;
                        float y = top - height * .012f;
                        var start = new Vector3(x, y, SurfaceZ(x, y));
                        Vector3 end = CollarPoint(side * collarEndAngle + (u - .5f) * .16f, .5f);
                        Vector3 point = Vector3.Lerp(start, end, v);
                        float outsideShirt = Mathf.Max(point.z, SurfaceZ(point.x, point.y));
                        point.z = Mathf.Lerp(point.z, outsideShirt, Mathf.Sin(v * Mathf.PI));
                        return point;
                    }
                );
            float pocketY = waist - height * .017f;
            apron.Grid(
                12,
                4,
                1,
                (u, v) =>
                {
                    float x = centerX + (u - .5f) * halfWidth;
                    float y = pocketY + v * height * .006f;
                    return new Vector3(x, y, SurfaceZ(x, y) + height * .001f);
                }
            );
            float badgeY = top - height * .025f;
            apron.Grid(
                6,
                3,
                1,
                (u, v) =>
                {
                    float x = centerX + (u - .5f) * height * .054f;
                    float y = badgeY + v * height * .014f;
                    return new Vector3(x, y, SurfaceZ(x, y) + height * .001f);
                }
            );
            Mesh mesh = apron.Build("BurgerStaffApron");
            mesh.bindposes = bones
                .Select(bone => bone.worldToLocalMatrix * animator.transform.localToWorldMatrix)
                .ToArray();
            mesh.boneWeights = mesh
                .vertices.Select(vertex =>
                {
                    Vector3 point =
                        Quaternion.Inverse(facing) * animator.transform.TransformPoint(vertex);
                    float y = point.y;
                    if (y < hips.y)
                    {
                        // 걷는 동안 허벅지가 앞치마를 뚫지 않도록 아랫단만 다리 움직임을 함께 따른다.
                        float legWeight = Mathf.InverseLerp(hips.y, bottom, y);
                        float rightWeight = Mathf.InverseLerp(
                            centerX - halfWidth * .25f,
                            centerX + halfWidth * .25f,
                            point.x
                        );
                        return new BoneWeight
                        {
                            boneIndex0 = 0,
                            weight0 = 1 - legWeight,
                            boneIndex1 = 4,
                            weight1 = legWeight * (1 - rightWeight),
                            boneIndex2 = 5,
                            weight2 = legWeight * rightWeight,
                        };
                    }
                    int lower = 0;
                    while (lower < 2 && y > bones[lower + 1].position.y)
                        lower++;
                    float blend = Mathf.InverseLerp(
                        bones[lower].position.y,
                        bones[lower + 1].position.y,
                        y
                    );
                    return new BoneWeight
                    {
                        boneIndex0 = lower,
                        weight0 = 1 - blend,
                        boneIndex1 = lower + 1,
                        weight1 = blend,
                    };
                })
                .ToArray();
            return mesh;
        }

        private sealed class ApronFitSurface
        {
            private readonly float[] frontRows;
            private readonly float centerX;
            private readonly float bottom;
            private readonly float top;
            private readonly float curvature;

            public ApronFitSurface(
                Vector3[] body,
                float height,
                float centerX,
                float halfWidth,
                float bottom,
                float top
            )
            {
                this.centerX = centerX;
                this.bottom = bottom;
                this.top = top;
                curvature = height * .02f / (halfWidth * halfWidth);
                frontRows = new float[12];
                for (int row = 0; row < frontRows.Length; row++)
                {
                    float y = Mathf.Lerp(bottom, top, (float)row / (frontRows.Length - 1));
                    frontRows[row] =
                        body.Where(point =>
                                Mathf.Abs(point.y - y) < height * .012f
                                && Mathf.Abs(point.x - centerX) <= halfWidth
                            )
                            .Max(point =>
                                point.z + curvature * (point.x - centerX) * (point.x - centerX)
                            )
                        + height * .003f;
                }
                // 작은 주름과 옷자락의 움푹한 곳을 복제하지 않고, 천이 바깥쪽을 이어 덮게 한다.
                for (int pass = 0; pass < 2; pass++)
                {
                    float[] previous = (float[])frontRows.Clone();
                    for (int row = 1; row < frontRows.Length - 1; row++)
                        frontRows[row] = Mathf.Max(
                            previous[row],
                            (previous[row - 1] + 2 * previous[row] + previous[row + 1]) * .25f
                        );
                }
            }

            public float Front(float x, float y)
            {
                float row = Mathf.InverseLerp(bottom, top, y) * (frontRows.Length - 1);
                int lower = Mathf.Min(Mathf.FloorToInt(row), frontRows.Length - 2);
                float center = Mathf.Lerp(frontRows[lower], frontRows[lower + 1], row - lower);
                float side = x - centerX;
                return center - curvature * side * side;
            }
        }

        private sealed class ApronBandFit
        {
            private readonly List<Vector2> outline = new List<Vector2>();
            private readonly Vector3 center;
            private readonly float padding;

            public ApronBandFit(
                Vector3[] body,
                Vector3 center,
                float halfWidth,
                float halfHeight,
                float padding,
                float minimumZ = float.NegativeInfinity
            )
            {
                this.center = center;
                this.padding = padding;
                Vector2[] points = body.Where(point =>
                        Mathf.Abs(point.y - center.y) < halfHeight
                        && Mathf.Abs(point.x - center.x) < halfWidth
                        && point.z > minimumZ
                    )
                    .Select(point => new Vector2(point.x, point.z))
                    .Distinct()
                    .OrderBy(point => point.x)
                    .ThenBy(point => point.y)
                    .ToArray();
                if (points.Length < 3)
                    throw new InvalidOperationException("앞치마 끈을 맞출 몸통 단면이 없습니다.");

                // 몸통 단면의 바깥 둘레를 감싸 끈이 옷 주름 속으로 파묻히지 않게 한다.
                foreach (Vector2 point in points)
                    Append(point, 0);
                int lowerCount = outline.Count;
                for (int index = points.Length - 2; index >= 0; index--)
                    Append(points[index], lowerCount - 1);
                outline.RemoveAt(outline.Count - 1);
            }

            private static float Cross(Vector2 left, Vector2 right) =>
                left.x * right.y - left.y * right.x;

            private void Append(Vector2 point, int start)
            {
                while (
                    outline.Count >= start + 2
                    && Cross(
                        outline[outline.Count - 1] - outline[outline.Count - 2],
                        point - outline[outline.Count - 1]
                    ) <= 0
                )
                    outline.RemoveAt(outline.Count - 1);
                outline.Add(point);
            }

            public Vector3 Point(float angle)
            {
                var origin = new Vector2(center.x, center.z);
                var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                for (int index = 0; index < outline.Count; index++)
                {
                    Vector2 start = outline[index] - origin;
                    Vector2 edge = outline[(index + 1) % outline.Count] - outline[index];
                    float denominator = Cross(direction, edge);
                    if (Mathf.Abs(denominator) < .000001f)
                        continue;
                    float distance = Cross(start, edge) / denominator;
                    float alongEdge = Cross(start, direction) / denominator;
                    if (distance < 0 || alongEdge < 0 || alongEdge > 1)
                        continue;
                    Vector2 point = origin + direction * (distance + padding);
                    return new Vector3(point.x, center.y, point.y);
                }
                throw new InvalidOperationException("앞치마 끈의 중심이 몸통 단면 밖에 있습니다.");
            }
        }

        private sealed class SurfaceMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int>[] triangles;
            private readonly Func<Vector3, Vector3> toLocal;
            private readonly bool reverseFaces;

            public SurfaceMesh(
                int materials,
                Func<Vector3, Vector3> toLocal,
                bool reverseFaces = false
            )
            {
                this.toLocal = toLocal;
                this.reverseFaces = reverseFaces;
                triangles = Enumerable.Range(0, materials).Select(_ => new List<int>()).ToArray();
            }

            public void Ellipsoid(Vector3 center, Vector3 radius, int material)
            {
                Grid(
                    40,
                    16,
                    material,
                    (u, v) =>
                    {
                        float around = u * Mathf.PI * 2;
                        float down = v * Mathf.PI;
                        return center
                            + new Vector3(
                                Mathf.Sin(around) * Mathf.Sin(down) * radius.x,
                                Mathf.Cos(down) * radius.y,
                                Mathf.Cos(around) * Mathf.Sin(down) * radius.z
                            );
                    }
                );
            }

            public int[] Grid(
                int columns,
                int rows,
                int material,
                Func<float, float, Vector3> point,
                int[] firstRow = null
            )
            {
                var indices = new int[(rows + 1) * (columns + 1)];
                for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                {
                    int index = y * (columns + 1) + x;
                    if (y == 0 && firstRow != null)
                        indices[index] = firstRow[x];
                    else
                    {
                        indices[index] = vertices.Count;
                        vertices.Add(toLocal(point((float)x / columns, (float)y / rows)));
                    }
                }
                for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int index = y * (columns + 1) + x;
                    int a = indices[index];
                    int b = indices[index + columns + 1];
                    int c = indices[index + 1];
                    int d = indices[index + columns + 2];
                    triangles[material]
                        .AddRange(
                            reverseFaces ? new[] { a, c, b, c, d, b } : new[] { a, b, c, c, b, d }
                        );
                }
                return indices.Skip(rows * (columns + 1)).ToArray();
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, subMeshCount = triangles.Length };
                mesh.SetVertices(vertices);
                for (int i = 0; i < triangles.Length; i++)
                    mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
