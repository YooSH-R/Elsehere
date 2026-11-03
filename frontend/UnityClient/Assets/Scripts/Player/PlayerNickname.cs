using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 접속한 계정의 닉네임을 공간 씬 사이에 전달한다. 입구 화면이 접속·가입할 때, 내 정보가 닉네임을 바꿀 때 넣고,
    /// 폰 메뉴의 날짜 줄이 읽는다. 영구 저장은 계정 API가 맡는다.
    /// </summary>
    public static class PlayerNickname
    {
        /// <summary>모르면 null이다(게스트 진입 직후, 서버 없이 씬만 볼 때).</summary>
        public static string Name { get; private set; }

        public static void Set(string nickname)
        {
            Name = string.IsNullOrEmpty(nickname) ? null : nickname;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Name = null;
        }
    }
}
