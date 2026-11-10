using System.IO;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.Learning;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Learning
{
    public static class BackgroundSoundBuilder
    {
        [MenuItem("Tools/A305/Audio/Add Background Sound Toggle")]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            Sprite on = SaveSprite("BackgroundSoundOn", false);
            Sprite off = SaveSprite("BackgroundSoundOff", true);
            BuilderTools.UpdateOrCreatePrefab(
                "Assets/Prefabs/Learning/LearningHud.prefab",
                "LearningHud",
                root => Attach(root.transform, on, off)
            );
            AssetDatabase.SaveAssets();
        }

        public static void Attach(Transform owner, Sprite on, Sprite off)
        {
            Transform old = owner.Find("BackgroundSoundButton");
            var rect =
                old != null
                    ? (RectTransform)old
                    : new GameObject(
                        "BackgroundSoundButton",
                        typeof(RectTransform),
                        typeof(Image),
                        typeof(Button)
                    ).GetComponent<RectTransform>();
            rect.SetParent(owner, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(
                -A305.World.HudTopBarLayout.Margin,
                -A305.World.HudTopBarLayout.Margin
            );
            rect.sizeDelta = Vector2.one * A305.World.HudTopBarLayout.Size;
            rect.SetAsLastSibling();
            var button = rect.GetComponent<Button>();
            var hit = rect.GetComponent<Image>();
            hit.color = Color.clear;
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            Transform existing = rect.Find("Speaker");
            var icon =
                existing != null
                    ? existing.GetComponent<Image>()
                    : new GameObject(
                        "Speaker",
                        typeof(RectTransform),
                        typeof(Image)
                    ).GetComponent<Image>();
            icon.transform.SetParent(rect, false);
            icon.rectTransform.anchorMin =
                icon.rectTransform.anchorMax =
                icon.rectTransform.pivot =
                    Vector2.one * .5f;
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.rectTransform.sizeDelta = Vector2.one * 88;
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            BuilderTools
                .GetOrAddComponent<BackgroundSoundToggle>(rect.gameObject)
                .Configure(button, icon, on, off);
        }

        private static Sprite SaveSprite(string name, bool muted)
        {
            string path = "Assets/Art/UI/Hud/" + name + ".png";
            if (!File.Exists(path))
            {
                Sprite sprite = Speaker(muted);
                File.WriteAllBytes(path, sprite.texture.EncodeToPNG());
                Object.DestroyImmediate(sprite.texture);
                Object.DestroyImmediate(sprite);
                AssetDatabase.ImportAsset(path);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static Sprite Speaker(bool muted)
        {
            var art = new UiArtCanvas(128, 128);
            Color gold = new Color32(255, 203, 74, 255);
            void Shape(Color color, float dy)
            {
                var points = new[]
                {
                    new Vector2(34, 51 + dy),
                    new Vector2(67, 34 + dy),
                    new Vector2(67, 94 + dy),
                    new Vector2(34, 77 + dy),
                    new Vector2(34, 51 + dy),
                };
                art.Polygon(color, points);
                art.Line(10, color, points);
                art.RoundRect(12, 49 + dy, 38, 79 + dy, 7, color);
            }
            Shape(gold, 0);
            if (!muted)
            {
                foreach (float radius in new[] { 25f, 42f })
                {
                    art.Arc(69, 64, radius, -40, 40, 5, gold);
                }
            }
            else
                art.Line(
                    9,
                    new Color32(187, 115, 57, 255),
                    new Vector2(27, 25),
                    new Vector2(95, 103)
                );
            Texture2D texture = SpeakerShadow(art.ToTexture());
            return Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f), 100);
        }

        private static Texture2D SpeakerShadow(Texture2D source)
        {
            Color[] pixels = source.GetPixels();
            var shaded = new Color[pixels.Length];
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float shadow = 0;
                for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    int sx = x + dx,
                        sy = y + dy + 4;
                    if (sx < 0 || sx >= 128 || sy < 0 || sy >= 128)
                        continue;
                    shadow += pixels[sy * 128 + sx].a * Mathf.Exp(-(dx * dx + dy * dy) / 8f);
                }
                Color top = pixels[y * 128 + x];
                float below = Mathf.Clamp01(shadow / 25f) * .2f * (1 - top.a);
                float alpha = top.a + below;
                shaded[y * 128 + x] =
                    alpha > 0
                        ? new Color(
                            (top.r * top.a + .35f * below) / alpha,
                            (top.g * top.a + .28f * below) / alpha,
                            (top.b * top.a + .17f * below) / alpha,
                            alpha
                        )
                        : Color.clear;
            }
            source.SetPixels(shaded);
            source.Apply();
            return source;
        }
    }
}
