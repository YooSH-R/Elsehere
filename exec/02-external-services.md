# ELSEHERE 외부 서비스 설정

새 환경을 실행하려면 아래 네 값을 준비해 [빌드·배포 매뉴얼의 환경 파일](01-build-and-deploy.md#4-서버-환경-파일-작성)에 넣습니다.

| 발급처 | 환경 변수 | 용도 |
| --- | --- | --- |
| Google Cloud | `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` | Google 로그인 |
| SSAFY GMS | `GMS_KEY` | NPC 응답, 판정, 피드백 등 텍스트 기능 |
| GroqCloud | `GROQ_API_KEY` | 음성 인식 |

## Google 로그인

1. Google Cloud 프로젝트에서 OAuth 동의 화면을 설정하고 **Web application** 유형의 OAuth 클라이언트를 만듭니다. 앱이 테스트 상태라면 서비스 확인에 쓸 계정을 테스트 사용자로 추가합니다.
2. **Authorized redirect URIs**에 실제 앱 주소의 `/api/v1/auth/callback/google`을 등록합니다. 운영은 `https://elsehere.kr/api/v1/auth/callback/google`, 개발은 `https://dev.elsehere.kr/api/v1/auth/callback/google`입니다. 로컬에서 백엔드를 직접 실행한다면 `http://localhost:8080/api/v1/auth/callback/google`을 사용합니다. 별도 검증용 앱은 그 주소와 포트를 그대로 등록합니다.
3. 발급한 Client ID와 Client Secret을 각각 `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`에 넣습니다. 백엔드가 `openid`, `email`, `profile` 범위로 로그인합니다. 브라우저에서 로그인 후 설정한 주소로 돌아오고 로그인 또는 가입 화면이 열리는지 확인합니다.

[Google OAuth 웹 앱 설정](https://developers.google.com/identity/protocols/oauth2/web-server) · [프로젝트 로그인 설정](../backend/README.md#google-로그인)

## SSAFY GMS

SSAFY 팀에 제공된 GMS 접근 권한으로 포털에 로그인하고 팀 프로젝트에서 사용할 키를 준비합니다. 발급받은 값을 AI 워커의 `GMS_KEY`에 설정합니다. 기본 모델 구성인 `LLM_PROFILE=gms-luna`는 GMS를 NPC 답변, 의도 판정, 피드백·기억 등 텍스트 생성에 사용하며 음성 인식의 대체 경로도 구성합니다. 공급자 주소와 역할별 모델은 `ai/worker/llm_profiles.json`에서 확인할 수 있습니다.

[SSAFY GMS 포털](https://gms.ssafy.io/) · [모델·공급자 설정](../ai/worker/llm_profiles.json)

## GroqCloud

GroqCloud에 로그인해 프로젝트의 API Keys에서 키를 발급받습니다. 발급받은 값을 AI 워커의 `GROQ_API_KEY`에 설정합니다. 기본 워커 프로필에서 Groq Whisper를 음성 인식에 사용합니다. 서버에 반영한 뒤 게스트로 짧게 영어를 말해 녹음이 자막으로 변환되는지 확인합니다.

[GroqCloud 키 발급 안내](https://console.groq.com/docs/quickstart) · [AI 워커 실행 변수](../ai/worker/README.md#실행)

## 빌드·호스팅 및 선택 연동

| 서비스 | 접근 및 설정 |
| --- | --- |
| SSAFY GitLab·GitLab Runner | 팀 GitLab 프로젝트에 접근 권한을 받은 뒤 저장소를 클론합니다. 팀 CI로 빌드할 때는 해당 프로젝트에 등록된 Runner를 사용합니다. 앱의 비밀값은 서버 환경 파일에서 관리하고, CI에서 쓰는 등록·배포 값은 권한 있는 담당자가 등록합니다. [GitLab 프로젝트 설정](../docs/gitlab-setup.md) · [Runner 운영](../docs/ci-runner.md) |
| Unity Hub·Unity 라이선스 | Unity 계정으로 Hub에 로그인하고 사용 가능한 라이선스를 활성화합니다. `6000.3.26f1`과 Web Build Support를 설치합니다. 배포 이미지에는 Unity에서 만든 WebGL 산출물이 들어갑니다. [Unity 설치 및 빌드](../docs/unity/setup.md#설치와-프로젝트-열기) |
| SSAFY 제공 Ubuntu 서버·Let's Encrypt | 접근 권한은 팀 서버 담당자가 부여합니다. 서비스에 사용할 도메인의 DNS를 호스트에 연결하고, Nginx와 Certbot으로 공개 HTTPS를 구성합니다. 기존 운영·개발 주소와 프록시 포트는 빌드·배포 매뉴얼을 따릅니다. [서버 구성](../docs/deployment.md) · [인증서 설정](../docs/https.md) |
| Kokoro 모델 배포 파일 | 워커 이미지를 빌드할 때 GitHub의 `model-files-v1.1`에서 음성 모델과 목소리 파일을 받아 체크섬을 검사합니다. 합성은 워커의 CPU에서도 실행되며 이 경로에 별도 API 계정이 필요하지 않습니다. [모델 준비와 실행 확인](../ai/worker/README.md#배포-모델-준비와-상태-확인) |
| GPU 워커, 선택 | 프로젝트 GPU 사용 권한과 SSH 터널이 준비된 환경에서 STT·무례 판정·NPC 답변·TTS 가속에 사용할 수 있습니다. 해당 주소와 `STT_GPU_TOKEN`, `GUARD_GPU_TOKEN`, `TTS_GPU_TOKEN`을 서버 환경 파일에 설정합니다. 백엔드와 AI 워커 사이에 쓰는 `AI_WORKER_TOKEN`은 별도로 정합니다. [GPU 연결 설정](../docs/gpu-worker-network.md) |
| Tripo Studio·API, 에셋 제작용 | 새 캐릭터나 소품을 제작할 때 Studio 계정 또는 `TRIPO_API_KEY`를 준비합니다. 이미 저장소에 포함된 에셋으로 게임을 빌드하고 실행할 때는 Tripo 키가 필요하지 않습니다. [Unity Tripo 작업대](../frontend/UnityClient/Assets/Scripts/Editor/Tripo/README.md) |

실제 인증 비밀값과 서버 개인 키는 접근 권한이 있는 환경에서 관리합니다. 서버 적용 후에는 OAuth 왕복, 음성 인식, NPC 답변, TTS 재생을 각각 확인합니다. [빌드·배포와 DB 복원](01-build-and-deploy.md)
