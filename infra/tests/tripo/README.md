# Tripo 로컬 통합 검증

`create_blender_fixture.py`는 본·스킨 가중치·5개 얼굴 Shape Key가 있는 검사용 모델을 만든다.
리틀 멍이나 제작용 에셋이 아니다.

`TripoDccBridgeSmoke.cs`는 공식 DCC Bridge가 설치된 **격리된 테스트 Unity 프로젝트**에서만 실행한다.
프로젝트의 `Assets`, `Packages`, `ProjectSettings`와 `infra/tests/tripo`를 임시 저장소 구조로 복사하고,
검증 코드를 Editor 전용 어셈블리에 두어 `A305.EditorTools`, `Tripo3D.UnityBridge.Editor`를 참조한다.
제작 프로젝트의 씬·모델을 테스트 대상으로 쓰지 않는다.

Blender로 fixture FBX를 만든 뒤 Unity batchmode에 다음 인수를 전달한다.

```text
-executeMethod TripoDccBridgeSmoke.Run
-tripoFixture <검사용 FBX 절대 경로>
-tripoBlender <Blender 실행 파일 절대 경로>
-tripoResults <결과 텍스트 절대 경로>
```

`-quit`은 쓰지 않는다. 비동기 검증이 완료되면 자체적으로 종료 코드를 반환한다.
공식 Bridge의 `127.0.0.1:60610` 포트를 사용하므로 다른 Bridge 창은 닫아야 한다.
WebSocket handshake·binary FBX 전송·Unity 가져오기·본/가중치/Shape Key·작업 기록 연결·Blender 왕복·원본 보존을 검사한다.
Tripo 계정, API 키, 생성 크레딧은 사용하지 않는다. 실제 Studio 로그인·브라우저 연결·실제 모델의 외형 검수는 별도다.

## CI 전용 입력

`create_ci_fixtures.py`는 `Humanoid.fbx`와 잘못된 내장 텍스처·색상/노멀 중복 연결을 포함한 `MisnamedColor.fbx`를 지정한 임시 폴더에 만든다. 제작용 모델은 복사하거나 수정하지 않는다. `test_ci_fixtures.py`는 Blender에서 생성물을 다시 가져와 필요한 본·가중치·UV·실제 표정 변형과 원래의 잘못된 텍스처 조건이 보존되는지 확인한다.

```text
blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python infra/tests/tripo/test_ci_fixtures.py
```

Blender 왕복과 실제 모델 가져오기는 Unity 자동 테스트로 검사하지 않는다. 생성 모델은 Tripo 작업 창의 자동 검사 결과와 검수 씬을 함께 보고 판단한다. CI 준비 스크립트는 아직 이 입력을 만들지만 현재 이를 사용하는 Unity 테스트는 없다.
