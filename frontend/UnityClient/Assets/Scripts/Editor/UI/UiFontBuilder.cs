using System;
using System.IO;
using A305.EditorTools.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace A305.EditorTools.UI
{
    /// <summary>
    /// 한국어를 표시할 UI 글꼴을 준비한다. TextMeshPro 기본 리소스를 가져오고, Pretendard로 TMP 글꼴 에셋을 만들어
    /// TMP 기본 글꼴로 지정한다. 브라우저(WebGL)에는 운영체제 글꼴이 없어 한국어 글꼴을 빌드에 포함해야 한다.
    /// 이미 있는 리소스와 글꼴 에셋은 다시 만들지 않는다.
    /// </summary>
    public static class UiFontBuilder
    {
        public const string MenuPath = "Tools/A305/UI/Set Up Fonts";
        public const string RegularFontAssetPath =
            "Assets/Art/UI/Fonts/Pretendard-Regular SDF.asset";
        public const string BoldFontAssetPath = "Assets/Art/UI/Fonts/Pretendard-Bold SDF.asset";

        /// <summary>Editor 사용 중 채워진 글자를 저장하지 않을 동적 글꼴 에셋</summary>
        public static readonly string[] DynamicFontAssetPaths =
        {
            RegularFontAssetPath,
            BoldFontAssetPath,
        };

        /// <summary>이 Builder가 글꼴 설정을 저장하는 동안에만 true. 그 밖의 저장에서는 동적 글꼴 에셋을 뺀다.</summary>
        public static bool IsSavingFontSettings { get; private set; }

        private const string SourceFontFolder = "Assets/ThirdParty/Pretendard";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string TmpEssentialsPackage =
            "Package Resources/TMP Essential Resources.unitypackage";

        // 이전 프로젝트에서 한국어 표시를 확인한 동적 글꼴 설정
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasSize = 2048;

        /// <summary>
        /// TMP 기본 리소스가 없으면 먼저 가져온다. 가져오기는 Editor가 다음 프레임에 처리하므로, 끝난 뒤 글꼴을 준비한다.
        /// batchmode에서는 끝나면 Editor를 종료하므로 <c>-quit</c> 없이 실행한다.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null)
            {
                SetUpFonts();
                ExitIfBatchMode(0);
                return;
            }

            PackageInfo ugui = PackageInfo.FindForAssetPath("Packages/com.unity.ugui/package.json");
            if (ugui == null)
            {
                throw new InvalidOperationException("com.unity.ugui 패키지를 찾지 못했습니다.");
            }

            AssetDatabase.importPackageCompleted += OnTmpEssentialsImported;
            AssetDatabase.importPackageFailed += OnTmpEssentialsFailed;
            AssetDatabase.ImportPackage(
                Path.Combine(ugui.resolvedPath, TmpEssentialsPackage),
                false
            );
        }

        private static void OnTmpEssentialsImported(string packageName)
        {
            StopWaitingForImport();
            SetUpFonts();
            ExitIfBatchMode(0);
        }

        private static void OnTmpEssentialsFailed(string packageName, string errorMessage)
        {
            StopWaitingForImport();
            Debug.LogError($"TMP 기본 리소스를 가져오지 못했습니다: {errorMessage}");
            ExitIfBatchMode(1);
        }

        private static void StopWaitingForImport()
        {
            AssetDatabase.importPackageCompleted -= OnTmpEssentialsImported;
            AssetDatabase.importPackageFailed -= OnTmpEssentialsFailed;
        }

        private static void SetUpFonts()
        {
            TMP_FontAsset regular = GetOrCreateFontAsset(
                "Pretendard-Regular",
                RegularFontAssetPath
            );
            GetOrCreateFontAsset("Pretendard-Bold", BoldFontAssetPath);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            TMP_FontAsset serviceFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                ServiceFontBuilder.FontPath
            );
            BuilderTools.SetReference(
                settings,
                "m_defaultFontAsset",
                serviceFont != null ? serviceFont : regular
            );

            IsSavingFontSettings = true;
            try
            {
                AssetDatabase.SaveAssets();
            }
            finally
            {
                IsSavingFontSettings = false;
            }

            Debug.Log($"UI 글꼴을 준비했습니다: {RegularFontAssetPath}");
        }

        private static void ExitIfBatchMode(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// 쓰는 글자만 실행 중에 채우는 동적 글꼴 에셋을 만든다. 한글 전체를 미리 담으면 아틀라스가 커지기 때문이다.
        /// 편집 중에 채워진 글자는 빌드할 때 비운다.
        /// </summary>
        private static TMP_FontAsset GetOrCreateFontAsset(string fontName, string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var sourceFont = BuilderTools.LoadRequiredAsset<Font>(
                $"{SourceFontFolder}/{fontName}.otf",
                "Pretendard 글꼴 파일 복사"
            );
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                SamplingPointSize,
                AtlasPadding,
                GlyphRenderMode.SDFAA,
                AtlasSize,
                AtlasSize,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true
            );
            fontAsset.name = Path.GetFileNameWithoutExtension(assetPath);

            BuilderTools.EnsureFolder(Path.GetDirectoryName(assetPath));
            AssetDatabase.CreateAsset(fontAsset, assetPath);
            fontAsset.atlasTextures[0].name = $"{fontAsset.name} Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            fontAsset.material.name = $"{fontAsset.name} Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            var fields = new SerializedObject(fontAsset);
            fields.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }
    }
}
