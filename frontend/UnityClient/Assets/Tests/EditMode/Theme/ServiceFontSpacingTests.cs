using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.Theme.Tests
{
    /// <summary>
    /// 모든 화면이 함께 쓰는 서비스 글꼴로 한국어 문장을 여러 줄 쓸 때 줄끼리 붙지 않고, 단어 중간에서 줄이 바뀌지 않는지 확인한다.
    /// </summary>
    public class ServiceFontSpacingTests
    {
        private const string ServiceFontPath = "Assets/Art/UI/Fonts/NanumSquareRound SDF.asset";
        private const string Sentence =
            "체크인까지 시간이 남았어요. 큰 캐리어는 호텔에 맡기고 잠시 쉬러 가려 해요.";

        private GameObject canvasObject;

        [TearDown]
        public void DestroyCanvas() => Object.DestroyImmediate(canvasObject);

        // 글꼴 줄 높이가 글자 크기의 1.13배라 온라인 화면에서 한글 줄이 맞닿아 빽빽해 보였다.
        [Test]
        public void ServiceFont_LeavesRoomBetweenLines()
        {
            FaceInfoRatio(out float ratio);

            Assert.That(ratio, Is.GreaterThanOrEqualTo(1.35f));
        }

        // '맡/기고'처럼 단어 중간에서 줄이 바뀌면 읽기 어렵다.
        [Test]
        public void KoreanSentence_WrapsBetweenWords()
        {
            TMP_Text text = Write(Sentence, 260);

            Assert.That(text.textInfo.lineCount, Is.GreaterThan(1));
            for (int i = 0; i < text.textInfo.lineCount - 1; i++)
            {
                int last = text.textInfo.lineInfo[i].lastCharacterIndex;
                Assert.That(
                    Sentence[last] == ' ' || Sentence[last + 1] == ' ',
                    Is.True,
                    $"{i + 1}번째 줄이 단어 중간에서 끝났습니다: {Sentence.Substring(0, last + 1)}"
                );
            }
        }

        private static void FaceInfoRatio(out float ratio)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ServiceFontPath);
            ratio = font.faceInfo.lineHeight / font.faceInfo.pointSize;
        }

        private TMP_Text Write(string sentence, float width)
        {
            canvasObject = new GameObject("Canvas", typeof(Canvas));
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(canvasObject.transform, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ServiceFontPath);
            text.fontSize = 24;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.rectTransform.sizeDelta = new Vector2(width, 400);
            text.text = sentence;
            text.ForceMeshUpdate();
            return text;
        }
    }
}
