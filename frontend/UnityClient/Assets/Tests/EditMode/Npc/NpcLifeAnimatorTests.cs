using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Npc.Tests
{
    /// <summary>생활 동작 컨트롤러로 바꿔 끼울 때 캐릭터가 쓰던 전용 클립을 잃지 않는지.</summary>
    public class NpcLifeAnimatorTests
    {
        private const string LocomotionPath = "Assets/Art/Player/LittleMung/Locomotion.controller";
        private const string LifePath = "Assets/Art/Characters/Npc/NpcLife.controller";
        private const string IdlePath = "Assets/Art/Player/LittleMung/Idle.anim";
        private const string WalkPath = "Assets/Art/Player/LittleMung/Walk.anim";

        [Test]
        public void KeepCharacterClips_CharacterIdle_IsPlayedInsideTheLifeController()
        {
            var locomotion = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                LocomotionPath
            );
            var life = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LifePath);
            var sharedIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
            var sharedWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkPath);
            var characterIdle = new AnimationClip { name = "Idle" };
            var character = new AnimatorOverrideController(locomotion);
            character[sharedIdle] = characterIdle;
            try
            {
                var combined =
                    NpcLifeAnimator.KeepCharacterClips(life, character)
                    as AnimatorOverrideController;

                Assert.That(combined, Is.Not.Null, "캐릭터 전용 클립을 얹은 생활 컨트롤러입니다.");
                Assert.That(combined.runtimeAnimatorController, Is.SameAs(life));
                Assert.That(combined[sharedIdle], Is.SameAs(characterIdle));
                Assert.That(combined[sharedWalk], Is.SameAs(sharedWalk));
            }
            finally
            {
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(characterIdle);
            }
        }

        [Test]
        public void KeepCharacterClips_SharedLocomotion_UsesTheLifeControllerAsIs()
        {
            var locomotion = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                LocomotionPath
            );
            var life = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LifePath);

            Assert.That(NpcLifeAnimator.KeepCharacterClips(life, locomotion), Is.SameAs(life));
        }
    }
}
