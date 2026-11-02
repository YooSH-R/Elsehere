using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace A305.Player.Tests
{
    /// <summary>프로젝트 입력 액션의 마우스 조작. 시점은 왼쪽 버튼을 누른 채 끌어 돌리고, 거의 움직이지 않고 뗀 왼쪽 클릭은 사물 카드 고정에 쓴다.</summary>
    public class PlayerInputActionsTests
    {
        private const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

        [Test]
        public void CameraRotate_IsHeldWithTheLeftMouseButton()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            InputAction rotate = actions.FindAction("Player/CameraRotate", throwIfNotFound: true);

            Assert.That(
                rotate.bindings.Select(binding => binding.path),
                Is.EqualTo(new[] { "<Mouse>/leftButton" })
            );
        }
    }
}
