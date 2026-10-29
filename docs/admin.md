# 운영자 어드민

운영자가 게임 콘텐츠를 고치는 관리 화면입니다. 백엔드가 Thymeleaf로 화면을 그리고, 저장은 화면의 스크립트가 관리 API를 호출합니다. 지금은 **NPC 페르소나 편집**만 제공합니다. 장소·NPC 기본 정보·사물·어휘·미션 관리는 Jira 에픽 S15P31A305-287의 후속 스토리에서 다룹니다.

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

| API | 하는 일 |
| --- | --- |
| `PUT /api/v1/admin/npcs/{npcCode}/persona` | 페르소나 저장. 본문 `{ "role", "facts", "scene" }`, 응답은 저장한 값 |

## 접근 권한

- `users.role`이 `ADMIN`인 사용자만 쓸 수 있습니다. 가입하면 항상 `USER`이고, 서비스 안에서 관리자를 지정하거나 스스로 관리자가 되는 기능은 없습니다.
- 로그인하지 않고 어드민 화면을 열면 Google 로그인으로 보냅니다. 로그인을 마치면 게임 화면으로 돌아가므로 `/admin/`을 다시 엽니다. 관리 API는 로그인하지 않았으면 `401 UNAUTHENTICATED`입니다.
- 관리자가 아니면 화면과 API 모두 `403 ADMIN_REQUIRED`입니다.
- 권한은 요청마다 DB에서 확인합니다. 관리자를 해제하면 다시 로그인하지 않아도 다음 요청부터 막힙니다.
- 저장 요청에도 게임 API와 같은 요청 위조 방지 헤더(`X-Requested-With`)를 붙입니다. 화면의 저장 버튼이 붙여 보냅니다.

### 관리자 지정·해제

관리자로 지정할 사람이 먼저 게임에 Google로 가입합니다. 서버 관리 계정으로 해당 환경의 DB에 접속해 이메일로 지정합니다. 게스트 계정은 지정하지 않습니다.

```sql
-- 지정
update users set role = 'ADMIN' where provider = 'GOOGLE' and email = '<관리자 Google 이메일>';
-- 해제
update users set role = 'USER' where provider = 'GOOGLE' and email = '<관리자 Google 이메일>';
-- 현재 관리자 확인
select id, nickname, email from users where role = 'ADMIN';
```

개발·운영 DB는 따로이므로 환경마다 지정합니다. 누구를 언제 지정했는지는 Jira에 기록합니다.

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
- 명세 제안안인 [NPC 설정 v2 인격 구조](api/npc-ai-contracts/01_npc_configuration_spec_v2.md)(성격·가치관·말투 등)는 AI 워커에 아직 연결되지 않아 이 화면에서 다루지 않습니다.

### 시드와의 관계

서버가 시작할 때 `WorldSeedLoader`가 `world.sql`을 실행합니다. NPC의 다른 값은 시드 값으로 갱신하지만 **페르소나는 NPC를 처음 넣을 때만** 시드 값을 씁니다. 그래서 어드민에서 고친 페르소나는 배포 뒤에도 남습니다.

- 새 NPC는 지금처럼 `world.sql`에 추가합니다. 처음 페르소나는 시드 값이 들어가고, 그 뒤로는 어드민에서 고칩니다.
- 이미 있는 NPC의 페르소나를 `world.sql`에서 고쳐도 반영되지 않습니다. 어드민에서 고칩니다.
- 새로 만든 DB(로컬 DB 초기화 등)에는 시드의 처음 값이 들어갑니다. 환경 사이에 페르소나를 옮기는 기능은 없습니다.

## 화면을 바꿀 때

- 화면 템플릿은 `backend/src/main/resources/templates/admin/`에 있고, 공통 머리글·메뉴는 `layout.html`입니다.
- 화면은 조회만 하고, 상태를 바꾸는 요청은 관리 API를 부릅니다. HTML 폼을 바로 전송하면 요청 위조 방지 헤더가 없어 거절됩니다.
- 새 관리 API는 `/api/v1/admin/` 아래에 둡니다. 보안 설정이 이 경로와 `/admin/**`에 관리자 권한 검사를 적용합니다.
