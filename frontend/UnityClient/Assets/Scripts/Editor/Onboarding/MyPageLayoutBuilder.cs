using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Onboarding
{
    /// <summary>내 정보 창과 설정 창의 항목 정렬, 설정 카드, 일반 굵기 버튼을 한 화면 안에서 맞춘다.</summary>
    public static class MyPageLayoutBuilder
    {
        private const string HudPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string CatalogPath = "Assets/Data/Theme/UiThemeCatalog.asset";

        [MenuItem("Tools/A305/Onboarding/Apply My Page Layout")]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(HudPath, Apply);
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject root)
        {
            Transform window = root.transform.Find("MyPage/Window");
            if (window == null)
                return;

            var catalog = AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(CatalogPath);
            Frame(window, catalog);
            TMP_Text profile = Text(window.Find("Profile"), catalog, UiThemeRole.Body, 22);
            profile.lineSpacing = 12;
            profile.richText = true;
            Size(profile.transform, 0, 110, flexibleWidth: 1);

            Transform nickname = window.Find("NicknameRow");
            nickname.GetComponent<HorizontalLayoutGroup>().spacing = 14;
            Size(nickname, 0, 52, flexibleWidth: 1);
            TMP_InputField input = nickname.Find("NicknameInput").GetComponent<TMP_InputField>();
            Size(input.transform, 0, 52, flexibleWidth: 1);
            input.textComponent.fontStyle = FontStyles.Normal;
            input.textComponent.fontSize = 22;
            ((TMP_Text)input.placeholder).fontSize = 22;
            Button(nickname.Find("NicknameButton"), catalog, 128, 52, "변경");
            Status(window, catalog);
            Transform buttons = window.Find("Buttons");
            Size(buttons, 0, 52, flexibleWidth: 1);
            buttons.GetComponent<HorizontalLayoutGroup>().spacing = 14;
            Button(buttons.Find("WithdrawButton"), catalog, 154, 52, "회원 탈퇴");
            Button(buttons.Find("LogoutButton"), catalog, 154, 52, "로그아웃");

            foreach (string dialogName in new[] { "LogoutDialog", "WithdrawDialog" })
            {
                Transform dialogButtons = root.transform.Find($"MyPage/{dialogName}/Buttons");
                if (dialogButtons == null)
                    continue;
                foreach (Transform button in dialogButtons)
                    if (button.GetComponent<UnityEngine.UI.Button>() != null)
                        Button(button, catalog, 154, 52, null);
            }

            // 시점 감도부터 선택 동의까지는 설정 창에 있다(S15P31A305-861).
            window = root.transform.Find("Settings/Window");
            if (window == null)
                return;
            Frame(window, catalog);
            Status(window, catalog);
            Setting(window.Find("LookSensitivity"), catalog, "시점 감도", "시점을 돌리는 속도");
            Setting(
                window.Find("NpcSpeechSpeed"),
                catalog,
                "NPC 말 빠르기",
                "다음 대사부터 적용돼요"
            );

            TMP_Text note = UiBuilderTools.FindOrCreateText(
                window,
                "DeviceNote",
                UiBuilderTools.RegularFont(),
                16,
                Color.gray,
                "설정은 이 기기에 저장돼요."
            );
            Text(note.transform, catalog, UiThemeRole.Muted, 16);
            note.text = "설정은 이 기기에 저장돼요.";
            // 효과음도 이 기기에 저장하므로 안내는 효과음 카드 아래에 둔다.
            Transform sound = window.Find("SoundEffects");
            if (sound != null)
            {
                sound.SetSiblingIndex(window.Find("NpcSpeechSpeed").GetSiblingIndex() + 1);
                SoundEffects(sound, catalog);
            }
            note.transform.SetSiblingIndex(
                (sound != null ? sound : window.Find("NpcSpeechSpeed")).GetSiblingIndex() + 1
            );
            Size(note.transform, 0, 22, flexibleWidth: 1);

            // 대화 난이도는 서버에 저장되므로 '이 기기에 저장' 안내 아래에 같은 설정 카드로 둔다(S15P31A305-799).
            Transform level = window.Find("ConversationLevel");
            if (level != null)
            {
                level.SetSiblingIndex(note.transform.GetSiblingIndex() + 1);
                ChoiceSetting(level, catalog, "대화 난이도");
            }

            Text(window.Find("ConsentsLabel"), catalog, UiThemeRole.Title, 22);
            Transform consents = window.Find("Consents");
            consents.GetComponent<VerticalLayoutGroup>().spacing = 8;
            Transform row = consents.Find("ConsentRowTemplate");
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.spacing = 14;
            rowLayout.padding = new RectOffset(0, 0, 0, 0);
            Size(row, 0, 48, flexibleWidth: 1);
            TMP_Text consent = Text(row.Find("Label"), catalog, UiThemeRole.Body, 20);
            consent.textWrappingMode = TextWrappingModes.Normal;
            Size(consent.transform, 0, 48, flexibleWidth: 1);
            Button(row.Find("ViewButton"), catalog, 76, 40, "보기");
        }

        /// <summary>설정 창의 효과음 카드만 다시 맞춘다. 두 창의 다른 줄은 건드리지 않는다.</summary>
        public static void ApplySoundEffects(GameObject root) =>
            SoundEffects(
                root.transform.Find("Settings/Window/SoundEffects"),
                AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(CatalogPath)
            );

        // 제목과 [켜기]·[끄기] 줄 아래에 크기 막대 줄을 다른 설정 막대와 같은 모양으로 둔다.
        private static void SoundEffects(Transform sound, UiThemeCatalog catalog)
        {
            ChoiceSetting(sound, catalog, "효과음");
            Transform volume = sound.Find("VolumeRow");
            if (volume == null)
                return;
            sound.GetComponent<VerticalLayoutGroup>().spacing = 8;
            SliderRow(volume, catalog, "Quiet", "Loud");
        }

        private static void Frame(Transform window, UiThemeCatalog catalog)
        {
            var rect = (RectTransform)window;
            rect.sizeDelta = new Vector2(740, rect.sizeDelta.y);
            var column = window.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(40, 40, 32, 32);
            column.spacing = 14;
            Text(window.Find("Title"), catalog, UiThemeRole.Title, 32);
        }

        private static void Status(Transform window, UiThemeCatalog catalog)
        {
            TMP_Text status = Text(window.Find("Status"), catalog, UiThemeRole.Muted, 16);
            status.textWrappingMode = TextWrappingModes.Normal;
            Size(status.transform, 0, 24, flexibleWidth: 1);
        }

        private static void Setting(
            Transform group,
            UiThemeCatalog catalog,
            string title,
            string help
        )
        {
            BuilderTools.GetOrAddComponent<Image>(group.gameObject).raycastTarget = false;
            Theme(group, catalog, UiThemeRole.Card);
            var column = group.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(22, 22, 16, 18);
            column.spacing = 8;
            TMP_Text heading = Text(group.Find("Title"), catalog, UiThemeRole.Body, 23);
            heading.text = title;
            Size(heading.transform, 0, 30, flexibleWidth: 1);
            TMP_Text description = Text(group.Find("Help"), catalog, UiThemeRole.Muted, 17);
            description.text = help;
            Size(description.transform, 0, 23, flexibleWidth: 1);
            SliderRow(group.Find("Row"), catalog, "Slow", "Fast");
        }

        // [낮은 쪽 이름] 막대 [높은 쪽 이름] 현재값 한 줄. 막대는 서비스 강조색으로 채운다.
        private static void SliderRow(
            Transform row,
            UiThemeCatalog catalog,
            string lowLabel,
            string highLabel
        )
        {
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            Size(row, 0, 36, flexibleWidth: 1);
            foreach (string name in new[] { lowLabel, highLabel })
            {
                TMP_Text label = Text(row.Find(name), catalog, UiThemeRole.Muted, 17);
                Size(label.transform, 55, 36);
            }
            TMP_Text value = Text(row.Find("Value"), catalog, UiThemeRole.Body, 22);
            value.alignment = TextAlignmentOptions.MidlineRight;
            Size(value.transform, 72, 36);
            Slider slider = row.Find("Slider").GetComponent<Slider>();
            Size(slider.transform, 0, 36, flexibleWidth: 1);
            Transform fill = slider.fillRect;
            if (fill != null)
            {
                RectTransform area = (RectTransform)fill.parent;
                area.anchorMin = new Vector2(area.anchorMin.x, .5f);
                area.anchorMax = new Vector2(area.anchorMax.x, .5f);
                area.anchoredPosition = new Vector2(area.anchoredPosition.x, 0);
                area.sizeDelta = new Vector2(area.sizeDelta.x, 18);
                ((RectTransform)fill).offsetMin = Vector2.zero;
                ((RectTransform)fill).offsetMax = Vector2.zero;
                fill.GetComponent<Image>().pixelsPerUnitMultiplier = 4;
                Theme(fill, catalog, UiThemeRole.SliderFill);
            }
            var track = (RectTransform)slider.transform.Find("Background");
            track.anchorMin = new Vector2(0, .5f);
            track.anchorMax = new Vector2(1, .5f);
            track.anchoredPosition = Vector2.zero;
            track.sizeDelta = new Vector2(0, 16);
            track.GetComponent<Image>().color = new Color(.68f, .64f, .59f, .3f);
        }

        // 창이 16:9 화면 높이 안에 들도록 제목과 고르는 버튼을 한 줄에 둔 카드다(대화 난이도, 효과음).
        // 버튼은 가입 화면 난이도 카드처럼 고른 것만 강조색으로 칠하고(SettingsView가 SetSelected), 이름은 다른 버튼과 같은 Jua 일반 굵기로 쓴다.
        private static void ChoiceSetting(Transform group, UiThemeCatalog catalog, string title)
        {
            BuilderTools.GetOrAddComponent<Image>(group.gameObject).raycastTarget = false;
            Theme(group, catalog, UiThemeRole.Card);
            group.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(22, 22, 14, 14);
            Transform line = group.Find("Row");
            line.GetComponent<HorizontalLayoutGroup>().spacing = 12;
            Size(line, 0, 46, flexibleWidth: 1);
            TMP_Text heading = Text(line.Find("Title"), catalog, UiThemeRole.Body, 23);
            heading.text = title;
            Size(heading.transform, 0, 46, flexibleWidth: 1);
            Transform choices = line.Find("Choices");
            var row = choices.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleRight;
            row.spacing = 12;
            foreach (Transform choice in choices)
            {
                TMP_Text label = choice.Find("Label").GetComponent<TMP_Text>();
                label.font = catalog.Get(UiThemeId.Jelly).DisplayFont;
                label.fontSharedMaterial = label.font.material;
                label.fontStyle = FontStyles.Normal;
                label.fontWeight = FontWeight.Regular;
                label.fontSize = 22;
                label.enableAutoSizing = false;
                label.alignment = TextAlignmentOptions.Midline;
                Size(choice, 120, 46);
            }
        }

        private static TMP_Text Text(
            Transform target,
            UiThemeCatalog catalog,
            UiThemeRole role,
            float size
        )
        {
            TMP_Text text = target.GetComponent<TMP_Text>();
            text.font = UiBuilderTools.RegularFont();
            text.fontSharedMaterial = text.font.material;
            text.fontStyle = FontStyles.Normal;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = Vector4.zero;
            Theme(target, catalog, role);
            return text;
        }

        private static void Button(
            Transform button,
            UiThemeCatalog catalog,
            float width,
            float height,
            string caption
        )
        {
            if (button == null)
                return;
            var oldTheme = button.GetComponent<ThemedElement>();
            UiTone tone =
                oldTheme != null
                    ? (UiTone)new SerializedObject(oldTheme).FindProperty("tone").enumValueIndex
                    : UiTone.Neutral;
            Theme(button, catalog, UiThemeRole.PillButton, tone);
            foreach (string decoration in new[] { "Outline", "Icon" })
            {
                Transform child = button.Find(decoration);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }
            TMP_Text label = button.Find("Label").GetComponent<TMP_Text>();
            ThemedElement labelTheme = label.GetComponent<ThemedElement>();
            if (labelTheme != null)
                Object.DestroyImmediate(labelTheme);
            label.font = catalog.Get(UiThemeId.Jelly).DisplayFont;
            label.fontSharedMaterial = label.font.material;
            label.fontStyle = FontStyles.Normal;
            label.fontWeight = FontWeight.Regular;
            label.fontSize = 22;
            label.enableAutoSizing = false;
            if (caption != null)
                label.text = caption;
            label.alignment = TextAlignmentOptions.Midline;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12, 6);
            label.rectTransform.offsetMax = new Vector2(-12, -6);
            Size(button, width, height);
        }

        private static void Theme(
            Transform target,
            UiThemeCatalog catalog,
            UiThemeRole role,
            UiTone tone = UiTone.Neutral
        )
        {
            var element = BuilderTools.GetOrAddComponent<ThemedElement>(target.gameObject);
            var fields = new SerializedObject(element);
            fields.FindProperty("tone").enumValueIndex = (int)tone;
            fields.FindProperty("icon").enumValueIndex = (int)UiIcon.None;
            fields.ApplyModifiedPropertiesWithoutUndo();
            element.Use(catalog, role);
        }

        private static void Size(
            Transform target,
            float width,
            float height,
            float flexibleWidth = 0
        )
        {
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(target.gameObject);
            size.minWidth = 0;
            size.preferredWidth = width;
            size.minHeight = size.preferredHeight = height;
            size.flexibleWidth = flexibleWidth;
            size.flexibleHeight = 0;
        }
    }
}
