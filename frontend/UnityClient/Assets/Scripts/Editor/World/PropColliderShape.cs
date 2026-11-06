using System;
using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using A305.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 가구·소품의 이동 콜라이더가 겉모양과 맞는지 본다. 리틀멍은 점프와 턱으로 약 0.8m까지 올라서므로,
    /// 겉모양 전체를 감싼 상자 콜라이더의 윗면이 실제 표면보다 높으면 그 위에 선 캐릭터가 공중에 떠 보인다.
    /// </summary>
    public static class PropColliderShape
    {
        /// <summary>상자 윗면이 실제 표면보다 이만큼 넘게 뜨면 겉모양과 맞지 않는 것으로 본다. 리틀멍이 넘는 턱 높이와 같다.</summary>
        public const float AllowedGapMeters = .15f;

        // 이보다 넓은 상자는 바닥·땅, 높은 상자는 벽·기둥으로 보고 검사하지 않는다.
        private const float FloorAreaSquareMeters = 25f;
        private const float StructureHeightMeters = 2.6f;

        // 이보다 얇으면서 WallHeightMeters보다 높은 상자는 벽·창틀로 본다.
        private const float WallThicknessMeters = .35f;
        private const float WallHeightMeters = 1.5f;

        // 상자 윗면을 가로세로 이 개수로 나눠 실제 표면 높이를 잰다.
        private const int SamplesPerSide = 5;

        public const string MenuPath = "Tools/A305/World/Fit Prop Colliders To Shape";

        private static readonly string[] ProductionScenes =
        {
            "HomeScene",
            "TownScene",
            "CafeScene",
            "HotelScene",
            "ShopScene",
            "PoliceStationScene",
        };

        /// <summary>
        /// 배포 씬에서 윗면이 뜬 상자 콜라이더를 메시 모양 콜라이더로 바꾼다.
        /// 사물 프리팹에서 온 상자는 프리팹에서 고쳐 모든 배치에 함께 반영하고, 씬에 직접 둔 상자는 씬에서 고친다.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void FitProductionScenes()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            var prefabBoxes = new Dictionary<string, HashSet<string>>();
            foreach (string name in ProductionScenes)
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath(name));
                foreach (BoxCollider box in FindFloatingBoxes(scene.GetRootGameObjects()))
                {
                    BoxCollider source = PrefabSource(box);
                    if (source == null)
                    {
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(source);
                    if (!prefabBoxes.TryGetValue(path, out HashSet<string> keys))
                    {
                        prefabBoxes.Add(path, keys = new HashSet<string>());
                    }

                    keys.Add(KeyOf(source));
                }
            }

            foreach (KeyValuePair<string, HashSet<string>> prefab in prefabBoxes)
            {
                BuilderTools.UpdatePrefabIfExists(
                    prefab.Key,
                    root =>
                    {
                        // 한 오브젝트의 상자를 지우면 나머지 상자 순서가 바뀌므로 먼저 모두 찾는다.
                        BoxCollider[] boxes = prefab
                            .Value.Select(key => FindByKey(root, key))
                            .ToArray();
                        foreach (BoxCollider box in boxes)
                        {
                            FitToShape(box);
                        }
                    }
                );
            }

            AssetDatabase.SaveAssets();
            foreach (string name in ProductionScenes)
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath(name));
                BoxCollider[] sceneBoxes = FindFloatingBoxes(scene.GetRootGameObjects())
                    .Where(box => PrefabSource(box) == null)
                    .ToArray();
                foreach (BoxCollider box in sceneBoxes)
                {
                    FitToShape(box);
                }

                if (sceneBoxes.Length > 0)
                {
                    BuilderTools.SaveScene(scene, ScenePath(name));
                }
            }
        }

        /// <summary>윗면이 실제 표면보다 <see cref="AllowedGapMeters"/> 넘게 뜬 이동 콜라이더 상자</summary>
        public static IEnumerable<BoxCollider> FindFloatingBoxes(IEnumerable<GameObject> roots)
        {
            return roots
                .SelectMany(root => root.GetComponentsInChildren<BoxCollider>(false))
                .Where(box => box.enabled && !box.isTrigger)
                .Where(box => !IsStructure(box) && !IsMoving(box))
                // 겉모양이 없는 상자는 일부러 둔 보이지 않는 벽으로 보고 그대로 둔다.
                .Where(box => VisualMeshes(box).Length > 0)
                .Where(box => TopGap(box) > AllowedGapMeters)
                .ToArray();
        }

        /// <summary>
        /// 상자를 지우고, 상자가 감싸던 메시마다 그 모양 그대로의 콜라이더를 단다.
        /// 안에 든 컵처럼 자기 콜라이더가 따로 있는 사물의 메시는 건드리지 않는다.
        /// </summary>
        public static void FitToShape(BoxCollider box)
        {
            foreach (MeshFilter mesh in VisualMeshes(box).Where(mesh => !HasOwnCollider(mesh, box)))
            {
                if (mesh.GetComponent<MeshCollider>() == null)
                {
                    mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                }
            }

            Object.DestroyImmediate(box);
        }

        public static string Describe(BoxCollider box)
        {
            return $"{PathOf(box.transform)} (윗면 {box.bounds.max.y:F2}m, 표면보다 최대 {TopGap(box):F2}m 뜸)";
        }

        /// <summary>상자 윗면이 그 아래 실제 표면보다 가장 많이 뜬 높이. 아래에 표면이 없으면 상자 바닥까지 잰다.</summary>
        public static float TopGap(BoxCollider box)
        {
            MeshFilter[] meshes = VisualMeshes(box);
            float bottom = box.bounds.min.y;
            float largestGap = 0f;
            for (int x = 0; x < SamplesPerSide; x++)
            {
                for (int z = 0; z < SamplesPerSide; z++)
                {
                    Vector3 top = TopPoint(
                        box,
                        (x + .5f) / SamplesPerSide,
                        (z + .5f) / SamplesPerSide
                    );
                    float surface = meshes
                        .Select(mesh => HighestSurface(mesh, top, bottom))
                        .DefaultIfEmpty(bottom)
                        .Max();
                    largestGap = Mathf.Max(largestGap, top.y - surface);
                }
            }

            return largestGap;
        }

        private static bool IsStructure(BoxCollider box)
        {
            Vector3 size = Vector3.Scale(box.size, Abs(box.transform.lossyScale));
            Vector3 world = box.bounds.size;
            bool isFloor = world.x * world.z > FloorAreaSquareMeters;
            bool isTall = world.y > StructureHeightMeters;
            bool isWall =
                Mathf.Min(size.x, size.z) < WallThicknessMeters && world.y > WallHeightMeters;
            return isFloor || isTall || isWall;
        }

        // 움직이는 플레이어·차의 콜라이더는 배치한 사물이 아니다.
        private static bool IsMoving(BoxCollider box)
        {
            return box.GetComponentInParent<CharacterController>() != null
                || box.GetComponentInParent<Rigidbody>() != null
                || box.GetComponentInParent<LoopingCar>() != null;
        }

        /// <summary>상자가 감싸는 겉모양. 상자를 단 오브젝트 아래에 메시가 없으면 부모 아래 메시를 본다.</summary>
        private static MeshFilter[] VisualMeshes(BoxCollider box)
        {
            Transform owner = HasMesh(box.transform) ? box.transform : box.transform.parent;
            if (owner == null)
            {
                return Array.Empty<MeshFilter>();
            }

            return owner
                .GetComponentsInChildren<MeshFilter>(false)
                .Where(mesh => mesh.sharedMesh != null && mesh.GetComponent<MeshRenderer>() != null)
                // 글자 메시는 겉모양 표면이 아니다.
                .Where(mesh => mesh.GetComponent<TMP_Text>() == null)
                .ToArray();
        }

        private static bool HasMesh(Transform owner)
        {
            return owner
                .GetComponentsInChildren<MeshFilter>(false)
                .Any(mesh => mesh.sharedMesh != null);
        }

        /// <summary>메시에서 상자를 단 오브젝트까지 올라가는 사이에 자기 이동 콜라이더를 가진 사물이 있는지</summary>
        private static bool HasOwnCollider(MeshFilter mesh, BoxCollider box)
        {
            for (Transform node = mesh.transform; node != null; node = node.parent)
            {
                if (node.GetComponents<Collider>().Any(other => other != box && !other.isTrigger))
                {
                    return true;
                }

                if (node == box.transform || node == box.transform.parent)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>상자가 정의된 가장 안쪽 사물 프리팹의 상자. 씬에 직접 두었거나 배치에서 덧붙인 상자면 null.</summary>
        private static BoxCollider PrefabSource(BoxCollider box)
        {
            BoxCollider source = null;
            for (
                BoxCollider next = PrefabUtility.GetCorrespondingObjectFromSource(box);
                next != null;
                next = PrefabUtility.GetCorrespondingObjectFromSource(next)
            )
            {
                if (!AssetDatabase.GetAssetPath(next).EndsWith(".prefab"))
                {
                    break;
                }

                source = next;
            }

            return source;
        }

        // 프리팹 안에서 상자를 다시 찾는 열쇠: 루트부터의 형제 순서와 그 오브젝트 안의 상자 순서
        private static string KeyOf(BoxCollider box)
        {
            var indices = new List<int>();
            for (Transform node = box.transform; node.parent != null; node = node.parent)
            {
                indices.Insert(0, node.GetSiblingIndex());
            }

            int boxIndex = Array.IndexOf(box.GetComponents<BoxCollider>(), box);
            return string.Join("/", indices) + "#" + boxIndex;
        }

        private static BoxCollider FindByKey(GameObject root, string key)
        {
            string[] parts = key.Split('#');
            Transform node = root.transform;
            foreach (string index in parts[0].Split('/').Where(part => part.Length > 0))
            {
                node = node.GetChild(int.Parse(index));
            }

            return node.GetComponents<BoxCollider>()[int.Parse(parts[1])];
        }

        private static string ScenePath(string name)
        {
            return "Assets/Scenes/Production/" + name + ".unity";
        }

        /// <summary>상자 윗면 위의 한 점. u·v는 윗면 안에서 0~1 위치다.</summary>
        private static Vector3 TopPoint(BoxCollider box, float u, float v)
        {
            Vector3 local =
                box.center
                + new Vector3((u - .5f) * box.size.x, box.size.y / 2f, (v - .5f) * box.size.z);
            return box.transform.TransformPoint(local);
        }

        /// <summary>top을 지나는 세로선이 메시와 만나는 가장 높은 점의 높이. top보다 높은 면은 보지 않는다.</summary>
        private static float HighestSurface(MeshFilter filter, Vector3 top, float bottom)
        {
            Bounds bounds = filter.GetComponent<MeshRenderer>().bounds;
            if (
                top.x < bounds.min.x
                || top.x > bounds.max.x
                || top.z < bounds.min.z
                || top.z > bounds.max.z
            )
            {
                return bottom;
            }

            Mesh mesh = filter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
            float highest = bottom;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 a = toWorld.MultiplyPoint3x4(vertices[triangles[index]]);
                Vector3 b = toWorld.MultiplyPoint3x4(vertices[triangles[index + 1]]);
                Vector3 c = toWorld.MultiplyPoint3x4(vertices[triangles[index + 2]]);
                if (TryHeightAt(a, b, c, top.x, top.z, out float height) && height <= top.y + .01f)
                {
                    highest = Mathf.Max(highest, height);
                }
            }

            return highest;
        }

        /// <summary>삼각형을 위에서 내려다볼 때 (x, z)가 안에 있으면 그 자리의 높이를 구한다.</summary>
        private static bool TryHeightAt(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            float x,
            float z,
            out float height
        )
        {
            height = 0f;
            float area = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(area) < 1e-9f)
            {
                return false;
            }

            float wa = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / area;
            float wb = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / area;
            float wc = 1f - wa - wb;
            if (wa < 0f || wb < 0f || wc < 0f)
            {
                return false;
            }

            height = wa * a.y + wb * b.y + wc * c.y;
            return true;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static string PathOf(Transform target)
        {
            return target.parent == null ? target.name : PathOf(target.parent) + "/" + target.name;
        }
    }
}
