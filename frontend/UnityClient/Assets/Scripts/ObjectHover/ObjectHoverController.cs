using System.Collections.Generic;
using A305.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.ObjectHover
{
    /// <summary>
    /// 포인터가 가리키는 사물을 찾아 툴팁을 보이거나 숨긴다. 플레이어가 움직이거나, 멀리 있거나,
    /// 대화처럼 이동이 잠긴 상호작용 중이면 보이지 않는다. 툴팁은 하나만 보인다.
    /// </summary>
    public class ObjectHoverController : MonoBehaviour
    {
        [SerializeField]
        private ObjectHoverSettings settings;

        [SerializeField]
        [Tooltip(
            "프리팹의 공용 단어 키로 뜻·예문을 조회하는 곳. 서버 연동 전에는 SharedObjectWords"
        )]
        private ObjectWordSource wordSource;

        [SerializeField]
        [Tooltip("포인터 방향을 계산할 플레이어 카메라")]
        private Camera viewCamera;

        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        private WordTooltipView tooltip;

        [SerializeField]
        [Tooltip("UI/Point. 화면의 포인터 위치")]
        private InputActionReference pointAction;

        private readonly ObjectHoverRaycast raycast = new ObjectHoverRaycast();
        private HoverableObject previousTarget;
        private int previousPart = -1;
        private readonly HashSet<string> keysWithoutWord = new HashSet<string>();
        private HoverTooltipState state;

        /// <summary>포인터 위치로 툴팁을 갱신한다. 매 프레임 Update에서 호출하며, 실제 마우스 없이 확인할 때 직접 호출할 수 있다.</summary>
        public void Refresh(Vector2 pointerScreenPosition, float deltaTimeSeconds)
        {
            HoverableObject target = FindPointedObject(
                pointerScreenPosition,
                out Vector3 pointedPosition
            );
            float distance = Vector3.Distance(player.transform.position, pointedPosition);
            int partIndex =
                target != null && target.Parts != null
                    ? target.Parts.FindPart(
                        target.transform.InverseTransformPoint(pointedPosition),
                        distance,
                        settings.DetailDistanceMeters
                    )
                    : -1;
            ObjectWordDefinition definition = null;
            if (target != null)
            {
                definition = partIndex < 0 ? target.Word : target.Parts.Parts[partIndex].Word;
            }
            if (target != previousTarget || partIndex != previousPart)
            {
                state.Update(null, false, 0f);
                previousTarget = target;
                previousPart = partIndex;
            }
            ObjectWord word = default;
            bool canShow =
                target != null
                && !player.IsMoving
                && !player.IsMovementLocked
                && distance <= settings.MaxDistanceMeters
                && TryGetWord(target, definition, out word);

            string wordKey = null;
            if (definition != null)
            {
                wordKey = definition.Key;
            }
            else if (target != null)
            {
                wordKey = target.UnityKey;
            }
            state.Update(wordKey, canShow, deltaTimeSeconds);

            if (state.VisibleKey == null)
            {
                tooltip.Hide();
                return;
            }

            tooltip.Show(word, pointerScreenPosition, settings);
        }

        private void Awake()
        {
            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            state = new HoverTooltipState(settings.ShowDelaySeconds);
        }

        private void OnEnable()
        {
            pointAction.action.Enable();
        }

        private void OnDisable()
        {
            if (tooltip != null)
            {
                tooltip.Hide();
            }
        }

        private void Update()
        {
            Refresh(pointAction.action.ReadValue<Vector2>(), Time.deltaTime);
        }

        /// <summary>포인터 방향의 가장 가까운 물체가 호버 사물이면 돌려준다. 플레이어 자신은 가리지 않는다.</summary>
        private HoverableObject FindPointedObject(
            Vector2 pointerScreenPosition,
            out Vector3 pointedPosition
        )
        {
            Ray ray = viewCamera.ScreenPointToRay(pointerScreenPosition);
            return raycast.Find(
                ray,
                settings.RayLengthMeters,
                settings.HoverLayers,
                player.transform,
                out pointedPosition
            );
        }

        private bool TryGetWord(
            HoverableObject target,
            ObjectWordDefinition definition,
            out ObjectWord word
        )
        {
            string unityKey = target.UnityKey;
            bool found =
                definition != null
                    ? wordSource.TryGetWord(definition, out word)
                    : wordSource.TryGetWord(unityKey, out word);
            if (found)
            {
                return true;
            }

            // 단어가 없는 사물은 툴팁 없이 넘어가고, 데이터를 채울 수 있게 한 번만 알린다.
            if (keysWithoutWord.Add(unityKey))
            {
                Debug.LogWarning($"사물 키 {unityKey}에 연결된 단어가 없습니다.", this);
            }

            return false;
        }

        private bool HasRequiredReferences()
        {
            bool hasAll = true;
            hasAll &= IsAssigned(settings, nameof(settings));
            hasAll &= IsAssigned(wordSource, nameof(wordSource));
            hasAll &= IsAssigned(viewCamera, nameof(viewCamera));
            hasAll &= IsAssigned(player, nameof(player));
            hasAll &= IsAssigned(tooltip, nameof(tooltip));
            hasAll &= IsAssigned(pointAction, nameof(pointAction));

            return hasAll;
        }

        private bool IsAssigned(Object reference, string fieldName)
        {
            if (reference != null)
            {
                return true;
            }

            Debug.LogError($"ObjectHoverController의 {fieldName}가 연결되지 않았습니다.", this);
            return false;
        }
    }
}
