using System.Linq;
using A305.EditorTools.Common;
using A305.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace A305.Npc.Tests
{
    /// <summary>
    /// 팔 관통 보정을 쓰는 NPC는 모두 카메라에서 먼 동안 보정을 끈다. 마을 NPC 16명이 매 프레임 보정하면 한 프레임이 100ms를 넘는다.
    /// 행인·직원·경찰처럼 공용 행인 프리팹으로 놓는 NPC는 프리팹에서 함께 받는다.
    /// </summary>
    public class NpcDetailDistanceSceneTests
    {
        [TestCase("Assets/Scenes/Production/TownScene.unity")]
        [TestCase("Assets/Scenes/Production/CafeScene.unity")]
        [TestCase("Assets/Scenes/Production/HotelScene.unity")]
        [TestCase("Assets/Scenes/Production/ShopScene.unity")]
        [TestCase("Assets/Scenes/Production/PoliceStationScene.unity")]
        public void NpcsWithArmClearance_TurnItOffWhenFarFromTheCamera(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath);
            NpcIdentity[] correctedNpcs = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Where(npc =>
                    npc.GetComponentsInChildren<LittleMungArmClearance>(true)
                        .Any(armClearance => armClearance.enabled)
                )
                .ToArray();
            Assume.That(
                correctedNpcs,
                Is.Not.Empty,
                scenePath + "에 팔 관통 보정을 쓰는 NPC가 없다."
            );

            string[] missing = correctedNpcs
                .Where(npc => npc.GetComponent<NpcDetailDistance>() == null)
                .Select(npc => npc.Code)
                .ToArray();

            Assert.That(missing, Is.Empty);
        }
    }
}
