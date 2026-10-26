# Elsehere 마을 디자인 비교 v1

> 이미지·ZIP·검수 기록은 Git에서 제외한 로컬 자료다. 아래 `artifacts/...` 경로는 로컬 보관본을 가리킨다.

세 가지 미국 소도시 방향을 비교하기 위한 콘셉트 이미지다. 선택이 확정된 디자인은 아니다.

## 공통 비교 기준

- 집, 카페, 호텔, 상점이 있는 걸어서 이동하는 동네
- 비슷한 높이의 사선 시점과 낮 조명
- 리틀 멍 캐릭터 시트를 인물과 재질 참고로 사용
- 화면 밖으로 이어지는 길을 두어 추가 구역을 연결하는 방향 제안

## 시안

| 디자인 | 주요 형태와 배치 | 의도한 인상 |
| --- | --- | --- |
| 브릭 메인스트리트 v1 | 붙어 있는 벽돌 상점, 평지붕과 파라펫, 직선 거리와 교차로 | 상점 방문과 길찾기가 중심인 미국 소도시 중심가 |
| 포치 그린 v1 | 떨어진 목조 건물, 뾰족한 박공지붕, 포치, 잔디 공원 | 이웃과 마주치는 포근한 주거 마을 |
| 어도비 크로싱 v1 | 낮고 계단식인 평지붕, 흙빛 벽, 목재 회랑과 안뜰 | 햇볕과 그늘의 대비가 있는 미국 남서부 마을 |

추천 출발점은 브릭 메인스트리트다. 직선 거리와 교차로를 중심으로 길찾기, 카페 주문, 호텔 방문 상황을 연결하기에 배치 의도가 명확하다. 포치 그린은 주거 생활감, 어도비 크로싱은 지역적 개성을 비교하기 위한 독립 후보로 유지한다.

## 검토 범위

이미지는 건축 형태와 분위기, 배치 방향을 비교하는 시안이다. 캐릭터의 정확한 머리:몸 비율, 건물 치수, 모듈 접합, 보행 충돌, 길찾기, Unity 성능, Tripo 모델 변환 결과는 검증하지 않았다. 출력에는 나뭇잎·꽃·벽돌 등 세부 묘사가 많으므로 실제 제작 범위와 형태 단순화 수준은 선택 후 정해야 한다. 확장 가능한 길의 방향을 그린 것이며 실제 맵 확장 기능을 구현한 것은 아니다.

## 건축 참고

- [미국 국립공원관리청: Pueblo Revival](https://www.nps.gov/subjects/architecture/pueblo.htm): 어도비안의 평지붕, 파라펫, 둥글게 처리한 벽 가장자리, 돌출 목재 보 형태 참고.
- [미국 국립공원관리청: Main Street 재활용·보존 웨비나](https://www.nps.gov/media/video/view.htm?id=1ADE64EE-2933-435E-B221-BA5820DD89B0): 메인스트리트의 소규모 상점과 벽돌·쇼윈도 구성 참고.

이 자료들은 이번 시안의 건축 참고다. 과거 대화에서 찾아본 경관 사진을 복구한 것으로 간주하지 않는다.

## 출력과 제작 프롬프트

생성 방식: 내장 image_gen. 각 디자인마다 별도 1회 생성.
생성 당시 참고 이미지: artifacts/mascot/little-mung-v6/roster-a-with-female.png. 현재 캐릭터 제작 기준은 [리틀 멍 v7](../mascot/little-mung-v7/README.md)로 관리한다.
프롬프트 수치는 생성 지시이며 측정 결과가 아니다.

### 브릭 메인스트리트 v1

이미지: town-overview.png (`artifacts/village/brick-mainstreet-v1/town-overview.png`, 로컬 보관)

별도 프롬프트: [generation-prompt.txt](brick-mainstreet-v1/generation-prompt.txt)

```text
Use case: stylized-concept.
Create one polished landscape 3:2 concept image for Elsehere, a friendly 3D language-learning game set in a fictional American small town. This is an environment design exploration, not a finished gameplay screenshot.
Reference image role: ONLY use the attached character roster for the inhabitants' cartoon proportions, faces and smooth matte 3D finish. Do not reproduce its sheet layout, labels or white backdrop. Inhabitants have enormous rounded-square heads and very tiny bodies, head height about 2.5 times the body below the chin, simple little eyes, tiny narrow noses. Place 4-5 small inhabitants naturally near doors or on sidewalks; architecture remains the subject.
Consistent comparison setup: an elevated 35-degree three-quarter view, moderately orthographic, showing a compact walkable neighborhood about one block across. Clear soft daytime lighting, tactile but clean matte game-asset materials, simple beveled edges, readable silhouettes, no realistic surface noise or depth-of-field blur. Detailed enough to judge the town, restrained enough to imagine a small modular Unity asset kit. Doors are wide enough for the oversized heads. Buildings have believable usable entrances and connected pedestrian routes.
The program includes a HOME, a CAFE, a HOTEL and a GENERAL STORE; indicate the businesses with small tasteful English signs on their facades, not floating UI labels. Include two street continuations that reach the image edges so the neighborhood can expand. Fill the image with the setting; no floating island base or closed display diorama.

Design identity: BRICK MAINSTREET v1. A snug, cheerful American Midwestern main-street district with strong urban continuity. The unmistakable organizing shape is ONE LONG STRAIGHT COMMERCIAL STREET, running diagonally from lower left to upper right, with a short perpendicular side street. Closely adjoining two-story red brick and warm cream brick storefronts form a varied but coherent street wall along the far side. Different simple rectangular parapet heights and cornices, tall vertical sash windows above, large rectangular shop windows below, striped canvas awnings, recessed wood-and-glass doors. Avoid pitched storybook roofs on the commercial buildings.
Arrange the small cafe with a forest-green awning at the near end of the row, the general store next to it, and a modest two-story hotel anchoring the street corner with a restrained vertical HOTEL blade sign. Put a small detached clapboard home with a front stoop just along the side street, clearly visible. A small leafy pocket garden occupies a gap rather than dominating the plan.
American street cues are readable but not cluttered: asphalt roadway, restrained double-yellow center line, concrete sidewalks and curb ramps, a crosswalk at the intersection, two parallel parking bays, one small parked pickup, simple street signs and a fire hydrant. Wide safe-looking walkable sidewalks, minimal vehicles. Trees only in regularly spaced street pits; the brick street wall remains visually dominant.
Palette/materials: baked tomato-red brick, buttery cream masonry, bottle-green awnings, warm black metal, muted blue-gray asphalt. Stylized brick courses, no distressed photorealism. Mood is a friendly everyday downtown where one meets the barista and hotelier on foot.
Keep the architecture boxy with softened bevels; do not make bulbous roofs, medieval facades, cobbled resort plazas, European half-timber houses, a central circular courtyard, or a suburban village green.
Small unobtrusive bottom caption: "01 / BRICK MAINSTREET v1". No other presentation graphics.
```

### 포치 그린 v1

이미지: town-overview.png (`artifacts/village/porch-green-v1/town-overview.png`, 로컬 보관)

별도 프롬프트: [generation-prompt.txt](porch-green-v1/generation-prompt.txt)

```text
Use case: stylized-concept.
Create one polished landscape 3:2 concept image for Elsehere, a friendly 3D language-learning game set in a fictional American small town. This is an environment design exploration, not a finished gameplay screenshot.
Reference image role: ONLY use the attached character roster for the inhabitants' cartoon proportions, faces and smooth matte 3D finish. Do not reproduce its sheet layout, labels or white backdrop. Inhabitants have enormous rounded-square heads and very tiny bodies, head height about 2.5 times the body below the chin, simple little eyes, tiny narrow noses. Place 4-5 small inhabitants naturally near doors or on sidewalks; architecture remains the subject.
Consistent comparison setup: an elevated 35-degree three-quarter view, moderately orthographic, showing a compact walkable neighborhood about one block across. Clear soft daytime lighting, tactile but clean matte game-asset materials, simple beveled edges, readable silhouettes, no realistic surface noise or depth-of-field blur. Detailed enough to judge the town, restrained enough to imagine a small modular Unity asset kit. Doors are wide enough for the oversized heads. Buildings have believable usable entrances and connected pedestrian routes.
The program includes a HOME, a CAFE, a HOTEL and a GENERAL STORE; indicate the businesses with small tasteful English signs on their facades, not floating UI labels. Include two street continuations that reach the image edges so the neighborhood can expand. Fill the image with the setting; no floating island base or closed display diorama.

Design identity: PORCH GREEN v1. An airy New England-inspired American village organized around a SMALL TRIANGULAR GRASS COMMON, a very different silhouette and layout from a dense main street. Every principal building is FREESTANDING, with visible air gaps, gardens and deep front porches. A narrow asphalt street bends around two sides of the green and branches outward; broad plain concrete walking paths cross the grass. The triangular green, angled building placement and layered gabled roof skyline should be obvious at thumbnail size.
Show five charming but restrained timber buildings: a small pale-blue player's home in the foreground with a deep front porch and porch swing; a butter-yellow cafe with a broad covered veranda, small CAFE sign and two tables; a taller white clapboard village hotel with dark blue steep gables, simple dormers and long porch, small HOTEL sign; a sage-green general store with a generous glazed shopfront and hanging GENERAL STORE sign; one small neighboring cottage for context.
Architectural identity: horizontal painted clapboard boards, white square porch posts, open wooden porch railings, recognizable steep triangular gables, modest asymmetrical add-on wings, crisp dark-shingle roofs. Roofs are angular not rounded balloons. Simple modular doors and windows recur. Low picket fences and mailbox posts mark the home plots; a single broad deciduous tree and bench sit on the green. Gardens are lawns and restrained flowering shrubs, not dense fantasy landscaping.
American street cues: asphalt, curb, one short marked crossing, quiet parking pull-off, domestic mailboxes and porches. Only one small parked station wagon. The paths connect all four destinations visibly. The common is grass with a path, NOT a paved plaza, fountain courtyard or central gazebo.
Palette/materials: soft cornflower blue, pale yellow, sage, warm white painted timber, cool slate roofs, fresh grass and warm wood. The mood is spacious, neighborly, relaxed, very cute without inflated architecture.
Avoid attached red-brick shop rows, flat-roof adobe, a European medieval village, stone-paved resort, dramatic castle/church, excessive ornament.
Small unobtrusive bottom caption: "02 / PORCH GREEN v1". No other presentation graphics.
```

### 어도비 크로싱 v1

이미지: town-overview.png (`artifacts/village/adobe-crossing-v1/town-overview.png`, 로컬 보관)

별도 프롬프트: [generation-prompt.txt](adobe-crossing-v1/generation-prompt.txt)

```text
Use case: stylized-concept.
Create one polished landscape 3:2 concept image for Elsehere, a friendly 3D language-learning game set in a fictional American small town. This is an environment design exploration, not a finished gameplay screenshot.
Reference image role: ONLY use the attached character roster for the inhabitants' cartoon proportions, faces and smooth matte 3D finish. Do not reproduce its sheet layout, labels or white backdrop. Inhabitants have enormous rounded-square heads and very tiny bodies, head height about 2.5 times the body below the chin, simple little eyes, tiny narrow noses. Place 4-5 small inhabitants naturally near doors or on sidewalks; architecture remains the subject.
Consistent comparison setup: an elevated 35-degree three-quarter view, moderately orthographic, showing a compact walkable neighborhood about one block across. Clear soft daytime lighting, tactile but clean matte game-asset materials, simple beveled edges, readable silhouettes, no realistic surface noise or depth-of-field blur. Detailed enough to judge the town, restrained enough to imagine a small modular Unity asset kit. Doors are wide enough for the oversized heads. Buildings have believable usable entrances and connected pedestrian routes.
The program includes a HOME, a CAFE, a HOTEL and a GENERAL STORE; indicate the businesses with small tasteful English signs on their facades, not floating UI labels. Include two street continuations that reach the image edges so the neighborhood can expand. Fill the image with the setting; no floating island base or closed display diorama.

Design identity: ADOBE CROSSING v1. A welcoming present-day American Southwest small town inspired by Pueblo Revival building forms, arranged as an ASYMMETRIC L-SHAPED GROUP OF LOW FLAT-ROOF VOLUMES around a shaded side courtyard connected directly to a quiet street. This must have a radically different skyline and ground plan from pitched-roof lawn villages and dense brick main streets. Low horizontal terraced silhouettes, thick earth-colored walls, rounded parapet edges, deeply recessed rectangular windows and doors, short exposed round wooden roof beams (vigas), shaded timber portals and pergolas. No tall pitched roofs anywhere.
Compose a low terracotta cafe at left opening onto a timber-shaded patio; a sand-colored two-story hotel at the back with stepped roof levels, a shaded balcony and simple HOTEL sign; an ochre general store beside the street with turquoise door and modest GENERAL STORE sign; and a pale peach single-story home offset in the foreground with a tiny enclosed entry yard and blue gate. Connect the buildings through one shaded L-shaped pedestrian courtyard opening onto the sidewalk, plus small narrow passageways, so the plan is legible and modular.
A modern asphalt street runs beside the town and continues in two directions, with low concrete curbs, a marked crossing, one small parked contemporary car, a plain street sign and a simple post-mounted community noticeboard. Show normal inhabited America, not an ancient ruin, exotic fantasy, Mexican festival or Western film set. No tribal symbols, costumes, flags, sacred objects or theme-park props.
Landscape is sparse and intentional: decomposed-granite ground, a couple of drought-tolerant broad-canopy desert trees providing shade, small agave clusters, restrained clay planters. No lush lawn, no forest of giant cactus. Use a pale desert ridge far in the background, subordinate to the town.
Palette/materials: peach adobe plaster, cinnamon, sandy cream, restrained turquoise painted doors, dark warm timber, blue-gray asphalt. Smooth tactile stylized plaster, visible broad material shapes, no grainy photorealism. Friendly soft daytime light with beautiful readable shade under the portal. Architecture should feel quiet, sun-warmed, and unusually well suited to these chunky cute inhabitants.
Avoid red-brick rows, steep gables, European cobblestones, bulbous cottage roofs, a symmetrical central plaza and all generic medieval-fantasy motifs.
Small unobtrusive bottom caption: "03 / ADOBE CROSSING v1". No other presentation graphics.
```
