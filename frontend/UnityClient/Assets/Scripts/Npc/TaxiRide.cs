using System;
using A305.Dialogue;
using A305.Player;
using A305.World;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 본길을 도는 택시. 인도에 서 있을 때 택시가 다가오면 [택시 세우기]가 보이고, 누르면 손님 옆 길가에 선다.
    /// 서 있는 택시 곁에서는 [기사에게 말 걸기]로 택시 기사와 대화한다. 대화를 마치거나 손님이 멀어지면 다시 달린다.
    /// 버튼은 행인의 [말 걸기]와 같은 자리(<see cref="PasserbyOfferView"/>)에 뜨고 E 키로도 누른다.
    /// </summary>
    [RequireComponent(typeof(LoopingCar))]
    public sealed class TaxiRide : MonoBehaviour
    {
        private const string HailLabel = "택시 세우기";
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

        private readonly TaxiFare fare = new TaxiFare();
        private LoopingCar car;
        private string promptLabel;

        public TaxiPhase Phase => fare.Phase;

        private void Awake()
        {
            car = GetComponent<LoopingCar>();
            if (driver == null || player == null || dialogue == null || offerView == null)
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
            }
        }

        private void OnDisable()
        {
            if (dialogue != null)
            {
                dialogue.Ended -= OnDialogueEnded;
            }

            HidePrompt();
        }

        private void Update()
        {
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

        private void UpdatePrompt(Vector3 customer, float distance)
        {
            bool isBusy =
                dialogue.IsTalking
                || player.IsMovementLocked
                || offerView.IsShowing
                || (offerView.IsTalkPromptShowing && promptLabel == null);
            if (isBusy)
            {
                HidePrompt();
                return;
            }

            if (
                fare.Phase == TaxiPhase.Cruising
                && TaxiFare.CanHail(car.Route, car.Distance, customer)
            )
            {
                ShowPrompt(HailLabel, Hail);
            }
            else if (fare.Phase == TaxiPhase.Waiting && distance < TalkDistanceMeters)
            {
                ShowPrompt(PasserbyOfferText.TalkLabel(dialogue.FindNpcName(driver.Code)), Talk);
            }
            else
            {
                HidePrompt();
            }
        }

        private void ShowPrompt(string label, Action press)
        {
            if (promptLabel == label)
            {
                return;
            }

            promptLabel = label;
            offerView.ShowTalkPrompt(this, label, press);
        }

        private void HidePrompt()
        {
            if (promptLabel == null)
            {
                return;
            }

            promptLabel = null;
            offerView.HideTalkPrompt(this);
        }

        private void Hail()
        {
            Vector3 customer = player.transform.position;
            if (!TaxiFare.CanHail(car.Route, car.Distance, customer) || !fare.TryHail())
            {
                return;
            }

            car.PullOverAt(TaxiFare.PullOverDistance(car.Route, car.Distance, customer));
            HidePrompt();
        }

        private void Talk()
        {
            HidePrompt();
            dialogue.TryStart(driver.Code, driver.LookTarget, ConversationInitiator.User);
        }

        private void OnDialogueEnded(string npcCode)
        {
            if (npcCode == driver.Code)
            {
                fare.EndRide();
            }
        }

        private float HorizontalDistance(Vector3 customer)
        {
            Vector3 offset = customer - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }
    }
}
