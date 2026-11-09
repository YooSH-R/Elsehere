using System;
using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Player
{
    public static class PlayerCharacterFitBuilder
    {
        [MenuItem("Tools/A305/Player/Fit Character Capsules And Camera")]
        public static void FitCharacters()
        {
            if (!BuilderTools.CanBuild())
                return;
            // 측정 전에 정의마다 현재 외형을 연결한다.
            PlayerCharacterBuilder.BuildCatalog();
            foreach (string id in new[] { "F", "M" })
                Fit(
                    BuilderTools.LoadRequiredAsset<PlayerCharacterDefinition>(
                        "Assets/Data/Player/Characters/" + id + ".asset",
                        id + " character definition"
                    )
                );
            PlayerCharacterBuilder.Build();
        }

        internal static void Fit(PlayerCharacterDefinition definition)
        {
            var copy = Object.Instantiate(definition.VisualPrefab);
            try
            {
                var animator = copy.GetComponentInChildren<Animator>();
                var idle = BuilderTools.LoadRequiredAsset<AnimationClip>(
                    "Assets/Art/Player/LittleMung/Idle.anim",
                    "Standing reference pose"
                );
                idle.SampleAnimation(animator.gameObject, 0);
                var headBones = new HashSet<Transform>(
                    animator
                        .GetBoneTransform(HumanBodyBones.Head)
                        .GetComponentsInChildren<Transform>()
                );
                var bodyPoints = new List<Vector3>();
                var headPoints = new List<Vector3>();
                var mesh = new Mesh();
                try
                {
                    foreach (var skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        skin.BakeMesh(mesh, true);
                        var vertices = mesh.vertices;
                        var weights = skin.sharedMesh.boneWeights;
                        var flags = skin.bones.Select(headBones.Contains).ToArray();
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            Vector3 point = skin.transform.TransformPoint(vertices[i]);
                            bodyPoints.Add(point);
                            var w = weights[i];
                            float influence =
                                (flags[w.boneIndex0] ? w.weight0 : 0)
                                + (flags[w.boneIndex1] ? w.weight1 : 0)
                                + (flags[w.boneIndex2] ? w.weight2 : 0)
                                + (flags[w.boneIndex3] ? w.weight3 : 0);
                            if (influence >= .75f)
                                headPoints.Add(point);
                        }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
                if (bodyPoints.Count == 0 || headPoints.Count == 0)
                    throw new InvalidOperationException(
                        "외형과 머리의 스킨 정점을 확인해야 합니다."
                    );
                var body = new Bounds(bodyPoints[0], Vector3.zero);
                var head = new Bounds(headPoints[0], Vector3.zero);
                foreach (var point in bodyPoints)
                    body.Encapsulate(point);
                foreach (var point in headPoints)
                    head.Encapsulate(point);
                float height = Mathf.Ceil(body.size.y * 100) / 100;
                // 충돌 캡슐은 머리 아래 몸통만 덮어 가구 사이 좁은 틈에도 들어가게 하고,
                // 큰 머리는 머리 아래 끝에서 시작하는 구로 따로 막아 낮은 가구는 머리 아래로 지나가게 한다.
                float radius = CeilToCentimeter(
                    HorizontalReach(bodyPoints.Where(point => point.y < head.min.y))
                );
                float headRadius = CeilToCentimeter(HorizontalReach(headPoints));
                float headCenterHeight = head.min.y - body.min.y + headRadius;
                var data = new SerializedObject(definition);
                data.FindProperty("heightMeters").floatValue = height;
                data.FindProperty("radiusMeters").floatValue = radius;
                data.FindProperty("headRadiusMeters").floatValue = headRadius;
                data.FindProperty("headCenterHeightMeters").floatValue = headCenterHeight;
                // 얼굴 중앙을 기본 눈높이로 둔다. 최종 눈 위치는 캐릭터 정의에서 미세 조정할 수 있다.
                data.FindProperty("cameraEyeHeightMeters").floatValue = head.center.y - body.min.y;
                data.FindProperty("cameraPivotHeightMeters").floatValue = height * .6f;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"{definition.CharacterId} fit: scale={copy.transform.localScale}, body={body}, head={head}, capsule={height:F3}/{radius:F3}, headSphere={headCenterHeight:F3}/{headRadius:F3}"
                );
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        /// <summary>점들이 캐릭터 세로축에서 가장 멀리 떨어진 수평 거리</summary>
        private static float HorizontalReach(IEnumerable<Vector3> points)
        {
            return points.Max(point => new Vector2(point.x, point.z).magnitude);
        }

        private static float CeilToCentimeter(float meters)
        {
            return Mathf.Ceil(meters * 100) / 100;
        }
    }
}
