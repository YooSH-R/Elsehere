using System;
using System.Collections.Generic;
using A305.Dialogue;
using A305.Player;
using A305.World;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 본길을 도는 택시. 인도에 서 있을 때 택시가 다가오면 [택시 세우기]가 보이고, 누르면 손님 옆 길가에 서면서
    /// 바로 뒷좌석에 태워 기사와 대화를 시작한다. 명확한 목적지를 말하면 이동해 보도에 내려준다.
    /// 버튼은 행인의 [말 걸기]와 같은 자리(<see cref="PasserbyOfferView"/>)에 뜨고 E 키로도 누른다.
    /// </summary>
    [RequireComponent(typeof(LoopingCar))]
    public sealed class TaxiRide : MonoBehaviour
    {
        private const float TalkDistanceMeters = 3.5f;

        [SerializeField]
        [Tooltip("운전석에 앉은 택시 기사")]
        private NpcIdentity driver;

        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        private DialogueController dialogue;

        [SerializeField]
        private PasserbyOfferView offerView;

        [SerializeField]
        private Transform passengerView;

        [SerializeField]
        private DialoguePanelView panel;

        [SerializeField]
        private List<TaxiDestination> destinations = new List<TaxiDestination>();
        private TaxiDestination destination;
        private bool passenger;
        private bool arriving;
        private CharacterController passengerCollider;
        private Vector3 pickupPosition;
        private float previousFieldOfView;
        private float previousNearClip;
        private Transform driverHead;
        private Quaternion restingHeadRotation;

        private readonly TaxiFare fare = new TaxiFare();
        private LoopingCar car;

        // 지금 보이는 버튼(종류·문구·남은 대화 수). 같은 버튼을 매 프레임 다시 그리지 않는다.
        private string shownPrompt;

        /// <summary>지나가는 택시를 세울 수 있는지. 튜토리얼 동안에는 다른 곳으로 가지 않게 막는다.</summary>
        public bool IsHailAllowed { get; set; } = true;

        public TaxiPhase Phase => fare.Phase;
        public bool IsPassenger => passenger;
        public string DestinationCode { get; private set; }

        private void Awake()
        {
            car = GetComponent<LoopingCar>();
            if (driver != null)
            {
                var animator = driver.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    driverHead = animator.GetBoneTransform(HumanBodyBones.Head);
                    restingHeadRotation = driverHead.localRotation;
                }
            }
            if (
                driver == null
                || player == null
                || dialogue == null
                || offerView == null
                || panel == null
                || passengerView == null
            )
            {
                Debug.LogError($"{name}의 TaxiRide 연결이 비어 있습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (dialogue != null)
            {
                dialogue.Ended += OnDialogueEnded;
                dialogue.UserTurnAccepted += OnUserTurnAccepted;
            }
        }

        private void OnDisable()
        {
            if (dialogue != null)
            {
                dialogue.Ended -= OnDialogueEnded;
                dialogue.UserTurnAccepted -= OnUserTurnAccepted;
            }

            if (passenger)
                LeaveTaxi();
            HidePrompt();
        }

        private void Update()
        {
            if (passenger)
            {
                if (destination != null && car.HasPulledOver && !arriving)
                {
                    arriving = true;
                    panel.SetVehicleStatus("도착했어요 · 하차 준비 중");
                    dialogue.End();
                }
                return;
            }
            Vector3 customer = player.transform.position;
            float distance = HorizontalDistance(customer);
            fare.Tick(distance, dialogue.IsTalking);
            if (fare.Phase == TaxiPhase.Cruising)
            {
                car.Release();
            }
            else if (fare.Phase == TaxiPhase.PullingOver && car.HasPulledOver)
            {
                fare.Arrive();
            }

            UpdatePrompt(customer, distance);
        }

        private void LateUpdate()
        {
            if (driverHead != null)
            {
                driverHead.localRotation = restingHeadRotation;
                if (passenger && destination == null)
                    driverHead.rotation =
                        Quaternion.AngleAxis(80, transform.up) * driverHead.rotation;
            }
            if (passenger)
                player.transform.position = transform.TransformPoint(new Vector3(.4f, .4f, -.9f));
        }

        private void UpdatePrompt(Vector3 customer, float distance)
        {
            bool isBusy =
                dialogue.IsTalking
                || player.IsMovementLocked
                || offerView.IsShowing
                || (offerView.IsTalkPromptShowing && shownPrompt == null);
            if (isBusy)
            {
                HidePrompt();
                return;
            }

            // 택시를 세우면 바로 기사와 대화하므로 게스트 대화를 다 쓰면 두 버튼 대신 이유를 보인다(기능 명세 F-39).
            ConversationAllowance allowance = dialogue.Allowance;
            TaxiPrompt prompt = TaxiPrompt.Choose(
                fare.Phase == TaxiPhase.Cruising
                    && IsHailAllowed
                    && TaxiFare.CanHail(car.Route, car.Distance, customer),
                fare.Phase == TaxiPhase.Waiting && distance < TalkDistanceMeters,
                allowance.CanStartConversation,
                allowance.RemainingConversations,
                dialogue.FindNpcName(driver.Code)
            );
            switch (prompt.Kind)
            {
                case TaxiPromptKind.Hail:
                    ShowPrompt(prompt, Hail);
                    break;
                case TaxiPromptKind.Talk:
                case TaxiPromptKind.Blocked:
                case TaxiPromptKind.Signup:
                    ShowPrompt(prompt, Talk);
                    break;
                default:
                    HidePrompt();
                    break;
            }
        }

        private void ShowPrompt(TaxiPrompt prompt, Action press)
        {
            string shown = $"{prompt.Kind}|{prompt.Label}|{prompt.Remaining}";
            if (shownPrompt == shown)
            {
                return;
            }

            shownPrompt = shown;
            if (prompt.Kind == TaxiPromptKind.Blocked)
            {
                offerView.ShowBlockedTalkPrompt(this, prompt.Label);
            }
            else if (prompt.Kind == TaxiPromptKind.Signup)
            {
                offerView.ShowSignupPrompt(this, prompt.Label, prompt.Remaining);
            }
            else
            {
                offerView.ShowTalkPrompt(this, prompt.Label, prompt.Remaining, press);
            }
        }

        private void HidePrompt()
        {
            if (shownPrompt == null)
            {
                return;
            }

            shownPrompt = null;
            offerView.HideTalkPrompt(this);
        }

        private void Hail()
        {
            Vector3 customer = player.transform.position;
            if (!TaxiFare.CanHail(car.Route, car.Distance, customer) || !fare.TryHail())
            {
                return;
            }

            // 손님 옆 길가에 바로 세우고 곧장 태운다. 태우지 못하면 서서 [말 걸기]를 기다린다.
            car.StopAt(TaxiFare.PullOverDistance(car.Route, car.Distance, customer));
            fare.Arrive();
            Talk();
        }

        private void Talk()
        {
            HidePrompt();
            if (!dialogue.TryStart(driver.Code, driver.LookTarget, ConversationInitiator.User))
                return;
            passenger = true;
            arriving = false;
            destination = null;
            DestinationCode = null;
            pickupPosition = player.transform.position;
            passengerCollider = player.GetComponent<CharacterController>();
            passengerCollider.enabled = false;
            player.enabled = false;
            car.SetPassengerOnBoard(true);
            Camera camera = player.CameraRig.GetComponent<Camera>();
            previousFieldOfView = camera.fieldOfView;
            previousNearClip = camera.nearClipPlane;
            camera.fieldOfView = 72;
            camera.nearClipPlane = .025f;
            player.CameraRig.EnterVehicleView(passengerView);
            panel.SetVehicleLayout(true);
            panel.SetVehicleStatus("목적지를 기사에게 알려주세요");
        }

        private void OnDialogueEnded(string npcCode)
        {
            if (npcCode == driver.Code)
            {
                LeaveTaxi();
                fare.EndRide();
            }
        }

        private void OnUserTurnAccepted(string npcCode, string text)
        {
            if (
                !passenger
                || npcCode != driver.Code
                || destination != null
                || !TaxiDestinationRequest.TryResolve(text, out string code)
            )
                return;
            foreach (TaxiDestination candidate in destinations)
            {
                if (candidate.Code == code)
                {
                    destination = candidate;
                    DestinationCode = code;
                    Vector3 curb = candidate.Stop;
                    float stop =
                        curb.x > 0
                            ? car.Route.NorthboundDistance(curb.z)
                            : car.Route.SouthboundDistance(curb.z);
                    car.PullOverAt(stop);
                    panel.SetVehicleStatus(candidate.DisplayName + "로 이동 중");
                    return;
                }
            }
            panel.SetVehicleStatus("아직 운행하지 않는 목적지예요");
        }

        private void LeaveTaxi()
        {
            if (!passenger)
                return;
            passenger = false;
            if (driverHead != null)
                driverHead.localRotation = restingHeadRotation;
            if (player == null || player.CameraRig == null)
                return;
            Vector3 point = pickupPosition;
            if (arriving && destination != null)
                point = destination.Alighting;
            else if (
                destination != null
                || Vector3.Distance(transform.position, pickupPosition) > 5
            )
            {
                point = transform.position;
                point.x = transform.position.x >= 0 ? 4.15f : -4.15f;
                point.y = pickupPosition.y;
            }
            player.enabled = true;
            player.Teleport(point, Quaternion.LookRotation(transform.right));
            if (passengerCollider != null)
                passengerCollider.enabled = true;
            player.CameraRig.ExitVehicleView();
            Camera camera = player.CameraRig.GetComponent<Camera>();
            camera.fieldOfView = previousFieldOfView;
            camera.nearClipPlane = previousNearClip;
            car.SetPassengerOnBoard(false);
            car.Release();
            // 승객인 채로 장면이 바뀌면 대화창이 택시보다 먼저 지워질 수 있다.
            if (panel != null)
                panel.SetVehicleLayout(false);
            destination = null;
            DestinationCode = null;
        }

        private float HorizontalDistance(Vector3 customer)
        {
            Vector3 offset = customer - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }
    }
}
