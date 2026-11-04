using System.Collections.Generic;
using A305.EditorTools.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace A305.EditorTools.UI
{
    /// <summary>화면 테마와 관계없이 한글·영문 UI에 같은 라운드 글꼴을 쓴다.</summary>
    public static class ServiceFontBuilder
    {
        public const string FontPath = "Assets/Art/UI/Fonts/NanumSquareRound SDF.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        // 글꼴 기본 줄 높이(글자 크기의 1.13배)로는 한글 여러 줄이 맞닿아 보여 1.4배로 띄운다.
        private const float ReadableLineHeight = 1.4f;

        public static bool IsSaving { get; private set; }

        [MenuItem("Tools/A305/UI/Apply Service Font")]
        public static void Prepare()
        {
            IsSaving = true;
            try
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (font == null)
                {
                    Font source = BuilderTools.LoadRequiredAsset<Font>(
                        "Assets/ThirdParty/NanumSquareRound/NanumSquareRoundOTFR.otf",
                        "서비스 기본 글꼴"
                    );
                    font = TMP_FontAsset.CreateFontAsset(
                        source,
                        90,
                        9,
                        GlyphRenderMode.SDFAA,
                        2048,
                        2048,
                        AtlasPopulationMode.Dynamic,
                        enableMultiAtlasSupport: true
                    );
                    font.name = "NanumSquareRound SDF";
                    font.fallbackFontAssetTable = new List<TMP_FontAsset>
                    {
                        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                            UiFontBuilder.RegularFontAssetPath
                        ),
                    };
                    AssetDatabase.CreateAsset(font, FontPath);
                    font.atlasTextures[0].name = font.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
                    font.material.name = font.name + " Material";
                    AssetDatabase.AddObjectToAsset(font.material, font);
                    var fields = new SerializedObject(font);
                    fields.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
                    fields.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (
                    string guid in AssetDatabase.FindAssets(
                        "t:UiThemeDefinition",
                        new[] { "Assets/Data/Theme" }
                    )
                )
                {
                    Object theme = AssetDatabase.LoadMainAssetAtPath(
                        AssetDatabase.GUIDToAssetPath(guid)
                    );
                    BuilderTools.SetReference(theme, "titleFont", font);
                }
                Object settings = AssetDatabase.LoadMainAssetAtPath(TmpSettingsPath);
                BuilderTools.SetReference(settings, "m_defaultFontAsset", font);
                SetReadableSpacing(font, settings);
                foreach (
                    string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                )
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab.GetComponentsInChildren<TMP_Text>(true).Length == 0)
                        continue;
                    BuilderTools.UpdatePrefabIfExists(
                        path,
                        root =>
                        {
                            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                                text.font = font;
                        }
                    );
                }
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                IsSaving = false;
            }
        }

        /// <summary>서비스 글꼴의 줄 간격과 한글 줄바꿈만 맞춘다. 프리팹은 다시 저장하지 않는다.</summary>
        [MenuItem("Tools/A305/UI/Apply Readable Text Spacing")]
        public static void ApplyReadableSpacing()
        {
            IsSaving = true;
            try
            {
                SetReadableSpacing(
                    BuilderTools.LoadRequiredAsset<TMP_FontAsset>(FontPath, "서비스 기본 글꼴"),
                    AssetDatabase.LoadMainAssetAtPath(TmpSettingsPath)
                );
                AssetDatabase.SaveAssets();
            }
            finally
            {
                IsSaving = false;
            }
        }

        // 한글은 TMP 기본값이면 글자마다 줄을 바꿀 수 있어 '맡/기고'처럼 단어가 갈라진다. 영어처럼 띄어쓰기에서 바꾼다.
        private static void SetReadableSpacing(TMP_FontAsset font, Object settings)
        {
            var fontFields = new SerializedObject(font);
            fontFields.FindProperty("m_FaceInfo.m_LineHeight").floatValue =
                font.faceInfo.pointSize * ReadableLineHeight;
            fontFields.ApplyModifiedPropertiesWithoutUndo();
            var settingFields = new SerializedObject(settings);
            settingFields.FindProperty("m_UseModernHangulLineBreakingRules").boolValue = true;
            settingFields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(settings);
        }
    }
}
