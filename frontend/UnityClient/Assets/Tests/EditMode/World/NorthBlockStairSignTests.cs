using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>생활용품점 바깥 계단의 찻집 안내 글자는 공중에 뜨지 않고 기둥 위 안내판에 붙어 있다.</summary>
    public sealed class NorthBlockStairSignTests
    {
        private const string HomeLivingPath = "Assets/Prefabs/World/NorthBlock/HomeLiving.prefab";

        [Test]
        public void TeaRoomStairNotice_IsMountedOnAPostedBoard()
        {
            var shop = AssetDatabase.LoadAssetAtPath<GameObject>(HomeLivingPath);
            Transform entrances = shop.transform.Find("TenantEntrances");

            Assert.That(
                entrances.Find("StairEntranceSign"),
                Is.Null,
                "판 없이 떠 있는 글자가 남아 있습니다."
            );
            Transform sign = entrances.Find("StairSign");
            Assert.That(sign, Is.Not.Null);
            Assert.That(sign.Find("StairSignPost"), Is.Not.Null);
            Assert.That(sign.Find("StairSignBoard"), Is.Not.Null);
            Assert.That(sign.Find("StairEntranceSign"), Is.Not.Null);
        }
    }
}
