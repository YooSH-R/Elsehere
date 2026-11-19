using System;
using TMPro;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 퀘스트 창 위의 확인 창. 하던 퀘스트를 그만두거나, 다른 퀘스트를 시작해 하던 퀘스트가 처음으로 돌아가기 전에 묻는다.
    /// [계속하기]와 바깥은 그대로 닫고, 확인 버튼만 맡긴 일을 한다. 자리와 버튼 연결은 QuestBoardBuilder가 넣는다.
    /// </summary>
    public sealed class QuestConfirmView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text title;

        [SerializeField]
        private TMP_Text body;

        [SerializeField]
        private TMP_Text confirmLabel;

        private Action confirmed;

        public bool IsOpen => gameObject.activeSelf;

        public void Show(string titleText, string bodyText, string label, Action onConfirm)
        {
            title.text = titleText;
            body.text = bodyText;
            confirmLabel.text = label;
            confirmed = onConfirm;
            gameObject.SetActive(true);
        }

        /// <summary>확인 버튼. 창을 닫은 뒤 맡긴 일을 한다.</summary>
        public void Confirm()
        {
            Action action = confirmed;
            Close();
            action?.Invoke();
        }

        /// <summary>[계속하기]·바깥. 아무것도 바꾸지 않고 닫는다.</summary>
        public void Close()
        {
            confirmed = null;
            gameObject.SetActive(false);
        }
    }
}
