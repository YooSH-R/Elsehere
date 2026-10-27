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

## 로컬 모델 왕복 검증

`TripoLocalWorkflowTests`는 자동 검사, 불합격 후보 유지·제외, 미검사 범위 표시를 확인한다.
`TripoLocalRoundTripTests`는 실제 FBX를 새 테스트 폴더에 복사해 Blender 왕복을 수행한다.
다음 환경 변수를 지정하지 않으면 실제 모델 테스트만 건너뛴다.

```powershell
$env:TRIPO_REVIEW_MODEL = '<로컬 FBX 절대 경로>'
$env:TRIPO_REVIEW_BLENDER = '<Blender 실행 파일 절대 경로>'
$env:TRIPO_REVIEW_OUTPUT = '<저장소 밖 결과 폴더>'
& '<Unity.exe 경로>' -batchmode -nographics -projectPath frontend/UnityClient `
  -runTests -testPlatform EditMode -testFilter A305.Tripo.Tests.TripoLocalRoundTripTests `
  -testResults '<결과 XML 절대 경로>' -logFile '<로그 절대 경로>'
```

모델을 열어 둔 Editor와 같은 프로젝트에서 동시에 실행하지 않는다. 테스트는 UI를 열거나
API를 호출하지 않는다. 출력 폴더에는 편집 복사본과 `roundtrip.json`이 남고 Unity 내부의
임시 모델·작업 기록은 정리된다. 원본 FBX, 작업용 FBX와 편집 `.blend`의 보존,
본·Shape Key 개수, 자동 검사 기록, 자동 승인 금지, 후보 제외 후 복원을 확인한다.
Head 본이 있으면 좌우 귀 위치의 가상 소켓도 검사해 실제 후보 결과에 반영한다.
소켓 편차를 발견한 모델은 검사 시스템 검증에 성공하더라도 제작용 모델 합격이 아니다.
실제 얼굴 변형이 없는 입력으로 표정 보존을 입증할 수 없으므로 기존 Blender fixture와
Shape Key 누락·소켓 이동 회귀 테스트도 함께 실행한다.
