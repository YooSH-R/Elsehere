using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.EditorTools.Common;
using A305.Learning;
using A305.Player;
using A305.Theme;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace A305.EditorTools.Learning
{
    /// <summary>
    /// 고정 데이터로 실제 단어장 프리팹의 탭·장소 칩을 렌더링한다(S15P31A305-748). 서버를 부르거나 씬을 저장하지 않는다.
    /// </summary>
    public static class VocabularyPlaceFiltersPreview
    {
        [MenuItem("Tools/A305/Learning/Capture Vocabulary Place Filters")]
        public static void Capture()
        {
            string output =
                Environment.GetEnvironmentVariable("VOCABULARY_PREVIEW_OUTPUT")
                ?? Path.GetFullPath("../../artifacts/vocabulary-place-filters");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/HomeScene.unity");
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            Transform player = BuilderTools.FindSingleInScene<PlayerMover>(scene).transform;
            Quaternion rotation = Quaternion.Euler(16, player.eulerAngles.y, 0);
            camera.transform.SetPositionAndRotation(
                player.position + Vector3.up * .97f + rotation * new Vector3(0, 0, -3.4f),
                rotation
            );
            foreach (Canvas other in BuilderTools.FindAllInScene<Canvas>(scene))
                other.gameObject.SetActive(false);
            var view = BuilderTools.FindSingleInScene<VocabularyView>(scene);
            view.gameObject.SetActive(true);
            Canvas canvas = view.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = .5f;
            // Editor에서는 Awake가 돌지 않으므로 버튼 연결과 줄 원본 숨기기를 직접 부른다.
            typeof(VocabularyView)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            var book = new VocabularyBook(new PreviewApi());
            book.LoadAsync().GetAwaiter().GetResult();

            foreach (
                UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
            )
            {
                UiThemeSelection.Shared.Select(theme);
                foreach (ThemedElement element in view.GetComponentsInChildren<ThemedElement>(true))
                    element.ApplyCurrent();
                Render(
                    camera,
                    1600,
                    1000,
                    () => view.Show(book),
                    Path.Combine(output, "conversation-all-" + theme + ".png")
                );
            }
            UiThemeSelection.Shared.Select(UiThemeId.Jelly);
            foreach (ThemedElement element in view.GetComponentsInChildren<ThemedElement>(true))
                element.ApplyCurrent();
            Render(
                camera,
                1600,
                1000,
                () =>
                {
                    view.Show(book);
                    Click(view, "Panel/PlaceChips/Food");
                    Click(view, "Panel/Content/Entries/Rows/Entry1");
                },
                Path.Combine(output, "conversation-food.png")
            );
            Render(
                camera,
                1600,
                1000,
                () =>
                {
                    view.Show(book);
                    Click(view, "Panel/Tabs/ObjectsTab");
                },
                Path.Combine(output, "objects.png")
            );
            Render(
                camera,
                540,
                960,
                () => view.Show(book),
                Path.Combine(output, "conversation-mobile.png")
            );
            Debug.Log("단어장 장소 칩 캡처: " + output);
        }

        private static void Click(VocabularyView view, string path) =>
            view.transform.Find(path).GetComponent<Button>().onClick.Invoke();

        private static void Render(
            Camera camera,
            int width,
            int height,
            Action arrange,
            string path
        )
        {
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            arrange();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }

        // 대화 표현 9개(음식 4·쇼핑 2·생활 서비스 2·일상 1, 안전 없음)와 사물 단어 3개
        private sealed class PreviewApi : IVocabularyApi
        {
            private static readonly (
                string Text,
                string Meaning,
                string Source,
                string Npc,
                string Place,
                string Group,
                string Sentence,
                string SentenceMeaning
            )[] Saved =
            {
                (
                    "For here or to go?",
                    "여기서 드세요, 가져가세요?",
                    "FEEDBACK",
                    "Mia",
                    "카페",
                    "FOOD",
                    "For here or to go? We have oat milk today.",
                    "여기서 드세요, 가져가세요? 오늘은 귀리 우유도 있어요."
                ),
                (
                    "oat milk",
                    "귀리 우유",
                    "NPC_LINE",
                    "Zoe",
                    "카페",
                    "FOOD",
                    "Would you like oat milk instead?",
                    "대신 귀리 우유로 드릴까요?"
                ),
                (
                    "Do you take cards?",
                    "카드 되나요?",
                    "FEEDBACK",
                    "Alex",
                    "드럭스토어",
                    "SHOPPING",
                    "We take cash and cards.",
                    "현금과 카드 다 받아요."
                ),
                (
                    "a refill",
                    "리필",
                    "NPC_LINE",
                    "Rosa",
                    "식당",
                    "FOOD",
                    "Can I get you a refill on that coffee?",
                    "커피 리필해 드릴까요?"
                ),
                (
                    "I'd like to send this package.",
                    "이 소포를 보내고 싶어요.",
                    "FEEDBACK",
                    "Linda",
                    "우체국",
                    "SERVICES",
                    null,
                    null
                ),
                (
                    "a loaf of bread",
                    "빵 한 덩이",
                    "NPC_LINE",
                    "Grace",
                    "빵집",
                    "FOOD",
                    "This loaf of bread just came out of the oven.",
                    "이 빵은 방금 오븐에서 나왔어요."
                ),
                (
                    "check in",
                    "체크인하다",
                    "NPC_LINE",
                    "Jake",
                    "호텔",
                    "SERVICES",
                    "Are you here to check in?",
                    "체크인하러 오셨어요?"
                ),
                (
                    "bouquet",
                    "꽃다발",
                    "NPC_LINE",
                    "Lily",
                    "꽃집",
                    "SHOPPING",
                    "I can wrap a bouquet for you.",
                    "꽃다발로 포장해 드릴게요."
                ),
                (
                    "What do you do for fun?",
                    "쉴 때 뭐 해요?",
                    "NPC_LINE",
                    "Zoe",
                    null,
                    "DAILY",
                    "I'm off today. What do you do for fun?",
                    "오늘은 쉬는 날이에요. 쉴 때 뭐 해요?"
                ),
                (
                    "sofa",
                    "소파",
                    "OBJECT",
                    null,
                    null,
                    null,
                    "He is sitting on the sofa.",
                    "그는 소파에 앉아 있어요."
                ),
                (
                    "bookshelf",
                    "책장",
                    "OBJECT",
                    null,
                    null,
                    null,
                    "The bookshelf is full of books.",
                    "책장에 책이 가득해요."
                ),
                (
                    "blanket",
                    "담요",
                    "OBJECT",
                    null,
                    null,
                    null,
                    "This blanket is warm.",
                    "이 담요는 따뜻해요."
                ),
            };

            public Task<VocabPage> GetPageAsync(string cursor)
            {
                List<VocabItem> items = Saved
                    .Select(
                        (word, i) =>
                            new VocabItem
                            {
                                UserVocabId = i + 1,
                                Vocab = new VocabWord
                                {
                                    Text = word.Text,
                                    MeaningKo = word.Meaning,
                                },
                                Context =
                                    word.Sentence == null
                                        ? null
                                        : new VocabContext
                                        {
                                            Text = word.Sentence,
                                            MeaningKo = word.SentenceMeaning,
                                        },
                                Source = new VocabSource
                                {
                                    SourceType = word.Source,
                                    NpcName = word.Npc,
                                    PlaceName = word.Place,
                                    PlaceGroup = word.Group,
                                },
                            }
                    )
                    .ToList();
                return Task.FromResult(new VocabPage { Items = items });
            }

            public Task<VocabItem> SaveNpcLineWordAsync(long turnId, string text) =>
                throw new NotSupportedException();

            public Task<VocabItem> SaveFeedbackWordAsync(long conversationId, string text) =>
                throw new NotSupportedException();

            public Task<VocabItem> UpdateMemoAsync(long id, string memo) =>
                throw new NotSupportedException();

            public Task<VocabItem> UpdateMemorizedAsync(long id, bool memorized) =>
                throw new NotSupportedException();

            public Task DeleteAsync(long id) => throw new NotSupportedException();
        }
    }
}
