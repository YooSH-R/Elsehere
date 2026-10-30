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
                Object settings = AssetDatabase.LoadMainAssetAtPath(
                    "Assets/TextMesh Pro/Resources/TMP Settings.asset"
                );
                BuilderTools.SetReference(settings, "m_defaultFontAsset", font);
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
    }
}
