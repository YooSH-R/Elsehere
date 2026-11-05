using System;
using System.Collections.Generic;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using A305.EditorTools.World;
using A305.Npc;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// 서서 지내는 NPC(마을 행인 Ben, 드럭스토어 직원 Alex)에 리틀 멍 외형을 연결한다. 다른 행인·직원도 쓰는 공용 Passerby 프리팹은
    /// 바꾸지 않고 장면의 NPC 인스턴스 아래에만 외형 프리팹을 둔다. 이미 연결한 외형은 다시 만들지 않고 Animator 참조·캡슐·시선 높이만 맞춘다.
    /// 운전석에 앉는 택시 기사 Joe는 TownStreetLifeBuilder가 맡는다.
    /// </summary>
    public static class NpcVisualBuilder
    {
        public const string MenuPath = "Tools/A305/Npc/Connect Character Visuals";
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string ShopScenePath = "Assets/Scenes/Production/ShopScene.unity";
        private const string ReviewFolder = "Assets/Prefabs/Npc/Review/";

        // 실내 직원·플레이어와 같은 계단 높이와 표면 두께. 행인 프리팹 기본값(0.3m·0.08m)이면 0.4m 가까운 가구도 밟고 올라서고 발이 바닥에서 떠 보인다.
        private const float StepOffsetMeters = .15f;
        private const float SkinWidthMeters = .02f;

        // TownProductionBuilder·상점 Builder가 붙였던 뼈대 없는 도형 외형. 이 외형에 Generic 뼈대를 붙인 BenVisual도 함께 치운다.
        private const string ProxyPrefabPath =
            TownBackgroundKit.Folder + "/LittleMungV7NpcVisual.prefab";

        // NPC 키별 장면과 외형. 공용 손짓 동작(Idle_Talking_Loop)이 엉거주춤해 보이는 NPC는 말하는 동안 자기 Idle로 세운다.
        private static readonly Dictionary<
            string,
            (string Scene, string Prefab, bool TalkGesture)
        > Visuals = new Dictionary<string, (string, string, bool)>
        {
            ["TOWN_PASSERBY"] = (
                TownScenePath,
                ReviewFolder + "LittleMung_Business_Male.prefab",
                false
            ),
            ["SHOP_STAFF"] = (
                ShopScenePath,
                ReviewFolder + "LittleMung_BlackYoung_Male.prefab",
                true
            ),
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            foreach (string scenePath in Visuals.Values.Select(visual => visual.Scene).Distinct())
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (NpcIdentity npc in BuilderTools.FindAllInScene<NpcIdentity>(scene))
                {
                    if (Visuals.TryGetValue(npc.Code, out var visual) && visual.Scene == scenePath)
                    {
                        Connect(npc);
                    }
                }

                BuilderTools.SaveScene(scene, scenePath);
            }
        }

        public static void Connect(NpcIdentity npc)
        {
            if (!Visuals.TryGetValue(npc.Code, out var entry))
            {
                throw new InvalidOperationException("No character visual for NPC " + npc.Code);
            }

            Connect(npc, entry.Prefab, entry.TalkGesture);
        }

        /// <summary>위 표에 없는 NPC(건물 직원 등)에 외형을 붙인다. 외형 고르기는 부르는 Builder가 맡는다.</summary>
        public static void Connect(NpcIdentity npc, string prefabPath, bool talkGesture)
        {
            var entry = (Prefab: prefabPath, TalkGesture: talkGesture);
            GameObject prefab = BuilderTools.LoadRequiredAsset<GameObject>(entry.Prefab, MenuPath);
            var driver = npc.GetComponent<CharacterAnimationDriver>();
            var previousAnimator =
                new SerializedObject(driver).FindProperty("animator").objectReferenceValue
                as Animator;
            Transform previousVisual = previousAnimator != null ? previousAnimator.transform : null;
            while (previousVisual != null && previousVisual.parent != npc.transform)
                previousVisual = previousVisual.parent;
            // 연결된 이전 외형만 교체한다. 소지품·시선 타깃·NPC 루트 설정은 보존한다.
            if (previousVisual != null && !IsInstanceOf(previousVisual, entry.Prefab))
                Object.DestroyImmediate(previousVisual.gameObject);
            foreach (Transform child in npc.transform.Cast<Transform>().ToArray())
            {
                if (
                    IsInstanceOf(child, ProxyPrefabPath)
                    || IsInstanceOf(child, BenRigBuilder.VisualPath)
                )
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            // Passerby 프리팹의 도형 자리 표시는 지우지 않고 꺼 둔다.
            Transform placeholder = npc.transform.Find("Visual");
            if (placeholder != null && placeholder.gameObject.activeSelf)
            {
                placeholder.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(placeholder.gameObject);
            }

            (float sole, float eyes, float height) = MeasureStanding(prefab);
            Transform visual = npc
                .transform.Cast<Transform>()
                .FirstOrDefault(child => IsInstanceOf(child, entry.Prefab));
            if (visual == null)
            {
                // 대기 자세의 발바닥이 프리팹 루트보다 약간 아래에 있어 NPC가 선 바닥에 맞춘다.
                visual = BuilderTools
                    .InstantiatePrefab(
                        prefab,
                        npc.transform,
                        Vector3.up * -sole,
                        Quaternion.identity
                    )
                    .transform;
                visual.name = prefab.name;
            }

            BuilderTools.SetReference(
                npc.GetComponent<CharacterAnimationDriver>(),
                "animator",
                visual.GetComponentInChildren<Animator>()
            );
            var passerby = new SerializedObject(npc.GetComponent<Passerby>());
            passerby.FindProperty("talkGesture").boolValue = entry.TalkGesture;
            passerby.ApplyModifiedPropertiesWithoutUndo();
            var capsule = npc.GetComponent<CharacterController>();
            // 기존 씬에서 조절한 외형 배율을 측정값에도 반영한다.
            float scale = visual.localScale.y / prefab.transform.localScale.y;
            capsule.height = height * scale;
            capsule.center = new Vector3(capsule.center.x, capsule.height / 2, capsule.center.z);
            FitFooting(capsule);
            Vector3 look = npc.LookTarget.localPosition;
            look.y = visual.localPosition.y + eyes * scale;
            npc.LookTarget.localPosition = look;
            PrefabUtility.RecordPrefabInstancePropertyModifications(npc.LookTarget);
        }

        /// <summary>서 있는 NPC 캡슐의 계단 높이와 표면 두께를 실내 직원·플레이어와 맞춘다.</summary>
        public static void FitFooting(CharacterController capsule)
        {
            capsule.stepOffset = StepOffsetMeters;
            capsule.skinWidth = SkinWidthMeters;
            PrefabUtility.RecordPrefabInstancePropertyModifications(capsule);
        }

        private static bool IsInstanceOf(Transform child, string prefabPath)
        {
            return PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)
                && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject)
                    == prefabPath;
        }

        // 원점에 둔 임시 사본에 대기 동작의 첫 자세를 적용해 발바닥·눈의 높이와 키를 잰다. 공용 Avatar는 눈을 Humanoid로 매핑하지 않아 뼈 이름으로 찾는다.
        private static (float sole, float eyes, float height) MeasureStanding(GameObject prefab)
        {
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Animator animator = copy.GetComponentInChildren<Animator>();
                animator.Rebind();
                animator.Update(0);
                SkinnedMeshRenderer body = copy.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(skin => skin.sharedMesh.vertexCount)
                    .First();
                var baked = new Mesh();
                float sole;
                float top;
                try
                {
                    body.BakeMesh(baked, true);
                    float[] heights = baked
                        .vertices.Select(vertex => body.transform.TransformPoint(vertex).y)
                        .ToArray();
                    sole = heights.Min();
                    top = heights.Max();
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }

                // 머리 비율을 바꿔도 마스터 본은 유지하므로, 이식한 눈의 실제 표면을 먼저 잰다.
                var eyeSurfaces = copy.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Where(skin =>
                        skin.sharedMesh.name == "LeftEye" || skin.sharedMesh.name == "RightEye"
                    )
                    .ToArray();
                float eyes;
                if (eyeSurfaces.Length == 2)
                {
                    eyes = 0;
                    var eyeMesh = new Mesh();
                    try
                    {
                        foreach (var eye in eyeSurfaces)
                        {
                            eye.BakeMesh(eyeMesh, true);
                            var heights = eyeMesh
                                .vertices.Select(v => eye.transform.TransformPoint(v).y)
                                .ToArray();
                            eyes += (heights.Min() + heights.Max()) * .25f;
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(eyeMesh);
                    }
                }
                else
                {
                    Transform[] bones = copy.GetComponentsInChildren<Transform>(true);
                    eyes =
                        (
                            bones.Single(bone => bone.name == "Left_Eye").position.y
                            + bones.Single(bone => bone.name == "Right_Eye").position.y
                        ) / 2;
                }
                return (sole, eyes, top - sole);
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }
    }
}
