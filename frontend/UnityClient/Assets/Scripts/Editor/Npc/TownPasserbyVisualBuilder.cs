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
    /// 마을 행인 Ben(TOWN_PASSERBY)에 비즈니스맨 외형을 연결한다. 다른 행인·직원도 쓰는 공용 Passerby 프리팹은 바꾸지 않고
    /// TownScene의 Ben 인스턴스 아래에만 외형 프리팹을 둔다. 이미 연결한 외형은 다시 만들지 않고 Animator 참조·캡슐·시선 높이만 맞춘다.
    /// </summary>
    public static class TownPasserbyVisualBuilder
    {
        public const string MenuPath = "Tools/A305/Npc/Connect Town Passerby Visual";
        public const string VisualPrefabPath =
            "Assets/Prefabs/Npc/Review/LittleMung_Business_Male.prefab";
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string TownPasserbyCode = "TOWN_PASSERBY";

        // TownProductionBuilder가 모든 NPC에 붙였던 뼈대 없는 도형 외형
        private const string ProxyPrefabPath =
            TownBackgroundKit.Folder + "/LittleMungV7NpcVisual.prefab";
        private const float ServiceHeightMeters = 1.7f;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            Scene town = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
            NpcIdentity ben = BuilderTools
                .FindAllInScene<NpcIdentity>(town)
                .Single(npc => npc.Code == TownPasserbyCode);
            Connect(ben);
            BuilderTools.SaveScene(town, TownScenePath);
        }

        public static void Connect(NpcIdentity ben)
        {
            GameObject prefab = BuilderTools.LoadRequiredAsset<GameObject>(
                VisualPrefabPath,
                MenuPath
            );
            foreach (Transform child in ben.transform.Cast<Transform>().ToArray())
            {
                if (IsInstanceOf(child, ProxyPrefabPath))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            // Passerby 프리팹의 도형 자리 표시는 지우지 않고 꺼 둔다.
            Transform placeholder = ben.transform.Find("Visual");
            if (placeholder != null && placeholder.gameObject.activeSelf)
            {
                placeholder.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(placeholder.gameObject);
            }

            (float sole, float eyes) = MeasureStanding(prefab);
            Transform visual = ben
                .transform.Cast<Transform>()
                .FirstOrDefault(child => IsInstanceOf(child, VisualPrefabPath));
            if (visual == null)
            {
                // 대기 자세의 발바닥이 프리팹 루트보다 약간 아래에 있어 Ben이 선 바닥에 맞춘다.
                visual = BuilderTools
                    .InstantiatePrefab(
                        prefab,
                        ben.transform,
                        Vector3.up * -sole,
                        Quaternion.identity
                    )
                    .transform;
                visual.name = prefab.name;
            }

            BuilderTools.SetReference(
                ben.GetComponent<CharacterAnimationDriver>(),
                "animator",
                visual.GetComponentInChildren<Animator>()
            );
            var capsule = ben.GetComponent<CharacterController>();
            capsule.height = ServiceHeightMeters;
            capsule.center = new Vector3(
                capsule.center.x,
                ServiceHeightMeters / 2,
                capsule.center.z
            );
            PrefabUtility.RecordPrefabInstancePropertyModifications(capsule);
            Vector3 look = ben.LookTarget.localPosition;
            look.y = visual.localPosition.y + eyes;
            ben.LookTarget.localPosition = look;
            PrefabUtility.RecordPrefabInstancePropertyModifications(ben.LookTarget);
        }

        private static bool IsInstanceOf(Transform child, string prefabPath)
        {
            return PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)
                && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject)
                    == prefabPath;
        }

        // 원점에 둔 임시 사본에 대기 동작의 첫 자세를 적용해 발바닥과 눈의 높이를 잰다. 공용 Avatar는 눈을 Humanoid로 매핑하지 않아 뼈 이름으로 찾는다.
        private static (float sole, float eyes) MeasureStanding(GameObject prefab)
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
                try
                {
                    body.BakeMesh(baked, true);
                    sole = baked.vertices.Min(vertex => body.transform.TransformPoint(vertex).y);
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }

                Transform[] bones = copy.GetComponentsInChildren<Transform>(true);
                float eyes =
                    (
                        bones.Single(bone => bone.name == "Left_Eye").position.y
                        + bones.Single(bone => bone.name == "Right_Eye").position.y
                    ) / 2;
                return (sole, eyes);
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }
    }
}
