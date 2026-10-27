using System;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoModelReviewTests
    {
        [Test]
        public void RequireHumanoid_WithoutValidAvatar_RejectsModel()
        {
            var model = new GameObject("Unrigged");
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    TripoModelReview.RequireHumanoid(model)
                );
                model.AddComponent<Animator>();
                Assert.Throws<InvalidOperationException>(() =>
                    TripoModelReview.RequireHumanoid(model)
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(model);
            }
        }
    }
}
