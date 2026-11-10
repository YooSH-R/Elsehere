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
        private readonly EntryMusicFader entryMusic = new EntryMusicFader();

        // Web 빌드에서 로딩 페이지가 틀던 접속 음악을 이어받았으면 그 음악만 쓰고 Unity 음악(music)은 틀지 않는다.
        private bool usesLoadingPageMusic;
        private (float volume, bool playing, bool enabled) sentToLoadingPage = (-1, false, false);
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
            float? loadingPageVolume = LoadingPageMusic.CurrentVolume();
            usesLoadingPageMusic = loadingPageVolume.HasValue;
            if (usesLoadingPageMusic)
                entryMusic.ContinueLoadingPageMusic(loadingPageVolume.Value);
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
            // 브라우저가 허용하는 첫 클릭·키·터치 이후에만 Unity 음악을 시작한다.
            if (
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (
                    Touchscreen.current != null
                    && Touchscreen.current.primaryTouch.press.wasPressedThisFrame
                )
            )
                entryMusic.AllowSound();
            bool enabled = BackgroundSoundPreference.Enabled;
            effects.mute = !enabled;
            entryMusic.Update(SceneManager.GetActiveScene().name, enabled, Time.unscaledDeltaTime);
            if (usesLoadingPageMusic)
                ApplyToLoadingPageMusic(enabled);
            else
                ApplyToMusicSource();
            if (Time.unscaledTime >= scanAt)
            {
                scanAt = Time.unscaledTime + .35f;
                UpdateEars();
                AttachClickSounds();
            }
        }

        private void ApplyToMusicSource()
        {
            if (entryMusic.Playing && !music.isPlaying && music.clip != null)
                music.Play();
            else if (!entryMusic.Playing && music.isPlaying)
                music.Stop();
            music.volume = entryMusic.Volume;
        }

        // 바뀐 값만 넘겨, 음악을 끈 게임 공간에서는 프레임마다 브라우저를 부르지 않는다.
        private void ApplyToLoadingPageMusic(bool enabled)
        {
            var state = (entryMusic.Volume, entryMusic.Playing, enabled);
            if (state == sentToLoadingPage)
                return;
            sentToLoadingPage = state;
            LoadingPageMusic.Apply(entryMusic.Volume, entryMusic.Playing, enabled);
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
