using System.Threading.Tasks;
using A305.Api;
using A305.Player;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 공간 씬에 들어오면 플레이어를 도착 출입구 앞에 세우고 카메라를 플레이어 뒤로 맞춘다.
    /// 이동 없이 씬을 열고 Play하면 씬에 놓인 플레이어 위치에서 시작한다.
    /// 장소 씬이면 서버에 재접속 위치로 저장한다(WORLD-05). 저장에 실패해도 플레이는 그대로 진행한다.
    /// </summary>
    public class SpaceArrival : MonoBehaviour
    {
        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        private PlayerCameraRig cameraRig;

        [SerializeField]
        [Tooltip(
            "재접속 위치를 저장할 API 서버 설정. 비우면 저장하지 않는다(서버 없이 씬만 확인할 때)."
        )]
        private ApiSettings apiSettings;

        // 플레이어·카메라가 Awake에서 준비를 마친 뒤 첫 프레임 전에 옮긴다.
        private void Start()
        {
            if (apiSettings != null)
            {
                _ = SaveLastSceneAsync();
            }

            if (player == null || cameraRig == null)
            {
                Debug.LogError("SpaceArrival의 player나 cameraRig가 연결되지 않았습니다.", this);
                return;
            }

            string doorwayId = SpaceTravel.TakeArrivalDoorwayId();
            if (string.IsNullOrEmpty(doorwayId))
            {
                return;
            }

            SpaceDoorway doorway = FindDoorway(doorwayId);
            if (doorway == null)
            {
                Debug.LogError(
                    $"도착할 출입구 {doorwayId}가 이 씬에 없어 씬에 놓인 위치에서 시작합니다.",
                    this
                );
                return;
            }

            Transform arrivalPoint = doorway.ArrivalPoint;
            player.Teleport(arrivalPoint.position, arrivalPoint.rotation);
            cameraRig.SnapBehindTarget();
        }

        private async Task SaveLastSceneAsync()
        {
            var recorder = new LastSceneRecorder(new LastSceneApi(apiSettings.CreateClient()));
            LastSceneRecordStatus status = await recorder.RecordAsync(gameObject.scene.name);
            if (status == LastSceneRecordStatus.Failed)
            {
                Debug.LogWarning(
                    $"재접속 위치를 저장하지 못했습니다({recorder.LastError.Code}): {recorder.LastError.Message}",
                    this
                );
            }
        }

        private static SpaceDoorway FindDoorway(string doorwayId)
        {
            foreach (
                SpaceDoorway doorway in FindObjectsByType<SpaceDoorway>(FindObjectsSortMode.None)
            )
            {
                if (doorway.DoorwayId == doorwayId)
                {
                    return doorway;
                }
            }

            return null;
        }
    }
}
