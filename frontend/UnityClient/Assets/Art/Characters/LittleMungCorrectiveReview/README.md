# 리틀 멍 관절 보정 검수 에셋

`Scenes/Sandbox/LittleMungCorrectivesSandbox.unity`를 열고 Play하면 어깨 중심 인사를 재생합니다. 손바닥은 캐릭터 앞쪽을 향합니다. 모델 자식의 Animator에서 `Diagnostic`을 켜면 걷기·뛰기·웅크림·팔 올리기·고개 돌리기로 구성한 진단용 동작으로 전환됩니다. 이 진단 동작은 게임용 애니메이션이 아닙니다.

`LittleMung.fbx`와 같은 이름의 `LittleMung.correctives.asset`이 짝입니다. 프로필에는 기본 자세의 본 회전과 모델별 보정축을 기록했습니다. `PoseCorrectiveDriver`가 Animator 이후 무릎 2개, 바지 앞뒤 4개, 웅크림 2개의 블렌드셰이프를 적용합니다. 검수용 `.anim`에는 보정값을 직접 굽지 않아 실제 드라이버 적용 여부를 확인할 수 있습니다.

검수 프리팹은 `Prefabs/Characters/LittleMungCorrectiveReview.prefab`입니다. 임포트는 Generic이며, 다른 골격에 대한 Humanoid 리타게팅은 이 검수본의 검증 범위에 포함하지 않습니다. 기준값 대조를 위해 진단·인사 클립은 무압축으로 가져옵니다. 배포용 압축 설정을 바꾸면 관절 보정 결과도 다시 확인해야 합니다.

이 에셋은 검수 후보입니다. 바지 경계 경고와 기존 높은 팔 올리기 진단 동작의 머리 관통이 남아 있습니다. 해당 높은 팔 올리기는 기본 인사 클립과 별개이며, 관통이 해결된 동작으로 취급하지 않습니다. 표정·립싱크용 얼굴 셰이프는 포함하지 않습니다.
