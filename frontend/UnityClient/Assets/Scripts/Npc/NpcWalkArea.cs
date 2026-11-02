using UnityEngine;
using UnityEngine.AI;

namespace A305.Npc
{
    /// <summary>
    /// NPC가 걸어 다닐 바닥(NavMesh). Builder가 장면마다 구워 둔 데이터를 장면이 켜져 있는 동안만 등록해, 겹쳐 불러온 다른 장면의 바닥과
    /// 섞이지 않게 한다. 가구 배치를 바꾸면 Builder(Tools → A305 → Npc → Build Daily Life)로 다시 굽는다.
    /// </summary>
    public sealed class NpcWalkArea : MonoBehaviour
    {
        [SerializeField]
        private NavMeshData data;

        private NavMeshDataInstance instance;

        private void OnEnable()
        {
            if (data != null)
            {
                instance = NavMesh.AddNavMeshData(data);
            }
        }

        private void OnDisable()
        {
            instance.Remove();
        }
    }
}
