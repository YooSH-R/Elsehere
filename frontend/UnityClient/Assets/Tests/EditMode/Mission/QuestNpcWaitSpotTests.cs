using System.Collections.Generic;
using A305.Npc;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 퀘스트에서 만나야 하는 행인은 그 단계 동안 정한 자리에서 기다린다. 비 오는 거리 단계의 Ben은 카페 출입문 앞에 선다.
    /// </summary>
    public sealed class QuestNpcWaitSpotTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void DestroyCreated()
        {
            foreach (GameObject target in created)
                Object.DestroyImmediate(target);
            created.Clear();
        }

        [Test]
        public void DuringBensStep_BenWaitsAtTheSpot()
        {
            Passerby ben = Npc("TOWN_PASSERBY", new Vector3(5f, 0f, -14f));
            QuestNpcWaitSpot spot = Spot(ben, new Vector3(-3.8f, 0f, -10.3f));

            spot.Apply(StateAt("TOWN_PASSERBY"));

            Assert.That(ben.IsWaitingForQuest, Is.True);
            Assert.That(
                Vector3.Distance(ben.transform.position, spot.transform.position),
                Is.LessThan(.01f),
                "카페 앞 자리로 옮겨 기다립니다."
            );
        }

        [Test]
        public void AfterBensStep_BenGoesBackToHisDay()
        {
            Passerby ben = Npc("TOWN_PASSERBY", new Vector3(5f, 0f, -14f));
            QuestNpcWaitSpot spot = Spot(ben, new Vector3(-3.8f, 0f, -10.3f));
            spot.Apply(StateAt("TOWN_PASSERBY"));

            spot.Apply(StateAt("SHOP_STAFF"));

            Assert.That(ben.IsWaitingForQuest, Is.False);
        }

        private Passerby Npc(string code, Vector3 position)
        {
            var npc = new GameObject(code);
            created.Add(npc);
            npc.transform.position = position;
            var passerby = npc.AddComponent<Passerby>();
            var identity = new SerializedObject(npc.GetComponent<NpcIdentity>());
            identity.FindProperty("code").stringValue = code;
            identity.ApplyModifiedPropertiesWithoutUndo();
            return passerby;
        }

        private QuestNpcWaitSpot Spot(Passerby npc, Vector3 position)
        {
            var target = new GameObject("WaitSpot");
            created.Add(target);
            target.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 270f, 0f));
            var spot = target.AddComponent<QuestNpcWaitSpot>();
            var fields = new SerializedObject(spot);
            fields.FindProperty("npc").objectReferenceValue = npc;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return spot;
        }

        private static FirstDayMissionState StateAt(string npcCode) =>
            new FirstDayMissionState
            {
                Started = true,
                Steps = new List<FirstDayMissionStep>
                {
                    new FirstDayMissionStep
                    {
                        Index = 0,
                        Scene = "TOWN",
                        NpcCode = npcCode,
                        Status = "CURRENT",
                    },
                },
            };
    }
}
