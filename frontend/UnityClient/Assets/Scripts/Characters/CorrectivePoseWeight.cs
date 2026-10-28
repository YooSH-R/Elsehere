using UnityEngine;

namespace A305.Characters
{
    public static class CorrectivePoseWeight
    {
        public static float Evaluate(
            Quaternion reference,
            Quaternion current,
            Vector3 axis,
            float startDegrees,
            float fullDegrees,
            bool magnitude
        )
        {
            float angle = Quaternion.Angle(reference, current);
            if (!magnitude)
            {
                Quaternion delta = Quaternion.Inverse(reference) * current;
                if (delta.w < 0f)
                    delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);
                float projected = Vector3.Dot(
                    new Vector3(delta.x, delta.y, delta.z),
                    axis.normalized
                );
                angle = 2f * Mathf.Atan2(projected, delta.w) * Mathf.Rad2Deg;
            }
            return Mathf.InverseLerp(startDegrees, fullDegrees, angle) * 100f;
        }
    }
}
