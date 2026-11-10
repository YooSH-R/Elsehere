using System.Collections.Generic;
using A305.Dialogue;
using A305.Npc;
using A305.Player;
using A305.World;
using UnityEngine;

namespace A305.Ambience
{
    /// <summary>이동 코드는 바꾸지 않고 보행자의 실제 위치 변화를 관찰한다.</summary>
    public sealed class FootstepPlayer : MonoBehaviour
    {
        private static FootstepPlayer instance;
        private readonly List<Walker> walkers = new List<Walker>();
        private FootstepCatalog catalog;
        private DialogueController dialogue;
        private TaxiRide taxi;
        private float refreshAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartFootsteps()
        {
#if UNITY_EDITOR
            if (Application.isBatchMode)
                return;
#endif
            if (instance != null)
                return;
            var clips = Resources.Load<FootstepCatalog>(FootstepCatalog.ResourcePath);
            if (clips == null || clips.Count == 0)
                return;
            var root = new GameObject("Footsteps");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<FootstepPlayer>();
            instance.catalog = clips;
        }

        private void RefreshWalkers()
        {
            walkers.RemoveAll(item => item.Root == null);
            foreach (PlayerMover player in FindObjectsByType<PlayerMover>(FindObjectsSortMode.None))
                AddWalker(player.transform, player);
            foreach (NpcIdentity npc in FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None))
                AddWalker(npc.transform, null);
            dialogue = FindAnyObjectByType<DialogueController>();
            taxi = FindAnyObjectByType<TaxiRide>();
            refreshAt = Time.unscaledTime + 1f;
        }

        private void AddWalker(Transform root, PlayerMover player)
        {
            if (walkers.Exists(item => item.Root == root))
                return;
            CharacterController body = root.GetComponent<CharacterController>();
            if (body == null)
                return;
            var node = new GameObject("Walking sound");
            node.transform.SetParent(root, false);
            AudioSource source = node.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = player != null ? 0f : 1f;
            source.minDistance = 1f;
            source.maxDistance = 12f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0;
            source.priority = player != null ? 100 : 200;
            walkers.Add(new Walker(root, body, player, source));
        }

        private void LateUpdate()
        {
            if (catalog == null)
                return;
            if (Time.unscaledTime >= refreshAt)
                RefreshWalkers();
            float duck = AmbiencePlan.Volume(1, dialogue != null && dialogue.IsTalking);
            foreach (Walker walker in walkers)
                walker.Tick(catalog, duck, taxi != null && taxi.IsPassenger);
        }

        private void OnDestroy()
        {
            foreach (Walker walker in walkers)
                if (walker.Source != null)
                    Destroy(walker.Source.gameObject);
            if (instance == this)
                instance = null;
        }

        private sealed class Walker
        {
            public readonly Transform Root;
            public readonly AudioSource Source;
            private readonly CharacterController body;
            private readonly PlayerMover player;
            private readonly FootstepStride stride = new FootstepStride();
            private Vector3 previous;
            private int clipIndex;

            public Walker(
                Transform root,
                CharacterController body,
                PlayerMover player,
                AudioSource source
            )
            {
                Root = root;
                this.body = body;
                this.player = player;
                Source = source;
                previous = root.position;
                clipIndex = Mathf.Abs(root.GetInstanceID() % 6);
            }

            public void Tick(FootstepCatalog clips, float duck, bool riding)
            {
                if (Root == null || Source == null || body == null)
                    return;
                Vector3 delta = Root.position - previous;
                previous = Root.position;
                delta.y = 0;
                bool walking = Root.gameObject.activeInHierarchy && body.enabled;
                if (player != null)
                    walking &= player.IsMoving && !player.IsMovementLocked && !riding;
                bool step = stride.Advance(
                    delta.magnitude,
                    Time.deltaTime,
                    body.isGrounded,
                    walking
                );
                Source.volume = (player != null ? .85f : .17f) * duck;
                if (step)
                    Source.PlayOneShot(clips.Step(clipIndex++));
            }
        }
    }
}
