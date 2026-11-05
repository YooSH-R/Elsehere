using System;
using System.Collections.Generic;
using A305.ObjectHover;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼 코치 마크 화면. 눌러야 할 버튼은 둘레를 반투명 검은 판으로 덮고 테두리로 밝혀 그 버튼만 누를 수 있게 하고,
    /// 걸어가야 할 숙소 문은 플레이어 발밑 바닥에 나침반처럼 원과 가장자리 화살표로 가리키고, 1인칭처럼 발밑이 화면에 없으면
    /// 화면 아래 가운데 화살표로 같은 방향을 알린다. 다가오는 Ben은 머리 위에서 오르내리는 화살표로 가리키되, 그 자리가
    /// 화면 밖이거나 위쪽 안내 카드에 걸리면 얼굴을 가리지 않게 띄우지 않는다. 밝히기·나침반·화살표 중 하나만 보인다.
    /// 설명은 밝힌 곳 옆에 꼬리 달린 말풍선으로 두고, 티칭처럼 읽고 넘길 설명에는 [다음]을 붙인다.
    /// 녹음 중 불처럼 설명과 함께 알릴 곳에는 같은 모양의 두 번째 말풍선을 더 띄운다.
    /// 판은 밝힐 버튼이 있는 캔버스 바로 위에 그려, 그보다 위에 있는 안내 카드는 가리지 않는다.
    /// 판은 화면을 덮는 막이라 고정한 단어 카드가 피하지 않는다.
    /// </summary>
    public class CoachMarkView : MonoBehaviour, IScreenBackdrop
    {
        private const float HolePadding = 10f;
        private const float BubbleGap = 28f;
        private const float BubbleMargin = 24f;
        private const float CaptionMinWidth = 220f;
        private const float MarkerEdgeMargin = 70f;

        // 두 번째 말풍선 너비. 대화창 오른쪽에 남는 자리(16:9에서 약 250, 16:10에서 약 200)에 들어가게 설명 말풍선보다 좁게 두고,
        // 이보다 좁아 글이 너무 길게 늘어지면 위에 둔다.
        private const float NoteMinWidth = 190f;
        private const float NoteMaxWidth = 250f;

        // 위쪽 안내 카드가 놓이는 높이. 여기에 걸린 머리 위 화살표는 카드에 가리므로 띄우지 않는다.
        private const float MarkerTopMargin = 260f;

        // 발밑 나침반: 발밑 원 지름, 가장자리 점 지름과 발밑에서 떨어진 거리, 점 위 화살표 길이, 점이 대상 쪽으로 오가는 폭,
        // 바닥에 묻히지 않게 띄우는 높이(미터).
        private const float CompassRingMeters = 1.3f;
        private const float CompassPointerMeters = 0.42f;
        private const float CompassPointerDistanceMeters = 0.8f;
        private const float CompassArrowMeters = 0.3f;
        private const float CompassBobMeters = 0.12f;
        private const float CompassLiftMeters = 0.03f;
        private const float CompassRingAlpha = 0.35f;

        // 발밑 나침반이 화면에 없을 때 화면 아래 가운데에 띄우는 화살표의 높이(캔버스 아래에서).
        private const float CompassScreenArrowBottom = 150f;
        private const float MarkerBobPixels = 12f;
        private const float MarkerBobSpeed = 4f;
        private const float FramePulseSpeed = 3f;
        private const int FrameSpriteSize = 96;
        private const float FrameRadius = 26f;

        [SerializeField]
        private Canvas canvas;

        [SerializeField]
        [Tooltip(
            "구멍 둘레를 덮는 아래·위·왼쪽·오른쪽 판. 클릭을 받아 구멍 밖을 누르지 못하게 한다."
        )]
        private RectTransform[] shades = new RectTransform[4];

        [SerializeField]
        [Tooltip("구멍 가장자리에 두르는 테두리")]
        private CanvasGroup frame;

        [SerializeField]
        private RectTransform caption;

        [SerializeField]
        private TMP_Text captionText;

        [SerializeField]
        [Tooltip("말풍선에서 밝힌 곳을 향하는 꼬리. 말풍선 가장자리에 붙인다.")]
        private RectTransform captionTail;

        [SerializeField]
        [Tooltip("티칭처럼 읽고 넘길 설명의 [다음]")]
        private Button nextButton;

        [SerializeField]
        [Tooltip("장면 속 대상 위에 띄우는 화살표 배지. 클릭을 받지 않는다.")]
        private RectTransform marker;

        [SerializeField]
        [Tooltip("배지 안의 화살표. 오른쪽을 가리키는 아이콘을 대상 쪽으로 돌린다.")]
        private RectTransform markerArrow;

        [SerializeField]
        [Tooltip("발밑 나침반의 원과 가장자리 점. 흰 원 그림에 화살표 배지와 같은 색을 입힌다.")]
        private Sprite compassDiscSprite;

        [SerializeField]
        [Tooltip("발밑 나침반 가장자리 점 위의 화살표. 오른쪽을 가리키는 그림")]
        private Sprite compassArrowSprite;

        private readonly List<RectTransform> targets = new List<RectTransform>();
        private readonly List<Rect> targetRects = new List<Rect>();
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform canvasRect;
        private float captionMaxWidth;
        private Transform markerTarget;
        private float markerHeightMeters;
        private Action nextAction;
        private Transform compassWalker;
        private Transform compassTarget;
        private Transform compassRoot;
        private Transform compassRing;
        private Transform compassPointer;
        private Transform compassArrow;
        private RectTransform note;
        private TMP_Text noteText;
        private RectTransform noteTail;
        private RectTransform noteTarget;
        private Sprite roundedBorderSprite;
        private Sprite roundedCornerShadeSprite;
        private Texture2D roundedBorderTexture;
        private Texture2D roundedCornerShadeTexture;

        private void Awake()
        {
            canvasRect = (RectTransform)canvas.transform;
            captionMaxWidth = caption.sizeDelta.x;
            nextButton.onClick.AddListener(() => nextAction?.Invoke());
            CreateRoundedFrame();
            CreateNote();
            Hide();
        }

        // 두 번째 말풍선은 설명 말풍선과 모양이 같도록 그대로 복사해 만들고, 읽고 넘길 설명이 아니므로 [다음]은 뺀다.
        private void CreateRoundedFrame()
        {
            foreach (Transform bar in frame.transform)
            {
                bar.gameObject.SetActive(false);
            }

            Image corners = CreateFrameImage("RoundedCorners");
            roundedCornerShadeSprite = CreateFrameSprite(true, out roundedCornerShadeTexture);
            corners.sprite = roundedCornerShadeSprite;
            corners.color = shades[0].GetComponent<Image>().color;

            Image border = CreateFrameImage("RoundedBorder");
            roundedBorderSprite = CreateFrameSprite(false, out roundedBorderTexture);
            border.sprite = roundedBorderSprite;
            border.color = marker.GetComponent<Image>().color;
        }

        private Image CreateFrameImage(string name)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                .GetComponent<Image>();
            image.transform.SetParent(frame.transform, false);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite CreateFrameSprite(bool shadeCorners, out Texture2D texture)
        {
            texture = new Texture2D(FrameSpriteSize, FrameSpriteSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[FrameSpriteSize * FrameSpriteSize];
            float half = FrameSpriteSize / 2f;
            float extent = half - 8f;
            for (int y = 0; y < FrameSpriteSize; y++)
            {
                for (int x = 0; x < FrameSpriteSize; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - half) - (extent - FrameRadius);
                    float dy = Mathf.Abs(y + 0.5f - half) - (extent - FrameRadius);
                    float distance =
                        Mathf.Min(Mathf.Max(dx, dy), 0f)
                        + new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude
                        - FrameRadius;
                    float edge = Mathf.Abs(distance);
                    float alpha = shadeCorners
                        ? Mathf.Clamp01(distance + 0.5f)
                        : Mathf.Max(
                            Mathf.Clamp01(3f - edge),
                            0.22f * Mathf.Clamp01((11f - edge) / 7f)
                        );
                    pixels[y * FrameSpriteSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, FrameSpriteSize, FrameSpriteSize),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(34f, 34f, 34f, 34f)
            );
        }

        private void CreateNote()
        {
            note = Instantiate(caption, caption.parent);
            note.name = "Note";
            noteText = CopyOf(captionText);
            noteTail = CopyOf(captionTail);
            Destroy(CopyOf(nextButton).gameObject);
            note.gameObject.SetActive(false);
        }

        // 설명 말풍선 안의 부품과 같은 자리에 있는 복사본 부품
        private T CopyOf<T>(T part)
            where T : Component
        {
            var path = new List<string>();
            for (Transform current = part.transform; current != caption; current = current.parent)
            {
                path.Insert(0, current.name);
            }

            return note.Find(string.Join("/", path)).GetComponent<T>();
        }

        /// <summary>대상 버튼들을 한 구멍으로 밝히고, 설명이 있으면 그 옆에 말풍선으로 보인다.</summary>
        /// <param name="next">있으면 말풍선에 [다음]을 두고 누르면 부른다. 티칭처럼 읽고 넘길 설명에 쓴다.</param>
        public void ShowSpotlight(
            IReadOnlyList<RectTransform> spotlightTargets,
            string text,
            Action next = null
        )
        {
            HideCompass();
            nextAction = next;
            markerTarget = null;
            marker.gameObject.SetActive(false);
            targets.Clear();
            targets.AddRange(spotlightTargets);
            captionText.text = text ?? "";
            nextButton.gameObject.SetActive(next != null);
            caption.gameObject.SetActive(!string.IsNullOrEmpty(text));
            // 말풍선 높이는 글 길이에 맞춰 바뀌므로 자리를 잡기 전에 크기를 다시 계산한다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(caption);
            DrawAboveTargets();
            SetSpotlightVisible(true);
            LateUpdate();
        }

        /// <summary>
        /// 장면 속 대상의 발밑에서 <paramref name="heightMeters"/> 위에 화살표를 띄운다. 그 자리가 화면 밖이거나
        /// 위쪽 안내 카드에 걸리면 띄우지 않는다.
        /// </summary>
        public void ShowMarker(Transform target, float heightMeters)
        {
            HideCompass();
            targets.Clear();
            SetSpotlightVisible(false);
            markerTarget = target;
            markerHeightMeters = heightMeters;
            LateUpdate();
        }

        /// <summary>
        /// <paramref name="walker"/>(플레이어) 발밑 바닥에 원을 깔고, 원 가장자리에서 <paramref name="target"/> 쪽을 가리키는 화살표를
        /// 둔다. 화면에 대상이 있든 없든 같은 자리에서 방향을 알리고, 캐릭터나 문을 가리지 않는다.
        /// </summary>
        public void ShowCompass(Transform walker, Transform target)
        {
            targets.Clear();
            SetSpotlightVisible(false);
            markerTarget = null;
            marker.gameObject.SetActive(false);
            compassWalker = walker;
            compassTarget = target;
            EnsureCompass();
            compassRoot.gameObject.SetActive(true);
            LateUpdate();
        }

        /// <summary>발밑 나침반이 화면에 없어(1인칭 등) 화면 아래 화살표로 방향을 알리는지</summary>
        public bool IsCompassArrowShowing => compassWalker != null && marker.gameObject.activeSelf;

        /// <summary>발밑 나침반이 보이는지</summary>
        public bool IsCompassShowing => compassRoot != null && compassRoot.gameObject.activeSelf;

        /// <summary>발밑 나침반이 가리키는 바닥 방향. 보이지 않으면 0이다.</summary>
        public Vector3 CompassDirection { get; private set; }

        public void Hide()
        {
            HideNote();
            HideCompass();
            targets.Clear();
            markerTarget = null;
            nextAction = null;
            SetSpotlightVisible(false);
            marker.gameObject.SetActive(false);
        }

        public bool IsSpotlightShowing => targets.Count > 0;

        /// <summary>말풍선에 [다음]이 보이는지</summary>
        public bool IsNextShowing =>
            caption.gameObject.activeSelf && nextButton.gameObject.activeSelf;

        /// <summary>말풍선 자리(캔버스 왼쪽 아래 기준). 말풍선이 없으면 크기가 0이다.</summary>
        public Rect CaptionRect { get; private set; }

        /// <summary>말풍선이 가리면 안 되는 다른 화면(대화·대화 노트 위에 남긴 [건너뛰기] 카드). 비어 있거나 숨어 있으면 보지 않는다.</summary>
        public RectTransform KeepClear { get; set; }

        /// <summary>밝힌 구멍 밖을 어둡게 덮어 누르지 못하게 하는지</summary>
        public bool IsDimmingOutside => IsSpotlightShowing;

        /// <summary>지금 밝히는 버튼들. 밝히지 않으면 비어 있다.</summary>
        public IReadOnlyList<RectTransform> SpotlightTargets => targets;

        /// <summary>화살표로 가리키는 장면 속 대상. 화면 밖이라 화살표가 숨어 있어도 대상은 남는다.</summary>
        public Transform MarkerTarget => markerTarget;

        /// <summary>밝힌 곳 옆 말풍선의 설명. 설명이 없으면 null이다.</summary>
        public string CaptionText => caption.gameObject.activeSelf ? captionText.text : null;

        /// <summary>지금 밝힌 구멍(캔버스 왼쪽 아래 기준). 밝힌 것이 없으면 크기가 0이다.</summary>
        public Rect Hole { get; private set; }

        /// <summary>두 번째 말풍선의 설명. 보이지 않으면 null이다.</summary>
        public string NoteText => note.gameObject.activeSelf ? noteText.text : null;

        /// <summary>두 번째 말풍선 자리(캔버스 왼쪽 아래 기준). 보이지 않으면 크기가 0이다.</summary>
        public Rect NoteRect { get; private set; }

        /// <summary>두 번째 말풍선이 가리키는 대상 자리(캔버스 왼쪽 아래 기준)</summary>
        public Rect NoteTargetRect { get; private set; }

        /// <summary>두 번째 말풍선 꼬리의 중심(캔버스 왼쪽 아래 기준)</summary>
        public Vector2 NoteTail { get; private set; }

        /// <summary>
        /// 밝힌 곳과 그 옆 설명은 그대로 두고, <paramref name="target"/> 옆에 같은 모양의 말풍선을 하나 더 띄운다.
        /// 녹음 중 불처럼 할 일을 알리는 동안 잠깐 더 알릴 곳에 쓴다. 대화 처리가 매 프레임 불러도 내용이 같으면 다시 그리지 않는다.
        /// </summary>
        public void ShowNote(RectTransform target, string text)
        {
            if (note.gameObject.activeSelf && noteTarget == target && noteText.text == text)
            {
                return;
            }

            noteTarget = target;
            noteText.text = text;
            note.gameObject.SetActive(true);
            // 말풍선 높이는 글 길이에 맞춰 바뀌므로 자리를 잡기 전에 크기를 다시 계산한다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(note);
            PlaceNote();
        }

        public void HideNote()
        {
            noteTarget = null;
            note.gameObject.SetActive(false);
            NoteRect = default;
            NoteTargetRect = default;
        }

        // 버튼은 레이아웃·말풍선 위치가 바뀌며 움직이므로 매 프레임 다시 맞춘다.
        private void LateUpdate()
        {
            if (targets.Count > 0)
            {
                PlaceSpotlight();
            }
            else if (markerTarget != null)
            {
                PlaceMarker();
            }
            else if (compassWalker != null && compassTarget != null)
            {
                PlaceCompass();
            }

            if (noteTarget != null)
            {
                PlaceNote();
            }
        }

        // 대상 옆에 두 번째 말풍선을 두고 꼬리를 대상 쪽 가장자리에 붙인다. 대상이 숨으면 말풍선도 숨긴다.
        private void PlaceNote()
        {
            bool isShown = noteTarget.gameObject.activeInHierarchy;
            note.gameObject.SetActive(isShown);
            if (!isShown)
            {
                return;
            }

            // 너비는 오른쪽에 남는 자리에 맞추고, 바뀌면 글 줄 수와 높이도 바뀌므로 크기를 다시 계산한다.
            Rect target = CanvasRectOf(noteTarget);
            Vector2 canvasSize = canvasRect.rect.size;
            float width = CoachMarkLayout.NoteWidth(
                target,
                canvasSize.x,
                BubbleGap,
                BubbleMargin,
                NoteMinWidth,
                NoteMaxWidth
            );
            if (!Mathf.Approximately(note.sizeDelta.x, width))
            {
                note.sizeDelta = new Vector2(width, note.sizeDelta.y);
                LayoutRebuilder.ForceRebuildLayoutImmediate(note);
            }

            CoachMarkLayout.BubblePose bubble = CoachMarkLayout.Note(
                target,
                note.rect.size,
                canvasSize,
                BubbleGap,
                BubbleMargin
            );
            note.anchoredPosition = bubble.Rect.position;
            noteTail.anchoredPosition = bubble.Tail - bubble.Rect.position;
            noteTail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            NoteRect = bubble.Rect;
            NoteTargetRect = target;
            NoteTail = bubble.Tail;
        }

        private void OnDestroy()
        {
            if (compassRoot != null)
            {
                Destroy(compassRoot.gameObject);
            }

            Destroy(roundedBorderSprite);
            Destroy(roundedCornerShadeSprite);
            Destroy(roundedBorderTexture);
            Destroy(roundedCornerShadeTexture);
        }

        // 원은 발밑에, 점과 화살표는 원 가장자리에서 대상 쪽으로 오가며 가리킨다.
        private void PlaceCompass()
        {
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * MarkerBobSpeed)) * CompassBobMeters;
            Vector3 feet = compassWalker.position + Vector3.up * CompassLiftMeters;
            CoachMarkLayout.CompassPose pose = CoachMarkLayout.Compass(
                feet,
                compassTarget.position,
                CompassPointerDistanceMeters + bob
            );
            CompassDirection = pose.Direction;
            compassRing.SetPositionAndRotation(feet, pose.Rotation);
            compassPointer.SetPositionAndRotation(
                pose.PointerPosition + Vector3.up * 0.002f,
                pose.Rotation
            );
            compassArrow.SetPositionAndRotation(
                pose.PointerPosition + Vector3.up * 0.004f,
                pose.Rotation
            );
            PlaceCompassScreenArrow(pose);
        }

        // 1인칭처럼 발밑 나침반이 화면에 없으면 화면 아래 가운데에 같은 방향을 가리키는 화살표 배지를 띄운다.
        private void PlaceCompassScreenArrow(CoachMarkLayout.CompassPose pose)
        {
            Camera camera = Camera.main;
            if (camera == null || IsOnScreen(camera.WorldToScreenPoint(pose.PointerPosition)))
            {
                marker.gameObject.SetActive(false);
                return;
            }

            float degrees = CoachMarkLayout.CompassScreenArrowDegrees(
                camera.transform.forward,
                pose.Direction
            );
            float radians = degrees * Mathf.Deg2Rad;
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * MarkerBobSpeed)) * MarkerBobPixels;
            marker.anchoredPosition =
                new Vector2(canvasRect.rect.width / 2f, CompassScreenArrowBottom)
                + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * bob;
            markerArrow.localRotation = Quaternion.Euler(0f, 0f, degrees);
            marker.gameObject.SetActive(true);
        }

        private static bool IsOnScreen(Vector3 screen)
        {
            return screen.z > 0f
                && screen.x >= 0f
                && screen.x <= Screen.width
                && screen.y >= 0f
                && screen.y <= Screen.height;
        }

        private void HideCompass()
        {
            if (compassWalker != null)
            {
                marker.gameObject.SetActive(false);
            }

            compassWalker = null;
            compassTarget = null;
            CompassDirection = Vector3.zero;
            if (compassRoot != null)
            {
                compassRoot.gameObject.SetActive(false);
            }
        }

        // 바닥에 눕힐 그림은 처음 가리킬 때 만든다. 다음 장소를 미리 불러오는 동안 다른 씬에 생기지 않게 이 씬으로 옮긴다.
        private void EnsureCompass()
        {
            if (compassRoot != null)
            {
                return;
            }

            compassRoot = new GameObject("TutorialCompass").transform;
            SceneManager.MoveGameObjectToScene(compassRoot.gameObject, gameObject.scene);
            Color color = marker.GetComponent<Image>().color;
            compassRing = GroundSprite(
                "Ring",
                compassDiscSprite,
                new Color(color.r, color.g, color.b, CompassRingAlpha),
                CompassRingMeters,
                0
            );
            compassPointer = GroundSprite(
                "Pointer",
                compassDiscSprite,
                color,
                CompassPointerMeters,
                1
            );
            compassArrow = GroundSprite(
                "Arrow",
                compassArrowSprite,
                Color.white,
                CompassArrowMeters,
                2
            );
        }

        private Transform GroundSprite(
            string name,
            Sprite sprite,
            Color color,
            float widthMeters,
            int order
        )
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(compassRoot, false);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.transform.localScale = Vector3.one * (widthMeters / sprite.bounds.size.x);
            return renderer.transform;
        }

        private void PlaceSpotlight()
        {
            targetRects.Clear();
            foreach (RectTransform target in targets)
            {
                if (target != null && target.gameObject.activeInHierarchy)
                {
                    targetRects.Add(CanvasRectOf(target));
                }
            }

            if (targetRects.Count == 0)
            {
                SetSpotlightVisible(false);
                return;
            }

            Vector2 canvasSize = canvasRect.rect.size;
            Rect hole = CoachMarkLayout.Hole(
                CoachMarkLayout.Union(targetRects),
                canvasSize,
                HolePadding
            );
            Hole = hole;
            Rect[] shadeRects = CoachMarkLayout.Shades(hole, canvasSize);
            for (int i = 0; i < shades.Length; i++)
            {
                Place(shades[i], shadeRects[i]);
            }

            Place((RectTransform)frame.transform, hole);
            frame.alpha = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * FramePulseSpeed);
            SetSpotlightVisible(true);
            if (caption.gameObject.activeSelf)
            {
                PlaceCaption(canvasSize, hole);
            }
        }

        // 밝힌 곳 옆에 말풍선을 두고, 꼬리를 밝힌 곳 쪽 가장자리에 붙인다. 꼬리는 마름모로 돌려 반만 밖으로 나오게 한다.
        private void PlaceCaption(Vector2 canvasSize, Rect hole)
        {
            // 넓은 스크롤 영역 옆에도 설명이 들어가도록 남은 폭에 맞춘다.
            float sideRoom = Mathf.Max(
                hole.xMin - BubbleGap - BubbleMargin,
                canvasSize.x - BubbleMargin - hole.xMax - BubbleGap
            );
            float width =
                sideRoom >= CaptionMinWidth
                    ? Mathf.Min(captionMaxWidth, sideRoom)
                    : captionMaxWidth;
            if (!Mathf.Approximately(caption.sizeDelta.x, width))
            {
                caption.sizeDelta = new Vector2(width, caption.sizeDelta.y);
                LayoutRebuilder.ForceRebuildLayoutImmediate(caption);
            }
            Rect keepClear =
                KeepClear != null && KeepClear.gameObject.activeInHierarchy
                    ? CanvasRectOf(KeepClear)
                    : default;
            CoachMarkLayout.BubblePose bubble = CoachMarkLayout.Bubble(
                hole,
                caption.rect.size,
                canvasSize,
                BubbleGap,
                BubbleMargin,
                keepClear
            );
            caption.anchoredPosition = bubble.Rect.position;
            CaptionRect = bubble.Rect;
            captionTail.anchoredPosition = bubble.Tail - bubble.Rect.position;
            captionTail.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private void PlaceMarker()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                marker.gameObject.SetActive(false);
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(
                markerTarget.position + Vector3.up * markerHeightMeters
            );
            Vector2 point = ToCanvas(screen);
            bool isShown = CoachMarkLayout.ShowsMarker(
                point,
                screen.z < 0f,
                canvasRect.rect.size,
                MarkerEdgeMargin,
                MarkerTopMargin
            );
            marker.gameObject.SetActive(isShown);
            if (!isShown)
            {
                return;
            }

            // 아래를 가리키며 위아래로 오르내려 눈에 띄게 한다.
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * MarkerBobSpeed)) * MarkerBobPixels;
            marker.anchoredPosition = point + Vector2.up * bob;
            markerArrow.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }

        // 다른 캔버스의 버튼 모서리(화면 픽셀)를 이 캔버스 왼쪽 아래 기준 좌표로 바꾼다.
        private Rect CanvasRectOf(RectTransform target)
        {
            target.GetWorldCorners(corners);
            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            Camera eventCamera =
                targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? targetCanvas.worldCamera
                    : null;
            Vector2 min = ToCanvas(
                RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0])
            );
            Vector2 max = ToCanvas(
                RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2])
            );
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Vector2 ToCanvas(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                null,
                out Vector2 local
            );
            return local + canvasRect.rect.size * canvasRect.pivot;
        }

        // 밝힐 버튼이 있는 캔버스 바로 위에 그려 버튼 둘레만 덮고, 그보다 위의 안내 카드는 가리지 않는다.
        private void DrawAboveTargets()
        {
            int order = 0;
            foreach (RectTransform target in targets)
            {
                Canvas targetCanvas = target != null ? target.GetComponentInParent<Canvas>() : null;
                if (targetCanvas != null)
                {
                    order = Mathf.Max(order, targetCanvas.rootCanvas.sortingOrder);
                }
            }

            canvas.sortingOrder = order + 1;
        }

        private void SetSpotlightVisible(bool visible)
        {
            foreach (RectTransform shade in shades)
            {
                shade.gameObject.SetActive(visible);
            }

            frame.gameObject.SetActive(visible);
            if (!visible)
            {
                caption.gameObject.SetActive(false);
                Hole = default;
                CaptionRect = default;
            }
        }

        private static void Place(RectTransform rect, Rect area)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = area.position;
            rect.sizeDelta = area.size;
        }
    }
}
