# Api 모듈

백엔드 API를 부르는 공용 클라이언트입니다. 요청 위조 방지 헤더, JSON 변환, 공통 오류 응답 해석을 한곳에서 맡아 기능마다 다시 만들지 않게 합니다. 입구 화면(`A305.Onboarding`)에서 처음 만들었고, 다른 기능도 이 클라이언트로 서버를 부릅니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `ApiClient` | `GetAsync`·`PostAsync`·`PutAsync`. 상태를 바꾸는 요청(POST·PUT)에 `X-Requested-With` 헤더를 붙이고, 2xx가 아니면 `ApiException`을 던집니다. 로그인이 풀렸다는 응답(401 `UNAUTHENTICATED`)은 `ApiSession.Expired`로도 알립니다. Unity 객체 없이 테스트합니다. |
| `ApiException`, `ApiErrorCodes` | 서버 오류 코드(예: `NICKNAME_TAKEN`)와 HTTP 상태. 응답을 받지 못하면 `NETWORK_ERROR`, 공통 오류 형식이 아니면(재배포 중 프록시 오류 페이지 등) `UNEXPECTED_RESPONSE`입니다. 화면이 문구를 그대로 보이므로 두 경우 모두 "서버에 연결하지 못했어요. 인터넷 연결을 확인하고 다시 시도해 주세요.", "서버가 잠시 응답하지 않아요. 잠시 후 다시 시도해 주세요."처럼 사용자가 읽을 문장으로 둡니다. |
| `ApiSession` | 서버가 로그인이 풀렸다고 알린 것을 화면에 전하는 이벤트(`Expired`). 로그인 세션은 서버 메모리에 있어 서버를 다시 배포하거나 오래 쉬면 사라집니다. 장소 화면의 안내 창([Onboarding](../Onboarding/README.md)의 `SessionExpiredView`)이 받아 처음 화면으로 돌려보냅니다. |
| `ApiJson` | 필드명은 camelCase, enum은 문자열이며 값이 없는 필드는 보내지 않습니다. enum의 서버 값은 `[EnumMember(Value = "IN_PROGRESS")]`로 적습니다. |
| `IApiTransport`, `UnityWebRequestTransport` | 실제 HTTP 전송. 테스트에서는 대체 구현으로 응답을 정합니다. |
| `ApiSettings` | 서버 주소와 응답 대기 시간(`Data/Api/ApiSettings.asset`). Web 빌드는 같은 출처라 상대 경로, Editor는 `http://localhost:8080`입니다. |
| `BrowserRedirect`, `AuthPaths`, `Plugins/WebGL/BrowserRedirect.jslib` | API 호출이 아니라 페이지 이동으로 여는 Google 로그인(AUTH-01)을 같은 탭에서 엽니다. 입구 화면과 장소의 게스트 가입 안내 창이 함께 씁니다. Editor는 브라우저 쿠키를 쓰지 않아 로그인 세션이 이어지지 않습니다. |
| `SurveyPrompt` | 사용자 설문 주소와 "설문을 권할 때" 신호(`Request`·`Requested`). 설문을 권하는 곳은 `SurveyPrompt.Request()`만 부르고, 장소 HUD의 설문 카드([Learning](../Learning/README.md)의 `SurveyPromptView`)가 받아 화면 위 가운데에서 내려옵니다. 지금은 대화를 다 쓴 게스트의 가입 안내 창이 부르고, 첫 호텔 퀘스트(`CHECK_IN`)를 끝낸 회원의 완료 창도 같은 신호를 쓰면 됩니다. 주소는 자리표시(`https://example.com/elsehere-survey`)라 운영 배포 전에 실제 주소로 바꿔야 합니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `com.unity.nuget.newtonsoft-json` | JSON 변환. 내장 `JsonUtility`는 null 값과 문자열 enum을 읽지 못합니다. |

## 사용 방법

```csharp
ApiClient client = apiSettings.CreateClient();
BootstrapResponse boot = await client.GetAsync<BootstrapResponse>("/api/v1/bootstrap");
```

- 로그인 상태는 세션 쿠키로 유지합니다. Web 빌드는 브라우저가, Editor는 UnityWebRequest가 쿠키를 저장하고 다시 보냅니다.
- 요청 DTO·응답 DTO는 각 기능 모듈에 두고, 공개 속성(`{ get; set; }`)으로 만듭니다. Newtonsoft가 리플렉션으로 읽으므로, Managed Stripping Level을 Minimal보다 높이면 DTO가 있는 어셈블리를 `link.xml`로 보존해야 합니다.
- 서버 오류 메시지(`ApiException.Message`)는 사용자에게 보여 줄 수 있는 문장입니다.

## 테스트

`Tests/EditMode/Api/`(`A305.Api.Tests`)에서 요청 주소·헤더·본문, 응답 해석, 오류·네트워크 실패와 그 문구, 로그인 끊김 알림(401 `UNAUTHENTICATED`만, 가입 대기·다른 거절은 아님)을 검증합니다.
