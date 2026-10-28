using UnityEngine;

namespace A305.World
{
    /// <summary>같은 씬 안의 매장 문. 캐릭터가 다가오면 문짝을 열어 통로를 비운다.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ShopSwingDoor : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("문짝과 손잡이가 함께 회전하는 경첩 Transform")]
        private Transform hinge;

        [SerializeField]
        [Tooltip("문이 닫혔을 때 통로를 막는 문짝 Collider")]
        private Collider leafCollider;
        private readonly Collider[] nearby = new Collider[32];
        private BoxCollider sensor;
        private Quaternion closedRotation;
        private float angle;

        private void Awake()
        {
            closedRotation = hinge.localRotation;
            sensor = GetComponent<BoxCollider>();
        }

        private void Update()
        {
            // Query the volume so a stationary or newly spawned controller is also detected.
            int count = Physics.OverlapBoxNonAlloc(
                transform.TransformPoint(sensor.center),
                Vector3.Scale(sensor.size / 2, transform.lossyScale),
                nearby,
                transform.rotation,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore
            );
            bool occupied = false;
            for (int index = 0; index < count; index++)
                occupied |= nearby[index] is CharacterController;
            angle = Mathf.MoveTowards(angle, occupied ? 95 : 0, 180 * Time.deltaTime);
            hinge.localRotation = closedRotation * Quaternion.Euler(0, angle, 0);
            // Keep the opening clear while swinging. Restore collision only after closing.
            leafCollider.enabled = angle < .1f;
        }

        private void OnDisable()
        {
            angle = 0;
            if (hinge != null)
                hinge.localRotation = closedRotation;
            if (leafCollider != null)
                leafCollider.enabled = true;
        }
    }
}
