# 리틀 멍 바리스타 외형 사본

기본 여자 플레이어의 골격과 이동 기능을 공유하는 바리스타 외형이다. 금발 옆 묶음머리, 초록 홍채, 테라코타 셔츠, 크림색 허리 앞치마, 세이지색 긴바지는 `frontend/ArtSources/Player/LittleMung/Barista/Reference/`의 4면 디자인을 기준으로 한다. 얼굴·체형은 기존 리틀 멍을 사용하며 원화의 머리 가닥과 의상 주름을 그대로 재현한 최종 모델은 아니다.

## Unity에서 확인

1. 분리 프로젝트의 `Assets/Scenes/Sandbox/LittleMungBaristaPlayerSandbox.unity`를 연다.
2. Play 후 WASD로 이동하고 Shift로 달리며 Space로 점프한다.
3. 계단·경사로에서 바지와 앞치마, 팔·손, 신발을 함께 확인한다.
4. 외형만 보려면 `Assets/Prefabs/Player/LittleMung/LittleMungBaristaPlayer.prefab`을 연다.

이 사본은 플레이어 조작을 재사용한다. NPC 대화·표정·립싱크 기능을 새로 연결하지 않는다.

## 부모와 외형의 관계

`LittleMungStairPlayer.prefab`을 부모로 둔 **Prefab Variant**다. 상속 관계를 해제하거나 골격을 다른 프리팹으로 복사하지 않는다.

- Animator·Avatar·애니메이션 컨트롤러, 입력·이동·접지·계단 보행·팔 간격 컴포넌트는 부모에서 상속한다.
- 바리스타 외형의 모든 스킨은 부모의 기존 본 Transform을 직접 참조한다. 별도의 런타임 Armature나 Animator를 추가하지 않는다.
- 기본 메시에서 기존 머리·옷 면을 바꾸고 이마 경계를 재구성한 `BaristaBody.asset`과 새 머리·의상의 `BaristaOutfit.fbx` 메시를 사용한다.
- 남아 있는 원래 정점의 가중치는 부모의 현재 메시에서 복사한다. 따라서 기존 밑창 보정도 복사한다. 새 이마 면은 Head, 새 의상은 공통 몸 본에 결합한다.
- 부모의 동작 설정·클립과 본 Transform 수정은 상속된다. **본 이름·계층·기준 자세 또는 바인드 포즈를 바꾸는 작업은 의상 재내보내기와 재검사가 필요하다.** 기준 골격의 변경을 임의 체형으로의 자동 의상 맞춤으로 해석하지 않는다.
- 외형 메시 자체는 별도다. 부모 메시의 새 얼굴 Blend Shape가 바리스타 메시에도 자동 생성되지는 않는다. 얼굴 표정을 추가할 때 두 외형의 형상과 이름 규격을 함께 검증해야 한다.

## Blender 수정본 반영

Blender 원본은 `frontend/ArtSources/Player/LittleMung/Barista/LittleMung_Barista.blend`이다. 기본 여자 모델 파일은 수정하지 않는다.

1. 바리스타 blend의 `BaristaBody`와 `BaristaOutfit`을 수정한다. 골격의 이름·계층·기준 자세는 공통 규격을 유지한다.
2. `export_player.py`로 별도 FBX를 내보낸다. 기존 파일을 덮어쓰지 않으므로 임시 출력 경로를 지정한다.
3. 확인한 FBX를 `Assets/Art/Player/LittleMung/Barista/BaristaOutfit.fbx`에 반영한다.
4. Unity의 **Tools → A305 → Player → Refresh Barista Appearance**를 실행한다. 외형 메시·재질·본 연결과 부모 가중치를 다시 반영한다. 기존 이동 컴포넌트와 상속 관계는 유지한다.
5. 아래 검사를 실행하고 같은 씬에서 외관을 확인한다.

**Build Barista Variant Sandbox**는 프리팹과 씬이 없을 때 만든다. 이미 있는 디자인을 재생성하지 않는다. **Refresh Barista Appearance**는 Blender에서 관리하는 외형 연결을 갱신하는 메뉴이므로, 해당 외형 오브젝트의 메시·재질 수정은 blend/FBX를 기준으로 관리한다.

`create_barista.py`는 최초 외형을 구성한 Blender 스크립트다. 원래 여자 모델을 읽어 새 디렉터리에만 저장하며 기존 blend를 덮어쓰지 않는다. 이후 사람이 수정한 blend를 이 스크립트로 재생성하지 않는다.

## 검증

- `LittleMungBaristaTests`: 부모 Variant 관계, 단일 Animator, 동일 Avatar·컨트롤러, 상속된 컴포넌트·본, 기준 골격과 의상 바인드 포즈 일치, 가중치 정규화.
- `LittleMungPlayerTests(Barista)`: 실제 입력과 골격 움직임을 사용하는 걷기·달리기·점프·정지, 회전·벽 충돌·계단·경사 이동, 발 뒤집힘과 손·전완의 머리·몸통 간격.
- 바인드 포즈 검사가 실패하면 부모 골격과 Blender 사본의 기준 자세를 먼저 맞춘다. 검사 허용값을 늘려 숨기지 않는다.
- 동작 캡처: PlayMode 검사에 `-playerCaptureOutput <저장소 밖 폴더> -playerCaptureVariant Barista`를 추가한다. Unity의 별도 카메라 렌더를 저장한다.
- 팔 간격 검사는 머리·몸통 타원체를 이용한 근사 검사다. 모든 옷 삼각형의 관통, 표정, 립싱크, WebGL 성능까지 합격했다는 뜻은 아니다. 외관은 렌더와 실제 씬에서 별도로 검수한다.

