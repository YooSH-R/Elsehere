# 카페 여자 감정 표정 제작 원본

`LittleMung_Cafe_Female_Emotions.blend`는 카페 여자 원본의 복사본입니다. 기존 입모양과 리깅을 유지하고 Base의 독립된 눈·눈꺼풀을 연결해 눈동자를 압축하지 않는 표정을 만듭니다. `CafeFemaleEmotions.json`은 Unity Builder에 전달하는 정점·법선 변형과 눈 부품 데이터입니다.

## 다시 제작

Blender의 백그라운드 Python에서 실행합니다. 외부 Python 패키지나 네트워크는 사용하지 않습니다.

```powershell
& '<Blender 설치 폴더>/blender.exe' --background --factory-startup --python-exit-code 1 --python frontend/ArtSources/Characters/CafeFemaleEmotion/author_emotions.py -- --output '<새 출력 폴더>'
```

출력 폴더에 같은 제작 파일이 있으면 중단합니다. 생성된 `.blend`, JSON, PNG를 검수한 후 이 폴더의 제작 복사본으로 옮깁니다. 원본 `CafeFemaleFace/`는 변경하지 않습니다. Unity 에셋은 `CafeEmotionBuilder`로 생성하며 자세한 사용법은 [말하기 표정 검수](../../../../docs/unity/npc-speaking-emotions.md)에 있습니다.

`BaseEyes/`는 이 카페 얼굴에 맞춘 Base 눈의 고정 입력입니다. `Surface.json`은 원본 카페 표면, `EyeFit.json`은 좌우 눈 중심·반경·기존 초록색 홍채 기준, `Blink.json`과 `.blend`는 `ServiceNpcs/create_business_eyelids.py`로 만든 눈·눈꺼풀·접합 면입니다. 기존 입의 열린 경계를 보존하며 좌우 접합 경계가 모두 연결됐는지 `BlinkAudit.json`에 기록합니다.

`author_emotions.py`는 카페 얼굴에서 측정한 좌표를 사용합니다. 다른 NPC는 기준점을 다시 맞춰야 합니다. 25·50·75·90·100%의 웃는 눈 변형은 눈 앞면을 따라가며 바깥 접합 링은 움직이지 않습니다. Blender의 중간 키는 제작 확인용이고 Unity에서는 하나의 다중 프레임 Blend Shape로 가져옵니다. 발음과 깜박임·감정의 실제 혼합은 Unity에서 확인해야 합니다.

자동 검사는 눈 부품의 접합과 몸 메시·골격 보존을 확인할 수 있지만, 귀여운 인상이나 미세한 주름을 대신 판단하지 않습니다. 정면·양쪽 사선 및 말하기 영상으로 외형을 검수합니다.

눈썹 변형은 눈꺼풀 쪽에서 완만하게 줄이고 머리카락 경계 아래에서 제한합니다. 입꼬리는 가운데가 꺾이지 않는 이차 곡선으로 만들며, 몸·입술·눈 접합 면에 동일한 변위장을 적용합니다. 검수 복사본의 미소 채널은 새 곡선으로 교체하지만 원본 모델과 발음별 변형은 보존합니다.

`eyeSquintLeft/Right`는 `eyeBlink`에 더하는 웃는 눈 곡선 보정입니다. Blender에서 웃는 눈을 보려면 해당 `eyeBlink`와 `eyeSquint`를 함께 적용합니다. 부분 깜박임에서는 닫힘을 유지하고 곡선만 줄이며, Unity 재생기가 이를 계산합니다.

`mouthLaughOpen`은 `viseme_AA`에 더하는 폭소용 입 벌림 보정입니다. Blender에서는 두 키를 함께 적용합니다. 입을 축별로 늘리는 대신 폭소용 곡선으로 입술·구강을 만들고 위 치아·혀를 배치합니다. `laugh_geometry.py`는 들쭉날쭉한 입 접합선을 둥글게 옮기고, 인접한 볼 정점에 변위를 분산합니다. 얼굴의 측정 곡면에 맞춰 깊이와 음영을 연결하고 입술 바깥 경계는 얼굴 변의 보간점에 맞춥니다. 이 변형은 폭소 채널에만 들어가며 중립 정점, 기존 미소·다른 감정·9개 발음 키는 보존합니다.
