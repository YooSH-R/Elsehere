using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Learning;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Theme
{
    /// <summary>
    /// 오른쪽 위 메뉴 목록(Panel/Menu)을 확정 시안 m1의 삼성폰 메뉴와 똑같이 만든다.
    /// 폰 틀·화면·아이콘·위젯 바탕처럼 변하지 않는 부분은 시안 HTML을 그대로 렌더링한 그림(Art/UI/Phone)을 쓰고,
    /// 시각·날짜·예산·할 일·진행 칸·배지 숫자·앱 이름처럼 바뀌는 부분만 시안의 자리와 글자 크기로 얹는다.
    /// 좌표는 시안(1600×900)의 px이며, 캔버스(1920×1080)에는 1.2를 곱해 놓는다. 앱 버튼은 기존 메뉴 버튼을 그대로 써서 기능 연결을 유지한다.
    /// </summary>
    public static class PhoneMenuBuilder
    {
        private const string MenuPath = "Tools/A305/UI/Apply Phone Menu";
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string ArtFolder = "Assets/Art/UI/Phone";
        private const string DisplayFontPath = "Assets/Art/UI/Fonts/Jua-Regular SDF.asset";
        private const string StatusFontPath = "Assets/Art/UI/Fonts/Pretendard-Bold SDF.asset";

        // 시안 1px = 캔버스 1.2
        private const float Px = 1.2f;

        // 시안의 폰 크기와, 폰 그림(그림자 자리 포함) 안에서 폰이 시작하는 자리
        private const float PhoneWidth = 430;
        private const float PhoneHeight = 770;
        private const float ArtWidth = 610;
        private const float ArtHeight = 980;
        private const float ArtLeft = 90;
        private const float ArtTop = 60;

        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color Muted = new Color32(125, 110, 91, 255);
        private static readonly Color Coral = new Color32(255, 119, 89, 255);

        // 앱 칸(시안 .app 110×128)의 왼쪽 위 자리
        private static readonly Vector2[] AppCells =
        {
            new Vector2(38, 338),
            new Vector2(160, 338),
            new Vector2(282, 338),
            new Vector2(38, 486),
            new Vector2(160, 486),
            new Vector2(282, 486),
        };

        private static readonly string[] AppNames =
        {
            "VocabularyButton",
            "HistoryButton",
            "MapButton",
            "HomeButton",
            "MyPageButton",
            "ControlsButton",
        };

        // 시안의 다섯 앱에, 메뉴에 있던 [조작법]을 같은 모양(노란 칸)으로 더했다.
        private static readonly string[] AppLabels =
        {
            "단어장",
            "피드백 목록",
            "마을 지도",
            "집으로",
            "내 정보",
            ControlsGuideText.MenuLabel,
        };

        [MenuItem(MenuPath)]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(HudPrefabPath, Apply);
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject root)
        {
            var phone = (RectTransform)root.transform.Find("Panel/Menu");
            Clear(phone);
            Body(phone);
            PhoneMenuView view = BuilderTools.GetOrAddComponent<PhoneMenuView>(phone.gameObject);
            // 그리는 순서: 폰 그림 → 여정 카드 그림 → 글자 → 앱 → 배지 → 내비게이션 키
            Picture("Art", phone, Art("PhoneBase"), -ArtLeft, -ArtTop, ArtWidth, ArtHeight)
                .transform.SetAsFirstSibling();
            Journey(view, phone);
            LockScreen(view, phone);
            Apps(view, phone);
            NavigationKeys(phone);
            Surroundings(root.transform);
        }

        // 시안처럼 폰을 펼친 동안 뒤 화면 가장자리를 어둡게 덮고(누르면 접히는 바탕), 오른쪽 위 휴대폰 버튼은 보이지 않게 한다.
        // 펼친 동안의 휴대폰 버튼은 보이지 않아도 같은 자리를 누르면 폰을 접는다.
        private static void Surroundings(Transform root)
        {
            Transform backdrop = root.Find("MenuBackdrop");
            var shade = backdrop.GetComponent<Image>();
            shade.sprite = Art("PhoneShade");
            shade.type = UnityEngine.UI.Image.Type.Simple;
            shade.color = Color.white;
            shade.raycastTarget = true;
            var rect = (RectTransform)backdrop;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Transform open = root.Find("Panel/MenuBar/OpenMenuButton");
            BuilderTools.GetOrAddComponent<CanvasGroup>(open.gameObject).alpha = 0;
        }

        // 앱 버튼만 남기고 예전 폰 부품을 지운다. 그림과 글자는 아래에서 시안 그대로 다시 만든다.
        private static void Clear(RectTransform phone)
        {
            for (int i = phone.childCount - 1; i >= 0; i--)
            {
                Transform child = phone.GetChild(i);
                if (System.Array.IndexOf(AppNames, child.name) < 0)
                    Object.DestroyImmediate(child.gameObject);
            }
            RemoveDecorations(phone.gameObject);
        }

        private static void RemoveDecorations(GameObject target)
        {
            foreach (Component part in target.GetComponents<Component>())
            {
                if (
                    part is LayoutGroup
                    || part is ContentSizeFitter
                    || part is ThemedElement
                    || part is SurfaceBleed
                    || part is PillContentLayout
                    || part is Mask
                )
                    Object.DestroyImmediate(part);
            }
        }

        // 폰 자리: 화면 오른쪽에서 130, 위에서 65(세로 가운데). 화면 높이가 다르면 PhoneMenuView가 세로 가운데를 다시 맞춘다.
        // 몸체는 투명한 누름 영역이라 폰을 눌러도 뒤의 바탕(누르면 메뉴를 접는다)으로 넘어가지 않는다.
        private static void Body(RectTransform phone)
        {
            BuilderTools.GetOrAddComponent<LayoutElement>(phone.gameObject).ignoreLayout = true;
            phone.anchorMin = phone.anchorMax = phone.pivot = Vector2.one;
            // HUD 패널은 화면 오른쪽 위 모서리에서 24씩 들어가 있다.
            phone.anchoredPosition = new Vector2(-(130 * Px - 24), -(65 * Px - 24));
            phone.sizeDelta = new Vector2(PhoneWidth * Px, PhoneHeight * Px);
            Clear(BuilderTools.GetOrAddComponent<Image>(phone.gameObject)).raycastTarget = true;
        }

        // 상태 표시줄 시각, 큰 시각, 날짜, 여행 예산 숫자
        private static void LockScreen(PhoneMenuView view, RectTransform phone)
        {
            TMP_Text status = Text(
                "StatusTime",
                phone,
                StatusFontPath,
                15,
                Ink,
                "오전 8:00",
                38,
                26.5f,
                120,
                15
            );
            // 글자 자리는 시안 글자 테두리에 맞춘 값이다(Unity 글자는 같은 칸에서 4.5px 아래에 그려진다).
            TMP_Text clock = Text(
                "Clock",
                phone,
                DisplayFontPath,
                66,
                Ink,
                "8:00",
                38,
                73.5f,
                220,
                59.4f
            );
            // 시안의 letter-spacing 1px(글자 크기 66px)
            clock.characterSpacing = 100f / 66f;
            TMP_Text date = Text(
                "Date",
                phone,
                DisplayFontPath,
                19,
                Muted,
                "1일차 · 오전",
                38,
                145.4f,
                230,
                19
            );
            // 숫자 칸은 시안의 padding-top 3px만큼 내려 둔다.
            TMP_Text amount = Text(
                "Amount",
                phone,
                DisplayFontPath,
                28,
                Ink,
                "20",
                341.5f,
                105.4f,
                60,
                28
            );
            BuilderTools.SetReference(view, "statusTime", status);
            BuilderTools.SetReference(view, "clock", clock);
            BuilderTools.SetReference(view, "date", date);
            BuilderTools.SetReference(view, "budget", amount);
        }

        // 오늘의 여정 카드: 시안에서 잘라 낸 카드 그림(화면 폭, 폰 기준 y 150~450) 위에 단계 칩 글자·할 일·진행 칸
        private static void Journey(PhoneMenuView view, RectTransform phone)
        {
            const float CardLeft = 12;
            const float CardTop = 150;
            RectTransform card = Picture(
                "Journey",
                phone,
                Art("PhoneJourney"),
                CardLeft,
                CardTop,
                406,
                300
            ).rectTransform;
            TMP_Text chip = Text(
                "ChipText",
                card,
                DisplayFontPath,
                14,
                Color.white,
                "1 / 7",
                313.7f - CardLeft,
                208.4f - CardTop,
                57.3f,
                26
            );
            chip.alignment = TextAlignmentOptions.Center;
            TMP_Text direction = Text(
                "Direction",
                card,
                DisplayFontPath,
                23,
                Ink,
                "",
                59 - CardLeft,
                243.4f - CardTop,
                312,
                27.6f
            );
            direction.overflowMode = TextOverflowModes.Ellipsis;
            // 진행 칸 그림은 빛 번짐 자리(사방 12px)를 품고 있어, 칸 사이 6px에서 그만큼 겹쳐 놓는다.
            RectTransform steps = Rect(
                "Steps",
                card,
                59 - 12 - CardLeft,
                285 - 12 - CardTop,
                312 + 24,
                32
            );
            var row = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(steps.gameObject);
            row.spacing = (6 - 24) * Px;
            row.childAlignment = TextAnchor.UpperLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            Image mark = Picture("Mark", steps, Art("PhoneStepUpcoming"), 0, 0, 64, 32);
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(mark.gameObject);
            size.preferredWidth = size.minWidth = (39.43f + 24) * Px;
            size.preferredHeight = size.minHeight = 32 * Px;
            BuilderTools.SetReference(view, "journeyCard", card.gameObject);
            BuilderTools.SetReference(view, "journeyChip", chip);
            BuilderTools.SetReference(view, "journeyDirection", direction);
            BuilderTools.SetReference(view, "journeyProgress", steps);
            BuilderTools.SetReference(view, "upcomingMark", Art("PhoneStepUpcoming"));
            BuilderTools.SetReference(view, "currentMark", Art("PhoneStepCurrent"));
            BuilderTools.SetReference(view, "doneMark", Art("PhoneStepDone"));
        }

        // 앱 다섯 개. 아이콘은 폰 그림에 들어 있고, 버튼은 시안의 앱 칸을 누르는 투명한 영역과 이름 글자다.
        private static void Apps(PhoneMenuView view, RectTransform phone)
        {
            for (int i = 0; i < AppNames.Length; i++)
            {
                Button app = App(phone, AppNames[i], AppLabels[i], AppCells[i]);
                app.transform.SetAsLastSibling();
                if (AppNames[i] == "MapButton")
                    BuilderTools.SetReference(view, "mapButton", app);
            }
            // 단어장 배지: 시안의 흰 알약 그림(그림자 자리 사방 14·12)과 빨간 숫자. 앱 칸 기준 (71, -6), 크기 33×28
            Transform badge = phone.Find("VocabularyButton/CountBadge");
            RemoveDecorations(badge.gameObject);
            Place(badge, 71 - 14, -6 - 12, 62, 56);
            Clear(BuilderTools.GetOrAddComponent<Image>(badge.gameObject)).sprite = Art(
                "PhoneBadge"
            );
            badge.GetComponent<Image>().color = Color.white;
            var count = badge.Find("Label").GetComponent<TMP_Text>();
            RemoveDecorations(count.gameObject);
            Style(count, DisplayFontPath, 15, Coral);
            count.alignment = TextAlignmentOptions.Center;
            Place(count.transform, 14, 13, 33, 28);
            badge.SetAsLastSibling();
        }

        private static Button App(RectTransform phone, string name, string caption, Vector2 cell)
        {
            RectTransform app = (RectTransform)phone.Find(name) ?? Rect(name, phone, 0, 0, 0, 0);
            foreach (string old in new[] { "Face", "Outline", "Icon", "Tile" })
            {
                Transform part = app.Find(old);
                if (part != null)
                    Object.DestroyImmediate(part.gameObject);
            }
            RemoveDecorations(app.gameObject);
            Place(app, cell.x, cell.y, 110, 128);
            var size = app.GetComponent<LayoutElement>();
            if (size != null)
                size.ignoreLayout = true;
            Image hit = Clear(BuilderTools.GetOrAddComponent<Image>(app.gameObject));
            hit.raycastTarget = true;
            var button = BuilderTools.GetOrAddComponent<Button>(app.gameObject);
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            // 앱 이름: Jua 18px, 아이콘(100) 아래 10px
            TMP_Text label = UiBuilderTools.FindOrCreateText(
                app,
                "Label",
                Font(DisplayFontPath),
                18 * Px,
                Ink,
                caption
            );
            RemoveDecorations(label.gameObject);
            Style(label, DisplayFontPath, 18, Ink);
            label.text = caption;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Place(label.transform, 0, 110, 110, 18);
            return button;
        }

        // 내비게이션 바(최근 앱·홈·뒤로) 아이콘은 폰 그림에 있고, 그 자리에 폰을 접는 투명한 버튼을 둔다.
        private static void NavigationKeys(RectTransform phone)
        {
            RectTransform keys = Rect("Screen", phone, 0, 0, PhoneWidth, PhoneHeight);
            string[] names = { "RecentsButton", "PhoneHomeButton", "BackButton" };
            float[] centers = { 106.3f, 215, 323.7f };
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform key = Rect(names[i], keys, centers[i] - 40, 706, 80, 52);
                Image hit = Clear(BuilderTools.GetOrAddComponent<Image>(key.gameObject));
                hit.raycastTarget = true;
                BuilderTools.GetOrAddComponent<Button>(key.gameObject).targetGraphic = hit;
            }
        }

        // 시안을 렌더링한 그림. 크게 그린 폰 그림이 줄지 않게 4096까지 그대로 가져온다.
        private static Sprite Art(string name)
        {
            string path = $"{ArtFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (
                importer.textureType != TextureImporterType.Sprite
                || importer.maxTextureSize < 4096
            )
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 4096;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Image Picture(
            string name,
            Transform parent,
            Sprite sprite,
            float x,
            float y,
            float width,
            float height
        )
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            Image image = Clear(BuilderTools.GetOrAddComponent<Image>(rect.gameObject));
            image.sprite = sprite;
            image.color = Color.white;
            return image;
        }

        // 그림 없는 투명한 이미지로 되돌린다. 누름 영역이나 그림을 새로 넣기 전에 쓴다.
        private static Image Clear(Image image)
        {
            image.sprite = null;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.material = null;
            image.color = Color.clear;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Text(
            string name,
            Transform parent,
            string fontPath,
            float size,
            Color color,
            string value,
            float x,
            float y,
            float width,
            float height
        )
        {
            TMP_Text text = UiBuilderTools.FindOrCreateText(
                parent,
                name,
                Font(fontPath),
                size * Px,
                color,
                value
            );
            Place(text.transform, x, y, width, height);
            Style(text, fontPath, size, color);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void Style(TMP_Text text, string fontPath, float size, Color color)
        {
            text.font = Font(fontPath);
            // 브라우저가 그린 시안 글자 두께에 맞춘 재질
            text.fontSharedMaterial =
                fontPath == DisplayFontPath ? HeavyDisplayMaterial() : text.font.material;
            text.fontSize = size * Px;
            text.fontStyle = FontStyles.Normal;
            text.enableAutoSizing = false;
            text.characterSpacing = 0;
            text.color = color;
            text.margin = Vector4.zero;
            text.raycastTarget = false;
        }

        private static TMP_FontAsset Font(string path) =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

        // Jua를 시안(브라우저) 두께로 그리는 재질. 글꼴 재질을 본떠 글자 면을 조금 넓힌다.
        private const string HeavyDisplayMaterialPath = ArtFolder + "/Jua Phone.mat";
        private const float DisplayDilate = 0.15f;

        private static Material HeavyDisplayMaterial()
        {
            TMP_FontAsset font = Font(DisplayFontPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(HeavyDisplayMaterialPath);
            if (material == null)
            {
                material = new Material(font.material) { name = "Jua Phone" };
                AssetDatabase.CreateAsset(material, HeavyDisplayMaterialPath);
            }
            material.CopyPropertiesFromMaterial(font.material);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, DisplayDilate);
            EditorUtility.SetDirty(material);
            return material;
        }

        // 시안 px 좌표(부모 왼쪽 위 기준)를 캔버스 자리로 놓는다.
        private static RectTransform Rect(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float height
        )
        {
            Transform existing = parent.Find(name);
            RectTransform rect =
                existing != null
                    ? (RectTransform)existing
                    : new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, x, y, width, height);
            return rect;
        }

        private static void Place(Transform target, float x, float y, float width, float height)
        {
            var rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x * Px, -y * Px);
            rect.sizeDelta = new Vector2(width * Px, height * Px);
        }
    }
}
