using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;

namespace A305.EditorTools.Player
{
    /// <summary>
    /// 장소마다 따로 있는 카메라 설정 모두에 3인칭에서 수평보다 올려다볼 수 있는 각도를 맞춘다.
    /// 새로 만드는 설정은 <see cref="PlayerCameraSettings"/>의 기본값을 쓰므로, 이미 있는 설정만 고친다.
    /// </summary>
    public static class CameraLookUpLimitBuilder
    {
        public const string MenuPath = "Tools/A305/Player/Apply Camera Look-Up Limit";

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            foreach (
                string guid in AssetDatabase.FindAssets(
                    "t:PlayerCameraSettings",
                    new[] { "Assets" }
                )
            )
            {
                var settings = AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>(
                    AssetDatabase.GUIDToAssetPath(guid)
                );
                var values = new SerializedObject(settings);
                values.FindProperty("thirdPersonMinPitchDegrees").floatValue =
                    -PlayerCameraSettings.ThirdPersonLookUpDegrees;
                values.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
