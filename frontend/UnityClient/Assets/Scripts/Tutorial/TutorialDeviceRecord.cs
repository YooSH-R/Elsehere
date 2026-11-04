using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>
    /// 이 기기(Web 빌드는 브라우저)에서 튜토리얼을 한 번이라도 끝냈는지(완료·건너뛰기). 게스트는 들어올 때마다 새 계정이라 서버 기록만으로는
    /// 같은 사람이 튜토리얼을 다시 보게 되므로, 기기에도 남겨 새 계정도 건너뛴 상태로 시작한다.
    /// </summary>
    /// <remarks>Editor에서는 튜토리얼을 거듭 확인해야 하므로 기록하지도 읽지도 않는다.</remarks>
    public sealed class TutorialDeviceRecord
    {
        public const string PreferenceKey = "A305.Tutorial.FinishedOnDevice";

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
    }
}
