using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace A305.Player.Tests
{
    /// <summary>프로젝트 입력 액션의 마우스 조작. 왼쪽 클릭은 사물 카드 고정에 쓰므로 시점은 오른쪽 버튼으로 돌린다.</summary>
    public class PlayerInputActionsTests
    {
        private const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

        [Test]
        public void CameraRotate_IsHeldWithTheRightMouseButton()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            InputAction rotate = actions.FindAction("Player/CameraRotate", throwIfNotFound: true);

            Assert.That(
                rotate.bindings.Select(binding => binding.path),
                Is.EqualTo(new[] { "<Mouse>/rightButton" })
            );
        }
    }
}
