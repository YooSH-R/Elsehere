# Theme 모듈

화면 디자인은 **글래스 팝** 하나만 씁니다. 테마를 고르는 화면과 메뉴는 없습니다. 모든 기능 화면은 `ThemedElement` 역할(창·카드·입력칸·알약 버튼·아이콘 버튼·배지·메뉴 줄·제목·본문 등)로 칠하므로, 디자인 정의 하나를 바꾸면 전체 화면에 함께 적용됩니다.

- **유리:** 창·카드·입력칸은 뒤 화면을 흐리게 비추는 반투명 유리(`A305/UI/Glass`)로 그립니다. 위쪽 하이라이트와 가장자리 빛, 아래로 번지는 부드러운 그림자가 있습니다.
- **입체 버튼:** 버튼·배지는 입체 재질(`A305/UI/Pop`)로 그립니다. 위가 밝은 그라데이션 앞면, 윗면 반사광, 아래 두께, 바닥 그림자가 있습니다. 버튼 쓰임 색만 바꾸면 노랑·코랄·하늘·초록 버튼이 됩니다.
- **글꼴:** 제목·버튼 이름·배지·메뉴 줄은 둥근 제목 글꼴 **Jua**, 본문은 읽기 쉬운 **나눔스퀘어라운드**를 씁니다. 두 글꼴에 없는 글자는 Pretendard로 대신 그립니다.
- **색:** 기본 흰색, 주 노랑 `#FFC23D`, 보조 코랄 `#FF7759`, 강조 하늘 `#45A8F0`, 완료 초록 `#4CB96B`
- **어두운 바탕:** 대화창처럼 장면 위에 바로 놓이는 화면은 상자 대신 아래에서 위로 옅어지는 어두운 그라데이션(`Scrim`, 색 `scrimColor`)을 깔고, 그 위 조작은 반투명한 흰 테두리 버튼·입력칸(`GhostControl`·`GhostInput`), 글자는 흰색(`TitleOnDark`·`BodyOnDark`)과 노랑 안내(`AccentOnDark`)로 씁니다. 그라데이션은 위쪽이 투명해 장면 클릭을 막지 않습니다. 반투명 버튼을 다음에 누를 곳으로 고르면(`selection`) 노랑 입체 버튼으로 바뀝니다.

**규격**
- **입력칸 높이:** 같은 줄에 놓인 입력칸과 버튼은 높이를 하나로 맞춥니다. 화면을 칠할 때(`UiThemeBuilder.Style`) 입력칸 높이를 같은 줄에서 가장 높은 버튼에 맞춥니다.
- **아이콘과 이름:** 알약 버튼의 아이콘과 이름은 한 묶음으로 버튼 가운데에 둡니다(`PillContentLayout`). 아이콘은 글자 크기에 맞추고 이름과 10px 간격을 둡니다.
- **아이콘을 두는 버튼:** 아이콘은 높이 64px 이상인 큰 주요 버튼에만 두고, 그보다 낮은 버튼은 이름만 둡니다. 이름을 숨긴 둥근 버튼(대화창 [보내기])은 아이콘만 가운데에 둡니다.

`ThemedElement`의 `quiet`는 보조 조작의 바탕을 숨기고, `selection`은 고른 카드·목록 줄을 주 색 쪽으로 칠합니다.

## 흐린 뒤 화면

유리 창은 `UiBackdropBlurFeature`가 만든 흐린 화면을 비춥니다. 이 렌더러 기능은 매 프레임 다음 순서로 동작합니다.

1. 게임 카메라가 반투명 물체를 그리기 전 화면을 1/4로 줄여 복사합니다.
2. 가로·세로 가우시안 흐림을 두 번 겁니다.
3. 결과를 전역 텍스처 `_A305UiBackdrop`에 둡니다.

화면 위 UI는 카메라가 다 그린 뒤에 그려지므로 같은 프레임의 흐린 화면을 씁니다. UI 자신은 흐림에 섞이지 않습니다. 이 기능은 웹이 쓰는 모바일 렌더러(`Settings/Mobile_Renderer.asset`)와 PC 렌더러에 붙어 있습니다. 기능이 없으면 유리는 이미지 색을 진하게 칠해 글자가 읽히게 합니다.

입구 화면처럼 3D 공간이 없는 씬에서는 카메라 배경색이 유리에 비칩니다. 그래서 입구 카메라를 화면 배경과 같은 크림색으로 칠합니다(`OnboardingBuilder.PaintCamera`).

## 그림과 여백

유리·버튼 그림은 셰이더가 읽는 채널로 그립니다.

| 그림 | R | G | B | A |
| --- | --- | --- | --- | --- |
| 유리(`Panel`·`Card`·`Input`) | 유리 모양 | 위쪽 하이라이트·가장자리 빛 | — | 모양 + 그림자 |
| 버튼(`Button`·`Pill`) | 앞면 모양 | 앞면 위아래 위치(위 1) | 두께 | 모양 + 그림자 |

어두운 바탕용 그림(`Scrim`·`Ghost`)은 재질 없이 그대로 그리는 흰 그림이며, 투명도에 모양을 담고 색은 이미지 색으로 곱합니다.

그림에는 그림자를 그리는 바깥 여백이 있습니다. `SurfaceBleed`가 그 여백만큼 그리는 면을 바깥으로 넓혀, 보이는 유리·버튼 모양은 칸 경계에 맞고 그림자는 칸 밖으로 번집니다. 9칸 나눔의 테두리 폭은 그대로이고, 누르는 영역도 칸 그대로입니다. 여백 값은 디자인 정의의 `panelBleed`·`cardBleed`·`inputBleed`·`buttonBleed`·`pillBleed`에 두고, 그림을 그린 값과 같게 Builder가 맞춥니다. 버튼 그림은 위아래 여백이 같아 버튼 이름이 앞면 가운데에 옵니다.

## Builder

- **Tools → A305 → UI → Build Glass Pop Design**(`GlassPopBuilder`)이 다음을 차례로 합니다.
  - 그림·재질·디자인 정의(`Data/Theme/GlassPop.asset`)를 준비합니다. 그림·재질은 없을 때만 만들고, 색은 처음 만들 때만 넣어 Inspector에서 고친 값을 유지합니다.
  - 테마 목록을 디자인 하나로 맞춥니다.
  - 흐린 뒤 화면 기능을 두 렌더러에 붙입니다.
  - 테마 역할이 있는 프리팹을 다시 칠해 저장합니다.
  - 입구 카메라 배경색을 맞춥니다.
- **Tools → A305 → UI → Build Themes**는 아이콘과 디자인 데이터를 준비하고 기존 화면 프리팹에 테마 역할을 붙입니다. `SpaceSceneSetup`도 같은 준비를 하므로 공간을 다시 구성해도 디자인이 남습니다. 집 씬에 예전 테마 고르기 화면이 남아 있으면 지웁니다.
- **Tools → A305 → UI → Apply HUD Menu**는 HUD 버튼을 오른쪽 위 메뉴(≡)로 묶습니다. 메뉴에는 [테마]를 두지 않습니다.
- **Tools → A305 → UI → Apply Unified Dialogs**는 대화 제안 창, 로그인·가입 화면, 피드백 목록·내 정보·로그아웃·탈퇴 확인 창을 같은 규칙으로 칠합니다.
- **Tools → A305 → UI → Apply Vocabulary Cards**는 단어장과 사물 단어 카드 프리팹만 갱신합니다.
- **Tools → A305 → UI → Apply Service Font**는 본문 글꼴과 기존 UI 프리팹의 글꼴을 맞춥니다. 원본은 네이버 공식 배포본이며, `Assets/ThirdParty/NanumSquareRound/LICENSE.txt`에 저작권 안내와 SIL OFL 1.1 전문이 있습니다.
- **Tools → A305 → UI → Apply Readable Text Spacing**은 프리팹을 건드리지 않고 두 설정만 다시 맞춥니다.
  - 한글 여러 줄이 맞닿아 보이지 않게 줄 높이를 글자 크기의 1.4배로 둡니다.
  - 단어 중간이 아니라 띄어쓰기에서 줄을 바꾸는 한글 줄바꿈 규칙을 켭니다.

서버에는 계정 화면 테마 값(`user.uiTheme`, USER-01)이 남아 있지만, 화면은 어떤 값이 와도 같은 디자인으로 그립니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `UiThemeId`, `UiThemeCodes` | 서버가 보내는 테마 코드(JELLY·STICKER·CLOUD)를 읽습니다. 테마 목록은 디자인 하나라 어떤 코드여도 같은 디자인입니다. |
| `UiThemeSelection` | 지금 적용할 테마와 바뀜 알림입니다. 씬을 옮겨도 유지되도록 `Shared` 하나를 함께 씁니다. |
| `UiThemeDefinition`, `UiThemeCatalog` | 디자인의 글꼴(본문·제목)·글자색, 유리·입체 재질, 창과 버튼 그림과 바깥 여백, 버튼 쓰임별 색, 지도 색과 테마 목록입니다. |
| `UiIconSet`, `UiIcon` | 버튼 아이콘 그림 묶음입니다. |
| `ThemedElement` | 화면 요소 하나를 역할에 맞는 그림·색·재질·글꼴로 칠합니다. (EditMode `ThemedElementTests`, `GlassPopThemeTests`) |
| `PillContentLayout` | 알약 버튼의 아이콘과 이름을 한 묶음으로 가운데에 두고, 낮은 버튼은 이름만 둡니다. (EditMode `PillContentLayoutTests`) |
| `SurfaceBleed` | 그림의 그림자 여백만큼 그리는 면을 넓혀 보이는 모양을 칸에 맞춥니다. (EditMode `SurfaceBleedTests`) |
| `UiBackdropBlurFeature` | 유리가 비출 흐린 뒤 화면을 만드는 URP 렌더러 기능입니다. |
| `ThemedTownMap`, `TownMapColors` | 실제 마을 배치를 바탕으로 그린 퀘스트 지도의 길·장소 카드·나무 아이콘과 목적지 표시를 디자인 색에 맞춥니다. |

## 에셋 위치

| 대상 | 위치 |
| --- | --- |
| 디자인 정의, 아이콘 묶음, 테마 목록 | `Assets/Data/Theme/` |
| 유리·버튼 그림, 재질, 셰이더 | `Assets/Art/UI/Theme/GlassPop/` |
| 아이콘 그림 | `Assets/Art/UI/Theme/Icons/` |
| 본문·제목 글꼴 에셋 | `Assets/Art/UI/Fonts/NanumSquareRound SDF.asset`, `Jua-Regular SDF.asset` (원본은 `Assets/ThirdParty/`) |

## 의존성

| 대상 | 용도 |
| --- | --- |
| uGUI, TextMeshPro | 화면 요소 칠하기 |
| URP | 흐린 뒤 화면을 만드는 렌더러 기능 |
