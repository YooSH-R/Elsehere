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

        /// <summary>
        /// 효과음 카드에 크기 막대 줄만 덧붙이고 칠한다(S15P31A305-949). 창 전체를 다시 맞추면 휴대폰 메뉴 아이콘까지
        /// 새로 만들어지므로 이 카드만 다룬다. 다시 실행해도 결과가 같다.
        /// </summary>
        [MenuItem("Tools/A305/Onboarding/Add Sound Volume Slider")]
        public static void ApplySoundVolumeToPrefab()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(
                HudPath,
                hud =>
                {
                    LearningBuilder.AddSoundVolume(hud);
                    MyPageLayoutBuilder.ApplySoundEffects(hud);
                    foreach (
                        ThemedElement element in hud
                            .transform.Find("Settings/Window/SoundEffects")
                            .GetComponentsInChildren<ThemedElement>(true)
                    )
                        element.ApplyCurrent();
                }
            );
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject hud)
        {
            // 효과음은 설정 창의 [켜기]·[끄기]와 크기 막대로 바꾼다.
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
