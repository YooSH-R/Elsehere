# Theme 모듈

플레이어는 처음 시작하는 집에서 화면 테마를 고릅니다. 테마는 **말랑 젤리**(기본)·**스티커 팝**·**구름 유리** 세 가지이며, 메인 화면의 지도·퀘스트 수첩·단어장, 대화창, 피드백, 단어장 창, 튜토리얼 안내, 사물 단어 카드에 함께 적용됩니다. 버튼은 글자 대신 이모티콘 느낌의 아이콘과 아래 작은 이름으로 보여 주고, 긴 이름이 필요한 버튼만 아이콘이 붙은 알약 모양을 씁니다. 제목·버튼 이름 글꼴은 테마마다 다르고(Jua·Do Hyeon·Sunflower), 긴 본문은 모든 테마가 Pretendard를 씁니다.

고른 테마는 바로 화면에 적용하고 계정에 저장합니다(`PUT /api/v1/me/ui-theme`). 접속할 때 `GET /api/v1/bootstrap`의 `user.uiTheme`을 읽어 같은 테마로 시작합니다. 저장하지 못하면 이전 테마로 되돌리고 안내를 보입니다. 계약은 [MVP API 명세](../../../../../docs/api/mvp-api.md)의 USER-01을 따릅니다.

**Tools → A305 → UI → Build Themes**가 제목 글꼴·아이콘·창과 버튼 그림·테마 데이터를 준비하고, 기존 화면 프리팹에 테마 역할을 붙인 뒤 집 씬에 테마 고르기 화면을 둡니다. 그림과 테마 데이터는 없을 때만 만들어, 디자인한 그림이나 Inspector에서 고친 색을 덮어쓰지 않습니다. `SpaceSceneSetup`도 같은 준비와 연결을 하므로 공간을 다시 구성해도 테마가 남습니다. **Tools → A305 → UI → Capture Theme Previews**는 세 테마로 집·마을·대화·피드백 화면을 그려 `THEME_PREVIEW_OUTPUT` 폴더에 저장합니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `UiThemeId`, `UiThemeCodes` | 테마 종류와 서버 코드(JELLY·STICKER·CLOUD). 모르는 코드는 말랑 젤리로 봅니다. |
| `UiThemeSelection` | 지금 적용할 테마와 바뀜 알림. 씬을 옮겨도 유지되도록 `Shared` 하나를 함께 씁니다. |
| `ThemePickerFlow`, `UiThemeApi` | 고른 테마를 바로 적용하고 계정에 저장합니다. 저장하지 못하면 되돌립니다. Unity 객체 없이 테스트합니다. |
| `UiThemeDefinition`, `UiThemeCatalog` | 테마 하나의 제목 글꼴·글자색·창과 버튼 그림·버튼 쓰임별 색, 그리고 테마 목록. |
| `UiIconSet`, `UiIcon` | 버튼 아이콘 그림 묶음. 스티커 팝만 남색 테두리를 더한 묶음을 씁니다. |
| `ThemedElement` | 화면 요소 하나를 역할(창·카드·아이콘 버튼·알약 버튼·배지·입력칸·제목·본문 등)에 맞게 칠하고 테마가 바뀌면 다시 칠합니다. |
| `ThemedTownMap`, `TownMapColors` | 실제 마을 배치를 바탕으로 그린 퀘스트 지도의 길·장소 카드·나무 아이콘과 목적지 표시를 선택한 테마에 맞춥니다. |
| `ThemePickerView` | 집에서만 보이는 팔레트 버튼과 테마 고르기 창. 각 카드는 그 테마로만 칠한 미리보기를 보여 줍니다. |

## 에셋 위치

| 대상 | 위치 |
| --- | --- |
| 테마 데이터, 아이콘 묶음, 테마 목록 | `Assets/Data/Theme/` |
| 아이콘·창·버튼 그림 | `Assets/Art/UI/Theme/` |
| 제목 글꼴 에셋 | `Assets/Art/UI/Fonts/` (원본은 `Assets/ThirdParty/{Jua,DoHyeon,Sunflower}/`) |
| 테마 고르기 화면 | `Assets/Prefabs/Theme/ThemePicker.prefab` |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Api` | 테마 저장 요청 |
| uGUI, TextMeshPro | 화면 요소 칠하기와 테마 고르기 화면 |
