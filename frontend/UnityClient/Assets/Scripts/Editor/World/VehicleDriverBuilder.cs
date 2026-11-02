using System;
using A305.EditorTools.Common;
using A305.Npc;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>기존 택시 기사의 운전 자세를 승용차에 재사용한다. 씬 배치와 택시 대화 연결은 유지한다.</summary>
    public static class VehicleDriverBuilder
    {
        private const string TownPath = "Assets/Scenes/Production/TownScene.unity";
        private const string SedanPath = "Assets/Prefabs/World/StreetLife/Sedan.prefab";

        [MenuItem("Tools/A305/World/Adjust Driver Steering")]
        public static void AdjustSteering()
        {
            if (!BuilderTools.CanBuild())
                return;
            var scene = EditorSceneManager.OpenScene(TownPath);
            TaxiRide taxi = BuilderTools.FindSingleInScene<TaxiRide>(scene);
            FitSteering(taxi.transform, taxi.GetComponentInChildren<NpcIdentity>(true).transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            GameObject sedan = PrefabUtility.LoadPrefabContents(SedanPath);
            try
            {
                FitSteering(sedan.transform, sedan.transform.Find("Driver"));
                PrefabUtility.SaveAsPrefabAsset(sedan, SedanPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(sedan);
            }
            AssetDatabase.SaveAssets();
            VehicleDriverPreview.Capture();
        }

        internal static void FitSteering(Transform car, Transform driver)
        {
            Transform wheel = car.Find("SteeringWheel");
            Transform column = car.Find("SteeringColumn");
            // 큰 머리 앞에 여유를 두고, 짧은 팔이 잡을 수 있도록 핸들을 낮춘다.
            wheel.localPosition = new Vector3(-.38f, .76f, .30f);
            wheel.localScale = new Vector3(.30f, .03f, .30f);
            wheel.localRotation = Quaternion.Euler(25, 0, 0);
            column.localPosition = new Vector3(-.38f, .62f, .37f);
            column.localRotation = Quaternion.Euler(25, 0, 0);

            Animator animator = driver.GetComponentInChildren<Animator>(true);
            FitHand(
                car,
                animator,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
                new Vector3(-.46f, .83f, .13f),
                -car.right
            );
            FitHand(
                car,
                animator,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand,
                new Vector3(-.30f, .83f, .13f),
                car.right
            );
            foreach (Transform bone in driver.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
            PrefabUtility.RecordPrefabInstancePropertyModifications(wheel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(column);
        }

        private static void FitHand(
            Transform car,
            Animator animator,
            HumanBodyBones upper,
            HumanBodyBones lower,
            HumanBodyBones handBone,
            Vector3 grip,
            Vector3 bend
        )
        {
            TaxiCabinBuilder.SolveLimb(
                animator,
                upper,
                lower,
                handBone,
                car.TransformPoint(grip),
                bend
            );
        }

        [MenuItem("Tools/A305/World/Add Vehicle Drivers")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;

            var scene = EditorSceneManager.OpenScene(TownPath);
            TaxiRide taxi = BuilderTools.FindSingleInScene<TaxiRide>(scene);
            NpcIdentity sourceDriver = taxi.GetComponentInChildren<NpcIdentity>(true);
            if (sourceDriver == null)
                throw new InvalidOperationException("택시의 기존 기사 모델이 필요합니다.");

            GameObject sedan = PrefabUtility.LoadPrefabContents(SedanPath);
            try
            {
                // 재실행할 때 Inspector에서 조절한 운전자 외형과 자세를 덮어쓰지 않는다.
                if (sedan.transform.Find("Driver") != null)
                    return;

                GameObject driver = CopyChild(sourceDriver.transform, sedan.transform, "Driver");
                // 승용차 운전자는 외형만 사용한다. 택시 대화 대상이나 플레이어 보정 기능을 복제하지 않는다.
                foreach (
                    MonoBehaviour behaviour in driver.GetComponentsInChildren<MonoBehaviour>(true)
                )
                    Object.DestroyImmediate(behaviour);
                foreach (Collider collider in driver.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);
                foreach (Animator animator in driver.GetComponentsInChildren<Animator>(true))
                    animator.enabled = false;

                CopyChild(taxi.transform.Find("SteeringWheel"), sedan.transform, "SteeringWheel");
                CopyChild(taxi.transform.Find("SteeringColumn"), sedan.transform, "SteeringColumn");
                PrefabUtility.SaveAsPrefabAsset(sedan, SedanPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(sedan);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(
                "승용차 프리팹에 운전자와 운전대를 추가했습니다. 기존 씬 인스턴스에도 적용됩니다."
            );
        }

        private static GameObject CopyChild(Transform source, Transform parent, string name)
        {
            if (source == null)
                throw new InvalidOperationException($"택시에서 {name}을 찾지 못했습니다.");
            GameObject copy = Object.Instantiate(source.gameObject, parent, false);
            copy.name = name;
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
            return copy;
        }
    }
}
