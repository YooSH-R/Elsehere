using A305.EditorTools.Common;
using A305.Mission;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Board = A305.EditorTools.Mission.QuestBoardBuilder;
using Ui = A305.EditorTools.Learning.FeedbackScreenBuilder;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 퀘스트를 끝내면 띄우는 '퀘스트 완료!' 팡파레(시안 f1)를 첫날 미션 프리팹의 Content/QuestComplete로 만든다. 자리는 시안 px × 1.2이고,
    /// 그림은 시안을 렌더링한 Art/UI/Quest 조각이다. 카드는 테마 유리(Panel)이고, 오른쪽 위 시계·코인 칩은 어둠 위에 보이게 팡파레보다 위에 둔다.
    /// [다음]은 FirstDayMissionView.ShowNextQuest에 연결한다.
    /// </summary>
    public static class QuestCompleteBuilder
    {
        private const string MenuPath = "Tools/A305/Mission/Apply Quest Complete";
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private const string SoundPath = "Assets/Audio/UI/QuestComplete.wav";
        private const string CoinPath = "Assets/Art/UI/Hud/Coin.png";
        private const float Px = Ui.Px;

        // 시안 f1 카드 700×500(모서리 44)은 화면 가운데에서 28 아래, 빛과 빛살(760·1100)은 20 아래가 가운데다.
        private const float CardWidth = 700;
        private const float CardHeight = 500;

        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color Muted = new Color32(125, 110, 91, 255);
        private static readonly Color RewardInk = new Color32(194, 122, 0, 255);
        private static readonly Color RewardUnit = new Color32(154, 106, 18, 255);

        [MenuItem(MenuPath)]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(PrefabPath, Apply);
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject root)
        {
            var mission = root.GetComponent<FirstDayMissionView>();
            Transform content = root.transform.Find("Content");
            Transform old = content.Find("QuestComplete");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            RectTransform complete = Ui.Rect("QuestComplete", content);
            Ui.Fill(complete);
            complete.SetSiblingIndex(content.Find("Status").GetSiblingIndex());
            var view = complete.gameObject.AddComponent<QuestCompleteView>();
            var fields = new SerializedObject(view);
            var group = complete.gameObject.AddComponent<CanvasGroup>();

            // 가장자리가 짙은 어둠(누르면 아무 일도 없이 뒤를 막는다), 카드 뒤 빛, 천천히 도는 빛살
            RectTransform dim = Ui.Rect("Dim", complete);
            Ui.Fill(dim);
            Ui.Picture(dim.gameObject, Board.Art("QuestCompleteDim")).raycastTarget = true;
            Centred(
                complete,
                "Glow",
                new Vector2(0, -20),
                new Vector2(760, 760),
                "QuestCompleteGlow"
            );
            RectTransform rays = Centred(
                complete,
                "Rays",
                new Vector2(0, -20),
                new Vector2(1100, 1100),
                "QuestCompleteRays"
            );

            // 색종이 견본(네모·동그라미 그림, 색은 실행 중에 입힌다)
            RectTransform confetti = Ui.Rect("Confetti", complete);
            Ui.Fill(confetti);
            Image piece = Centred(
                    confetti,
                    "Piece",
                    Vector2.zero,
                    new Vector2(16, 9),
                    "QuestConfetti"
                )
                .GetComponent<Image>();
            piece.gameObject.SetActive(false);

            RectTransform card = Ui.Rect("Card", complete);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.anchoredPosition = new Vector2(0, -28) * Px;
            card.sizeDelta = new Vector2(CardWidth, CardHeight) * Px;
            Board.Glass(card, 44);
            BuildCard(fields, card);

            // 코인 칩으로 날아가는 코인(30). 코인 그림은 42에 그림자 자리(사방 12, 아래 18)가 있다.
            RectTransform coin = Ui.Rect("FlyingCoin", complete);
            coin.anchorMin = coin.anchorMax = coin.pivot = new Vector2(.5f, .5f);
            coin.sizeDelta = new Vector2(30, 30) * Px;
            RectTransform coinArt = Ui.Rect("Art", coin);
            const float coinScale = 30f / 42f;
            Ui.Spread(coinArt, 12 * coinScale, 12 * coinScale, 12 * coinScale, 18 * coinScale);
            Ui.Picture(coinArt.gameObject, AssetDatabase.LoadAssetAtPath<Sprite>(CoinPath));
            coin.gameObject.SetActive(false);

            var sound = complete.gameObject.AddComponent<AudioSource>();
            sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
            sound.playOnAwake = false;
            sound.spatialBlend = 0;
            sound.volume = .7f;

            fields.FindProperty("group").objectReferenceValue = group;
            fields.FindProperty("rays").objectReferenceValue = rays;
            fields.FindProperty("card").objectReferenceValue = card;
            fields.FindProperty("confetti").objectReferenceValue = piece;
            fields.FindProperty("confettiDot").objectReferenceValue = Board.Art("QuestConfettiDot");
            fields.FindProperty("flyingCoin").objectReferenceValue = coin;
            fields.FindProperty("fanfare").objectReferenceValue = sound;
            fields.ApplyModifiedPropertiesWithoutUndo();
            complete.gameObject.SetActive(false);
            BuilderTools.SetReference(mission, "questComplete", view);
        }

        // 카드(시안 .done-card): 리본·퀘스트 아이콘(76)·제목(Jua 32)·도장 수(17)·작은 도장 줄·보상 알약·큰 '완료' 도장·[다음]
        private static void BuildCard(SerializedObject fields, RectTransform card)
        {
            RectTransform ribbon = Board.Centred(
                card,
                "Ribbon",
                new Vector2(350, 9),
                new Vector2(465.03f, 84),
                (48, 12, 48, 56),
                Board.Art("QuestCompleteRibbon")
            );
            const float iconScale = 76f / 52f;
            Image icon = Board.Picture(
                card,
                "Icon",
                312,
                87,
                76,
                76,
                (14 * iconScale, 8 * iconScale, 14 * iconScale, 22 * iconScale),
                Board.Art("QuestIconHome")
            );
            TMP_Text title = Board.Text(
                card,
                "QuestTitle",
                0,
                179,
                CardWidth,
                40,
                Ui.DisplayFontPath,
                32,
                Ink
            );
            title.alignment = TextAlignmentOptions.Midline;
            TMP_Text subtitle = Board.Text(
                card,
                "Subtitle",
                0,
                227,
                CardWidth,
                20,
                Ui.BodyFontPath,
                17,
                Muted
            );
            subtitle.alignment = TextAlignmentOptions.Midline;

            // 작은 도장(78, 사이 14). 도장 그림은 178에 사방 6 여백이라 칸을 그만큼 키우고 사이를 줄인다.
            const float stampBox = 78 * 190f / 178f;
            RectTransform stamps = Ui.Rect("MiniStamps", card);
            Ui.TopLeft(stamps, 0, 265, CardWidth, 82);
            var row = stamps.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = (14 - (stampBox - 78)) * Px;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            RectTransform stamp = Ui.Rect("MiniStamp", stamps);
            stamp.sizeDelta = new Vector2(stampBox, stampBox) * Px;
            Ui.Picture(stamp.gameObject, Board.Art("QuestStampHotel")).preserveAspect = true;
            stamp.gameObject.SetActive(false);

            // 보상 알약(코인 그림 포함, 숫자와 단위는 글자)
            Board.Picture(
                card,
                "RewardPill",
                218.3f,
                369,
                263.39f,
                84,
                (24, 10, 24, 46),
                Board.Art("QuestCompleteReward")
            );
            RectTransform rewardCoin = Ui.Rect("RewardCoin", card);
            Ui.TopLeft(rewardCoin, 252.3f, 383, 56, 56);
            TMP_Text reward = Board.Text(
                card,
                "Reward",
                322.3f,
                379,
                100,
                64,
                Ui.DisplayFontPath,
                48,
                RewardInk
            );
            reward.margin = new Vector4(0, 4 * Px, 0, 0);
            TMP_Text unit = Board.Text(
                card,
                "Unit",
                411.56f,
                392,
                40,
                38,
                Ui.DisplayFontPath,
                22,
                RewardUnit
            );
            unit.text = "코인";
            unit.margin = new Vector4(0, 4 * Px, 0, 0);

            // 큰 '완료' 도장(190×194, 사방 16 여백)과 찍힐 때 튀는 잉크 자국
            RectTransform bigStamp = Board.Centred(
                card,
                "BigStamp",
                new Vector2(565 + 95, 25 + 97),
                new Vector2(190, 194),
                (16, 16, 16, 16),
                Board.Art("QuestCompleteStamp")
            );
            var bigStampGroup = bigStamp.gameObject.AddComponent<CanvasGroup>();
            bigStampGroup.blocksRaycasts = false;
            RectTransform impacts = Ui.Rect("Impacts", card);
            Ui.Fill(impacts);
            var impactGroup = impacts.gameObject.AddComponent<CanvasGroup>();
            impactGroup.blocksRaycasts = false;
            impactGroup.alpha = 0;
            foreach (
                var (x, y, angle) in new[]
                {
                    (560f, 30f, -20f),
                    (585f, 8f, -45f),
                    (690f, 18f, 30f),
                    (700f, 54f, 5f),
                    (552f, 150f, 20f),
                }
            )
            {
                RectTransform impact = Board.Centred(
                    impacts,
                    "Impact",
                    new Vector2(x + 6, y + 2.5f),
                    new Vector2(14, 7),
                    (0, 0, 0, 0),
                    Board.Art("QuestCompleteImpact")
                );
                impact.localRotation = Quaternion.Euler(0, 0, -angle);
            }

            Button next = Board.ArtButton(
                card,
                "Next",
                304.19f,
                469,
                91.62f,
                60,
                (22, 12, 22, 40),
                Board.Art("QuestCompleteNext")
            );
            var mission = card.GetComponentInParent<FirstDayMissionView>(true);
            Board.Listen(next, mission.ShowNextQuest);

            fields.FindProperty("ribbon").objectReferenceValue = ribbon;
            fields.FindProperty("icon").objectReferenceValue = icon;
            fields.FindProperty("title").objectReferenceValue = title;
            fields.FindProperty("subtitle").objectReferenceValue = subtitle;
            fields.FindProperty("miniStamp").objectReferenceValue = stamp.GetComponent<Image>();
            fields.FindProperty("reward").objectReferenceValue = reward;
            fields.FindProperty("rewardCoin").objectReferenceValue = rewardCoin;
            fields.FindProperty("bigStamp").objectReferenceValue = bigStampGroup;
            fields.FindProperty("impacts").objectReferenceValue = impactGroup;
        }

        // 화면 가운데에서 (x, y) 시안 px만큼 옮긴 자리를 가운데로 하는 그림
        private static RectTransform Centred(
            Transform parent,
            string name,
            Vector2 offset,
            Vector2 size,
            string art
        )
        {
            RectTransform rect = Ui.Rect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = offset * Px;
            rect.sizeDelta = size * Px;
            Ui.Picture(rect.gameObject, Board.Art(art));
            return rect;
        }
    }
}
