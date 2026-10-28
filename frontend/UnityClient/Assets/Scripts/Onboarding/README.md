# Onboarding 모듈

접속 직후의 입구 화면(화면 01~05)입니다. 로그인, 가입 정보 입력, 배울 언어 선택, 레벨 테스트, NPC 대화 난이도 설정을 거쳐 튜토리얼(`HomeScene`)로 보내고, 초기 설정을 마친 사용자는 마지막 장소(없으면 마을 `TownScene`)로 보냅니다. 게스트는 캐릭터만 골라 바로 튜토리얼로 갑니다. 배포용 씬 `EntryScene`이 Scene List의 첫 씬입니다.

서버 연동이 있는 게임 전용 기능입니다. 진행 판단은 일반 C# 클래스에 두고, 패널은 입력 전달과 표시만 맡습니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `OnboardingFlow` | 접속 상태로 첫 화면을 정하고, 화면에 필요한 데이터를 불러 둔 뒤 다음 화면으로 넘깁니다. 요청이 실패하면 화면을 그대로 두고 서버 메시지를 담으며, 로그인이 풀렸으면 로그인 화면으로 돌아갑니다. |
| `EntryRouter` | BOOT-01과 ONB-08의 마지막 레벨 테스트 상태로 화면을 정합니다. |
| `SignupForm`, `NicknameRule` | 가입 정보 입력 상태와 닉네임 규칙. 서버(AUTH-04)와 같은 기준으로 입력 중에 바로 안내합니다. |
| `IOnboardingApi`, `OnboardingApi` | BOOT-01, AUTH-01·03·04·06·07, ONB-01·02·07·08·09, WORLD-01과 개발용 로그인의 경로와 본문 |
| `OnboardingScreen` | 패널 입력을 `OnboardingFlow`에 전달하고 결과 화면을 보여 줍니다. 튜토리얼·마을 단계가 되면 씬을 바꿉니다. |
| `LoginPanelView` 외 패널 | 화면 01~05와 게스트 캐릭터 선택(`GuestPanelView`). 언어·난이도 카드와 약관 줄은 템플릿을 복제합니다(`TemplateClones`). |
| `OnboardingSettings` | 튜토리얼·마을 씬 이름과 개발용 로그인 사용자(`Data/Onboarding/OnboardingSettings.asset`) |
| `BrowserRedirect`, `Plugins/WebGL/BrowserRedirect.jslib` | Google 로그인을 같은 탭에서 엽니다. |
| 나머지 | API 요청·응답 DTO |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Api` | 서버 호출 |
| uGUI, TextMeshPro | 화면 |
| `com.github.kou-yeung.webglinput` | Web 빌드의 닉네임 입력창에서 한글 조합(IME)을 받습니다. Builder가 입력창에 붙입니다. |

## 동작 규칙

| 상황 | 동작 |
| --- | --- |
| 로그인하지 않음(BOOT-01 401) | 화면 01. [Google로 계속하기]는 같은 탭에서 Google 로그인으로 이동하고, 끝나면 서버가 첫 화면으로 돌려보냅니다. 실패하면 `?loginError=true`로 돌아와 안내합니다. |
| [게스트로 시작하기] | 서버를 부르기 전에 캐릭터 선택 화면을 엽니다. 캐릭터를 골라야 [시작하기](AUTH-07)를 누를 수 있고, [뒤로]는 화면 01입니다. 닉네임은 서버가 `guest-` 뒤에 숫자를 붙여 정하고, 언어·난이도는 영어·초급으로 정해 튜토리얼로 갑니다. 게스트는 NPC 대화 횟수가 정해져 있어, 다 쓰면 대화 시작 때 서버 안내(`AI_QUOTA_EXHAUSTED`)가 대화 상태 문구로 보입니다. |
| 가입 대기 | 화면 02. 닉네임 규칙과 [중복 확인](AUTH-06), 캐릭터, 필수 약관 동의가 모두 있어야 [가입하기]를 누를 수 있습니다. 닉네임을 바꾸면 다시 확인해야 합니다. |
| `LANG` | 화면 03. 고를 수 없는 언어는 "준비 중"입니다. |
| `LEVEL` | ONB-08의 마지막 테스트가 없거나 진행 중이면 화면 04, 끝났으면 화면 05입니다. |
| 화면 04 | MVP는 [레벨 테스트 진행하기]를 막아 두고 [건너뛰기](ONB-07)만 씁니다. |
| 화면 05 | 고른 적이 없으면 고를 수 있는 첫 단계(초급)를 미리 골라 둡니다. 추천 단계에는 "레벨 테스트 추천" 배지가 붙습니다. |
| `TUTORIAL` | `HomeScene`을 엽니다. 튜토리얼은 처음부터 다시 진행합니다. |
| `DONE` | 마지막 장소(`lastSceneCode`)가 있으면 장소 목록(WORLD-01)에서 그 장소의 Unity 씬(`unityKey`)을 찾아 엽니다. 값이 없거나, 닫혔거나, 목록이나 이 빌드의 Scene List에 없으면 `TownScene`을 엽니다. 장소 안의 위치는 씬에 놓인 플레이어 위치에서 시작합니다. |

- Editor와 개발 빌드에서는 [개발용 로그인]이 보입니다. 백엔드를 local 프로필로 띄우고, `OnboardingSettings`의 사용자 ID가 DB에 있어야 합니다. Editor는 브라우저 쿠키를 쓰지 않으므로 Google 로그인 대신 이 버튼을 씁니다.
- 가입 화면은 Google 가입 대기 세션이 있어야 열리므로 로컬에서는 Google 로그인 설정이 있어야 확인할 수 있습니다.

## 확인 방법

1. **Tools → A305 → Onboarding → Build**로 프리팹·설정·씬을 만듭니다. 처음이면 **UI → Set Up Fonts**를 먼저 실행합니다.
2. 백엔드를 local 프로필로 실행하고 `Scenes/Sandbox/OnboardingSandbox.unity`를 Play합니다.
3. 한글 입력은 Editor에서 확인되지 않습니다. **Tools → A305 → Onboarding → Build Ime Check Web**으로 입력창 하나만 둔 Web 빌드(`Builds/ImeCheck`)를 만들어 브라우저에서 한글을 입력해 봅니다.

## 테스트

`Tests/EditMode/Onboarding/`(`A305.Onboarding.Tests`)에서 화면 분기, 닉네임 규칙, 가입 입력 상태, API 경로, 진행 흐름(가짜 API)을 검증합니다.
