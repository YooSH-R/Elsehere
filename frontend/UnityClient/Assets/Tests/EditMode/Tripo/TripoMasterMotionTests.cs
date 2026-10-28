using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public class TripoMasterMotionTests
    {
        [Test]
        public void MotionAuditRejectsMissingControllerAndPassesMasterCorrections()
        {
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Player/Characters/Female.prefab"
            );
            Assert.That(TripoMasterMotionAudit.Inspect(master).Errors, Is.Empty);
            var broken = Object.Instantiate(master);
            try
            {
                broken.GetComponentInChildren<Animator>().runtimeAnimatorController = null;
                Assert.That(TripoMasterMotionAudit.Inspect(broken).Passed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }
    }
}
