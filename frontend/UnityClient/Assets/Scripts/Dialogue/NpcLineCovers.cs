using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    /// <summary>
    /// 자막을 끈 NPC 대사를 단어마다 둥근 회색 막대로 덮는다. 대사 글자는 투명하게 자리만 차지하므로(<see cref="DialogueLinesText"/>)
    /// 막대 사이 간격으로 띄어쓰기와 줄바꿈만 보이고 글자는 보이지 않는다.
    /// </summary>
    public sealed class NpcLineCovers
    {
        public const string CoverName = "HiddenWord";

        private static readonly Color CoverColor = new Color32(107, 107, 118, 255);

        // 막대 위아래를 글꼴 줄 높이보다 조금 줄여 위아래 줄의 막대가 붙어 보이지 않게 한다.
        private const float VerticalInset = .12f;

        // 둥근 모서리 그림의 크기와 모서리 반지름(px). 9칸 나누기로 단어 길이만큼 늘린다.
        private const int BarTextureSize = 24;
        private const int CornerRadius = 8;

        private static Sprite roundedBar;

        private readonly TMP_Text text;
        private readonly List<Image> covers = new List<Image>();

        public NpcLineCovers(TMP_Text text)
        {
            this.text = text;
        }

        /// <summary>
        /// 지금 대사 글자 배치에 맞춰 막대를 다시 놓는다. 대사나 대사 칸 폭이 바뀐 뒤 부른다.
        /// 막대는 대사 글자의 자식이라 글자를 꺼도 남으므로, 대답 로딩처럼 대사 글자를 꺼 둔 동안은 막대도 감춘다.
        /// </summary>
        public void Redraw()
        {
            var words = new List<Rect>();
            if (text.enabled)
            {
                text.ForceMeshUpdate();
                words = HiddenWordAreas(text.textInfo);
            }

            for (int i = 0; i < words.Count; i++)
                Place(Cover(i), words[i]);
            for (int i = words.Count; i < covers.Count; i++)
                covers[i].gameObject.SetActive(false);
        }

        // 가린 대사 링크 안의 글자를 띄어쓰기와 줄바꿈으로 나눠, 단어마다 차지한 영역을 대사 글자 기준 좌표로 구한다.
        private static List<Rect> HiddenWordAreas(TMP_TextInfo info)
        {
            var words = new List<Rect>();
            for (int link = 0; link < info.linkCount; link++)
            {
                TMP_LinkInfo hidden = info.linkInfo[link];
                if (hidden.GetLinkID() != DialogueLinesText.HiddenLineLink)
                    continue;

                Rect? word = null;
                int wordLine = -1;
                int end = hidden.linkTextfirstCharacterIndex + hidden.linkTextLength;
                for (int i = hidden.linkTextfirstCharacterIndex; i < end; i++)
                {
                    TMP_CharacterInfo character = info.characterInfo[i];
                    bool isGap = char.IsWhiteSpace(character.character) || !character.isVisible;
                    if (word != null && (isGap || character.lineNumber != wordLine))
                    {
                        words.Add(word.Value);
                        word = null;
                    }

                    if (isGap)
                        continue;
                    Rect letter = LetterArea(character);
                    word = word == null ? letter : Union(word.Value, letter);
                    wordLine = character.lineNumber;
                }

                if (word != null)
                    words.Add(word.Value);
            }

            return words;
        }

        // 글자 모양과 상관없이 같은 줄의 막대 높이가 같도록 글꼴의 줄 높이(ascender~descender)를 쓴다.
        private static Rect LetterArea(TMP_CharacterInfo character)
        {
            float inset = (character.ascender - character.descender) * VerticalInset;
            return Rect.MinMaxRect(
                character.bottomLeft.x,
                character.descender + inset,
                character.topRight.x,
                character.ascender - inset
            );
        }

        private static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax)
            );

        private Image Cover(int index)
        {
            if (index < covers.Count)
                return covers[index];

            var cover = new GameObject(CoverName, typeof(RectTransform), typeof(Image));
            cover.transform.SetParent(text.transform, false);
            var image = cover.GetComponent<Image>();
            image.sprite = RoundedBar();
            image.type = Image.Type.Sliced;
            image.color = CoverColor;
            // 막대가 대사 칸의 단어 클릭과 스크롤을 막지 않게 한다.
            image.raycastTarget = false;
            covers.Add(image);
            return image;
        }

        private static void Place(Image cover, Rect area)
        {
            cover.gameObject.SetActive(true);
            RectTransform rect = cover.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = area.size;
            rect.localPosition = area.center;
        }

        // 그림 파일을 따로 두지 않도록 둥근 모서리 막대를 한 번만 만들어 함께 쓴다.
        private static Sprite RoundedBar()
        {
            if (roundedBar != null)
                return roundedBar;

            var texture = new Texture2D(BarTextureSize, BarTextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            float inner = BarTextureSize - CornerRadius;
            for (int y = 0; y < BarTextureSize; y++)
            {
                for (int x = 0; x < BarTextureSize; x++)
                {
                    // 가장 가까운 모서리 원 중심에서의 거리로 바깥 1px만 부드럽게 깎는다.
                    var pixel = new Vector2(x + .5f, y + .5f);
                    var corner = new Vector2(
                        Mathf.Clamp(pixel.x, CornerRadius, inner),
                        Mathf.Clamp(pixel.y, CornerRadius, inner)
                    );
                    float alpha = Mathf.Clamp01(
                        CornerRadius - Vector2.Distance(pixel, corner) + .5f
                    );
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }

            texture.Apply();
            roundedBar = Sprite.Create(
                texture,
                new Rect(0, 0, BarTextureSize, BarTextureSize),
                new Vector2(.5f, .5f),
                100,
                0,
                SpriteMeshType.FullRect,
                Vector4.one * CornerRadius
            );
            roundedBar.hideFlags = HideFlags.DontSave;
            return roundedBar;
        }
    }
}
