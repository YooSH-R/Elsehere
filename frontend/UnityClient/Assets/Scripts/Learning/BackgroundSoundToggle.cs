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
            if (icon != null)
                icon.sprite = BackgroundSoundPreference.Enabled ? enabledSprite : mutedSprite;
        }
    }
}
