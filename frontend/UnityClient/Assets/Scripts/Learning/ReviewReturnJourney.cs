using UnityEngine;

namespace A305.Learning
{
    /// <summary>복습 전에 있던 장면·실내·위치와 시점. Transform을 보관하지 않아 씬이 사라져도 남는다.</summary>
    public sealed class ReviewReturnLocation
    {
        public ReviewReturnLocation(
            string sceneName,
            Pose pose,
            float viewYaw,
            string interiorSceneName = null
        )
        {
            SceneName = sceneName;
            Pose = pose;
            ViewYaw = viewYaw;
            InteriorSceneName = interiorSceneName;
        }

        public string SceneName { get; }
        public Pose Pose { get; }
        public float ViewYaw { get; }
        public string InteriorSceneName { get; }
    }

    /// <summary>장면을 옮겨도 첫 출발점을 유지하고, 실제 복귀가 끝난 뒤에만 비운다.</summary>
    public sealed class ReviewReturnJourney
    {
        public ReviewReturnLocation Origin { get; private set; }
        public bool IsReturning { get; private set; }

        public bool TryBegin(ReviewReturnLocation origin)
        {
            if (Origin != null || origin == null || string.IsNullOrWhiteSpace(origin.SceneName))
                return false;
            Origin = origin;
            return true;
        }

        public bool RequestReturn()
        {
            if (Origin == null)
                return false;
            IsReturning = true;
            return true;
        }

        public bool CompleteReturn(string sceneName)
        {
            if (!IsReturning || Origin?.SceneName != sceneName)
                return false;
            Clear();
            return true;
        }

        public void Clear()
        {
            Origin = null;
            IsReturning = false;
        }
    }
}
