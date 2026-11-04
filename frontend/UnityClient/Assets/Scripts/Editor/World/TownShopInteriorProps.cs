using System;
using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 매장 실내 소품 프리팹. KayKit 모델을 감싼 래퍼와 기본 도형으로 만든 조각을 <see cref="Folder"/>에 없을 때만 만든다.
    /// 소품은 뜻이 맞는 기존 호버 단어만 다시 쓰고(맞는 단어가 없으면 비움), 바닥에 놓는 큰 가구에만 이동 충돌을 둔다.
    /// </summary>
    internal static class TownShopInteriorProps
    {
        internal const string Folder = "Assets/Prefabs/World/TownInteriors/Props";
        internal const string ArtFolder = "Assets/Art/World/TownInteriors";
        internal const string MeshFolder = ArtFolder + "/Meshes";

        private const string FurnitureSource = "Assets/ThirdParty/KayKitFurnitureBits/";
        private const string RestaurantSource = "Assets/ThirdParty/KayKitRestaurantBits/";

        /// <summary>
        /// KayKit 모델 하나. KayKit은 바닥 한 칸이 2m라 대두 캐릭터(1.6m) 옆 가구 크기로 줄인다.
        /// 주방 가구는 깊이도 2m라 앞뒤만 더 줄여 8m 매장 벽에 붙인다.
        /// </summary>
        private sealed class KitModel
        {
            public KitModel(string name, string source, Vector3 scale, string word, bool solid)
            {
                Name = name;
                Source = source;
                Scale = scale;
                Word = word;
                Solid = solid;
            }

            public string Name { get; }
            public string Source { get; }
            public Vector3 Scale { get; }
            public string Word { get; }
            public bool Solid { get; }
        }

        private static readonly Vector3 FurnitureScale = Vector3.one * .7f;
        private static readonly Vector3 KitchenScale = new Vector3(.8f, .88f, .42f);

        private static readonly KitModel[] KitModels =
        {
            // 가구(Furniture Bits)
            FromFurniture("Armchair", "armchair_pillows", FurnitureScale, "chair", true),
            FromFurniture("CushionChair", "chair_A", FurnitureScale, "chair", true),
            FromFurniture("WoodChair", "chair_B_wood", FurnitureScale, "chair", true),
            FromFurniture("WoodStool", "chair_stool_wood", FurnitureScale, "stool", true),
            FromFurniture("SquareTable", "table_medium", FurnitureScale, "table", true),
            FromFurniture("SideTable", "table_small", FurnitureScale, "table", true),
            FromFurniture("LowCabinet", "cabinet_medium", FurnitureScale, "cabinet", true),
            FromFurniture(
                "DecoratedCabinet",
                "cabinet_medium_decorated",
                FurnitureScale,
                "cabinet",
                true
            ),
            FromFurniture("SmallCabinet", "cabinet_small", FurnitureScale, "cabinet", true),
            FromFurniture(
                "DecoratedSmallCabinet",
                "cabinet_small_decorated",
                FurnitureScale,
                "cabinet",
                true
            ),
            FromFurniture(
                "WallShelfLongDecorated",
                "shelf_B_large_decorated",
                FurnitureScale,
                "shelf",
                false
            ),
            FromFurniture(
                "WallShelfShortDecorated",
                "shelf_B_small_decorated",
                FurnitureScale,
                "shelf",
                false
            ),
            FromFurniture("StandingLamp", "lamp_standing", FurnitureScale, "lamp", false),
            FromFurniture("TableLamp", "lamp_table", Vector3.one * .45f, "lamp", false),
            FromFurniture(
                "PictureSmallA",
                "pictureframe_small_A",
                Vector3.one * .6f,
                "picture",
                false
            ),
            FromFurniture(
                "PictureSmallB",
                "pictureframe_small_B",
                Vector3.one * .6f,
                "picture",
                false
            ),
            FromFurniture(
                "StandingPicture",
                "pictureframe_standing_A",
                Vector3.one * .45f,
                "picture",
                false
            ),
            FromFurniture("RugYellow", "rug_rectangle_A", FurnitureScale, "rug", false),
            FromFurniture("RugBlue", "rug_rectangle_B", FurnitureScale, "rug", false),
            FromFurniture(
                "RugStripesYellow",
                "rug_rectangle_stripes_A",
                FurnitureScale,
                "rug",
                false
            ),
            FromFurniture(
                "RugStripesBlue",
                "rug_rectangle_stripes_B",
                FurnitureScale,
                "rug",
                false
            ),
            FromFurniture("RugOvalYellow", "rug_oval_A", FurnitureScale, "rug", false),
            FromFurniture("RugOvalBlue", "rug_oval_B", FurnitureScale, "rug", false),
            FromFurniture("PillowA", "pillow_A", Vector3.one * .55f, "pillow", false),
            FromFurniture("PillowB", "pillow_B", Vector3.one * .55f, "pillow", false),
            FromFurniture("BookSet", "book_set", Vector3.one * .45f, "book", false),
            FromFurniture("CactusMedium", "cactus_medium_A", Vector3.one * .6f, "plant", false),
            // 주방·식당(Restaurant Bits)
            FromRestaurant("Stove", "stove_multi", KitchenScale, "stove", true),
            FromRestaurant("RangeHood", "extractorhood", KitchenScale, null, false),
            FromRestaurant(
                "KitchenFridge",
                "fridge_A_decorated",
                new Vector3(.5f, .8f, .4f),
                "refrigerator",
                true
            ),
            FromRestaurant(
                "KitchenCounter",
                "kitchencounter_straight_A",
                KitchenScale,
                "kitchen_counter",
                true
            ),
            FromRestaurant(
                "KitchenCounterDecorated",
                "kitchencounter_straight_A_decorated",
                KitchenScale,
                "kitchen_counter",
                true
            ),
            FromRestaurant("BunCrate", "crate_buns", Vector3.one * .4f, "crate", false),
            FromRestaurant("WoodCrate", "crate", Vector3.one * .4f, "crate", false),
            FromRestaurant("JarTall", "jar_A_large", Vector3.one * .4f, "jar", false),
            FromRestaurant("JarRed", "jar_B_large", Vector3.one * .4f, "jar", false),
            FromRestaurant("JarGreen", "jar_C_medium", Vector3.one * .4f, "jar", false),
            FromRestaurant("JarAmber", "jar_D_large", Vector3.one * .4f, "jar", false),
            FromRestaurant("CookingPot", "pot_A", Vector3.one * .35f, null, false),
            FromRestaurant("FryingPan", "pan_A", Vector3.one * .35f, null, false),
            FromRestaurant("CuttingBoard", "cuttingboard", Vector3.one * .35f, null, false),
            FromRestaurant("DinerPlate", "plate", Vector3.one * .3f, "plate", false),
            FromRestaurant("Burger", "food_burger", Vector3.one * .28f, "plate", false),
            FromRestaurant("Ketchup", "ketchup", Vector3.one * .3f, null, false),
            FromRestaurant("Mustard", "mustard", Vector3.one * .3f, null, false),
            FromRestaurant("TableMenu", "menu", Vector3.one * .35f, "menu", false),
        };

        private static KitModel FromFurniture(
            string name,
            string model,
            Vector3 scale,
            string word,
            bool solid
        ) => new KitModel(name, FurnitureSource + model + ".fbx", scale, word, solid);

        private static KitModel FromRestaurant(
            string name,
            string model,
            Vector3 scale,
            string word,
            bool solid
        ) => new KitModel(name, RestaurantSource + model + ".fbx", scale, word, solid);

        // 다시 만들기 동안 이미 덮어쓴 프리팹 경로. null이면 평소처럼 없을 때만 만든다.
        private static HashSet<string> overwritten;

        /// <summary>이후 만드는 소품·공간 프리팹을 한 번씩 새로 저장한다. 손으로 고친 디자인은 사라진다.</summary>
        internal static void BeginOverwrite() => overwritten = new HashSet<string>();

        internal static void EndOverwrite() => overwritten = null;

        /// <summary>프리팹을 없을 때만 만든다. 다시 만들기 중이면 경로마다 한 번 덮어써 같은 GUID를 유지한다.</summary>
        internal static GameObject CreateOrOverwrite(string path, Func<GameObject> build) =>
            overwritten != null && overwritten.Add(path)
                ? BuilderTools.SavePrefab(build(), path)
                : BuilderTools.CreatePrefabIfMissing(path, build);

        /// <summary>KayKit 래퍼를 놓는다. 래퍼가 없으면 먼저 만든다.</summary>
        internal static Transform Place(
            string name,
            Transform parent,
            Vector3 position,
            float yaw = 0
        )
        {
            KitModel model = KitModels.SingleOrDefault(candidate => candidate.Name == name);
            if (model == null)
                throw new InvalidOperationException("등록하지 않은 KayKit 소품입니다: " + name);
            GameObject prefab = CreateOrOverwrite(
                Folder + "/" + name + ".prefab",
                () => Wrap(model)
            );
            return BuilderTools
                .InstantiatePrefab(prefab, parent, position, Quaternion.Euler(0, yaw, 0))
                .transform;
        }

        // 원본 FBX를 자식으로 두고 크기만 줄인다. 피벗은 원본(바닥 가운데, 벽 소품은 벽에 닿는 뒷면)을 따른다.
        private static GameObject Wrap(KitModel model)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(model.Source);
            if (source == null)
                throw new InvalidOperationException("KayKit 원본이 없습니다: " + model.Source);
            var root = new GameObject(model.Name);
            GameObject visual = BuilderTools.InstantiatePrefab(
                source,
                root.transform,
                Vector3.zero,
                Quaternion.identity
            );
            visual.transform.localScale = model.Scale;
            Material atlas = Atlas(model.Source.StartsWith(FurnitureSource));
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => atlas).ToArray();
            if (model.Solid)
                AddMovementCollider(root);
            if (model.Word != null)
                NorthBlockAssets.Bind(root, model.Word);
            return root;
        }

        /// <summary>
        /// 바닥에 놓는 큰 가구의 이동 충돌. 겉모양을 감싼 상자를 달고, 의자 등받이처럼 상자 윗면이 실제 표면보다 뜨면
        /// 그 위에 올라선 캐릭터가 떠 보이므로 <see cref="PropColliderShape"/>로 메시 모양 충돌로 바꾼다.
        /// </summary>
        private static void AddMovementCollider(GameObject root)
        {
            Bounds bounds = LocalBounds(root);
            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
            Physics.SyncTransforms();
            if (PropColliderShape.TopGap(box) > PropColliderShape.AllowedGapMeters)
                PropColliderShape.FitToShape(box);
        }

        /// <summary>선반·탁자 위에 올린 재사용 프리팹의 이동 충돌을 뺀다. 호버 영역(Trigger)은 남는다.</summary>
        internal static void RemoveMovementColliders(GameObject placed)
        {
            foreach (
                Collider collider in placed
                    .GetComponentsInChildren<Collider>(true)
                    .Where(collider => !collider.isTrigger)
            )
                Object.DestroyImmediate(collider);
        }

        // KayKit은 팩마다 그라데이션 아틀라스 한 장을 쓴다. 마을의 무광 재질에 맞춘다.
        private static Material Atlas(bool furniture)
        {
            string name = furniture ? "KayKitFurnitureAtlas" : "KayKitRestaurantAtlas";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(ArtFolder + "/" + name + ".mat");
            if (existing != null)
                return existing;
            Material material = BuilderTools.GetOrCreateMaterial(ArtFolder, name, Color.white);
            material.SetTexture(
                "_BaseMap",
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    furniture
                        ? FurnitureSource + "furniturebits_texture.png"
                        : RestaurantSource + "restaurantbits_texture.png"
                )
            );
            material.SetFloat("_Smoothness", .16f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Bounds LocalBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Matrix4x4 toLocal = root.transform.worldToLocalMatrix;
            Bounds bounds = default;
            bool first = true;
            foreach (Renderer renderer in renderers)
            {
                Bounds world = renderer.bounds;
                foreach (Vector3 corner in Corners(world))
                {
                    Vector3 local = toLocal.MultiplyPoint3x4(corner);
                    if (first)
                        bounds = new Bounds(local, Vector3.zero);
                    else
                        bounds.Encapsulate(local);
                    first = false;
                }
            }
            return bounds;
        }

        private static IEnumerable<Vector3> Corners(Bounds bounds)
        {
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
        }

        /// <summary>
        /// 도형 조각 프리팹. 없을 때만 만들고, 같은 재질끼리 메시를 합쳐 Renderer 수를 줄인다.
        /// <paramref name="solid"/>이면 바닥에 놓는 큰 가구로 보고 이동 충돌을 단다.
        /// </summary>
        internal static Transform Piece(
            string name,
            Transform parent,
            Vector3 position,
            float yaw,
            bool solid,
            Func<GameObject> build
        )
        {
            GameObject prefab = CreateOrOverwrite(
                Folder + "/" + name + ".prefab",
                () =>
                {
                    GameObject root = build();
                    root.name = name;
                    Combine(root, name);
                    if (solid)
                        AddMovementCollider(root);
                    return root;
                }
            );
            return BuilderTools
                .InstantiatePrefab(prefab, parent, position, Quaternion.Euler(0, yaw, 0))
                .transform;
        }

        // 글자(TextMeshPro)와 중첩 프리팹은 그대로 두고, 직접 만든 도형만 재질별로 합친다.
        private static void Combine(GameObject root, string name)
        {
            MeshFilter[] parts = root.GetComponentsInChildren<MeshFilter>(true)
                .Where(part =>
                    part.GetComponent<TMPro.TMP_Text>() == null
                    && !PrefabUtility.IsPartOfPrefabInstance(part.gameObject)
                    && part.GetComponent<MeshRenderer>() != null
                )
                .ToArray();
            BuilderTools.EnsureFolder(MeshFolder);
            foreach (
                var group in parts.GroupBy(part => part.GetComponent<MeshRenderer>().sharedMaterial)
            )
            {
                CombineInstance[] instances = group
                    .Select(part => new CombineInstance
                    {
                        mesh = part.sharedMesh,
                        transform =
                            root.transform.worldToLocalMatrix * part.transform.localToWorldMatrix,
                    })
                    .ToArray();
                string path = MeshFolder + "/" + name + "_" + group.Key.name + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                {
                    mesh = new Mesh { name = name + "_" + group.Key.name };
                    mesh.CombineMeshes(instances);
                    AssetDatabase.CreateAsset(mesh, path);
                }
                else
                {
                    // 다시 만들기: 같은 메시 에셋에 새 모양을 담아 참조를 유지한다.
                    mesh.Clear();
                    mesh.CombineMeshes(instances);
                    EditorUtility.SetDirty(mesh);
                }
                MainstreetArtKit.MeshObject(
                    group.Key.name,
                    root.transform,
                    Vector3.zero,
                    Vector3.one,
                    mesh,
                    group.Key
                );
            }
            foreach (MeshFilter part in parts)
            {
                Object.DestroyImmediate(part.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(part);
            }
            // 메시를 뺀 뒤 아무것도 남지 않은 도형 객체를 지운다. 글자를 품은 묶음은 남긴다.
            Transform[] empty;
            do
            {
                empty = root.GetComponentsInChildren<Transform>(true)
                    .Where(node =>
                        node != root.transform
                        && node.childCount == 0
                        && node.GetComponents<Component>().Length == 1
                    )
                    .ToArray();
                foreach (Transform node in empty)
                    Object.DestroyImmediate(node.gameObject);
            } while (empty.Length > 0);
        }
    }
}
