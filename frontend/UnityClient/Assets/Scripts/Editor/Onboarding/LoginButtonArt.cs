using System.IO;
using A305.EditorTools.Theme;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Onboarding
{
    /// <summary>로그인 버튼의 글자 없는 공용 바탕과 게스트 아이콘. 글자는 TMP로 따로 배치한다.</summary>
    internal static class LoginButtonArt
    {
        private const string Folder = "Assets/Art/UI/Login/";

        public static void Ensure()
        {
            if (!File.Exists(Folder + "LoginButtonSurface.png"))
            {
                var canvas = new UiArtCanvas(448, 124);
                canvas.SoftShadow(24, 28, 424, 92, 32, 10, new Color(0.25f, 0.2f, 0.14f, .20f));
                canvas.RoundRect(24, 30, 424, 94, 32, new Color32(208, 197, 177, 255));
                canvas.RoundRect(24, 36, 424, 100, 32, new Color32(255, 253, 247, 255));
                canvas.RoundRect(35, 87, 413, 99, 6, new Color(1, 1, 1, .42f));
                Save("LoginButtonSurface", canvas);
            }
            if (!File.Exists(Folder + "LoginGoogleIcon.png"))
            {
                var original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                original.LoadImage(File.ReadAllBytes(Folder + "LoginGoogle.png"));
                var mark = new Texture2D(50, 50, TextureFormat.RGBA32, false);
                Color[] pixels = original.GetPixels(307, 132, 50, 50);
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color pixel = pixels[index];
                    float saturation =
                        Mathf.Max(pixel.r, pixel.g, pixel.b) - Mathf.Min(pixel.r, pixel.g, pixel.b);
                    if (saturation < .08f)
                        pixels[index] = Color.clear;
                }
                mark.SetPixels(pixels);
                mark.Apply();
                SaveTexture("LoginGoogleIcon", mark);
                Object.DestroyImmediate(original);
            }
            if (!File.Exists(Folder + "LoginGuestIcon.png"))
            {
                var canvas = new UiArtCanvas(64, 64);
                canvas.Circle(32, 43, 10, Color.white);
                canvas.RoundRect(8, 6, 56, 30, 18, Color.white);
                Save("LoginGuestIcon", canvas);
            }
        }

        private static void Save(string name, UiArtCanvas canvas)
        {
            SaveTexture(name, canvas.ToTexture());
        }

        private static void SaveTexture(string name, Texture2D texture)
        {
            string path = Folder + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
