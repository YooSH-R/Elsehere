using UnityEngine;

namespace A305.Player
{
    [CreateAssetMenu(menuName = "A305/Player/Character Catalog")]
    public sealed class PlayerCharacterCatalog : ScriptableObject
    {
        [SerializeField]
        private PlayerCharacterDefinition defaultCharacter;

        [SerializeField]
        private PlayerCharacterDefinition[] characters;

        public PlayerCharacterDefinition Resolve(string characterId)
        {
            if (characters != null)
            {
                foreach (PlayerCharacterDefinition character in characters)
                {
                    if (character != null && character.CharacterId == characterId)
                        return character;
                }
            }
            return defaultCharacter;
        }
    }
}
