using System;

namespace A305.EditorTools.Tripo
{
    [Serializable]
    public sealed class TripoRevision
    {
        public string candidatePath;
        public string activePath;
        public string previousPath;
        public string validatedFingerprint;
        public bool validationPassed;
        public bool visuallyApproved;

        public void Restore()
        {
            if (string.IsNullOrWhiteSpace(previousPath))
                throw new InvalidOperationException("복원할 이전 버전이 없습니다.");
            string restoredPath = previousPath;
            previousPath = activePath;
            activePath = restoredPath;
            candidatePath = string.Empty;
            Invalidate();
        }

        public void Apply(string currentFingerprint)
        {
            if (string.IsNullOrWhiteSpace(candidatePath))
                throw new InvalidOperationException("적용할 수정본이 없습니다.");
            if (!validationPassed || !visuallyApproved)
                throw new InvalidOperationException("검사 통과와 시각 검수가 필요합니다.");
            if (
                string.IsNullOrEmpty(currentFingerprint)
                || currentFingerprint != validatedFingerprint
            )
                throw new InvalidOperationException(
                    "모델 또는 설정이 변경되었습니다. 다시 검증하세요."
                );
            previousPath = activePath;
            activePath = candidatePath;
            candidatePath = "";
            Invalidate();
        }

        public void Invalidate()
        {
            validationPassed = false;
            visuallyApproved = false;
            validatedFingerprint = "";
        }
    }
}
