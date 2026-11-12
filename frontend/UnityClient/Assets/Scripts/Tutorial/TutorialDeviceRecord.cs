using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>
    /// 이 기기(Web 빌드는 브라우저)에서 튜토리얼을 한 번이라도 끝냈는지(완료·건너뛰기)와 퀘스트 카드 안내를 확인했는지.
    /// 게스트는 들어올 때마다 새 계정이라 서버 기록만으로는 같은 사람이 튜토리얼을 다시 보게 되므로, 기기에도 남겨 새 계정도 건너뛴 상태로 시작한다.
    /// 첫 퀘스트는 꼭 해야 하므로 퀘스트 카드 안내는 이 기기에서 확인한 적이 없으면 새 계정에도 보인다.
    /// </summary>
    /// <remarks>Editor에서는 튜토리얼을 거듭 확인해야 하므로 기록하지도 읽지도 않는다.</remarks>
    public sealed class TutorialDeviceRecord
    {
        public const string PreferenceKey = "A305.Tutorial.FinishedOnDevice";
        public const string QuestGuidePreferenceKey = "A305.Tutorial.QuestGuideSeenOnDevice";

        private readonly bool isEnabled;

        /// <param name="isEnabled">기록을 읽고 쓰는지. 앱은 Editor가 아닐 때만 켠다.</param>
        public TutorialDeviceRecord(bool isEnabled)
        {
            this.isEnabled = isEnabled;
        }

        /// <summary>앱이 쓰는 기록. 테스트는 켠 기록으로 바꿔 끼운다.</summary>
        public static TutorialDeviceRecord Shared { get; set; } =
            new TutorialDeviceRecord(!Application.isEditor);

        public bool IsFinished => isEnabled && PlayerPrefs.GetInt(PreferenceKey, 0) == 1;

        public void MarkFinished()
        {
            if (!isEnabled)
            {
                return;
            }

            PlayerPrefs.SetInt(PreferenceKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 이 기기에서 오늘의 목표 안내를 보고 퀘스트 카드를 눌렀는지. 튜토리얼을 끝낸 기기의 새 계정도 이 안내는 확인하지 않았으면 보게 한다.
        /// </summary>
        public bool IsQuestGuideSeen =>
            isEnabled && PlayerPrefs.GetInt(QuestGuidePreferenceKey, 0) == 1;

        public void MarkQuestGuideSeen()
        {
            if (!isEnabled)
            {
                return;
            }

            PlayerPrefs.SetInt(QuestGuidePreferenceKey, 1);
            PlayerPrefs.Save();
        }
    }
}
