using System.Collections.Generic;
using A305.Dialogue;
using A305.Npc;
using A305.Player;
using A305.Weather;
using A305.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.Ambience
{
    /// <summary>서비스 씬과 실제 출입·대화·차량 상태만 관찰한다. 서버 호출이나 이동 처리를 하지 않는다.</summary>
    public sealed class ServiceAmbiencePlayer : MonoBehaviour
    {
        private const float NormalVolume = .6f;
        private static ServiceAmbiencePlayer instance;
        private ServiceAmbienceCatalog catalog;
        private readonly AudioSource[] loops = new AudioSource[2];
        private readonly List<CarSound> cars = new List<CarSound>();
        private AudioSource effects;
        private AudioSource taxiDoor;
        private DialogueController dialogue;
        private PlayerMover player;
        private TaxiRide taxi;
        private WeatherPresenter weather;
        private AmbienceBed bed;
        private int current;
        private bool wasPassenger;
        private float refreshAt;
        private float bellAfter;
        private bool Talking => dialogue != null && dialogue.IsTalking;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartServiceAudio()
        {
#if UNITY_EDITOR
            if (Application.isBatchMode)
                return;
#endif
            if (instance != null)
                return;
            ServiceAmbienceCatalog settings = Resources.Load<ServiceAmbienceCatalog>(
                ServiceAmbienceCatalog.ResourcePath
            );
            if (settings == null)
            {
                Debug.LogWarning("장소 음원 카탈로그를 빌드해야 합니다.");
                return;
            }
            var root = new GameObject("ServiceAmbience");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<ServiceAmbiencePlayer>();
            instance.Configure(settings);
        }

        public void Configure(ServiceAmbienceCatalog settings)
        {
            catalog = settings;
            loops[0] = Source("Place A", transform, false);
            loops[1] = Source("Place B", transform, false);
            loops[0].loop = loops[1].loop = true;
            effects = Source("Events", transform, false);
            taxiDoor = Source("Taxi door", transform, false);
            SceneManager.sceneLoaded += SceneLoaded;
            RefreshReferences();
        }

        private static AudioSource Source(string name, Transform parent, bool spatial)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = 0;
            source.spatialBlend = spatial ? 1 : 0;
            source.minDistance = 3;
            source.maxDistance = 25;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            return source;
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode) => refreshAt = 0;

        private void RefreshReferences()
        {
            player = FindAnyObjectByType<PlayerMover>();
            taxi = FindAnyObjectByType<TaxiRide>();
            weather = FindAnyObjectByType<WeatherPresenter>();
            DialogueController next = FindAnyObjectByType<DialogueController>();
            if (dialogue != next)
            {
                if (dialogue != null)
                    dialogue.Started -= DialogueStarted;
                dialogue = next;
                if (dialogue != null)
                    dialogue.Started += DialogueStarted;
            }
            foreach (LoopingCar car in FindObjectsByType<LoopingCar>(FindObjectsSortMode.None))
                if (!cars.Exists(item => item.Car == car))
                    cars.Add(new CarSound(car, Source("Passing and horn", car.transform, true)));
            cars.RemoveAll(item => item.Car == null);
            refreshAt = Time.unscaledTime + 1;
        }

        private void DialogueStarted(string npcCode)
        {
            if (npcCode != "HOTEL_STAFF" || Time.unscaledTime < bellAfter)
                return;
            Play(AmbienceEffect.HotelBell);
            bellAfter = Time.unscaledTime + 20;
        }

        private void Update()
        {
            if (catalog == null)
                return;
            if (Time.unscaledTime >= refreshAt)
                RefreshReferences();
            bool passenger = taxi != null && taxi.IsPassenger;
            string scene =
                player != null ? player.gameObject.scene.name : SceneManager.GetActiveScene().name;
            bool rain =
                weather != null
                && weather.Current != null
                && weather.Current.Code != null
                && weather.Current.Code.Contains("RAIN");
            string placeCode = PlaceScenes.IsTownShop(scene) ? PlaceScenes.CodeOf(scene) : null;
            AmbienceBed next = AmbiencePlan.Resolve(scene, placeCode, passenger, rain);
            if (passenger != wasPassenger && next != AmbienceBed.None)
                Play(AmbienceEffect.TaxiDoorClose);
            wasPassenger = passenger;
            if (next != bed)
                ChangePlace(next);
            float gain =
                AmbiencePlan.Volume(NormalVolume, Talking) * BackgroundSoundPreference.Volume;
            bool muted = !BackgroundSoundPreference.Enabled;
            for (int index = 0; index < loops.Length; index++)
            {
                AudioSource source = loops[index];
                source.mute = muted;
                float target = index == current && bed != AmbienceBed.None ? gain : 0;
                source.volume = Mathf.MoveTowards(
                    source.volume,
                    target,
                    Time.unscaledDeltaTime * (Talking ? 2.4f : .8f)
                );
                if (target == 0 && source.volume == 0 && source.isPlaying)
                    source.Stop();
            }
            effects.volume = gain;
            effects.mute = muted;
            taxiDoor.mute = muted;
            foreach (CarSound car in cars)
            {
                if (car.Source != null)
                    car.Source.mute = muted;
                car.Tick(player, AmbiencePlan.IsStreet(bed), gain, catalog);
            }
        }

        private void ChangePlace(AmbienceBed next)
        {
            if (
                bed != AmbienceBed.None
                && next != AmbienceBed.None
                && bed != AmbienceBed.Taxi
                && next != AmbienceBed.Taxi
                && !(AmbiencePlan.IsStreet(bed) && AmbiencePlan.IsStreet(next))
            )
                Play(AmbienceEffect.DoorClose);
            bed = next;
            current = 1 - current;
            AudioSource source = loops[current];
            source.Stop();
            source.clip = catalog.Bed(next);
            source.volume = 0;
            if (source.clip != null)
                source.Play();
        }

        private void Play(AmbienceEffect effect)
        {
            if (!BackgroundSoundPreference.Enabled)
                return;
            AudioClip clip = catalog.Effect(effect);
            if (clip == null)
                return;
            AudioSource source = effect == AmbienceEffect.TaxiDoorClose ? taxiDoor : effects;
            source.volume =
                AmbiencePlan.EffectVolume(effect, NormalVolume, Talking)
                * BackgroundSoundPreference.Volume;
            source.PlayOneShot(clip);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (dialogue != null)
                dialogue.Started -= DialogueStarted;
            foreach (CarSound car in cars)
                if (car.Source != null)
                    Destroy(car.Source.gameObject);
            if (instance == this)
                instance = null;
        }

        private sealed class CarSound
        {
            public readonly LoopingCar Car;
            public readonly AudioSource Source;
            private readonly VehicleSoundGate gate = new VehicleSoundGate();
            private Vector3 previous;

            public CarSound(LoopingCar car, AudioSource source)
            {
                Car = car;
                Source = source;
                previous = car.transform.position;
            }

            public void Tick(
                PlayerMover player,
                bool outdoors,
                float gain,
                ServiceAmbienceCatalog settings
            )
            {
                if (Car == null || Source == null)
                    return;
                Vector3 position = Car.transform.position;
                bool moving =
                    (position - previous).sqrMagnitude
                    > .01f * Time.unscaledDeltaTime * Time.unscaledDeltaTime;
                previous = position;
                Source.volume = gain;
                if (player == null || !outdoors || Car.IsHeld)
                    return;
                float meters = Vector3.Distance(position, player.transform.position);
                if (gate.TryPass(Time.unscaledTimeAsDouble, meters, moving))
                    Source.PlayOneShot(settings.Effect(AmbienceEffect.CarPass), .65f);
                bool green = Car.Lights != null && Car.Lights.Main == SignalLight.Green;
                if (
                    gate.TryHorn(
                        Time.unscaledTimeAsDouble,
                        meters,
                        moving,
                        green,
                        Time.unscaledDeltaTime
                    )
                )
                    Source.PlayOneShot(settings.Effect(AmbienceEffect.ShortHorn), .4f);
            }
        }
    }
}
