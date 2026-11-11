using System.IO;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.UI
{
    /// <summary>
    /// 웹 빌드의 텍스처 압축(DXT)은 4픽셀 블록 단위라, 가로·세로가 4의 배수가 아닌 UI 그림은 원본(픽셀당 4바이트)으로 들어간다.
    /// 그런 그림의 오른쪽·위를 투명하게 1~3픽셀 덧대 4의 배수로 맞춘다. 9-slice 테두리는 덧댄 만큼 오른쪽·위를 늘려
    /// 늘어나는 가운데 영역과 모서리 모양을 그대로 둔다. 화면에서 그림은 0.3% 이하로만 작아진다.
    /// </summary>
    public static class UiImagePadding
    {
        private const string UiFolder = "Assets/Art/UI";

        [MenuItem("Tools/A305/UI/Pad UI Images To Multiple Of 4")]
        public static void PadAll()
        {
            int padded = 0;
            foreach (
                string file in Directory.GetFiles(UiFolder, "*.png", SearchOption.AllDirectories)
            )
            {
                if (Pad(file.Replace('\\', '/')))
                {
                    padded++;
                }
            }

            Debug.Log($"UI 그림 {padded}장을 4의 배수로 덧댔습니다.");
        }

        /// <returns>덧댔으면 true. 이미 4의 배수면 그대로 두고 false.</returns>
        public static bool Pad(string assetPath)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(assetPath));
            int width = source.width;
            int height = source.height;
            int paddedWidth = (width + 3) / 4 * 4;
            int paddedHeight = (height + 3) / 4 * 4;
            if (paddedWidth == width && paddedHeight == height)
            {
                Object.DestroyImmediate(source);
                return false;
            }

            // 텍스처 좌표는 왼쪽 아래가 원점이라 원본을 (0, 0)에 두면 덧댄 칸은 오른쪽과 위에 생긴다.
            var padded = new Texture2D(paddedWidth, paddedHeight, TextureFormat.RGBA32, false);
            padded.SetPixels32(new Color32[paddedWidth * paddedHeight]);
            padded.SetPixels32(0, 0, width, height, source.GetPixels32());
            padded.Apply();
            File.WriteAllBytes(assetPath, padded.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(padded);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            Vector4 border = importer.spriteBorder;
            if (border != Vector4.zero)
            {
                // spriteBorder는 왼쪽·아래·오른쪽·위 순서다.
                importer.spriteBorder = new Vector4(
                    border.x,
                    border.y,
                    border.z + paddedWidth - width,
                    border.w + paddedHeight - height
                );
            }
            importer.SaveAndReimport();
            return true;
        }
    }
}
