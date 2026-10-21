# Characters 모듈

플레이어와 NPC가 함께 쓰는 캐릭터 표현을 담당합니다. 지금은 이동 상태를 모델의 Animator로 전달하는 `CharacterAnimationDriver`만 있습니다.

도형으로 개발하는 동안에는 Animator가 없으므로 아무것도 하지 않습니다. 모델과 애니메이션을 붙일 때 이동 코드를 고치지 않고 Animator Controller만 준비하면 되도록 하기 위함입니다. 다른 모듈과 게임 코드를 참조하지 않습니다.

## 모델을 붙일 때

1. 캐릭터 루트의 자식에 모델을 두고, 모델에 Animator와 Animator Controller를 연결합니다.
2. 캐릭터 루트의 `CharacterAnimationDriver`에 그 Animator를 연결합니다. 비워 두면 자식 객체에서 찾습니다.
3. Animator Controller에 아래 파라미터를 만들고 상태 전환에 사용합니다.

| 파라미터 | 형식 | 값 |
| --- | --- | --- |
| `Speed` | Float | 수평 이동 속도(m/s) |
| `IsGrounded` | Bool | 땅에 닿아 있는지 |
| `VerticalSpeed` | Float | 수직 속도(m/s). 양수는 올라가는 중, 음수는 떨어지는 중 |

Controller에 없는 파라미터는 전달하지 않으며, 시작할 때 Console에 경고로 알립니다.

## 사용하는 쪽

`PlayerMover`가 매 프레임 `SetLocomotion(수평 속도, 땅에 닿음, 수직 속도)`를 호출합니다. NPC 이동을 구현할 때도 같은 메서드로 상태를 전달합니다.
