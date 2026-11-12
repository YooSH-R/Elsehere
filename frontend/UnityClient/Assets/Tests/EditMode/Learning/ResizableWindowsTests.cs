using System.Reflection;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Learning.Tests
{
    /// <summary>
    /// 크기를 바꾼 학습 창의 칸 배치. 목록·상세는 창과 함께 늘고 줄며, 탭·오른쪽 버튼은 오른쪽에, 쪽 넘김·상태 줄은 아래에 붙는다.
    /// 화면이 다시 배치해도 사용자가 바꾼 크기는 그대로다.
    /// </summary>
    public class ResizableWindowsTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string VocabularyPrefabPath =
            "Assets/Prefabs/Learning/VocabularyScreen.prefab";
        private const string FeedbackPrefabPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";

        private GameObject root;

        [TearDown]
        public void Clean() => Object.DestroyImmediate(root);

        [Test]
        public void ConversationHistory_NarrowerWindow_KeepsTabsAndRowButtonsOnTheRight()
        {
            RectTransform window = Open<ConversationHistoryView>(HudPrefabPath).PanelRect;
            float width = window.rect.width;
            float tabs = RightGap(window, "Tabs");
            float openButton = RightGap(window, "Rows/Row0/OpenButton");
            float rows = Item(window, "Rows").rect.width;

            Resize(window, new Vector2Int(1, 0), new Vector2(-200, 0));

            Assert.That(window.rect.width, Is.EqualTo(width - 200).Within(.01f));
            Assert.That(RightGap(window, "Tabs"), Is.EqualTo(tabs).Within(.01f));
            Assert.That(
                RightGap(window, "Rows/Row0/OpenButton"),
                Is.EqualTo(openButton).Within(.01f)
            );
            Assert.That(Item(window, "Rows").rect.width, Is.EqualTo(rows - 200).Within(.01f));
        }

        // 한 쪽에 고정된 줄 4개가 들어가야 하므로 원래 높이보다 줄이지 않는다.
        [Test]
        public void ConversationHistory_CannotBeMadeShorterThanItsRows()
        {
            RectTransform window = Open<ConversationHistoryView>(HudPrefabPath).PanelRect;
            float height = window.rect.height;

            Resize(window, new Vector2Int(0, -1), new Vector2(0, -100));

            Assert.That(window.rect.height, Is.EqualTo(height).Within(.01f));
        }

        [Test]
        public void Neighbors_NarrowerWindow_KeepsAffinityOnTheRightAndThePagerCentered()
        {
            NeighborsView view = Open<NeighborsView>(HudPrefabPath);
            RectTransform window = (RectTransform)PrivateField<GameObject>(view, "panel").transform;
            window.gameObject.SetActive(true);
            float affinity = RightGap(window, "Rows/Row0/Affinity");
            float pager = CenterOffset(window, "Pager");
            float facts = Item(window, "Card/Facts").rect.width;

            Resize(window, new Vector2Int(-1, 0), new Vector2(-200, 0));

            Assert.That(RightGap(window, "Rows/Row0/Affinity"), Is.EqualTo(affinity).Within(.01f));
            Assert.That(CenterOffset(window, "Pager"), Is.EqualTo(pager).Within(.01f));
            Assert.That(
                Item(window, "Card/Facts").rect.width,
                Is.EqualTo(facts - 200).Within(.01f)
            );
        }

        [Test]
        public void Vocabulary_BiggerWindow_GrowsTheListAndDetailsAndKeepsButtonsInPlace()
        {
            RectTransform window = Open<VocabularyView>(VocabularyPrefabPath).PanelRect;
            UseLargeScreen(window);
            window.gameObject.SetActive(true);
            float details = Item(window, "Content/Details").rect.width;
            float entries = Item(window, "Content/Entries").rect.height;
            float delete = RightGap(window, "Content/DeleteButton");
            float previous = BottomGap(window, "Content/PreviousButton");
            float status = BottomGap(window, "Status");

            Resize(window, new Vector2Int(1, -1), new Vector2(150, 100));

            Assert.That(
                Item(window, "Content/Details").rect.width,
                Is.EqualTo(details + 150).Within(.01f)
            );
            Assert.That(
                Item(window, "Content/Entries").rect.height,
                Is.EqualTo(entries + 100).Within(.01f)
            );
            Assert.That(RightGap(window, "Content/DeleteButton"), Is.EqualTo(delete).Within(.01f));
            Assert.That(
                BottomGap(window, "Content/PreviousButton"),
                Is.EqualTo(previous).Within(.01f)
            );
            Assert.That(BottomGap(window, "Status"), Is.EqualTo(status).Within(.01f));
        }

        // A~Z 글자와 상세(단어·뜻·예시 문장·메모)가 고정 높이로 쌓여 있어 원래 높이보다 줄이지 않는다.
        [Test]
        public void Vocabulary_CannotBeMadeShorterThanItsLetterIndexAndDetails()
        {
            RectTransform window = Open<VocabularyView>(VocabularyPrefabPath).PanelRect;
            float height = window.rect.height;

            Resize(window, new Vector2Int(0, -1), new Vector2(0, -100));

            Assert.That(window.rect.height, Is.EqualTo(height).Within(.01f));
        }

        // 제목·부제(예: "대화 피드백 Diego와 나눈 대화")와 오른쪽 탭이 원래 너비에서 거의 붙어 있어 더 좁히지 않는다.
        [Test]
        public void Feedback_CannotBeMadeNarrowerThanItsTitleAndTabs()
        {
            FeedbackView view = Open<FeedbackView>(FeedbackPrefabPath);
            RectTransform window = view.PanelRect;
            UseLargeScreen(window);
            window.gameObject.SetActive(true);
            view.UpdateLayout();
            float width = window.rect.width;

            Resize(window, new Vector2Int(-1, 0), new Vector2(-100, 0));

            Assert.That(window.rect.width, Is.EqualTo(width).Within(.01f));
        }

        // 손대지 않은 피드백 창은 화면에 맞춘 크기를 따르고, 사용자가 바꾼 크기는 다시 맞춰도 그대로 둔다.
        [Test]
        public void Feedback_FollowsTheScreenUntilTheUserResizesIt()
        {
            FeedbackView view = Open<FeedbackView>(FeedbackPrefabPath);
            RectTransform window = view.PanelRect;
            Canvas canvas = window.GetComponentInParent<Canvas>().rootCanvas;
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(900, 700);
            window.gameObject.SetActive(true);

            view.UpdateLayout();
            // 화면 여백(24) 안: 900×700 - 48
            Assert.That(window.rect.size, Is.EqualTo(new Vector2(852, 652)));

            Resize(window, new Vector2Int(0, -1), new Vector2(0, -100));
            view.UpdateLayout();

            Assert.That(window.rect.size, Is.EqualTo(new Vector2(852, 552)));
        }

        // 창을 키울 자리가 있도록 화면(가장 바깥 캔버스)을 크게 둔다.
        private static void UseLargeScreen(RectTransform window)
        {
            Canvas canvas = window.GetComponentInParent<Canvas>().rootCanvas;
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(2400, 1600);
        }

        private T Open<T>(string prefabPath)
            where T : MonoBehaviour
        {
            root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            T view = root.GetComponentInChildren<T>(true);
            typeof(T)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            return view;
        }

        private static T PrivateField<T>(object owner, string name) =>
            (T)
                owner
                    .GetType()
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(owner);

        // change: 창 단위로 늘릴(+)·줄일(-) 양. side 쪽 변·모서리를 끈다.
        private static void Resize(RectTransform window, Vector2Int side, Vector2 change)
        {
            var frame = window.GetComponent<WindowFrame>();
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame.BeginResize(side), Is.True);
            float scale = window.localScale.x;
            frame.DragResize(new Vector2(side.x * change.x, side.y * change.y) * scale);
        }

        private static RectTransform Item(RectTransform window, string path)
        {
            var item = (RectTransform)window.Find(path);
            Assert.That(item, Is.Not.Null, path);
            return item;
        }

        // 창 안에서 칸이 차지한 자리(창 단위)
        private static Rect PlaceIn(RectTransform window, string path)
        {
            var corners = new Vector3[4];
            Item(window, path).GetWorldCorners(corners);
            Vector2 min = window.InverseTransformPoint(corners[0]);
            Vector2 max = window.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static float RightGap(RectTransform window, string path) =>
            window.rect.xMax - PlaceIn(window, path).xMax;

        private static float BottomGap(RectTransform window, string path) =>
            PlaceIn(window, path).yMin - window.rect.yMin;

        private static float CenterOffset(RectTransform window, string path) =>
            PlaceIn(window, path).center.x - window.rect.center.x;
    }
}
