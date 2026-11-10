using System;
using System.Threading.Tasks;
using A305.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Note = A305.Onboarding.LevelTestSpeakView.AnswerNote;

namespace A305.Onboarding
{
    /// <summary>
    /// 화면 04 레벨 테스트. 안내(L1), 듣기(L2)·말하기(L3) 문항, 결과(L4)를 한 유리 창에서 바꿔 보이고, 창 높이는 보이는 내용에 맞춘다
    /// (창의 ContentSizeFitter). 듣기 음성 재생과 말하기 녹음도 이 창이 맡는다. 진행 판단은 <see cref="LevelTestSession"/>,
    /// 서버 호출은 <see cref="OnboardingFlow"/>가 맡으므로 고른 보기·쓴 답·들은 횟수는 이벤트로 알리고 다시 <see cref="Show"/>를 받는다.
    /// </summary>
    public class LevelTestPanelView : MonoBehaviour
    {
        private const string NextLabel = "다음";
        private const string GradingLabel = "채점하고 있어요...";
        private const string SpeechFailedNotice =
            "음성을 불러오지 못했어요. [다시 듣기]를 눌러 주세요.";

        [Header("안내")]
        [SerializeField]
        private GameObject introPage;

        [SerializeField]
        private Button startButton;

        [SerializeField]
        private Button skipButton;

        [Header("문항")]
        [SerializeField]
        private GameObject itemHeader;

        [SerializeField]
        private TMP_Text progressText;

        [SerializeField]
        [Tooltip("진행 막대. 오른쪽 기준점을 진행률만큼 옮겨 늘인다")]
        private RectTransform progressFill;

        [SerializeField]
        private Button quitButton;

        [SerializeField]
        private LevelTestListenView listenView;

        [SerializeField]
        private LevelTestSpeakView speakView;

        [SerializeField]
        private GameObject itemFooter;

        [SerializeField]
        private Button nextButton;

        [SerializeField]
        private TMP_Text nextLabel;

        [Header("결과")]
        [SerializeField]
        private LevelTestResultView resultView;

        [Header("음성")]
        [SerializeField]
        private LevelTestSpeechPlayer speechPlayer;

        [SerializeField]
        private VoiceRecorder voiceRecorder;

        private LevelTestSession shownTest;
        private LevelTestItem shownItem;
        private bool canSpeak;
        private bool isMicrophoneUnavailable;
        private bool wasTooQuiet;

        // [그만하기]나 문항 이동으로 멈춘 녹음은 받아 적지 않는다.
        private bool discardsRecording;

        public event Action StartRequested;
        public event Action SkipRequested;
        public event Action QuitRequested;

        /// <summary>문항의 [다음]. 마지막 문항이면 채점을 기다리는 동안 버튼 이름을 바꿔 둔다.</summary>
        public event Action NextRequested;

        /// <summary>결과의 [다음]</summary>
        public event Action FinishRequested;
        public event Action<int> ChoiceSelected;

        /// <summary>듣기 음성을 받아 들려주기 시작했다. 처음 듣기와 다시 듣기 모두 알린다.</summary>
        public event Action ListeningPlayed;
        public event Action<string> AnswerChanged;

        /// <summary>말하기 답을 녹음했다(16kHz 모노 WAV). 받아 적은 말은 답 칸을 바꾼다.</summary>
        public event Action<byte[]> AnswerRecorded;

        /// <summary>화면 아래 안내 문구로 알릴 문제</summary>
        public event Action<string> NoticeRaised;

        private void Awake()
        {
            startButton.onClick.AddListener(() => StartRequested?.Invoke());
            skipButton.onClick.AddListener(() => SkipRequested?.Invoke());
            quitButton.onClick.AddListener(Quit);
            nextButton.onClick.AddListener(Next);
            listenView.PlayRequested += PlayListening;
            listenView.ChoiceSelected += choice => ChoiceSelected?.Invoke(choice);
            speakView.AnswerChanged += answer => AnswerChanged?.Invoke(answer);
            speakView.MicrophoneRequested += ToggleRecording;
            resultView.NextRequested += () => FinishRequested?.Invoke();
            voiceRecorder.AccessChanged += ShowMicrophoneAccess;
            voiceRecorder.RecordingFinished += FinishRecording;
        }

        /// <param name="test">푸는 테스트. 시작하기 전이면 null이고 안내를 보인다.</param>
        /// <param name="voiceAgreed">음성 처리에 동의했는지. 동의하지 않았으면 마이크를 막고 직접 쓰게 한다.</param>
        public void Show(LevelTestSession test, bool voiceAgreed, bool guestPreview = false)
        {
            TMP_Text introNote = introPage.transform.Find("Note")?.GetComponent<TMP_Text>();
            if (introNote != null)
                introNote.text = guestPreview
                    ? "게스트는 초급으로 시작해요. 레벨테스트는 로그인 후 이용할 수 있어요."
                    : "건너뛰면 초급으로 시작해요. 난이도는 다음 화면에서 바꿀 수 있어요.";
            shownTest = test;
            canSpeak = voiceAgreed;
            bool hasResult = test?.Result != null;
            LevelTestItem item = test == null || hasResult ? null : test.CurrentItem;
            if (item != shownItem)
            {
                StartItem(item);
            }

            introPage.SetActive(test == null);
            itemHeader.SetActive(item != null);
            itemFooter.SetActive(item != null);
            listenView.gameObject.SetActive(item?.Type == LevelTestItemType.Listen);
            speakView.gameObject.SetActive(item?.Type == LevelTestItemType.Speak);
            resultView.gameObject.SetActive(hasResult);

            if (hasResult)
            {
                resultView.Show(test);
            }
            else if (item != null)
            {
                ShowItem(test, item);
            }
        }

        private void ShowItem(LevelTestSession test, LevelTestItem item)
        {
            progressText.text = test.ProgressText;
            progressFill.anchorMax = new Vector2(test.Progress, 1);
            nextLabel.text = NextLabel;
            RefreshNextButton();
            if (item.Type == LevelTestItemType.Listen)
            {
                ShowListening();
            }
            else
            {
                speakView.Show(item, test.SpokenAnswer, canSpeak);
                speakView.ShowNote(CurrentNote());
            }
        }

        // 녹음은 무음·최대 시간으로 저절로 끝나기도 하므로 말하기 문항에서는 매 프레임 안내 문구와 [다음]을 맞춘다.
        private void Update()
        {
            if (shownItem?.Type == LevelTestItemType.Speak)
            {
                speakView.ShowNote(CurrentNote());
                RefreshNextButton();
            }
        }

        private void OnDisable()
        {
            StopRecordingWithoutAnswer();
            speechPlayer.Stop();
        }

        // 지난 문항의 음성·녹음을 멈추고, 듣기 문항은 처음 한 번을 바로 들려준다.
        private void StartItem(LevelTestItem item)
        {
            shownItem = item;
            speechPlayer.Stop();
            StopRecordingWithoutAnswer();
            isMicrophoneUnavailable = false;
            wasTooQuiet = false;
            if (item?.Type == LevelTestItemType.Listen)
            {
                PlayListening();
            }
        }

        private void ShowListening()
        {
            listenView.Show(
                shownItem,
                shownTest.ChosenChoice,
                shownTest.CanPlayListening && !speechPlayer.IsLoading
            );
        }

        private void RefreshNextButton()
        {
            nextButton.interactable = shownTest.CanGoNext && !voiceRecorder.IsRecording;
        }

        // 처음 듣기와 다시 듣기. 받지 못하면 들은 횟수를 세지 않아 다시 누를 수 있다.
        private async void PlayListening()
        {
            LevelTestSession test = shownTest;
            LevelTestItem item = shownItem;
            if (item == null || !test.CanPlayListening || speechPlayer.IsLoading)
            {
                return;
            }

            Task<bool> playing = speechPlayer.PlayAsync(item.ItemCode);
            ShowListening();
            bool played;
            try
            {
                played = await playing;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                played = false;
            }

            // 받는 사이 다음 문항으로 넘어갔거나 화면을 떠났으면 그대로 둔다.
            if (this == null || item != shownItem)
            {
                return;
            }

            if (played)
            {
                ListeningPlayed?.Invoke();
            }
            else
            {
                NoticeRaised?.Invoke(SpeechFailedNotice);
                ShowListening();
            }
        }

        private void ToggleRecording()
        {
            if (voiceRecorder.IsRecording)
            {
                voiceRecorder.StopRecording();
                return;
            }

            isMicrophoneUnavailable = false;
            wasTooQuiet = false;
            voiceRecorder.StartRecording();
        }

        private void StopRecordingWithoutAnswer()
        {
            if (voiceRecorder.IsRecording)
            {
                discardsRecording = true;
                voiceRecorder.StopRecording();
            }
        }

        // 말소리가 거의 없는 녹음을 받아 적으면 엉뚱한 문장이 답 칸에 들어오므로 다시 말해 달라고만 한다.
        private void FinishRecording(RecordedVoice voice)
        {
            if (discardsRecording)
            {
                discardsRecording = false;
                return;
            }

            if (!voice.HasSpeech(voiceRecorder.SilenceLevel))
            {
                wasTooQuiet = true;
                return;
            }

            AnswerRecorded?.Invoke(voice.WavBytes);
        }

        private void ShowMicrophoneAccess(MicrophoneAccess access)
        {
            isMicrophoneUnavailable = access != MicrophoneAccess.Granted;
        }

        private Note CurrentNote()
        {
            if (!canSpeak)
            {
                return Note.NoVoiceConsent;
            }

            if (voiceRecorder.IsPreparing)
            {
                return Note.Preparing;
            }

            if (voiceRecorder.IsRecording)
            {
                return Note.Recording;
            }

            if (isMicrophoneUnavailable)
            {
                return Note.MicrophoneUnavailable;
            }

            return wasTooQuiet ? Note.TooQuiet : Note.Ready;
        }

        private void Next()
        {
            if (shownTest.IsLastItem)
            {
                nextLabel.text = GradingLabel;
            }

            NextRequested?.Invoke();
        }

        private void Quit()
        {
            StopRecordingWithoutAnswer();
            speechPlayer.Stop();
            QuitRequested?.Invoke();
        }
    }
}
