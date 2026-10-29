using UnityEngine;

namespace A305.ObjectHover
{
    [CreateAssetMenu(menuName = "A305/Object Hover/Word", fileName = "NewWord")]
    public sealed class ObjectWordDefinition : ScriptableObject
    {
        [SerializeField]
        private ObjectWordDetails details = new ObjectWordDetails();

        [SerializeField, HideInInspector]
        private string assetGuid;
        public ObjectWordDetails Details => details;
        public string Key => details.Key;
    }
}
