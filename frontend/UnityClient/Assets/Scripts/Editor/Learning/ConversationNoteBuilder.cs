using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Learning
{
    /// <summary>
    /// 피드백 목록과 복습장을 [대화 노트] 하나로 합친다(확정 시안 N1~N3). 휴대폰 앱은 다섯 개로 줄이고, HUD의 목록 창을 대화 노트로 바꾸고,
    /// 대화 피드백 창의 [복습장에 추가]와 복습장 목록 부품을 지운다. 각 부분의 모양은 그 화면의 Builder가 정한다.
    /// </summary>
    public static class ConversationNoteBuilder
    {
        private const string HudPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string FeedbackScreenPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";

        [MenuItem("Tools/A305/Learning/Apply Conversation Note")]
        public static void Apply()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(
                ReviewWorkbookBuilder.PrefabPath,
                ReviewWorkbookBuilder.KeepOnlyPracticeResult
            );
            BuilderTools.UpdatePrefabIfExists(
                FeedbackScreenPath,
                root =>
                {
                    Transform save = root.transform.Find("Panel/ReviewSaveButton");
                    if (save != null)
                        Object.DestroyImmediate(save.gameObject);
                }
            );
            BuilderTools.UpdatePrefabIfExists(
                HudPath,
                hud =>
                {
                    FeedbackListBuilder.Apply(hud.transform);
                    PhoneLearningMenuBuilder.Apply(hud);
                }
            );
            AssetDatabase.SaveAssets();
        }
    }
}
