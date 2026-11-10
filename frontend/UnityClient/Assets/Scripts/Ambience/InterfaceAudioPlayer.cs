using A305.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.Ambience
{
    /// <summary>접속 BGM과 UI 효과음만 재생한다. 음성·STT·서버에는 관여하지 않는다.</summary>
    public sealed class InterfaceAudioPlayer : MonoBehaviour
    {
        private static InterfaceAudioPlayer instance;
        private InterfaceAudioCatalog catalog;
        private AudioSource music;
        private AudioSource effects;
        private readonly UiSoundGate gate = new UiSoundGate();
        private bool unlocked;
        private float scanAt;
        public AudioSource MusicSource => music;
        public AudioSource EffectsSource => effects;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
#if UNITY_EDITOR
            if (Application.isBatchMode)
                return;
#endif
            if (instance != null)
                return;
            var settings = Resources.Load<InterfaceAudioCatalog>(
                InterfaceAudioCatalog.ResourcePath
            );
            if (settings == null)
                return;
            var root = new GameObject("InterfaceAudio");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<InterfaceAudioPlayer>();
            instance.Configure(settings);
        }

        public void Configure(InterfaceAudioCatalog settings)
        {
            catalog = settings;
            music = Source("Entry music");
            effects = Source("UI cues");
            music.clip = settings.Music;
            music.loop = true;
            music.volume = 0;
            UiSoundEvents.Requested += PlayCue;
            SceneManager.sceneLoaded += SceneLoaded;
            AttachClicks();
        }

        private AudioSource Source(string label)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            return source;
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            scanAt = 0;
            AttachClicks();
        }

        private void OnDestroy()
        {
            UiSoundEvents.Requested -= PlayCue;
            SceneManager.sceneLoaded -= SceneLoaded;
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (catalog == null)
                return;
            // 브라우저가 허용하는 첫 클릭·키·터치 이후에만 음악을 시작한다.
            unlocked |=
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (
                    Touchscreen.current != null
                    && Touchscreen.current.primaryTouch.press.wasPressedThisFrame
                );
            bool enabled = BackgroundSoundPreference.Enabled;
            effects.mute = !enabled;
            string scene = SceneManager.GetActiveScene().name;
            float target = EntryMusicPlan.Volume(scene, unlocked, enabled);
            if (target > 0 && !music.isPlaying && music.clip != null)
                music.Play();
            music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime * .36f);
            if (scene != "EntryScene" && music.volume <= .0001f && music.isPlaying)
                music.Stop();
            if (Time.unscaledTime >= scanAt)
            {
                scanAt = Time.unscaledTime + .35f;
                AttachClicks();
            }
        }

        private static void AttachClicks()
        {
            foreach (
                Button button in FindObjectsByType<Button>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
            )
                if (!button.TryGetComponent<UiClickSound>(out _))
                    button.gameObject.AddComponent<UiClickSound>();
        }

        private void PlayCue(UiSound sound)
        {
            if (catalog == null)
                return;
            AudioClip clip = catalog.Cue(sound);
            if (
                clip == null
                || !gate.TryPlay(
                    sound,
                    BackgroundSoundPreference.Enabled,
                    Time.unscaledTimeAsDouble
                )
            )
                return;
            float level =
                sound == UiSound.Click ? .28f
                : sound == UiSound.Menu ? .25f
                : .36f;
            effects.PlayOneShot(clip, level);
        }
    }
}
