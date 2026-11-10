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

        // 접속 화면처럼 카메라 귀(AudioListener)가 없는 씬에서만 켜는 귀. 다른 귀가 있으면 끈다.
        private AudioListener ears;
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
            ears = gameObject.AddComponent<AudioListener>();
            UpdateEars();
            UiSoundEvents.Requested += PlayCue;
            SceneManager.sceneLoaded += SceneLoaded;
            AttachClickSounds();
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
            UpdateEars();
            AttachClickSounds();
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
                UpdateEars();
                AttachClickSounds();
            }
        }

        // 소리를 들을 귀가 하나는 있게 한다. 카메라 귀가 있는 씬에서는 내 귀를 꺼 귀가 둘이 되지 않게 한다.
        private void UpdateEars()
        {
            bool other = false;
            foreach (
                AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
            )
                other |= listener != ears && listener.isActiveAndEnabled;
            ears.enabled = !other;
        }

        /// <summary>
        /// 누르는 조작(버튼·토글·드롭다운)에 클릭음을 붙인다. 끌어서 쓰는 슬라이더·스크롤바와 글자 입력칸에는 붙이지 않는다.
        /// </summary>
        public static void AttachClickSounds()
        {
            foreach (
                Selectable control in FindObjectsByType<Selectable>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
            )
                if (UiClickSound.Clicks(control) && !control.TryGetComponent<UiClickSound>(out _))
                    control.gameObject.AddComponent<UiClickSound>();
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
