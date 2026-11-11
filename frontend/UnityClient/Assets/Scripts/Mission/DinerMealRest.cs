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
    /// <summary>햄버거 가게 좌석을 연결하고, 주문 대기 중일 때만 착석 시간을 센다.</summary>
    public sealed class DinerMealRest : IDisposable
    {
        private readonly PlayerMover player;
        private readonly DialogueController dialogue;
        private readonly PasserbyOfferView offer;
        private readonly Action receive;
        private readonly SeatedMealTimer timer = new SeatedMealTimer();
        private readonly List<(Transform Marker, float CushionTop, float FrontEdge)> seats = new();
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
            if (!waiting)
                timer.Reset();
            var shop = ShopInterior.Occupied;
            if (shop == null || shop.PlaceCode != "DINER" || !shop.IsLoaded || pose == null)
            {
                Stand();
                offer.HideTalkPrompt(this);
                return;
            }
            EnsureSeats(SceneManager.GetSceneByName(shop.SceneName));
            if (seatedHere && !pose.IsSeated)
            {
                if (player.CameraRig != null)
                    player.CameraRig.ExitSeatView();
                seatedHere = false;
            }
            if (seatedHere)
            {
                if (!waiting)
                {
                    offer.ShowTalkPrompt(this, "일어서기", Stand, showTalkIcon: false);
                    return;
                }
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
            var nearest = seats
                .Where(seat => seat.Marker != null)
                .OrderBy(seat => Vector3.Distance(player.transform.position, seat.Marker.position))
                .FirstOrDefault();
            if (
                nearest.Marker == null
                || Vector3.Distance(player.transform.position, nearest.Marker.position) > 1.8f
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
                        seatedHere = pose.Sit(
                            nearest.Marker,
                            nearest.CushionTop,
                            nearest.FrontEdge
                        );
                        if (seatedHere && player.CameraRig != null)
                            player.CameraRig.EnterSeatView(nearest.Marker.Find("MealView"));
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
            Transform counter = objects.FirstOrDefault(item => item.name == "DinerCounter");
            foreach (
                Transform chair in objects.Where(item =>
                    item.name == "BoothSeat" || item.name == "Stool"
                )
            )
            {
                bool isStool = chair.name == "Stool";
                Transform surface = isStool
                    ? counter
                    : tables
                        .OrderBy(item => Vector3.Distance(item.position, chair.position))
                        .FirstOrDefault();
                if (surface == null)
                    continue;
                // TownStorefrontBuilder의 부스와 스툴은 높이와 앉는 면 깊이가 다르다.
                float cushionTop = isStool ? .79f : .48f;
                float frontEdge = isStool ? .21f : .25f;
                Transform marker = new GameObject("DinerMealSeat").transform;
                marker.SetParent(chair.parent, false);
                marker.position = chair.position - Vector3.up * (isStool ? .74f : .24f);
                Vector3 forward = isStool ? -counter.right : surface.position - chair.position;
                forward.y = 0;
                marker.rotation = Quaternion.LookRotation(forward);
                Transform eye = new GameObject("MealView").transform;
                eye.SetParent(marker, false);
                eye.localPosition = new Vector3(0, cushionTop + .67f, .12f);
                eye.localRotation = Quaternion.Euler(12, 0, 0);
                seats.Add((marker, cushionTop, frontEdge));
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
            foreach (var seat in seats)
                if (seat.Marker != null)
                    UnityEngine.Object.Destroy(seat.Marker.gameObject);
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
