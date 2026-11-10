using A305.Ambience;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    public sealed class BackgroundSoundToggle : MonoBehaviour
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private Image icon;

        [SerializeField]
        private Sprite enabledSprite;

        [SerializeField]
        private Sprite mutedSprite;

        [SerializeField]
        private Rect enabledIconGeometry;

        [SerializeField]
        private Rect mutedIconGeometry;

        public void ConfigureIconLayout(Rect enabled, Rect muted)
        {
            enabledIconGeometry = enabled;
            mutedIconGeometry = muted;
            Refresh();
        }

        public void Configure(Button target, Image picture, Sprite on, Sprite off)
        {
            if (button != null)
                button.onClick.RemoveListener(Toggle);
            button = target;
            icon = picture;
            enabledSprite = on;
            mutedSprite = off;
            if (isActiveAndEnabled && button != null)
                button.onClick.AddListener(Toggle);
            Refresh();
        }

        private void OnEnable()
        {
            if (button != null)
                button.onClick.AddListener(Toggle);
            Refresh();
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(Toggle);
        }

        private void Toggle()
        {
            BackgroundSoundPreference.Enabled = !BackgroundSoundPreference.Enabled;
            Refresh();
        }

        private void Refresh()
        {
            if (icon == null)
                return;
            bool enabled = BackgroundSoundPreference.Enabled;
            icon.sprite = enabled ? enabledSprite : mutedSprite;
            Rect fit = enabled ? enabledIconGeometry : mutedIconGeometry;
            if (fit.height <= 0)
                return;
            icon.rectTransform.anchoredPosition = fit.position;
            icon.rectTransform.sizeDelta = fit.size;
        }
    }
}
