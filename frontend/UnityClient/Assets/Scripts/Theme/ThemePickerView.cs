using System;
using A305.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme
{
    /// <summary>
    /// 테마 고르기 창. 집 씬에서는 HUD 메뉴의 [테마]가 연다. 고른 테마는 바로 적용하고 계정에 저장한다.
    /// </summary>
    public sealed class ThemePickerView : MonoBehaviour
    {
        [SerializeField]
        private ApiSettings apiSettings;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private GameObject window;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private TMP_Text message;

        [SerializeField]
        private ThemeOption[] options = Array.Empty<ThemeOption>();

        private ThemePickerFlow flow;
        private bool saving;

        private void Awake()
        {
            if (apiSettings == null || openButton == null || window == null || closeButton == null)
            {
                Debug.LogError("테마 고르기 화면의 API 설정·버튼·창 참조가 필요합니다.", this);
                enabled = false;
                return;
            }
            flow = new ThemePickerFlow(
                UiThemeSelection.Shared,
                new UiThemeApi(apiSettings.CreateClient())
            );
            window.SetActive(false);
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            foreach (ThemeOption option in options)
            {
                UiThemeId theme = option.Theme;
                option.Button.onClick.AddListener(() => Choose(theme));
            }
        }

        private void OnEnable()
        {
            UiThemeSelection.Shared.Changed += OnThemeChanged;
            ShowSelected();
        }

        private void OnDisable()
        {
            UiThemeSelection.Shared.Changed -= OnThemeChanged;
        }

        private void OnDestroy()
        {
            if (openButton != null)
                openButton.onClick.RemoveListener(Open);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
            foreach (ThemeOption option in options)
                if (option.Button != null)
                    option.Button.onClick.RemoveAllListeners();
        }

        private void Open()
        {
            message.text = "";
            window.SetActive(true);
            ShowSelected();
        }

        private void Close() => window.SetActive(false);

        private async void Choose(UiThemeId theme)
        {
            if (saving)
                return;
            saving = true;
            message.text = "";
            string error = await flow.ChooseAsync(theme);
            if (this == null)
                return;
            saving = false;
            message.text = error ?? "";
        }

        private void OnThemeChanged(UiThemeId theme) => ShowSelected();

        private void ShowSelected()
        {
            foreach (ThemeOption option in options)
                if (option.SelectedMark != null)
                    option.SelectedMark.SetActive(option.Theme == UiThemeSelection.Shared.Current);
        }

        [Serializable]
        private sealed class ThemeOption
        {
            [SerializeField]
            private UiThemeId theme;

            [SerializeField]
            private Button button;

            [SerializeField]
            private GameObject selectedMark;

            public UiThemeId Theme => theme;
            public Button Button => button;
            public GameObject SelectedMark => selectedMark;
        }
    }
}
