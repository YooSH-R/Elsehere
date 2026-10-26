using UnityEngine;

namespace A305.Onboarding
{
    /// <summary>
    /// 개수가 서버 데이터에 따라 달라지는 목록(언어 카드·약관)은 숨겨 둔 템플릿을 복제해 채운다. 다시 그릴 때 이전 복제본을 지운다.
    /// </summary>
    public static class TemplateClones
    {
        public static T Add<T>(T template, Transform parent)
            where T : Component
        {
            T clone = Object.Instantiate(template, parent);
            clone.gameObject.SetActive(true);
            return clone;
        }

        public static void Clear(Transform parent, Component template)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                if (child != template.gameObject)
                {
                    Object.Destroy(child);
                }
            }
        }
    }
}
