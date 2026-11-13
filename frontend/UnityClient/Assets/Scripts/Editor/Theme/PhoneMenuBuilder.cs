using System.IO;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Learning;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        private const string RemoveJourneyMenuPath = "Tools/A305/UI/Remove Phone Quest Widget";
        private const string BalanceMenuPath = "Tools/A305/UI/Apply Phone Balance Widget";
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string ArtFolder = "Assets/Art/UI/Phone";
        private const string DisplayFontPath = "Assets/Art/UI/Fonts/Jua-Regular SDF.asset";
        private const string StatusFontPath = "Assets/Art/UI/Fonts/Pretendard-Bold SDF.asset";

        // 시안 1px = 캔버스 1.2
        private const float Px = 1.2f;

        // 그림 안의 24~232 영역이 다른 앱과 같은 (5, 0)의 100px 정사각형에 맞는다.
        private const float ControlsIconSize = 100f * 256f / 208f;
        private const float ControlsIconLeft = 5f - 24f * ControlsIconSize / 256f;
        private const float ControlsIconTop = -24f * ControlsIconSize / 256f;

        // 시안의 폰 크기와, 폰 그림(그림자 자리 포함) 안에서 폰이 시작하는 자리
        private const float PhoneWidth = 430;
        private const float PhoneHeight = 770;
        private const float ArtWidth = 610;
        private const float ArtHeight = 980;
        private const float ArtLeft = 90;
        private const float ArtTop = 60;
        private static readonly Rect BalanceCard = new Rect(286, 90, 106, 77);
        private static readonly Rect BalanceSurface = new Rect(293, 99, 79, 65);

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
            "대화 노트",
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

        /// <summary>기존 폰 구성과 기능 연결을 유지하고 빠진 조작법 그림만 추가한다.</summary>
        [MenuItem("Tools/A305/UI/Restore Phone Controls Icon")]
        public static void RestoreControlsIconToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(
                HudPrefabPath,
                root =>
                    Picture(
                        "Icon",
                        root.transform.Find("Panel/Menu/ControlsButton"),
                        ControlsIcon(),
                        ControlsIconLeft,
                        ControlsIconTop,
                        ControlsIconSize,
                        ControlsIconSize
                    )
            );
            AssetDatabase.SaveAssets();
        }

        /// <summary>조작법 그림만 같은 줄의 앱에 맞춰 평평한 그라데이션과 가운데 그림으로 갱신한다.</summary>
        [MenuItem("Tools/A305/UI/Update Phone Controls Icon Style")]
        public static void UpdateControlsIconStyle()
        {
            WriteControlsIcon();
            RestoreControlsIconToPrefab();
        }

        /// <summary>저장된 HUD를 외부 호출 없이 렌더링한다. PHONE_PREVIEW_OUTPUT은 저장 폴더다.</summary>
        public static void CaptureControlsPreview()
        {
            string output = System.Environment.GetEnvironmentVariable("PHONE_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new System.InvalidOperationException("Set PHONE_PREVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(222, 221, 208, 255);
            var root = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath)
                );
            foreach (Transform child in root.transform)
                child.gameObject.SetActive(child.name == "Panel" || child.name == "MenuBackdrop");
            root.transform.Find("Panel/MenuBar").gameObject.SetActive(false);
            Transform menu = root.transform.Find("Panel/Menu");
            menu.gameObject.SetActive(true);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            foreach (
                Vector2Int size in new[] { new Vector2Int(1600, 1000), new Vector2Int(844, 390) }
            )
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                camera.aspect = (float)size.x / size.y;
                camera.Render();
                Canvas.ForceUpdateCanvases();
                menu.GetComponent<PhoneMenuView>().SendMessage("OnEnable");
                menu.Find("Balance/Amount").GetComponent<TMP_Text>().text = "76";
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                File.WriteAllBytes(
                    Path.Combine(output, $"phone-{size.x}x{size.y}.png"),
                    pixels.EncodeToPNG()
                );
                var corners = new Vector3[4];
                ((RectTransform)menu.Find("Balance")).GetWorldCorners(corners);
                Vector3 bottom = camera.WorldToScreenPoint(corners[0]);
                Vector3 top = camera.WorldToScreenPoint(corners[2]);
                var widget = new Texture2D(
                    Mathf.CeilToInt(top.x - bottom.x) + 20,
                    Mathf.CeilToInt(top.y - bottom.y) + 20,
                    TextureFormat.RGB24,
                    false
                );
                widget.ReadPixels(
                    new Rect(bottom.x - 10, bottom.y - 10, widget.width, widget.height),
                    0,
                    0
                );
                File.WriteAllBytes(
                    Path.Combine(output, $"coin-{size.x}x{size.y}.png"),
                    widget.EncodeToPNG()
                );
                Object.DestroyImmediate(widget);
                RenderTexture.active = null;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        /// <summary>폰을 다시 만들지 않고 잠금 화면의 '오늘의 여정' 위젯만 지운다. 퀘스트 진행은 왼쪽 위 퀘스트 알약·카드가 보인다.</summary>
        [MenuItem(RemoveJourneyMenuPath)]
        public static void RemoveJourneyFromPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(
                HudPrefabPath,
                root => RemoveJourney((RectTransform)root.transform.Find("Panel/Menu"))
            );
            AssetDatabase.SaveAssets();
        }

        [MenuItem(BalanceMenuPath)]
        public static void ApplyBalanceToPrefab()
        {
            BalanceSurfaceArt(regenerate: true);
            BuilderTools.UpdatePrefabIfExists(
                HudPrefabPath,
                root =>
                {
                    var phone = (RectTransform)root.transform.Find("Panel/Menu");
                    Balance(phone.GetComponent<PhoneMenuView>(), phone);
                }
            );
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject root)
        {
            var phone = (RectTransform)root.transform.Find("Panel/Menu");
            Clear(phone);
            Body(phone);
            PhoneMenuView view = BuilderTools.GetOrAddComponent<PhoneMenuView>(phone.gameObject);
            // 그리는 순서: 폰 그림 → 글자 → 앱 → 배지 → 내비게이션 키
            Picture("Art", phone, Art("PhoneBase"), -ArtLeft, -ArtTop, ArtWidth, ArtHeight)
                .transform.SetAsFirstSibling();
            LockScreen(view, phone);
            Apps(view, phone);
            NavigationKeys(phone);
            Surroundings(root.transform);
            PhoneLearningMenuBuilder.Apply(root);
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
            PhoneButton(root);
        }

        // 시안 g1의 파란 휴대폰 버튼(64×64, 오른쪽·위 28). 그림은 시안을 렌더링한 것(그림자 자리 왼쪽 32·위 24)이다.
        // 펼친 동안의 버튼은 시안 m1처럼 보이지 않지만 같은 자리를 누르면 접힌다.
        private static void PhoneButton(Transform root)
        {
            Transform panel = root.Find("Panel");
            // HUD 패널은 화면 모서리에서 24 들어가 있어, 버튼이 28에 오도록 남은 만큼 안쪽 여백을 둔다.
            var column = panel.GetComponent<VerticalLayoutGroup>();
            int inset = Mathf.RoundToInt(28 * Px - 24);
            column.padding = new RectOffset(
                column.padding.left,
                inset,
                inset,
                column.padding.bottom
            );
            column.childAlignment = TextAnchor.UpperRight;
            foreach (string name in new[] { "MenuButton", "OpenMenuButton" })
            {
                Transform button = panel.Find("MenuBar/" + name);
                foreach (string part in new[] { "Face", "Outline", "Icon", "Label", "Art" })
                {
                    Transform old = button.Find(part);
                    if (old != null)
                        Object.DestroyImmediate(old.gameObject);
                }
                RemoveDecorations(button.gameObject);
                var size = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
                size.minWidth =
                    size.preferredWidth =
                    size.minHeight =
                    size.preferredHeight =
                        64 * Px;
                Image hit = Clear(BuilderTools.GetOrAddComponent<Image>(button.gameObject));
                hit.raycastTarget = true;
                var clickable = button.GetComponent<Button>();
                clickable.targetGraphic = hit;
                clickable.transition = Selectable.Transition.None;
                if (name == "MenuButton")
                    Picture(
                        "Art",
                        button,
                        Art("PhoneButton", "Assets/Art/UI/Hud"),
                        -32,
                        -24,
                        128,
                        136
                    );
                else
                    BuilderTools.GetOrAddComponent<CanvasGroup>(button.gameObject).alpha = 0;
            }
            var hud = root.GetComponent<LearningHudView>();
            BuilderTools.SetReference(hud, "phoneButtonBar", panel.Find("MenuBar").gameObject);
        }

        // 앱 버튼만 남기고 예전 폰 부품을 지운다. 그림과 글자는 아래에서 시안 그대로 다시 만든다.
        private static void Clear(RectTransform phone)
        {
            for (int i = phone.childCount - 1; i >= 0; i--)
            {
                Transform child = phone.GetChild(i);
                if (
                    System.Array.IndexOf(AppNames, child.name) < 0
                    && child.name != "NeighborsButton"
                    && child.name != "SettingsButton"
                )
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

        // 상태 표시줄 시각, 큰 시각, 날짜, 코인 숫자
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
            BuilderTools.SetReference(view, "statusTime", status);
            BuilderTools.SetReference(view, "clock", clock);
            BuilderTools.SetReference(view, "date", date);
            Balance(view, phone);
        }

        private static void Balance(PhoneMenuView view, RectTransform phone)
        {
            Transform oldAmount = phone.Find("Amount");
            if (oldAmount != null)
                Object.DestroyImmediate(oldAmount.gameObject);

            Image surface = Picture(
                "BalanceSurface",
                phone,
                BalanceSurfaceArt(),
                BalanceSurface.x,
                BalanceSurface.y,
                BalanceSurface.width,
                BalanceSurface.height
            );
            surface.transform.SetSiblingIndex(phone.Find("Art").GetSiblingIndex() + 1);

            RectTransform row = Rect(
                "Balance",
                phone,
                BalanceCard.x,
                BalanceCard.y,
                BalanceCard.width,
                BalanceCard.height
            );
            var layout = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(row.gameObject);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 5 * Px;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var coinArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Hud/Coin.png");
            Image coin = Picture("Coin", row, coinArt, 0, 0, 40, 40);
            coin.preserveAspect = true;
            var iconSize = BuilderTools.GetOrAddComponent<LayoutElement>(coin.gameObject);
            iconSize.preferredWidth = iconSize.preferredHeight = 40 * Px;
            TMP_Text amount = Text("Amount", row, DisplayFontPath, 28, Ink, "20", 0, 0, 0, 34);
            amount.alignment = TextAlignmentOptions.Midline;
            BuilderTools.SetReference(view, "budget", amount);
        }

        private static Sprite BalanceSurfaceArt(bool regenerate = false)
        {
            const string name = "PhoneBalanceSurface";
            string path = $"{ArtFolder}/{name}.png";
            if (File.Exists(path) && !regenerate)
                return ImportBalanceSurface(name, path);

            // 폰 그림의 위젯 테두리와 그림자는 두고, 안쪽의 기존 동전과 글자만 배경색으로 덮는다.
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes($"{ArtFolder}/PhoneBase.png"));
            float scale = source.width / ArtWidth;
            int width = Mathf.RoundToInt(BalanceSurface.width * scale);
            int height = Mathf.RoundToInt(BalanceSurface.height * scale);
            int left = Mathf.RoundToInt((ArtLeft + BalanceSurface.x) * scale);
            int bottom = source.height - Mathf.RoundToInt((ArtTop + BalanceSurface.yMax) * scale);
            var surface = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                // 글자 아래까지 덮되 둥근 카드 모서리 밖의 배경색이 안쪽으로 번지지 않게 한다.
                float phoneY = BalanceSurface.yMax - y / scale;
                int sampleLeft = phoneY > 140 ? left + Mathf.RoundToInt(23 * scale) : left;
                Color start = source.GetPixel(sampleLeft, bottom + y);
                Color end = source.GetPixel(left + width - 1, bottom + y);
                for (int x = 0; x < width; x++)
                {
                    Color fill = Color.Lerp(start, end, (float)x / (width - 1));
                    int edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
                    fill.a = Mathf.Clamp01(edge / scale);
                    Vector2 point = new Vector2(BalanceSurface.x + x / scale, phoneY);
                    Vector2 nearest = new Vector2(
                        Mathf.Clamp(point.x, BalanceCard.xMin + 25, BalanceCard.xMax - 25),
                        Mathf.Clamp(point.y, BalanceCard.yMin + 25, BalanceCard.yMax - 25)
                    );
                    fill.a *= Mathf.Clamp01(24 - Vector2.Distance(point, nearest));
                    pixels[y * width + x] = fill;
                }
            }
            surface.SetPixels(pixels);
            surface.Apply();
            File.WriteAllBytes(path, surface.EncodeToPNG());
            Object.DestroyImmediate(surface);
            Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return ImportBalanceSurface(name, path);
        }

        private static Sprite ImportBalanceSurface(string name, string path)
        {
            UiImagePadding.Pad(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureCompression != TextureImporterCompression.Compressed)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            return Art(name);
        }

        // 잠금 화면에 퀘스트 위젯을 두지 않는다. 퀘스트 진행은 왼쪽 위 퀘스트 알약·카드가 보이므로, 예전에 만든 '오늘의 여정' 카드가 있으면 지운다.
        private static void RemoveJourney(RectTransform phone)
        {
            Transform journey = phone.Find("Journey");
            if (journey != null)
                Object.DestroyImmediate(journey.gameObject);
        }

        // 기존 다섯 앱의 아이콘은 폰 그림에 있다. 조작법은 별도 그림으로 같은 앱 칸에 덧붙인다.
        private static void Apps(PhoneMenuView view, RectTransform phone)
        {
            for (int i = 0; i < AppNames.Length; i++)
            {
                Button app = App(phone, AppNames[i], AppLabels[i], AppCells[i]);
                app.transform.SetAsLastSibling();
                if (AppNames[i] == "MapButton")
                    BuilderTools.SetReference(view, "mapButton", app);
                if (AppNames[i] == "ControlsButton")
                    Picture(
                        "Icon",
                        app.transform,
                        ControlsIcon(),
                        ControlsIconLeft,
                        ControlsIconTop,
                        ControlsIconSize,
                        ControlsIconSize
                    );
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

        // 원래 폰 배경에는 여섯 번째 앱 그림이 없다. 디자이너가 교체한 그림은 유지한다.
        private static Sprite ControlsIcon()
        {
            string path = ArtFolder + "/PhoneControls.png";
            if (!File.Exists(path))
                WriteControlsIcon();
            return Art("PhoneControls");
        }

        private static void WriteControlsIcon()
        {
            var art = new UiArtCanvas(256, 256);
            art.SoftShadow(24, 24, 232, 232, 62, 22, new Color(.3f, .2f, .1f, .14f));
            Color bottom = new Color32(233, 193, 98, 255);
            Color top = new Color32(250, 225, 162, 255);
            for (int y = 24; y < 232; y++)
            {
                art.Custom(
                    new Rect(24, y, 208, 1),
                    Color.Lerp(bottom, top, (y - 24f) / 208f),
                    InsideControlsPlate
                );
            }
            art.Custom(new Rect(24, 228, 208, 4), new Color(1, 1, 1, .32f), InsideControlsPlate);
            const float scale = .9f;
            float X(float x) => 128f + (x - 128f) * scale;
            float Y(float y) => 128f + (y + 12.5f - 128f) * scale;
            Color white = new Color32(255, 255, 250, 255);
            art.RoundRect(X(64), Y(92), X(192), Y(158), 30 * scale, white);
            art.Circle(X(84), Y(96), 23 * scale, white);
            art.Circle(X(172), Y(96), 23 * scale, white);
            Color detail = new Color32(184, 133, 65, 255);
            art.RoundRect(X(86), Y(112), X(112), Y(120), 3 * scale, detail);
            art.RoundRect(X(95), Y(103), X(103), Y(129), 3 * scale, detail);
            art.Circle(X(156), Y(115), 7 * scale, new Color32(255, 153, 120, 255));
            art.Circle(X(174), Y(132), 7 * scale, new Color32(255, 208, 102, 255));
            Texture2D texture = art.ToTexture();
            string path = ArtFolder + "/PhoneControls.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static bool InsideControlsPlate(Vector2 point)
        {
            var nearest = new Vector2(Mathf.Clamp(point.x, 86, 170), Mathf.Clamp(point.y, 86, 170));
            return (point - nearest).sqrMagnitude <= 62f * 62f;
        }

        private static Button App(RectTransform phone, string name, string caption, Vector2 cell)
        {
            var app = (RectTransform)phone.Find(name);
            if (app == null)
                app = Rect(name, phone, 0, 0, 0, 0);
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
        private static Sprite Art(string name, string folder = ArtFolder)
        {
            string path = $"{folder}/{name}.png";
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
