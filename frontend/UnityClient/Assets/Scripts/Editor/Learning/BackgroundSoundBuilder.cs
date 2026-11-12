using System.IO;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.Learning;
using A305.World;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace A305.EditorTools.Learning
{
    public static class BackgroundSoundBuilder
    {
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string ArtFolder = "Assets/Art/UI/Hud";
        private const string DisplayFontPath = "Assets/Art/UI/Fonts/Jua-Regular SDF.asset";

        // 시안 px × 1.2 = 캔버스 단위
        private const float Px = 1.2f;
        private static readonly Color Ink = new Color32(47, 37, 27, 255);

        [MenuItem("Tools/A305/Audio/Add Background Sound Toggle")]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            Sprite on = SaveSprite("BackgroundSoundOn", false);
            Sprite off = SaveSprite("BackgroundSoundOff", true);
            BuilderTools.UpdateOrCreatePrefab(
                HudPrefabPath,
                "LearningHud",
                root =>
                {
                    Attach(root.transform, on, off);
                    AttachVolume(root.transform);
                }
            );
            AssetDatabase.SaveAssets();
        }

        /// <summary>HUD를 다시 만들지 않고 스피커와 그 아래의 배경 소리 가로 막대(시안 A)만 만든다.</summary>
        [MenuItem("Tools/A305/Audio/Add Background Sound Volume")]
        public static void BuildVolume()
        {
            BuilderTools.ThrowIfPlaying();
            Sprite on = SaveSprite("BackgroundSoundOn", false);
            Sprite off = SaveSprite("BackgroundSoundOff", true);
            BuilderTools.UpdatePrefabIfExists(
                HudPrefabPath,
                root =>
                {
                    Attach(root.transform, on, off);
                    AttachVolume(root.transform);
                }
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
            rect.sizeDelta = new Vector2(
                A305.World.HudTopBarLayout.SpeakerWidth,
                A305.World.HudTopBarLayout.Size
            );
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
            var toggle = BuilderTools.GetOrAddComponent<BackgroundSoundToggle>(rect.gameObject);
            toggle.Configure(icon, on, off);
            Listen(button.onClick, toggle.ToggleVolumeWindow);
            // 꺼짐 그림은 켜짐 그림과 같은 캔버스에 사선만 더했으므로, 켜짐 그림으로 잰 자리를 함께 써 크기를 맞춘다.
            toggle.ConfigureIconLayout(
                OpticalIconPlacement.Measure(on, A305.World.HudTopBarLayout.SpeakerHeight)
            );
        }

        /// <summary>
        /// 스피커 아래 가로 막대(시안 A): 스피커 오른쪽 끝에 맞춰 12 아래에 유리 판(360×128)을 두고, '배경 소리'(Jua 22),
        /// 막대 옆 스피커(48) · 막대(186×12, 손잡이 30) · 퍼센트(Jua 22)를 놓는다. 판 바깥 전체는 누르면 닫는 칸이다.
        /// 그림은 시안 HTML을 2.4배로 렌더링한 것이고(그림자·꼭지 여백 포함), 처음에는 닫아 둔다.
        /// </summary>
        public static void AttachVolume(Transform owner)
        {
            var toggle = owner.Find("BackgroundSoundButton").GetComponent<BackgroundSoundToggle>();
            Transform old = owner.Find("BackgroundSoundVolume");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            RectTransform window = Rect("BackgroundSoundVolume", owner);
            Stretch(window);
            window.SetAsLastSibling();

            RectTransform closeArea = Rect("CloseArea", window);
            Stretch(closeArea);
            Listen(Hit(closeArea).onClick, toggle.CloseVolumeWindow);

            RectTransform panel = Rect("Panel", window);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(
                -HudTopBarLayout.Margin,
                -(HudTopBarLayout.Margin + HudTopBarLayout.Size + 12 * Px)
            );
            panel.sizeDelta = new Vector2(360, 128) * Px;
            panel.gameObject.AddComponent<Image>().color = Color.clear;
            Art(panel, "BackgroundVolumePanel", new Vector2(25, 24), new Vector2(410, 190));

            TMP_Text title = Text(panel, "Title", new Rect(22, 18, 316, 28), "배경 소리");
            title.alignment = TextAlignmentOptions.MidlineLeft;

            RectTransform mute = Box(panel, "Mute", new Rect(22, 60, 48, 48));
            Art(mute, "BackgroundVolumeMute", new Vector2(6, 5), new Vector2(60, 65));
            Listen(Hit(mute).onClick, toggle.ToggleMute);
            RectTransform muteIcon = Box(mute, "Icon", new Rect(4, 4, 40, 40));
            var muteImage = muteIcon.gameObject.AddComponent<Image>();
            muteImage.raycastTarget = false;
            muteImage.preserveAspect = true;

            Slider bar = Bar(panel, toggle);

            TMP_Text percent = Text(panel, "Percent", new Rect(284, 70, 54, 28), "100%");
            percent.alignment = TextAlignmentOptions.MidlineRight;

            toggle.ConfigureVolume(window.gameObject, bar, muteImage, percent);
            window.gameObject.SetActive(false);
        }

        // 막대: 줄(186×12)은 줄 칸 가운데(위 18), 채움은 둥근 끝을 살린 9칸 그림, 손잡이(30)는 줄 가운데에 둔다.
        // 손잡이는 0%·100%에서도 줄 안에 있도록 양 끝에서 반지름(15)만큼 안쪽으로만 움직이고, 채움은 손잡이 가운데까지 칠한다.
        // 누르는 칸은 높이 48 전체다.
        private static Slider Bar(RectTransform panel, BackgroundSoundToggle toggle)
        {
            RectTransform bar = Box(panel, "Bar", new Rect(84, 60, 186, 48));
            bar.gameObject.AddComponent<Image>().color = Color.clear;
            RectTransform track = Box(bar, "Track", new Rect(0, 18, 190, 15));
            Picture(track, "BackgroundVolumeTrack");

            RectTransform fillArea = Box(bar, "Fill Area", new Rect(15, 18, 156, 15));
            RectTransform fill = Rect("Fill", fillArea);
            Stretch(fill);
            fill.offsetMin = new Vector2(-15 * Px, 0);
            Image fillImage = Picture(fill, "BackgroundVolumeFill", new Vector4(15, 0, 15, 0));
            fillImage.type = Image.Type.Sliced;
            fillImage.pixelsPerUnitMultiplier = 2;

            RectTransform handleArea = Box(bar, "Handle Slide Area", new Rect(15, 9, 156, 30));
            RectTransform handle = Rect("Handle", handleArea);
            handle.sizeDelta = new Vector2(30 * Px, 0);
            var handleHit = handle.gameObject.AddComponent<Image>();
            handleHit.color = Color.clear;
            Art(handle, "BackgroundVolumeThumb", new Vector2(7.5f, 5), new Vector2(45, 45));

            var slider = bar.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleHit;
            UnityEventTools.AddPersistentListener(slider.onValueChanged, toggle.SetVolume);
            slider.onValueChanged.SetPersistentListenerState(
                0,
                UnityEventCallState.EditorAndRuntime
            );
            return slider;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // 부모 왼쪽 위에서 잰 시안 px 자리
        private static RectTransform Box(Transform parent, string name, Rect place)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(place.x, -place.y) * Px;
            rect.sizeDelta = place.size * Px;
            return rect;
        }

        // 그림자·꼭지 여백까지 렌더링한 그림을 자리보다 그 여백만큼 넓게 깐다.
        private static void Art(RectTransform owner, string sprite, Vector2 margin, Vector2 canvas)
        {
            RectTransform art = Box(
                owner,
                "Art",
                new Rect(-margin.x, -margin.y, canvas.x, canvas.y)
            );
            art.SetAsFirstSibling();
            Picture(art, sprite);
        }

        private static Image Picture(RectTransform rect, string name, Vector4 border = default)
        {
            string path = $"{ArtFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (
                importer.textureType != TextureImporterType.Sprite
                || importer.spriteBorder != border
            )
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            image.raycastTarget = false;
            return image;
        }

        private static Button Hit(RectTransform rect)
        {
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            return button;
        }

        private static TMP_Text Text(RectTransform parent, string name, Rect place, string value)
        {
            RectTransform rect = Box(parent, name, place);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
            text.fontSize = 22 * Px;
            text.color = Ink;
            text.text = value;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.margin = new Vector4(0, 2 * Px, 0, 0);
            text.raycastTarget = false;
            return text;
        }

        // 에디터 테스트에서도 누를 수 있게 저장되는 연결로 둔다. 다시 만들 때 겹치지 않게 비우고 넣는다.
        private static void Listen(UnityEvent onClick, UnityAction action)
        {
            while (onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(onClick, 0);
            UnityEventTools.AddPersistentListener(onClick, action);
            onClick.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
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
