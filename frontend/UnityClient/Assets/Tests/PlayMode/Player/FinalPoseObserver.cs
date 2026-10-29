#if UNITY_EDITOR
using System;
using UnityEngine;

namespace A305.Player.Tests
{
    // 코루틴은 LateUpdate보다 먼저 재개된다. 렌더와 같은 최종 자세를 관찰한다.
    [DefaultExecutionOrder(2000)]
    public sealed class FinalPoseObserver : MonoBehaviour
    {
        public Action Observe;

        private void LateUpdate() => Observe?.Invoke();
    }
}
#endif
