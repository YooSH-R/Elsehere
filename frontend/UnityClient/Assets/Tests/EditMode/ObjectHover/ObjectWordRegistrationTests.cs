using A305.EditorTools.ObjectHover;
using NUnit.Framework;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordRegistrationTests
    {
        [TestCase(0, true)]
        [TestCase(2, false)]
        public void RequiresWord_SkipsShapesOutsidePointerRaycasts(int layer, bool expected)
        {
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                shape.layer = layer;
                Assert.That(
                    ObjectWordRegistration.RequiresWord(shape.GetComponent<MeshRenderer>()),
                    Is.EqualTo(expected)
                );
            }
            finally
            {
                Object.DestroyImmediate(shape);
            }
        }
    }
}
