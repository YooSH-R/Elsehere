using System;
using System.Collections.Generic;
using System.Linq;
using A305.Dialogue;
using A305.Npc;
using A305.Player;
using A305.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.Mission
{
    /// <summary>식당의 실제 부스에 앉아 있는 시간만 센다. 서버 수령 실패는 자동 반복하지 않고 다시 누를 수 있다.</summary>
    public sealed class DinerMealRest : IDisposable
    {
        private readonly PlayerMover player;
        private readonly DialogueController dialogue;
        private readonly PasserbyOfferView offer;
        private readonly Action receive;
        private readonly SeatedMealTimer timer = new SeatedMealTimer();
        private readonly List<Transform> seats = new List<Transform>();
        private readonly PlayerSeatPose pose;
        private bool seatedHere;
        private int sceneHandle = int.MinValue;

        public DinerMealRest(
            PlayerMover player,
            DialogueController dialogue,
            PasserbyOfferView offer,
            Action receive
        )
        {
            this.player = player;
            this.dialogue = dialogue;
            this.offer = offer;
            this.receive = receive;
            pose = player.GetComponent<PlayerSeatPose>();
        }

        public void Tick(float seconds, FirstDayMissionState state, bool busy)
        {
            bool waiting = state?.IsTownStory == true && state.CanRest && state.Stage == 0;
            Scene here = player.gameObject.scene;
            if (!waiting || PlaceScenes.CodeOf(here.name) != "DINER" || pose == null)
            {
                Stand();
                offer.HideTalkPrompt(this);
                if (!waiting)
                    timer.Reset();
                return;
            }
            EnsureSeats(here);
            if (seatedHere && !pose.IsSeated)
            {
                if (player.CameraRig != null)
                    player.CameraRig.ExitSeatView();
                seatedHere = false;
            }
            if (seatedHere)
            {
                if (timer.Tick(seconds, true))
                {
                    Stand();
                    offer.HideTalkPrompt(this);
                    receive();
                    return;
                }
                FollowUpStore.Shared.SetSeatedWaitRemaining("DINER_STAFF", timer.RemainingSeconds);
                offer.ShowMealWaitPrompt(
                    this,
                    $"음식 준비 중 · {Mathf.CeilToInt(timer.RemainingSeconds)}초",
                    Stand
                );
                return;
            }
            if (busy || dialogue.IsTalking || player.IsMovementLocked || offer.IsShowing)
            {
                offer.HideTalkPrompt(this);
                return;
            }
            Transform nearest = seats
                .Where(seat => seat != null)
                .OrderBy(seat => Vector3.Distance(player.transform.position, seat.position))
                .FirstOrDefault();
            if (
                nearest == null
                || Vector3.Distance(player.transform.position, nearest.position) > 1.8f
            )
            {
                offer.HideTalkPrompt(this);
                return;
            }
            if (offer.IsTalkPromptShowing && !offer.IsTalkPromptShownBy(this))
                return;
            offer.ShowTalkPrompt(
                this,
                timer.IsComplete ? "음식 받기" : "잠깐 앉기",
                () =>
                {
                    offer.HideTalkPrompt(this);
                    if (timer.IsComplete)
                        receive();
                    else
                    {
                        seatedHere = pose.Sit(nearest, .48f, .25f);
                        if (seatedHere && player.CameraRig != null)
                            player.CameraRig.EnterSeatView(nearest.Find("MealView"));
                    }
                },
                showTalkIcon: false
            );
        }

        private void EnsureSeats(Scene scene)
        {
            if (sceneHandle == scene.handle)
                return;
            ClearSeats();
            sceneHandle = scene.handle;
            Transform[] objects = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToArray();
            Transform[] tables = objects.Where(item => item.name == "DinerTable").ToArray();
            foreach (Transform booth in objects.Where(item => item.name == "BoothSeat"))
            {
                Transform table = tables
                    .OrderBy(item => Vector3.Distance(item.position, booth.position))
                    .FirstOrDefault();
                if (table == null)
                    continue;
                Transform marker = new GameObject("DinerMealSeat").transform;
                marker.SetParent(booth.parent, false);
                marker.position = booth.position - Vector3.up * .24f;
                Vector3 forward = table.position - booth.position;
                forward.y = 0;
                marker.rotation = Quaternion.LookRotation(forward);
                Transform eye = new GameObject("MealView").transform;
                eye.SetParent(marker, false);
                eye.localPosition = new Vector3(0, 1.15f, .12f);
                eye.localRotation = Quaternion.Euler(12, 0, 0);
                seats.Add(marker);
            }
        }

        private void Stand()
        {
            if (seatedHere && pose != null)
            {
                pose.Stand();
                if (player.CameraRig != null)
                    player.CameraRig.ExitSeatView();
            }
            seatedHere = false;
        }

        private void ClearSeats()
        {
            foreach (Transform seat in seats)
                if (seat != null)
                    UnityEngine.Object.Destroy(seat.gameObject);
            seats.Clear();
        }

        public void Dispose()
        {
            Stand();
            offer.HideTalkPrompt(this);
            ClearSeats();
        }
    }
}
