using UnityEngine;

public class DemoCubeRotator : MonoBehaviour
{
    [SerializeField]
    private float rotationSpeedDegreesPerSecond = 90f;

    private void Update()
    {
        float rotationAngle =
            rotationSpeedDegreesPerSecond * Time.deltaTime;

        transform.Rotate(0f, rotationAngle, 0f, Space.Self);
    }
}
