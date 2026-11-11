using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace A305.ObjectHover
{
    /// <summary>
    /// 포인터가 가리키는 사물을 찾아 툴팁을 보이거나 숨긴다. 플레이어가 움직이거나, 멀리 있거나,
    /// 대화처럼 이동이 잠긴 상호작용 중이면 보이지 않는다. 툴팁은 하나만 보인다.
    /// 사물을 클릭하면 카드를 그 자리에 고정해 커서가 벗어나도 카드 안 버튼을 누를 수 있게 하고,
    /// [닫기]·빈 곳 클릭·Esc나 플레이어가 멀어지거나 대화가 시작되면 고정을 푼다.
    /// 장소에 들어오면 서버 단어 카드(WORLD-02)를 받아 저장·숨김 상태를 카드에 반영한다. 숨긴 단어는 호버로 띄우지 않고 클릭해야 보인다.
    /// </summary>
    public class ObjectHoverController : MonoBehaviour
    {
        // 화면 가로나 세로를 이 비율보다 넓게 차지하는 사물은 카드가 피할 수 없다고 본다.
        private const float LargeObjectScreenShare = .5f;

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

        // 카드 프리팹의 서버 연결. 없거나 꺼져 있으면 Unity 단어로만 카드를 보인다.
        private ObjectWordServerLink serverLink;

        // 이 장소의 서버 단어 카드. 받기 전이면 null이고, 받지 못하면 카드가 비어 있다.
        private ObjectWordCards cards;

        // 고정한 카드의 사물 위치. 플레이어가 이보다 최대 거리 넘게 멀어지면 고정을 푼다.
        private Vector3 pinnedPosition;

        // 고정한 카드의 단어 키와 클릭할 때의 카드 자리 기준. 카드 버튼을 누른 뒤 같은 자리에 다시 그린다.
        private string pinnedWordKey;
        private CardAnchor pinnedAnchor;

        // 사물 클릭을 판단하려고 누른 자리를 기억한다. UI 위에서 눌렀으면 비운다.
        private Vector2? pressedAt;

        // 포인터 아래 UI를 찾는 데 매 프레임 다시 쓴다.
        private PointerEventData uiPointer;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private readonly List<GameObject> uiHitObjects = new List<GameObject>();

        // 사물·캐릭터의 화면 영역을 구하는 데 매 프레임 다시 쓴다.
        private readonly List<Renderer> renderers = new List<Renderer>();

        /// <summary>
        /// 포인터 위치를 클릭했다. 단어가 있는 가까운 사물이면 그 카드를 클릭한 자리에 고정하고, 아니면 고정을 푼다.
        /// Update에서 클릭을 판단해 호출하며, 실제 마우스 없이 확인할 때 직접 호출할 수 있다.
        /// </summary>
        public void Click(Vector2 pointerScreenPosition)
        {
            HoverableObject target = FindPointedObject(
                pointerScreenPosition,
                out Vector3 pointedPosition
            );
            float distance = Vector3.Distance(player.transform.position, pointedPosition);
            ObjectWordDefinition definition = DefinitionAt(
                target,
                pointedPosition,
                distance,
                out _
            );
            if (IsUiAt(pointerScreenPosition))
                return;
            if (!TryGetClickableWord(target, definition, distance, out ObjectWord word))
            {
                Unpin();
                return;
            }

            pinnedWordKey = WordKeyOf(target, definition);
            pinnedAnchor = AnchorFor(target, pointerScreenPosition);
            pinnedPosition = pointedPosition;
            state.Pin(pinnedWordKey);
            tooltip.ShowPinned(word, pinnedAnchor, settings, PinnedButtons());
        }

        /// <summary>포인터 위치로 툴팁을 갱신한다. 매 프레임 Update에서 호출하며, 실제 마우스 없이 확인할 때 직접 호출할 수 있다.</summary>
        public void Refresh(Vector2 pointerScreenPosition, float deltaTimeSeconds)
        {
            HoverableObject target = FindPointedObject(
                pointerScreenPosition,
                out Vector3 pointedPosition
            );
            float distance = Vector3.Distance(player.transform.position, pointedPosition);
            ObjectWordDefinition definition = DefinitionAt(
                target,
                pointedPosition,
                distance,
                out int partIndex
            );
            bool overUi = IsUiAt(pointerScreenPosition);
            ObjectWord word = default;
            bool canClick = !overUi && TryGetClickableWord(target, definition, distance, out word);
            if (canClick)
                InteractionCursor.MarkWorldTarget();

            if (state.IsPinned)
            {
                KeepOrClosePinnedCard();
                // 고정한 뒤 열린 창(수첩·가이드 등)에 가려지지 않게 자리를 다시 정한다.
                tooltip.UpdatePlacement();
                return;
            }

            if (target != previousTarget || partIndex != previousPart)
            {
                state.Update(null, false, 0f);
                previousTarget = target;
                previousPart = partIndex;
            }
            string wordKey = WordKeyOf(target, definition);
            bool canShow =
                canClick
                && ObjectHoverRules.CanHover(
                    overUi,
                    player.IsMoving,
                    player.IsMovementLocked,
                    distance <= settings.MaxDistanceMeters,
                    PlayerTouchControls.ShouldShowControls()
                )
                && (cards == null || cards.CanHover(wordKey));

            state.Update(wordKey, canShow, deltaTimeSeconds);

            if (state.VisibleKey == null)
            {
                tooltip.Hide();
                return;
            }

            tooltip.Show(word, AnchorFor(target, pointerScreenPosition), settings);
        }

        private void Awake()
        {
            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            state = new HoverTooltipState(settings.ShowDelaySeconds);
            serverLink = tooltip.GetComponent<ObjectWordServerLink>();
            tooltip.CloseRequested += Unpin;
            tooltip.SaveRequested += SaveWord;
            tooltip.HoverHiddenToggleRequested += ToggleHoverHidden;
            tooltip.PronunciationRequested += PlayPronunciation;
            ObjectWordCards.SavedWordRemoved += ForgetSavedWord;
        }

        // 장소의 서버 단어 카드를 받는다. 서버를 쓰지 않거나 서버 장소가 아닌 씬(검수용)이면 Unity 단어로만 보인다.
        private async void Start()
        {
            if (
                serverLink == null
                || !serverLink.IsAvailable
                || !ObjectWordScenes.TryGetSceneCode(gameObject.scene.name, out string sceneCode)
            )
            {
                return;
            }

            var loading = new ObjectWordCards(serverLink.CreateApi());
            try
            {
                await loading.LoadAsync(sceneCode);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return;
            }

            if (this == null)
            {
                return;
            }

            // Editor에서는 서버 없이도 [저장]·[알아요]를 눌러 볼 수 있게 확인용 단어 카드로 바꾼다. 빌드는 버튼 없이 둔다.
            if (!loading.IsLoaded && Application.isEditor)
            {
                Debug.LogWarning(
                    "서버에서 단어 카드를 받지 못해 Editor 확인용 단어 카드로 저장·숨김을 해 봅니다. 이 Play 동안만 기억하고, 저장한 단어는 Editor 확인용 단어장에 보입니다.",
                    this
                );
                loading = new ObjectWordCards(new LocalObjectWordApi(SceneWordDetails()));
                await loading.LoadAsync(sceneCode);
            }

            cards = loading;
            RefreshPinnedCard();
        }

        // 이 장소에서 가리킬 수 있는 사물의 공용 단어(가구 전체와 세부 부분)
        private IEnumerable<ObjectWordDetails> SceneWordDetails()
        {
            foreach (
                HoverableObject target in FindObjectsByType<HoverableObject>(
                    FindObjectsSortMode.None
                )
            )
            {
                if (target.Word != null)
                {
                    yield return target.Word.Details;
                }

                if (target.Parts == null)
                {
                    continue;
                }

                foreach (ObjectWordPartsDefinition.Part part in target.Parts.Parts)
                {
                    if (part.Word != null)
                    {
                        yield return part.Word.Details;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            ObjectWordCards.SavedWordRemoved -= ForgetSavedWord;
            if (tooltip != null)
            {
                tooltip.CloseRequested -= Unpin;
                tooltip.SaveRequested -= SaveWord;
                tooltip.HoverHiddenToggleRequested -= ToggleHoverHidden;
                tooltip.PronunciationRequested -= PlayPronunciation;
            }
        }

        private void OnEnable()
        {
            pointAction.action.Enable();
        }

        private void OnDisable()
        {
            if (state != null)
            {
                state.Unpin();
            }

            if (tooltip != null)
            {
                tooltip.Hide();
            }
        }

        // 클릭으로 고정한 사물 카드는 ESC 목록(UiEscape)에 올려, 나중에 연 다른 창보다 먼저 닫히지 않게 한다.
        private UiEscapeWindow pinnedEscape;

        private void Update()
        {
            Vector2 pointer = pointAction.action.ReadValue<Vector2>();
            DetectClick(pointer);
            pinnedEscape ??= new UiEscapeWindow(() => state.IsPinned, Unpin);
            pinnedEscape.Watch();

            Refresh(pointer, Time.deltaTime);
        }

        // 누른 자리에서 거의 움직이지 않고 떼면 사물 클릭으로 본다. 시점을 돌린 드래그와,
        // 카드의 [닫기]·HUD처럼 UI를 누른 것은 빼고 휴대폰 탭도 같은 방식으로 처리한다.
        private void DetectClick(Vector2 pointer)
        {
            Pointer device = Pointer.current;
            if (device == null)
            {
                return;
            }

            if (device.press.wasPressedThisFrame)
            {
                pressedAt = IsUiAt(pointer) ? null : pointer;
            }
            else if (device.press.wasReleasedThisFrame && pressedAt.HasValue)
            {
                bool isClick = PointerClick.IsClick(pressedAt.Value, pointer);
                pressedAt = null;
                if (isClick)
                {
                    Click(pointer);
                }
            }
        }

        // 화면 좌표에 HUD 버튼·메뉴·창 같은 UI가 있어 사물 호버·클릭을 막는지. 호버 카드는 클릭을 받지 않아 걸리지 않는다.
        // 휴대폰·터치 노트북의 터치 조작판은 화면을 덮지만 끌기만 받으므로 막지 않는다(WorldClickBlocker).
        // 실제 마우스 대신 좌표를 넣어 확인할 수 있도록 EventSystem의 현재 포인터가 아니라 좌표로 UI를 찾는다.
        private bool IsUiAt(Vector2 screenPosition)
        {
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                return false;
            }

            uiPointer ??= new PointerEventData(events);
            uiPointer.position = screenPosition;
            uiHits.Clear();
            events.RaycastAll(uiPointer, uiHits);
            uiHitObjects.Clear();
            foreach (RaycastResult hit in uiHits)
            {
                uiHitObjects.Add(hit.gameObject);
            }

            return WorldClickBlocker.IsBlockedBy(uiHitObjects);
        }

        // 대화처럼 이동이 잠기거나 플레이어가 멀어지면 고정을 푼다. 걷는 동안에는 그대로 둔다.
        private void KeepOrClosePinnedCard()
        {
            float distance = Vector3.Distance(player.transform.position, pinnedPosition);
            if (player.IsMovementLocked || distance > settings.MaxDistanceMeters)
            {
                Unpin();
            }
        }

        private void Unpin()
        {
            state.Unpin();
            pinnedWordKey = null;
            tooltip.Hide();
            // 고정을 푼 자리의 사물도 다시 지연 시간을 지나야 호버 카드가 보이게 한다.
            previousTarget = null;
            previousPart = -1;
        }

        private WordCardButtons PinnedButtons()
        {
            return cards != null && cards.TryGet(pinnedWordKey, out WordCard card)
                ? WordCardButtons.For(
                    card,
                    serverLink != null && serverLink.BrowserPronunciationAvailable
                )
                : WordCardButtons.None;
        }

        // 카드 버튼으로 상태가 바뀌었거나 서버 단어 카드를 늦게 받았으면 고정한 카드를 같은 자리에 다시 그린다.
        private void RefreshPinnedCard()
        {
            if (!state.IsPinned || cards == null || !cards.TryGet(pinnedWordKey, out WordCard card))
            {
                return;
            }

            tooltip.ShowPinned(
                card.Word,
                pinnedAnchor,
                settings,
                WordCardButtons.For(
                    card,
                    serverLink != null && serverLink.BrowserPronunciationAvailable
                )
            );
        }

        // 단어장에서 지운 단어는 이 장소에 머무는 동안에도 카드에서 다시 저장할 수 있게 한다.
        private void ForgetSavedWord(string text)
        {
            cards?.ForgetSaved(text);
            RefreshPinnedCard();
        }

        private void SaveWord()
        {
            RunCardRequest(wordKey => cards.SaveAsync(wordKey));
        }

        private void ToggleHoverHidden()
        {
            RunCardRequest(wordKey =>
                cards.SetHoverHiddenAsync(
                    wordKey,
                    cards.TryGet(wordKey, out WordCard card) && !card.HoverHidden
                )
            );
        }

        // 요청을 보내는 동안 버튼을 막은 카드를 보이고, 끝나면 결과(저장한 예문, 실패 이유)를 다시 그린다.
        private async void RunCardRequest(Func<string, Task> request)
        {
            if (cards == null || pinnedWordKey == null)
            {
                return;
            }

            Task running = request(pinnedWordKey);
            RefreshPinnedCard();
            try
            {
                await running;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (this != null)
            {
                RefreshPinnedCard();
            }
        }

        private async void PlayPronunciation()
        {
            if (cards == null || !cards.TryGet(pinnedWordKey, out WordCard card))
            {
                return;
            }

            bool played =
                card.HasPronunciation && await serverLink.PlayPronunciationAsync(card.VocabId);
            if (!played && this != null)
                played = serverLink.PronounceInBrowser(card.Word.Text);
            if (!played && this != null && state.IsPinned)
            {
                tooltip.ShowStatus("발음을 불러오지 못했어요. 잠시 후 다시 눌러 주세요.");
            }
        }

        /// <summary>가까이에서 가구의 세부 영역을 가리키면 그 부분의 단어, 아니면 사물 전체의 단어.</summary>
        private ObjectWordDefinition DefinitionAt(
            HoverableObject target,
            Vector3 pointedPosition,
            float distance,
            out int partIndex
        )
        {
            partIndex =
                target != null && target.Parts != null
                    ? target.Parts.FindPart(
                        target.transform.InverseTransformPoint(pointedPosition),
                        distance,
                        settings.DetailDistanceMeters
                    )
                    : -1;
            if (target == null)
            {
                return null;
            }

            return partIndex < 0 ? target.Word : target.Parts.Parts[partIndex].Word;
        }

        private static string WordKeyOf(HoverableObject target, ObjectWordDefinition definition)
        {
            if (definition != null)
            {
                return definition.Key;
            }

            return target != null ? target.UnityKey : null;
        }

        /// <summary>
        /// 카드를 둘 자리 기준. 가리킨 사물과 캐릭터의 화면 영역을 함께 넘겨 카드가 덮지 않게 한다.
        /// 바닥·벽처럼 화면을 넓게 차지하는 사물은 덮지 않을 수 없으므로 포인터 한 점만 피한다.
        /// </summary>
        private CardAnchor AnchorFor(HoverableObject target, Vector2 pointerScreenPosition)
        {
            Rect screen = viewCamera.pixelRect;
            Rect pointedArea =
                ScreenAreaOf(target) is Rect area
                && area.width <= screen.width * LargeObjectScreenShare
                && area.height <= screen.height * LargeObjectScreenShare
                    ? area
                    : new Rect(pointerScreenPosition, Vector2.zero);
            return new CardAnchor(pointerScreenPosition, pointedArea, ScreenAreaOf(player));
        }

        /// <summary>보이는 Renderer를 감싸는 상자를 화면에 비춘 영역(픽셀). Renderer가 없거나 상자 일부가 카메라 뒤에 있으면 없다.</summary>
        private Rect? ScreenAreaOf(Component root)
        {
            root.GetComponentsInChildren(renderers);
            Bounds? visible = null;
            foreach (Renderer renderer in renderers)
            {
                if (
                    !renderer.enabled
                    || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                )
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                if (visible.HasValue)
                {
                    bounds.Encapsulate(visible.Value);
                }

                visible = bounds;
            }

            if (!visible.HasValue)
            {
                return null;
            }

            Vector3 min = visible.Value.min;
            Vector3 max = visible.Value.max;
            Vector2 screenMin = Vector2.positiveInfinity;
            Vector2 screenMax = Vector2.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z
                );
                Vector3 onScreen = viewCamera.WorldToScreenPoint(corner);
                if (onScreen.z <= viewCamera.nearClipPlane)
                {
                    return null;
                }

                screenMin = Vector2.Min(screenMin, onScreen);
                screenMax = Vector2.Max(screenMax, onScreen);
            }

            return Rect.MinMaxRect(screenMin.x, screenMin.y, screenMax.x, screenMax.y);
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

        // 걷는 동안이나 호버에서 숨긴 단어도 클릭하면 카드를 열 수 있다.
        private bool TryGetClickableWord(
            HoverableObject target,
            ObjectWordDefinition definition,
            float distance,
            out ObjectWord word
        )
        {
            word = default;
            return target != null
                && !player.IsMovementLocked
                && distance <= settings.MaxDistanceMeters
                && TryGetWord(target, definition, out word);
        }

        private bool TryGetWord(
            HoverableObject target,
            ObjectWordDefinition definition,
            out ObjectWord word
        )
        {
            // 서버 단어 카드가 있으면 사용자의 저장 상태에 맞춘 카드를 쓴다.
            if (cards != null && cards.TryGet(WordKeyOf(target, definition), out WordCard card))
            {
                word = card.Word;
                return true;
            }

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
