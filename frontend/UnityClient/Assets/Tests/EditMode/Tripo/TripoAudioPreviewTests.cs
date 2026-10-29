using A305.EditorTools.Tripo;
using NUnit.Framework;

namespace A305.Tripo.Tests
{
    public class TripoAudioPreviewTests
    {
        [Test]
        public void UnityVersion_ProvidesPlaybackCursorAndStopForEditorPreview()
        {
            Assert.That(
                TripoAudioPreview.IsSupported,
                Is.True,
                "고정 Unity 버전의 음성 미리보기 API를 확인합니다. 실제 소리 출력 시험과는 별도입니다."
            );
        }
    }
}
