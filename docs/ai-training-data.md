# AI 개선 데이터 활용 기준

배포 서버에서 플레이한 사용자의 대화 기록과 녹음 음성을 AI 개선(무례 판정·음성 인식 등의 평가와 학습)에 쓰는 기준입니다. 동의한 사용자의 데이터만, 동의한 뒤의 데이터만 씁니다.

## 동의 항목

가입 화면(AUTH-03·04)에 선택 항목 두 개를 둡니다. 체크하지 않아도 서비스는 그대로 쓸 수 있으며, 가입 뒤에도 USER-02·03으로 켜고 끕니다.

| 항목 | 화면 제목 | 활용 범위 |
| --- | --- | --- |
| `TRAINING_TEXT` | AI 개선에 내 대화 기록 활용 | NPC와 나눈 대화 문장(보낸 문장, 음성 인식 원문, NPC 대사, 무례 판정 결과) |
| `TRAINING_VOICE` | AI 개선에 내 녹음 음성 활용 | 음성 입력 녹음 파일과 그 인식 결과 |

- 두 항목은 서로 따로 켜고 끕니다. 녹음은 인식 결과와 짝으로 쓰므로 대화 기록 동의가 없어도 쓸 수 있습니다.
- 게스트는 동의 기록이 없으므로 활용 대상이 아닙니다. 게스트에게는 동의를 받지 않습니다.
- 동의 버전은 `2026-10`입니다. 아래 안내 문구를 바꾸면 [ConsentCatalog](../backend/src/main/java/com/elsehere/auth/service/ConsentCatalog.java)의 버전을 올립니다. 옛 버전에 동의한 사용자는 다시 동의할 때까지 동의하지 않은 것으로 봅니다.

## 안내 문구 (버전 2026-10)

약관 문서 주소(`/ai-training/2026-10`)에 보여 줄 내용입니다. 약관 페이지를 제공하는 방식은 아직 정하지 않았습니다 `[미정]`.

> **AI 개선을 위한 데이터 활용 (선택)**
>
> 동의하지 않아도 서비스는 그대로 이용할 수 있습니다. 만 14세 이상만 가입할 수 있습니다.
>
> - **대화 기록 활용**: 동의한 뒤에 시작한 NPC 대화의 문장(보낸 문장, 음성 인식 결과, NPC 대사, 무례 판정 결과)을 무례 판정·대화 품질 같은 AI 기능의 평가와 학습에 사용합니다.
> - **녹음 음성 활용**: 동의한 뒤의 음성 입력 녹음과 그 인식 결과를 저장해 음성 인식 품질의 평가와 학습에 사용합니다. 동의하지 않으면 녹음은 인식 후 저장하지 않습니다.
> - **함께 쓰지 않는 정보**: 이메일, 닉네임은 활용 데이터에 넣지 않습니다.
> - **보관**: 프로젝트 종료 후 파기합니다.
> - **철회**: 언제든 동의를 끌 수 있습니다. 녹음 음성 동의를 끄면 저장된 녹음을 바로 지웁니다. 대화 기록 동의를 끄면 그 뒤로는 활용 데이터에서 뺍니다.

## 저장과 철회

- 대화 기록은 서비스 기능(대화 기록, 피드백, NPC 기억)에 필요해 동의와 관계없이 `turns`에 저장됩니다. 학습에는 내보내기 기준을 통과한 것만 씁니다.
- 녹음은 `TRAINING_VOICE`에 동의한 사용자의 음성 인식(CONV-02)이 성공했을 때만 `training_recordings`와 학습용 녹음 폴더에 저장합니다. 무음·인식 실패는 저장하지 않습니다. 저장 위치는 [NPC 음성 파일 저장 운영](npc-audio-storage.md)의 `training-recordings/` 폴더입니다.
- 철회하면 `user_consents.withdrawn_at`에 시각을 남깁니다. 녹음 음성 동의를 철회하면 그 사용자의 녹음 파일과 기록을 바로 지웁니다.
- 프로젝트 종료 후 파기는 자동 정리가 아니라 운영 절차로 합니다. `training_recordings`와 학습용 녹음 폴더를 지우고, 내려받은 내보내기 파일도 함께 지웁니다.

## 내보내기

학습·평가에는 [ai/tools/training_export](../ai/tools/training_export/)의 SQL로 꺼낸 데이터만 씁니다. 결과는 JSON 한 줄이 한 행이며, 사용자는 서비스 내부 ID(`user_id`)로만 구분합니다. 사람 단위로 개발·평가를 나눌 때 이 ID를 씁니다.

| 파일 | 담는 데이터 |
| --- | --- |
| `text.sql` | 지금 버전의 `TRAINING_TEXT`에 동의하고 철회하지 않은 사용자의, 동의한 시각 이후에 시작한 대화의 턴 |
| `recordings.sql` | 지금 버전의 `TRAINING_VOICE`에 동의하고 철회하지 않은 사용자의 녹음 기록. 파일은 `audio_key` 이름으로 학습용 녹음 폴더에 있습니다. |

서버 관리 계정으로 DB 컨테이너에서 실행합니다. 개발 환경은 `a305-dev`, 운영 환경은 `a305-prod` 프로젝트 이름을 씁니다.

```bash
sudo docker exec -i a305-dev-database-1 sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At -v version=2026-10' < ai/tools/training_export/text.sql > training_text.jsonl
sudo docker exec -i a305-dev-database-1 sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At -v version=2026-10' < ai/tools/training_export/recordings.sql > training_recordings.jsonl
```

- `version`은 ConsentCatalog의 현재 버전과 같게 줍니다.
- 녹음 파일은 `training_recordings.jsonl`의 `audio_key`에 해당하는 파일만 백엔드 컨테이너의 `/app/data/npc-audio/training-recordings/`에서 복사합니다. 내보낸 뒤에 철회한 사용자가 있으면 다음 내보내기 결과로 다시 맞춥니다.
- 내보낸 파일은 Git, Jira, MR, 메신저에 올리지 않습니다. 문장 원문을 로그나 보고에 옮기지 않고 집계로만 공유합니다.
