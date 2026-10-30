using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// [다시 듣기]로 재생할 NPC 음성. 지금 대사의 음성만 갖는다. 새 대사가 오면 지난 대사의 음성을 버리고, 지난 대사의 음성을 받는
    /// 사이에 새 대사로 바뀌었으면 받은 음성을 넣지 않는다.
    /// </summary>
    public sealed class NpcSpeechSlot
    {
        // 지금 대사의 음성 주소(/api/v1/turns/{id}/speech). 지금 대사에 음성이 없으면 null이다.
        private string expectedUrl;

        /// <summary>받아 둔 지금 대사의 음성. 아직 받지 못했거나 음성이 없으면 null이다.</summary>
        public AudioClip Clip { get; private set; }

        /// <summary>새 대사의 음성을 받기 시작한다.</summary>
        public void Expect(string url)
        {
            expectedUrl = url;
            Clip = null;
        }

        /// <summary>새 대사에 음성이 없다(합성 실패 등).</summary>
        public void Clear()
        {
            expectedUrl = null;
            Clip = null;
        }

        /// <returns>지금 대사의 음성이라 넣었으면 true. 그사이 대사가 바뀌었으면 false.</returns>
        public bool TryFill(string url, AudioClip clip)
        {
            if (url != expectedUrl)
            {
                return false;
            }

            Clip = clip;
            return true;
        }
    }
}
