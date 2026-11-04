# 운영자 어드민

운영자가 게임 콘텐츠를 고치는 관리 화면입니다. 백엔드가 Thymeleaf로 화면을 그리고, 저장은 화면의 스크립트가 관리 API를 호출합니다. 지금은 **NPC 페르소나 편집**·**관리자 지정·해제** 화면과 화면 없는 **사물 단어 발음 만들기** API만 제공합니다. 장소·NPC 기본 정보·사물·어휘·미션 관리는 Jira 에픽 S15P31A305-287의 후속 스토리에서 다룹니다.

## 주소

| 구분 | 개발 | 운영 |
| --- | --- | --- |
| 어드민 화면 | `https://dev.elsehere.kr/admin/` | `https://elsehere.kr/admin/` |
| 관리 API | `/api/v1/admin/**` | `/api/v1/admin/**` |

게임과 같은 출처에서 제공하므로 게임의 Google 로그인 세션을 그대로 씁니다. 웹 컨테이너의 Nginx([web.conf](../infra/nginx/web.conf))가 `/admin`을 백엔드로 넘깁니다.

| 화면 | 주소 | 하는 일 |
| --- | --- | --- |
| NPC 목록 | `/admin/npcs` | NPC 코드·이름·역할 이름·장소를 보고 페르소나 화면으로 이동 |
| NPC 페르소나 | `/admin/npcs/{npcCode}` | 페르소나(role·facts·scene)를 고쳐 저장 |
| AI 사용량 | `/admin/ai-usage?days=7` | 최근 1~90일(기본 7일)의 AI 호출을 날짜·종류·모델·게스트/가입자·사용자별로 묶어 봄 |
| 관리자 | `/admin/admins` | 지금 관리자 목록을 보고, Google 이메일로 추가하거나 해제 |

| API | 하는 일 |
| --- | --- |
| `PUT /api/v1/admin/npcs/{npcCode}/persona` | 페르소나 저장. 본문 `{ "role", "facts", "scene" }`, 응답은 저장한 값 |
| `POST /api/v1/admin/admins` | 관리자 추가. 본문 `{ "email" }`, 응답은 추가한 사람 `{ "userId", "nickname", "email" }` |
| `DELETE /api/v1/admin/admins/{userId}` | 관리자 해제. `204` |
| `GET /api/v1/admin/pronunciations` | 사물 단어 발음 진행 상황 |
| `POST /api/v1/admin/pronunciations` | 발음 파일이 없는 사물 단어의 발음을 백그라운드에서 만들기 시작. `202`와 진행 상황 |

## 접근 권한

- `users.role`이 `ADMIN`인 사용자만 쓸 수 있습니다. 가입하면 항상 `USER`이고, 관리자만 다른 사람을 관리자로 지정할 수 있습니다. 스스로 관리자가 되는 기능은 없습니다.
- 로그인하지 않고 어드민 화면을 열면 Google 로그인으로 보냅니다. 로그인을 마치면 게임 화면으로 돌아가므로 `/admin/`을 다시 엽니다. 관리 API는 로그인하지 않았으면 `401 UNAUTHENTICATED`입니다.
- 관리자가 아니면 화면과 API 모두 `403 ADMIN_REQUIRED`입니다.
- 권한은 요청마다 DB에서 확인합니다. 관리자를 해제하면 다시 로그인하지 않아도 다음 요청부터 막힙니다.
- 저장 요청에도 게임 API와 같은 요청 위조 방지 헤더(`X-Requested-With`)를 붙입니다. 화면의 저장 버튼이 붙여 보냅니다.

### 관리자 지정·해제

관리자로 지정할 사람이 먼저 게임에 Google로 가입합니다. 관리자가 어드민의 **관리자** 화면(`/admin/admins`)에서 그 사람의 Google 이메일을 적어 추가하고, 목록의 [해제]로 해제합니다.

- 이메일은 대소문자를 가리지 않고 Google 가입자 중에서 찾습니다. 가입하지 않은 이메일이면 `404 ADMIN_CANDIDATE_NOT_FOUND`입니다. 게스트 계정은 지정하지 않습니다.
- 이미 관리자인 사람을 다시 추가해도 오류 없이 그대로 둡니다.
- 관리자가 한 명도 남지 않는 일을 막으려고 자기 자신은 해제할 수 없습니다(`409 ADMIN_SELF_REVOKE`). 목록에서 자기 자신에게는 [해제] 버튼이 없습니다. 두 관리자가 동시에 서로를 해제하는 경우까지는 막지 않으므로, 관리자가 모두 해제되면 아래 SQL로 다시 지정합니다.
- 지정·해제는 백엔드 로그 `admin_granted`·`admin_revoked`에 대상(`user_id`)과 실행한 관리자(`by_user_id`)의 사용자 ID로 남습니다. 누구를 왜 지정했는지는 Jira에 기록합니다.
- 개발·운영 DB는 따로이므로 환경마다 지정합니다.

관리자가 아직 없는 새 환경의 첫 관리자는 서버 관리 계정으로 해당 환경의 DB에 접속해 지정합니다.

```sql
-- 첫 관리자 지정
update users set role = 'ADMIN' where provider = 'GOOGLE' and email = '<관리자 Google 이메일>';
-- 현재 관리자 확인
select id, nickname, email from users where role = 'ADMIN';
```

## NPC 페르소나

페르소나는 대화할 때 AI 워커에 보내는 NPC 설명입니다([AI 워커 명세](api/ai-worker.md)의 `npc`). 영어로 적습니다.

| 칸 | 필수 | 최대 길이 | 내용 |
| --- | --- | --- | --- |
| `role` | O | 200자 | NPC가 누구인지 한 문장. 워커가 이름 뒤에 붙입니다(예: `Mia, the barista at the corner cafe.`) |
| `facts` | | 2,000자 | 가격, 파는 물건, 지켜야 할 규칙처럼 대화에서 지켜야 할 사실 |
| `scene` | | 300자 | NPC가 있는 곳의 설명 |

- 저장하면 다음 대화 요청부터 바로 반영됩니다. 진행 중인 대화도 다음 턴부터 바뀐 값을 씁니다.
- 앞뒤 공백은 지우고, 비운 칸은 빈 문자열로 저장합니다.
- 이름·역할 이름·장소·목소리·먼저 말 걸기 대사는 화면에 보여 주기만 합니다. 이 값은 시드(`world.sql`)가 관리합니다.
- 인격 카드([NPC 설정 v2 인격 구조](api/npc-ai-contracts/01_npc_configuration_spec_v2.md)의 성격·가치관·말투 등)는 이 화면에서 다루지 않습니다. 카드는 시드가 `content/npc-configurations.json`에서 넣습니다([백엔드 README](../backend/README.md#시드-데이터)).
- 인격 카드가 있는 NPC(행인 Ben·카페 Mia·호텔 Jake·드럭스토어 Alex·택시 기사 Joe·경찰관 Dan과 거리 매장 직원 9명: 우체국 Linda·식당 Rosa·은행 Henry·빨래방 Gus·꽃집 Lily·이발소 Tony·베이커리 Grace·오락실 Max·생활용품점 Nina, 교대를 나눠 맡는 카페 Zoe·Leo·호텔 Owen·Clara·Walter·Diego·드럭스토어 Hannah·Samuel·경찰서 Victor·Kevin·Tyler·행인 Ruby·Ethan·Maya·Helen·Frank·Arthur·Margaret, 쉬는 날 마을에서 지내는 소방관 Ray·Rachel·Andre)는 NPC 대답을 카드로 만들고, `role`은 무례·의도 판정, 번역, 힌트·추천 단어, 뜻 보기, 기억 카드처럼 NPC를 짧게 소개하는 곳에만 씁니다. `facts`·`scene`은 카드와 함께 대답에도 씁니다. 카드가 없는 NPC는 세 칸으로 대답합니다.

### 시드와의 관계

서버가 시작할 때 `WorldSeedLoader`가 `world.sql`을 실행합니다. NPC의 다른 값은 시드 값으로 갱신하지만 **페르소나는 NPC를 처음 넣을 때만** 시드 값을 씁니다. 그래서 어드민에서 고친 페르소나는 배포 뒤에도 남습니다.

- 새 NPC는 지금처럼 `world.sql`에 추가합니다. 처음 페르소나는 시드 값이 들어가고, 그 뒤로는 어드민에서 고칩니다.
- 이미 있는 NPC의 페르소나를 `world.sql`에서 고쳐도 반영되지 않습니다. 어드민에서 고칩니다.
- 새로 만든 DB(로컬 DB 초기화 등)에는 시드의 처음 값이 들어갑니다. 환경 사이에 페르소나를 옮기는 기능은 없습니다.

## AI 사용량

AI 호출 기록(`ai_requests`)을 묶어 보여 줍니다. 호출 한 번이 한 줄이며 사용자, 대화, 호출 종류, 상태, 모델, 입력·출력 토큰, 음성 길이가 남습니다.

- **기간:** 최근 1·7·30·90일 가운데 고릅니다. 1~90일을 벗어나면 `400 VALIDATION_FAILED`입니다.
- **묶는 기준:** 날짜(한국 날짜), 호출 종류, 모델, 게스트/가입자입니다. 토큰을 많이 쓴 사용자는 상위 10명까지 사용자 ID만 보여 줍니다(이메일·닉네임은 보여 주지 않음).
- **실패:** 워커 오류로 끝난 호출입니다. 모든 AI 호출은 실패해도 기록됩니다(`AiGateway`).
- **세지 않는 것:** 예약만 하고 끝내지 않은 기록(`RESERVED`), 모델을 부르지 않는 관계 갱신(`/v1/npc/relationship`)
- **모델 "없음":** 워커 오류로 끝난 호출과 음성 합성(TTS)은 모델과 토큰을 알리지 않아 "없음"으로 묶입니다.
- **금액:** 모델별 단가가 정해지지 않아 보여 주지 않습니다.

## 사물 단어 발음 만들기

사물 카드의 [발음 듣기](api/mvp-api.md)(WORLD-04)가 쓰는 WAV를 AI 워커 TTS(`af_heart`, 속도 1.0)로 만듭니다. 파일은 음성 볼륨의 `pronunciation/` 폴더에 `{wordKey}.wav`로 저장합니다([NPC 음성 저장 운영](npc-audio-storage.md)). 발음 파일이 없는 사물 단어는 카드에서 발음 버튼이 숨겨집니다(`hasPronunciation: false`).

- 발음 파일이 없는 단어만 만들므로 여러 번 실행해도 됩니다. 합성에 실패한 단어는 건너뛰고 결과의 `failedWordKeys`에 남기며, 다시 실행하면 그 단어만 다시 만듭니다.
- 한 번에 하나만 실행합니다. 진행 중에 다시 시작하면 새로 시작하지 않고 진행 상황만 돌려줍니다. 실행 상태와 마지막 결과는 서버 메모리에만 있어 서버를 다시 시작하면 사라집니다(만든 파일은 남습니다).
- 개발·운영 볼륨은 따로이므로 환경마다 실행합니다. 새 사물 단어를 Unity에서 추가해 배포한 뒤에도 다시 실행합니다.
- AI 워커에 TTS 모델이 준비되어 있어야 합니다. 준비되지 않았으면 모든 단어가 `failedWordKeys`에 남습니다.
- 합성 호출은 실행한 관리자의 `ai_requests`(`kind: TTS`)로 기록합니다.

화면은 따로 없습니다. 관리자로 로그인한 브라우저에서 어드민 화면(`/admin/`)을 열고 개발자 도구 콘솔에서 실행합니다.

```js
// 시작
await (await fetch("/api/v1/admin/pronunciations", {
  method: "POST", headers: { "X-Requested-With": "ElsehereAdmin" }
})).json();
// 진행 상황: { total, ready, missing, running, lastRun: { created, failedWordKeys } }
await (await fetch("/api/v1/admin/pronunciations")).json();
```

`running`이 `false`가 되고 `missing`이 0이면 끝난 것입니다.

## 화면을 바꿀 때

- 화면 템플릿은 `backend/src/main/resources/templates/admin/`에 있고, 공통 머리글·메뉴는 `layout.html`입니다.
- 화면은 조회만 하고, 상태를 바꾸는 요청은 관리 API를 부릅니다. HTML 폼을 바로 전송하면 요청 위조 방지 헤더가 없어 거절됩니다.
- 새 관리 API는 `/api/v1/admin/` 아래에 둡니다. 보안 설정이 이 경로와 `/admin/**`에 관리자 권한 검사를 적용합니다.
