using System;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Mission;
using A305.Npc;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 마을 벤치와 호텔 소파에 카페 의자와 같은 앉기 자리를 붙인다. 가구 배치와 디자인은 그대로 두고 가구 자식으로 자리 표시만 맞춘다.
    /// </summary>
    public static class BenchSofaSeatBuilder
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Production/TownScene.unity",
            "Assets/Scenes/Production/HotelScene.unity",
        };

        // 앉는 면은 가구 원점에서 잰 값이고 앞(+z)이 앉아서 보는 방향이다. 자리는 가구 폭 방향(x)으로 놓는다.
        // 같은 모델을 그리는 가구는 프리팹 중첩이나 풀기와 상관없이 같은 모양으로 본다.
        private static readonly (
            string meshPath,
            float cushionTopMeters,
            float frontEdgeMeters,
            float[] seatOffsetsMeters
        )[] SeatModels =
        {
            // 메인스트리트 벤치(MainstreetArtKit.Bench): 좌판 널 윗면 0.325m, 앞 끝 0.205m. 폭 1.6m를 반씩 나눠 두 자리
            (
                "Assets/Art/World/MainstreetArt/Meshes/ParkBench_HoneyWood.asset",
                .325f,
                .205f,
                new[] { -.4f, .4f }
            ),
            // 공원 벤치(TinyTreats bench.fbx를 폭 1.6m로 0.8배): 좌판 윗면 0.40m, 앞 모서리 0.527m. 폭을 반씩 나눠 두 자리
            (
                "Assets/ThirdParty/TinyTreats/PrettyPark/Models/bench.fbx",
                .40f,
                .527f,
                new[] { -.4f, .4f }
            ),
            // 세이지 소파(HomeInteriorKit.Sofa): 쿠션 윗면 0.705m, 앞 모서리 0.485m. 쿠션 두 개의 가운데가 두 자리
            (
                "Assets/Art/World/MainstreetArt/Meshes/Home_SageSofa_Sage.asset",
                .705f,
                .485f,
                new[] { -.57f, .57f }
            ),
        };

        [MenuItem("Tools/A305/Mission/Connect Bench And Sofa Sitting")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            foreach (string path in ScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(path);
                Connect(scene);
                BuilderTools.SaveScene(scene, path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Bench and sofa sitting connected in town and hotel scenes.");
        }

        private static void Connect(Scene scene)
        {
            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            DialogueController dialogue = BuilderTools.FindSingleInScene<DialogueController>(scene);
            PasserbyOfferView offer = BuilderTools.FindSingleInScene<PasserbyOfferView>(scene);
            BuilderTools.GetOrAddComponent<PlayerSeatPose>(player.gameObject);

            MeshFilter[] models = BuilderTools.FindAllInScene<MeshFilter>(scene);
            int furnitureCount = 0;
            foreach (var seatModel in SeatModels)
            foreach (MeshFilter model in models.Where(filter => Draws(filter, seatModel.meshPath)))
            {
                Transform furniture = model.transform.parent;
                for (int index = 0; index < seatModel.seatOffsetsMeters.Length; index++)
                {
                    RestSeat seat = GetOrAddSeat(
                        furniture,
                        $"RestSeat{index + 1}",
                        seatModel.seatOffsetsMeters[index]
                    );
                    BuilderTools.SetReference(seat, "player", player);
                    BuilderTools.SetReference(seat, "dialogue", dialogue);
                    BuilderTools.SetReference(seat, "offerView", offer);
                    var fields = new SerializedObject(seat);
                    fields.FindProperty("cushionTopMeters").floatValue = seatModel.cushionTopMeters;
                    fields.FindProperty("frontEdgeMeters").floatValue = seatModel.frontEdgeMeters;
                    fields.ApplyModifiedPropertiesWithoutUndo();
                }
                furnitureCount++;
            }
            if (furnitureCount == 0)
                throw new InvalidOperationException("No bench or sofa to sit on in " + scene.name);
        }

        private static bool Draws(MeshFilter model, string meshPath) =>
            model.gameObject.activeInHierarchy
            && model.sharedMesh != null
            && AssetDatabase.GetAssetPath(model.sharedMesh) == meshPath;

        private static RestSeat GetOrAddSeat(Transform furniture, string name, float offsetMeters)
        {
            Transform marker = furniture.Find(name);
            if (marker == null)
            {
                marker = new GameObject(name).transform;
                marker.SetParent(furniture, false);
            }
            marker.localPosition = new Vector3(offsetMeters, 0f, 0f);
            marker.localRotation = Quaternion.identity;
            marker.localScale = Vector3.one;
            return BuilderTools.GetOrAddComponent<RestSeat>(marker.gameObject);
        }
    }
}
