using A305.EditorTools.Common;
using A305.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Theme
{
    /// <summary>
    /// HUD 알림 카드의 자리. 오른쪽 위 메뉴 칸에 두면 상단 아이콘 줄에 가려지므로, 화면 위 가운데의 상단 줄 아래에 크게 띄운다.
    /// 열려 있는 창 위에서도 보이도록 따로 높은 순서의 캔버스에 두고, 클릭은 받지 않는다. 카드의 모양(테마)은 건드리지 않는다.
    /// </summary>
    public static class HudNoticeBuilder
    {
        public const string MenuPath = "Tools/A305/UI/Place Hud Notice";

        private const string HudPath = "Assets/Prefabs/Learning/LearningHud.prefab";

        // 설문 카드(1500)보다 위, 로그인 끊김 안내(2000)보다 아래
        private const int SortingOrder = 1600;
        private const float Width = 680f;
        private const float FontSize = 30f;
        private const float Top =
            HudTopBarLayout.Margin + HudTopBarLayout.Size + HudTopBarLayout.Gap;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(HudPath, root => Place(root.transform));
            AssetDatabase.SaveAssets();
        }

        /// <summary>알림 층과 카드를 찾거나 만들어 자리를 맞추고 카드를 돌려준다. 메뉴 칸에 있던 카드와 글자는 옮겨 온다.</summary>
        public static RectTransform Place(Transform root)
        {
            RectTransform layer = FindOrCreate(root, "Notice");
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;
            var canvas = BuilderTools.GetOrAddComponent<Canvas>(layer.gameObject);
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            Transform old = root.Find("Panel/MessageCard");
            if (old != null && layer.Find("MessageCard") == null)
                old.SetParent(layer, false);
            RectTransform card = FindOrCreate(layer, "MessageCard");
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = new Vector2(0f, -Top);
            card.sizeDelta = new Vector2(Width, card.sizeDelta.y);
            var fitter = BuilderTools.GetOrAddComponent<ContentSizeFitter>(card.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Transform message = card.Find("Message");
            if (message == null)
                message = root.Find("Panel/Message");
            if (message != null)
            {
                message.SetParent(card, false);
                var text = message.GetComponent<TMP_Text>();
                text.fontSize = FontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
            }
            return card;
        }

        private static RectTransform FindOrCreate(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return (RectTransform)existing;
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
