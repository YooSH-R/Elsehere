using A305.EditorTools.Common;
using A305.EditorTools.Learning;
using A305.EditorTools.Theme;
using A305.Theme;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Onboarding
{
    /// <summary>
    /// HUD 프리팹의 내 정보 창을 내 정보와 설정 창으로 나누고, 휴대폰에 [설정] 앱을 넣고, 오른쪽 위 스피커를 없앤다(S15P31A305-861).
    /// HUD 전체 Builder를 다시 돌리면 다른 Builder가 마무리한 디자인까지 바뀌므로 이 구성만 다룬다. 다시 실행해도 결과가 같다.
    /// </summary>
    public static class SettingsWindowBuilder
    {
        private const string HudPath = "Assets/Prefabs/Learning/LearningHud.prefab";

        [MenuItem("Tools/A305/Onboarding/Apply Settings Window")]
        public static void ApplyToPrefab()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(HudPath, Apply);
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject hud)
        {
            // 효과음은 설정 창의 [켜기]·[끄기]로 바꾼다.
            foreach (string speaker in new[] { "BackgroundSoundButton", "BackgroundSoundVolume" })
            {
                Transform old = hud.transform.Find(speaker);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }

            LearningBuilder.SplitSettings(hud);
            PhoneLearningMenuBuilder.Apply(hud);
            MyPageLayoutBuilder.Apply(hud);
            foreach (string window in new[] { "MyPage", "Settings" })
            {
                foreach (
                    ThemedElement element in hud
                        .transform.Find(window)
                        .GetComponentsInChildren<ThemedElement>(true)
                )
                    element.ApplyCurrent();
            }
        }
    }
}
