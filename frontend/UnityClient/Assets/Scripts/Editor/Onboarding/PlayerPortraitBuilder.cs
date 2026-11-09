using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Characters;
using A305.EditorTools.Common;
using A305.EditorTools.Tripo;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Onboarding
{
    /// <summary>플레이어 복사본을 정면에서 촬영해 게스트·가입 카드의 기본/미소 사진을 갱신한다.</summary>
    public static class PlayerPortraitBuilder
    {
        private const string Art = "Assets/Art/UI/Entry/";

        [MenuItem("Tools/A305/Onboarding/Capture Player Portraits")]
        public static void Capture()
        {
            BuilderTools.ThrowIfPlaying();
            if (
                !Application.isBatchMode
                && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()
            )
                return;
            string output = Environment.GetEnvironmentVariable("PLAYER_REVIEW_OUTPUT");
            var scenes = EditorSceneManager
                .GetSceneManagerSetup()
                .Where(scene => !string.IsNullOrEmpty(scene.path))
                .ToArray();
            try
            {
                foreach (string id in new[] { "F", "M" })
                    CaptureCharacter(id, output);
                AssetDatabase.Refresh();
            }
            finally
            {
                if (scenes.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(scenes);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void CaptureCharacter(string id, string output)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var definition = AssetDatabase.LoadAssetAtPath<PlayerCharacterDefinition>(
                "Assets/Data/Player/Characters/" + id + ".asset"
            );
            var root = new GameObject("Portrait copy", typeof(CharacterController));
            var model = Object.Instantiate(definition.VisualPrefab, root.transform);
            Animator animator = model.GetComponentInChildren<Animator>();
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            var torso = new[]
            {
                HumanBodyBones.Hips,
                HumanBodyBones.Spine,
                HumanBodyBones.Chest,
                HumanBodyBones.UpperChest,
                HumanBodyBones.Neck,
                HumanBodyBones.Head,
            }
                .Select(animator.GetBoneTransform)
                .Where(bone => bone != null)
                .ToArray();
            Quaternion[] rotations = torso.Select(bone => bone.rotation).ToArray();
            Vector3 restFront = TripoModelPreview.GetViewDirection(model, 0);
            using var motion = new TripoMotionPreview(root);
            motion.Apply(TripoReviewMotion.Idle, 0);
            Vector3 front = TripoModelPreview.GetViewDirection(model, 0);
            if (id == "M")
            {
                // 머리만 돌리면 Idle의 몸통 비틀림이 남는다. 골반부터 정면 자세를 복원한다.
                Quaternion heading = Quaternion.FromToRotation(restFront, front);
                for (int i = 0; i < torso.Length; i++)
                    torso[i].rotation = heading * rotations[i];
                FaceCamera(model, headBone, front);
            }
            else
            {
                // 여성 카드는 기존 촬영 자세를 그대로 유지한다.
                using var poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
                var pose = new HumanPose();
                poseHandler.GetHumanPose(ref pose);
                for (int i = 0; i < HumanTrait.MuscleName.Length; i++)
                {
                    string name = HumanTrait.MuscleName[i];
                    if (
                        (name.StartsWith("Head") || name.StartsWith("Neck"))
                        && (name.Contains("Tilt") || name.Contains("Turn"))
                    )
                        pose.muscles[i] = 0;
                }
                poseHandler.SetHumanPose(ref pose);
                front = TripoModelPreview.GetViewDirection(model, 0);
            }
            var surfaces = new List<(Mesh Mesh, Transform Transform, Vector3[] BaseVertices)>();
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skin in skins)
            {
                var mesh = new Mesh();
                skin.BakeMesh(mesh, true);
                SubdivideMouth(mesh, skin.transform);
                var part = new GameObject(
                    "Portrait surface",
                    typeof(MeshFilter),
                    typeof(MeshRenderer)
                );
                part.transform.SetParent(skin.transform, false);
                part.GetComponent<MeshFilter>().sharedMesh = mesh;
                part.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                surfaces.Add((mesh, part.transform, mesh.vertices));
                skin.enabled = false;
            }
            Bounds head = CharacterHeadProportions.HeadBounds(model);
            var camera = new GameObject("Portrait camera").AddComponent<Camera>();
            camera.orthographic = true;
            // 캐릭터 높이·머리 위치 대신 사진에서 머리가 차지하는 높이와 위 여백을 맞춘다.
            camera.orthographicSize = head.size.y / (2 * .76f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.96f, .95f, .92f);
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 20;
            var focus = new Vector3(
                head.center.x,
                head.max.y - camera.orthographicSize * .84f,
                head.center.z
            );
            camera.transform.position = focus + front * 5;
            camera.transform.LookAt(focus);
            Debug.Log($"카드 촬영 구도: {id}, 머리 {head}, 화면 높이 비율 0.76, 위 여백 0.08");
            var light = new GameObject("Portrait light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = .9f;
            light.transform.rotation = camera.transform.rotation * Quaternion.Euler(20, -20, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.8f, .8f, .8f);
            try
            {
                foreach (bool smile in new[] { false, true })
                {
                    for (int i = 0; i < skins.Length; i++)
                    {
                        surfaces[i].Mesh.vertices = surfaces[i].BaseVertices;
                        PoseMouth(surfaces[i].Mesh, surfaces[i].Transform, skins[i], smile);
                    }
                    foreach (string prefix in new[] { "EntryCard", "SignupCard" })
                    {
                        bool signup = prefix == "SignupCard";
                        RectInt photo = signup
                            ? new RectInt(92, 78, 596, 285)
                            : new RectInt(96, 82, 540, 413);
                        Texture2D portrait = Render(camera, photo.width, photo.height);
                        string name = prefix + id + (smile ? "On" : "Off") + ".png";
                        if (!string.IsNullOrEmpty(output))
                        {
                            Directory.CreateDirectory(output);
                            File.WriteAllBytes(Path.Combine(output, name), portrait.EncodeToPNG());
                        }
                        Compose(Art + name, portrait, photo, smile, signup ? 646 : 598);
                        Object.DestroyImmediate(portrait);
                    }
                }
            }
            finally
            {
                foreach (var surface in surfaces)
                    Object.DestroyImmediate(surface.Mesh);
            }
        }

        // 본의 축이 얼굴과 일치하지 않는 모델도 있어 실제 양쪽 눈 흰자 위치로 정면을 잡는다.
        private static void FaceCamera(GameObject model, Transform headBone, Vector3 front)
        {
            Bounds head = CharacterHeadProportions.HeadBounds(model);
            Vector3 right = Vector3.Cross(Vector3.up, front).normalized;
            Bounds? leftEye = null,
                rightEye = null;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                string path = AssetDatabase.GetAssetPath(
                    skin.sharedMaterial.GetTexture("_BaseMap")
                );
                if (string.IsNullOrEmpty(path))
                    continue;
                var texture = new Texture2D(2, 2);
                var mesh = new Mesh();
                try
                {
                    texture.LoadImage(File.ReadAllBytes(path));
                    skin.BakeMesh(mesh, true);
                    Vector3[] vertices = mesh.vertices;
                    Vector2[] uv = mesh.uv;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 point = skin.transform.TransformPoint(vertices[i]);
                        Vector3 offset = point - head.center;
                        float x = Vector3.Dot(offset, right) / head.size.x;
                        float y = (point.y - head.min.y) / head.size.y;
                        if (
                            Mathf.Abs(x) < .08f
                            || Mathf.Abs(x) > .36f
                            || y < .18f
                            || y > .65f
                            || Vector3.Dot(offset, front) < head.size.z * .2f
                        )
                            continue;
                        Color color = texture.GetPixelBilinear(uv[i].x, uv[i].y);
                        if (
                            Mathf.Min(color.r, color.g, color.b) < .55f
                            || Mathf.Max(color.r, color.g, color.b)
                                - Mathf.Min(color.r, color.g, color.b)
                                > .2f
                        )
                            continue;
                        Bounds eye =
                            (x < 0 ? leftEye : rightEye) ?? new Bounds(point, Vector3.zero);
                        eye.Encapsulate(point);
                        if (x < 0)
                            leftEye = eye;
                        else
                            rightEye = eye;
                    }
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                    Object.DestroyImmediate(mesh);
                }
            }
            if (!leftEye.HasValue || !rightEye.HasValue)
                throw new InvalidOperationException(
                    "정면 촬영용 양쪽 눈 영역을 확인하지 못했습니다."
                );
            Quaternion correction = Quaternion.FromToRotation(
                rightEye.Value.center - leftEye.Value.center,
                right
            );
            float angle = Quaternion.Angle(Quaternion.identity, correction);
            if (angle > 12)
                throw new InvalidOperationException(
                    "정면 촬영용 눈 영역이 예상 범위를 벗어났습니다."
                );
            headBone.rotation = correction * headBone.rotation;
            Debug.Log(
                $"카드 정면 보정: {model.name}, 눈 {leftEye.Value.center:F4} / {rightEye.Value.center:F4}, 회전 {angle:F2}도"
            );
        }

        // 런타임 메시에는 표정 채널이 없으므로 촬영용으로 구운 메시의 입 주변만 부드럽게 변형한다.
        private static void PoseMouth(
            Mesh mesh,
            Transform space,
            SkinnedMeshRenderer source,
            bool smile
        )
        {
            string texturePath = AssetDatabase.GetAssetPath(
                source.sharedMaterial.GetTexture("_BaseMap")
            );
            if (string.IsNullOrEmpty(texturePath))
                return;
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(texturePath));
                Vector3[] vertices = mesh.vertices;
                Vector2[] uv = mesh.uv;
                var points = new List<Vector3>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 point = space.TransformPoint(vertices[i]);
                    if (
                        Mathf.Abs(point.x) > .09f
                        || point.y < .78f
                        || point.y > .95f
                        || point.z < .2f
                    )
                        continue;
                    Color color = texture.GetPixelBilinear(uv[i].x, uv[i].y);
                    if (color.r < .48f && color.g < .3f && color.b < .26f)
                        points.Add(point);
                }
                if (points.Count == 0)
                    throw new InvalidOperationException(
                        "촬영 복사본에서 입 영역을 찾지 못했습니다: " + source.name
                    );
                Bounds mouth = new Bounds(points[0], Vector3.zero);
                foreach (Vector3 point in points)
                    mouth.Encapsulate(point);
                Debug.Log($"정면 표정 촬영: {source.name}, 미소 {smile}, 입 {mouth}");
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 point = space.TransformPoint(vertices[i]);
                    Vector3 offset = point - mouth.center;
                    float radius = new Vector3(
                        offset.x / .12f,
                        offset.y / .055f,
                        offset.z / .06f
                    ).magnitude;
                    if (radius >= 1)
                        continue;
                    float weight = Mathf.SmoothStep(1, 0, radius);
                    point.x += offset.x * (smile ? .4f : 0) * weight;
                    point.y +=
                        (
                            (smile ? .018f : -.014f)
                                * Mathf.Pow(Mathf.Clamp01(Mathf.Abs(offset.x) / .04f), 1.5f)
                            + (smile ? -.003f : .003f)
                        ) * weight;
                    vertices[i] = space.InverseTransformPoint(point);
                }
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        // 원래 입 주변의 큰 삼각형 그대로 움직이면 V자로 꺾인다. 촬영용 면만 선형 분할해 곡선을 보존한다.
        private static void SubdivideMouth(Mesh mesh, Transform space)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                var vertices = mesh.vertices.ToList();
                var normals = mesh.normals.ToList();
                var uv = mesh.uv.ToList();
                var edges = new Dictionary<(int, int), int>();
                int Midpoint(int a, int b)
                {
                    var key = (Math.Min(a, b), Math.Max(a, b));
                    if (edges.TryGetValue(key, out int found))
                        return found;
                    int index = vertices.Count;
                    vertices.Add((vertices[a] + vertices[b]) * .5f);
                    normals.Add((normals[a] + normals[b]).normalized);
                    uv.Add((uv[a] + uv[b]) * .5f);
                    edges.Add(key, index);
                    return index;
                }
                var triangles = new List<int>();
                int[] original = mesh.triangles;
                for (int i = 0; i < original.Length; i += 3)
                {
                    int a = original[i],
                        b = original[i + 1],
                        c = original[i + 2];
                    Vector3 center = space.TransformPoint(
                        (vertices[a] + vertices[b] + vertices[c]) / 3
                    );
                    if (
                        Mathf.Abs(center.x) > .23f
                        || center.y < .67f
                        || center.y > 1.07f
                        || center.z < .2f
                    )
                    {
                        triangles.AddRange(new[] { a, b, c });
                        continue;
                    }
                    int ab = Midpoint(a, b),
                        bc = Midpoint(b, c),
                        ca = Midpoint(c, a);
                    triangles.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
                }
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(triangles, 0);
            }
        }

        private static Texture2D Render(Camera camera, int width, int height)
        {
            var target = new RenderTexture(width * 2, height * 2, 24) { antiAliasing = 4 };
            var image = new Texture2D(width * 2, height * 2, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
                image.Apply();
                return image;
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
            }
        }

        // 기존 카드의 한글·테두리·선택 체크를 보존하고 사진 영역만 같은 비율로 교체한다.
        private static void Compose(
            string path,
            Texture2D photo,
            RectInt rect,
            bool selected,
            float checkX
        )
        {
            var card = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                card.LoadImage(File.ReadAllBytes(path));
                for (int y = 0; y < rect.height; y++)
                for (int x = 0; x < rect.width; x++)
                {
                    float dx = Mathf.Max(48 - x, x - (rect.width - 49), 0);
                    float dy = Mathf.Max(48 - y, y - (rect.height - 49), 0);
                    if (dx * dx + dy * dy > 48 * 48)
                        continue;
                    Color color = photo.GetPixelBilinear(
                        (x + .5f) / rect.width,
                        1 - (y + .5f) / rect.height
                    );
                    card.SetPixel(rect.x + x, card.height - 1 - rect.y - y, color);
                }
                if (selected)
                    DrawCheck(card, checkX);
                card.Apply();
                File.WriteAllBytes(path, card.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(card);
            }
        }

        private static void DrawCheck(Texture2D card, float centerX)
        {
            float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
            {
                Vector2 segment = end - start;
                float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
                return Vector2.Distance(point, start + segment * t);
            }
            for (int y = 71; y <= 169; y++)
            for (int x = (int)centerX - 49; x <= centerX + 49; x++)
            {
                var point = new Vector2(x - centerX, y - 120);
                float radius = point.magnitude;
                if (radius > 49)
                    continue;
                Color color = radius > 46 ? new Color(1, .84f, .78f) : new Color(1, .46f, .34f);
                if (
                    Mathf.Min(
                        DistanceToSegment(point, new Vector2(-14, 0), new Vector2(-4, 10)),
                        DistanceToSegment(point, new Vector2(-4, 10), new Vector2(16, -11))
                    ) < 2.8f
                )
                    color = Color.white;
                card.SetPixel(x, card.height - 1 - y, color);
            }
        }
    }
}
