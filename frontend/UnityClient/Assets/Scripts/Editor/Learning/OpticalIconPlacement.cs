using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Learning
{
    /// <summary>투명 여백과 캐스트 그림자를 제외한 실제 그림을 가운데 맞춘다.</summary>
    public static class OpticalIconPlacement
    {
        public static Rect Measure(Sprite sprite, float height)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!t.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))))
                    throw new InvalidOperationException("아이콘을 읽지 못했습니다.");
                Color32[] pixels = t.GetPixels32();
                int left = t.width,
                    right = -1,
                    bottom = t.height,
                    top = -1;
                for (int y = 0; y < t.height; y++)
                for (int x = 0; x < t.width; x++)
                {
                    if (pixels[y * t.width + x].a < 230)
                        continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    bottom = Math.Min(bottom, y);
                    top = Math.Max(top, y);
                }
                if (right < left)
                    throw new InvalidOperationException("빈 아이콘: " + sprite.name);
                float scale = height / (top - bottom + 1);
                var size = new Vector2(t.width, t.height) * scale;
                var center = new Vector2((left + right + 1) / 2f, (bottom + top + 1) / 2f);
                return new Rect((new Vector2(t.width, t.height) / 2f - center) * scale, size);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(t);
            }
        }

        public static void Apply(Image image, float height)
        {
            Rect fit = Measure(image.sprite, height);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = fit.position;
            rect.sizeDelta = fit.size;
            image.preserveAspect = true;
        }
    }
}
