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
        [MenuItem("Tools/A305/Player/Fit Female Capsule And Camera")]
        public static void FitFemale()
        {
            if (!BuilderTools.CanBuild())
                return;
            var definition = BuilderTools.LoadRequiredAsset<PlayerCharacterDefinition>(
                "Assets/Data/Player/Characters/F.asset",
                "Female character definition"
            );
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
                float radius =
                    Mathf.Ceil(
                        headPoints.Max(point => new Vector2(point.x, point.z).magnitude) * 100
                    ) / 100;
                var data = new SerializedObject(definition);
                data.FindProperty("heightMeters").floatValue = height;
                data.FindProperty("radiusMeters").floatValue = radius;
                // 얼굴 중앙을 기본 눈높이로 둔다. 최종 눈 위치는 F 설정에서 미세 조정할 수 있다.
                data.FindProperty("cameraEyeHeightMeters").floatValue = head.center.y - body.min.y;
                data.FindProperty("cameraPivotHeightMeters").floatValue = height * .6f;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"Female fit: scale={copy.transform.localScale}, body={body}, head={head}, capsule={height:F3}/{radius:F3}"
                );
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
            PlayerCharacterBuilder.Build();
        }
    }
}
