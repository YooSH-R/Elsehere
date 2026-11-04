using System.Collections.Generic;
using UnityEngine;

namespace A305.Characters
{
    /// <summary>
    /// 캐릭터의 이동 상태를 모델의 Animator 파라미터로 전달한다.
    /// 도형만 있을 때는 Animator가 없으므로 아무것도 하지 않는다. 모델을 붙일 때는
    /// Animator Controller에 Speed(Float), IsGrounded(Bool), VerticalSpeed(Float) 파라미터를 만든다.
    /// </summary>
    public class CharacterAnimationDriver : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int IsGroundedParameter = Animator.StringToHash("IsGrounded");
        private static readonly int VerticalSpeedParameter = Animator.StringToHash("VerticalSpeed");

        // NPC 전용 컨트롤러에만 있는 동작 번호(일하기·말하기·앉기 등). 0이면 이동 동작을 재생한다.
        private static readonly int ActionParameter = Animator.StringToHash("Action");

        [SerializeField]
        [Tooltip("모델의 Animator. 비워 두면 자식 객체에서 찾는다.")]
        private Animator animator;

        private readonly HashSet<int> availableParameters = new HashSet<int>();

        public void SetLocomotion(
            float horizontalSpeedMetersPerSecond,
            bool isGrounded,
            float verticalSpeedMetersPerSecond
        )
        {
            if (animator == null)
            {
                return;
            }

            SetFloatIfAvailable(SpeedParameter, horizontalSpeedMetersPerSecond);
            SetBoolIfAvailable(IsGroundedParameter, isGrounded);
            SetFloatIfAvailable(VerticalSpeedParameter, verticalSpeedMetersPerSecond);
        }

        /// <summary>동작 번호를 전달한다. Action 파라미터가 없는 컨트롤러(플레이어 등)에는 아무것도 하지 않는다.</summary>
        public void SetAction(int action)
        {
            if (animator != null && availableParameters.Contains(ActionParameter))
            {
                animator.SetInteger(ActionParameter, action);
            }
        }

        private void Awake()
        {
            BindAnimator(animator != null ? animator : GetComponentInChildren<Animator>());
        }

        public void BindAnimator(Animator modelAnimator)
        {
            animator = modelAnimator;
            availableParameters.Clear();
            if (animator == null)
                return;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                availableParameters.Add(parameter.nameHash);
            WarnMissingParameter(SpeedParameter, "Speed");
            WarnMissingParameter(IsGroundedParameter, "IsGrounded");
            WarnMissingParameter(VerticalSpeedParameter, "VerticalSpeed");
        }

        private void WarnMissingParameter(int parameterHash, string parameterName)
        {
            if (!availableParameters.Contains(parameterHash))
            {
                Debug.LogWarning(
                    $"Animator Controller에 '{parameterName}' 파라미터가 없어 해당 상태를 전달하지 않습니다.",
                    animator
                );
            }
        }

        private void SetFloatIfAvailable(int parameterHash, float value)
        {
            if (availableParameters.Contains(parameterHash))
            {
                animator.SetFloat(parameterHash, value);
            }
        }

        private void SetBoolIfAvailable(int parameterHash, bool value)
        {
            if (availableParameters.Contains(parameterHash))
            {
                animator.SetBool(parameterHash, value);
            }
        }
    }
}
