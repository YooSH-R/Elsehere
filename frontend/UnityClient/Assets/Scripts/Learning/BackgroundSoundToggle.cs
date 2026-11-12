using A305.Ambience;
using A305.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 오른쪽 위 스피커. 누르면 바로 아래에 배경 소리 가로 막대(시안 A)를 열고, 막대 바깥을 누르면 닫는다.
    /// 막대로 크기(0~100%)를 고르고, 막대 옆 스피커로 바로 끄고 켠다(<see cref="BackgroundSoundPreference"/>).
    /// 소리가 꺼져 있거나 0%이면 두 스피커를 꺼짐 그림으로 바꾼다. 버튼·막대는 BackgroundSoundBuilder가 연결한다.
    /// </summary>
    public sealed class BackgroundSoundToggle : MonoBehaviour
    {
        [SerializeField]
        private Image icon;

        [SerializeField]
        private Sprite enabledSprite;

        [SerializeField]
        private Sprite mutedSprite;

        // 켜짐 그림의 스피커 몸통에 맞춘 자리. 꺼짐 그림은 사선 때문에 그림 범위가 넓어도 같은 자리에 그려 스피커 크기가 변하지 않는다.
        [SerializeField]
        private Rect iconGeometry;

        [Header("배경 소리 가로 막대")]
        [SerializeField]
        private GameObject volumeWindow;

        [SerializeField]
        private Slider volumeBar;

        [SerializeField]
        private Image muteIcon;

        [SerializeField]
        private TMP_Text percent;

        public void ConfigureIconLayout(Rect fit)
        {
            iconGeometry = fit;
            Refresh();
        }

        public void Configure(Image picture, Sprite on, Sprite off)
        {
            icon = picture;
            enabledSprite = on;
            mutedSprite = off;
            Refresh();
        }

        public void ConfigureVolume(GameObject window, Slider bar, Image mute, TMP_Text label)
        {
            volumeWindow = window;
            volumeBar = bar;
            muteIcon = mute;
            percent = label;
            Refresh();
        }

        /// <summary>오른쪽 위 스피커: 막대를 열고, 열려 있으면 닫는다.</summary>
        public void ToggleVolumeWindow()
        {
            if (volumeWindow.activeSelf)
                CloseVolumeWindow();
            else
                OpenVolumeWindow();
        }

        /// <summary>막대를 연다. 다른 창·퀘스트 카드·미니맵보다 위에 그린다.</summary>
        public void OpenVolumeWindow()
        {
            volumeWindow.SetActive(true);
            UiLayer.Open(this);
            Refresh();
        }

        /// <summary>막대 바깥을 누름: 막대를 닫는다.</summary>
        public void CloseVolumeWindow()
        {
            volumeWindow.SetActive(false);
            UiLayer.Close(this);
        }

        /// <summary>막대를 끌어 고른 크기(0~1).</summary>
        public void SetVolume(float level)
        {
            BackgroundSoundPreference.SetLevel(level);
            Refresh();
        }

        /// <summary>막대 옆 스피커: 바로 끄고, 다시 누르면 고른 크기로 켠다.</summary>
        public void ToggleMute()
        {
            BackgroundSoundPreference.ToggleMute();
            Refresh();
        }

        private void OnEnable() => Refresh();

        private void OnDisable()
        {
            if (volumeWindow != null && volumeWindow.activeSelf)
                CloseVolumeWindow();
        }

        private void Refresh()
        {
            float level = BackgroundSoundPreference.Level;
            bool audible = level > 0;
            if (volumeBar != null)
                volumeBar.SetValueWithoutNotify(level);
            if (percent != null)
                percent.text = $"{Mathf.RoundToInt(level * 100)}%";
            if (muteIcon != null)
                muteIcon.sprite = audible ? enabledSprite : mutedSprite;
            if (icon == null)
                return;
            icon.sprite = audible ? enabledSprite : mutedSprite;
            if (iconGeometry.height <= 0)
                return;
            icon.rectTransform.anchoredPosition = iconGeometry.position;
            icon.rectTransform.sizeDelta = iconGeometry.size;
        }
    }
}
