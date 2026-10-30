# 남자 Base의 행인 인스턴스

`LittleMung_Passerby_Male.prefab`은 `LittleMung_Base_Male_170.prefab`의 Prefab Variant입니다. 1.7m Base의 몸·얼굴 메시, Avatar, 표정·립싱크 연결과 이동 Animator를 상속합니다. 원본 Base는 편집하지 않습니다.

- 프리팹: `Assets/Prefabs/Characters/Mannequin/Npc/LittleMung_Passerby_Male.prefab`
- 외형 확인 씬: `Assets/Scenes/Sandbox/Mannequin/LittleMung_Passerby_Male_Outfit.unity`
- 상의: `Visual/Wardrobe/UpperSlot/Suit_Navy_Upper`
- 바지·신발 슬롯: `Visual/Wardrobe/PantsSlot`, `ShoesSlot`
- 머리 자리: Animator의 `Head` 본 아래 `HairSlot`

사용자가 생성한 상의·바지·머리의 맞춤 복사본을 장착합니다. 신발 자리는 비어 있습니다. 머리는 생성본의 앞머리에 두상 기준 두피 면과 뒤쪽 머리카락 덩어리를 구성한 외관 검수 후보입니다. 다른 캐릭터에서 임의로 잘라 낸 부품은 사용하지 않습니다. 상의는 사용자 생성 FBX의 앞섶·칼라·넥타이를 바탕으로 Base에 맞춘 별도 메시입니다. 목·손목·밑단의 막힌 면을 열고, 소매 굽힘 구간은 고리 형태의 면으로 다시 만들었습니다. 원본 FBX는 수정하지 않습니다. `PasserbyParts.json`은 이 상의의 장착 데이터이며 Base를 수정하는 데이터가 아닙니다.

새 머리는 `HairSlot` 아래에 붙여 위치·회전·크기를 맞춥니다. 바지와 신발이 스킨 메시라면 전용 슬롯에 두는 것 외에 Base의 bones/rootBone과 bindpose를 맞춰야 합니다. 파일을 드래그하는 것만으로 다른 골격의 스킨 메시가 자동 호환되지는 않습니다.

행인 행동은 기존 `Passerby`, `NpcIdentity`, 마을 행인 설정과 `CharacterAnimationDriver`를 사용합니다. 플레이어 입력·이동 컴포넌트는 이 Variant에서만 비활성화됩니다. 서버 NPC 코드는 기존 설계대로 씬 인스턴스에서 지정합니다. 배치할 때 `player`, `dialogue`, `offerView`를 해당 씬 객체로 연결해야 합니다. 검수 씬에서는 외형·표정만 확인하도록 행인 접근 행동을 비활성화했습니다. Production 씬에는 배치하지 않았습니다.

부모 Base 변경은 오버라이드하지 않은 몸·얼굴·표정·Animator 설정에 전파됩니다. 상의 메시와 가중치는 별도 에셋이므로 Base 체형 또는 본의 기준 자세를 바꾸면 착용 상태를 다시 검수해야 합니다. 모든 동작에서 옷 관통이 없다는 보장은 아닙니다.

상의 착용 시 `BodyUnderSuit` 머티리얼은 옷으로 덮인 몸통·팔과 바지가 덮는 하체 부위를 알파 마스크로 가립니다. 열린 소매 안쪽의 손목 피부와 바짓단 아래 발은 보존합니다. 하체 가림은 바지가 장착되어 있을 때만 적용합니다. Base 메시·UV·블렌드셰이프는 그대로 공유하며, 얼굴·손 영역의 UV는 보호합니다. 머티리얼을 원래 `MannequinSkin`으로 되돌리면 몸 전체가 다시 보입니다. 옷을 교체할 때는 가리는 부위도 새 옷에 맞게 갱신해야 합니다. 머티리얼의 피부 틴트 기능은 유지합니다.

## 현재 검수 범위

이 프리팹은 외형 검수용 후보입니다. Base 연결 검사는 의상의 정지 핏·변형 품질 합격을 뜻하지 않습니다. 정지 핏을 수정하고 동일한 Idle·Walk·Run 동작을 각각 24개 시점에서 렌더해 확인하는 후보입니다. 몸 숨김을 끄면 일부 동작의 어깨 안쪽·소매 경계에 작은 관통이 남습니다. 착용 머티리얼은 기존 몸 마스크를 사용합니다. 외관 승인이나 모든 동작의 무관통 보장은 아닙니다.

`restrict_upper_weights.py`는 기존 장착 JSON의 정점·면·UV를 유지하면서 재킷 영향 본을 몸통·목·어깨·위팔·아래팔로 제한합니다. 다리·손가락·머리 본에 의상이 끌려가는 문제를 방지하는 작업이며, 의상 형태 자체를 맞추는 기능은 아닙니다. Blender 백그라운드에서 `--source <장착 JSON> --measurement <Base 측정 JSON> --output <결과 폴더>`로 실행합니다. 원본 입력 경로와 출력 경로를 분리해야 합니다.

자동 렌더에서는 `PASSERBY_SHOW_BODY=1`로 몸 숨김을 끄고 실제 관통·핏을 확인할 수 있습니다. `PASSERBY_REST_ONLY=1`은 정지 방향 비교만 출력합니다. `PASSERBY_PANTS_FIT=1`은 상의·머리를 렌더에서 숨기고 몸 가림을 해제한 하체 확대 비교입니다. `PASSERBY_REST_ONLY=1`과 함께 사용해 허리·가랑이·밑단을 확인합니다. 이 옵션들은 저장된 프리팹·씬을 바꾸지 않습니다.


## 상의 편집과 가져오기

`Suit_Navy_Upper_Fitted.blend`는 완성 후보 상의와 정적 Base 참고 메시를 담습니다. 상의의 UV와 본별 가중치가 저장되어 있으며, 참고 몸은 원본과 분리된 복사본입니다. 이 파일 자체에 Base의 얼굴 블렌드셰이프나 Animator를 복제하지 않습니다. Unity의 NPC Variant가 부모 Base에서 이 기능을 상속합니다.

Blender 수정 후에는 백그라운드에서 `export_fitted_upper.py --blend <복사본.blend> --output <별도 결과.json>`을 실행합니다. 결과를 확인한 뒤 `PasserbyParts.json`으로 반영하고 Unity의 `A305.EditorTools.Characters.PasserbyOutfitBuilder.UpdateUpperFit`을 실행합니다. 이 Builder는 상의 메시만 갱신하며 Base·Avatar·표정 에셋을 저장하지 않습니다. 원본 입력 FBX를 다시 생성할 필요가 없고 API를 호출하지 않습니다.

`PASSERBY_REVIEW_OUTPUT`을 지정해 `A305.EditorTools.Characters.PasserbyOutfitFitSamples.Export`를 실행하면 복사본의 정지 자세와 Idle·Walk·Run 각 12시점의 몸·옷 좌표 및 본 변환을 JSON으로 내보냅니다. 이는 오프라인 맞춤 작업용 측정 자료이며 자동 합격 판정이나 런타임 보정 기능이 아닙니다.

상의 형태·면 구성·가중치는 시각 에셋 작업이므로 렌더 비교로 검수합니다. `PasserbyOutfitTests`와 `MaleBaseVariantTests`는 기존 Base 공유, Avatar·표정 연결, 정상화한 가중치와 허용 본 범위를 확인합니다. 이 테스트 통과를 옷의 핏 승인으로 취급하지 않습니다.

## 머리·바지 장착 복사본

`HairPantsParts.json`은 사용자 생성 부품을 1.7m Base에 맞춘 정점·UV·면·가중치 자료입니다. `PasserbyOutfitBuilder.ApplyHairAndPants`는 이 자료를 기존 Variant의 `HairSlot`, `PantsSlot`에 연결합니다. Base와 입력 FBX, 얼굴 메시, Avatar와 Animator는 저장하지 않습니다. 다시 실행하면 해당 두 부품의 맞춤 복사본을 갱신하므로 수동 편집한 결과는 별도 복사본으로 보관해야 합니다.

- `Hair_SidePart_Brown_Fitted.blend`: 생성된 앞머리를 활용하고 두상에 맞는 두피 면과 뒤쪽 머리카락 덩어리를 구성한 편집 복사본입니다. Head 본에만 연결됩니다. 원본의 후면 정보가 부족하여 만든 후보이며, 참고 이미지와 외관이 같은지 별도 확인해야 합니다.
- `Suit_Navy_Pants_Fitted.blend`: 생성 바지를 Base의 허리·엉덩이·무릎·발목 위치에 맞춘 편집 복사본입니다. 골반과 양쪽 위·아래 다리의 연속 가중치를 사용하며 손·발가락·머리 본의 영향을 받지 않습니다.
- 두 Blender 파일의 `Base170Reference`는 편집용 참고 복사본입니다. 표정과 Animator가 들어 있는 새로운 Base가 아닙니다. 실제 표정·립싱크·애니메이션은 Unity Variant가 기존 Base에서 상속합니다.

메시를 갱신할 때는 Mesh API로 정점·면·UV·본 가중치·bindpose를 함께 교체하고 기존 에셋 GUID를 유지합니다. `CopySerialized`만으로 네이티브 정점 버퍼 갱신을 가정하지 않습니다. 저장된 메시의 정점 수·면 인덱스·장착 위치를 JSON과 대조하는 회귀 검사를 포함합니다.

`PASSERBY_REVIEW_YAW=90`은 측면 동작 렌더입니다. 기본값은 정면이며, 동작 중 어깨 방향을 기준으로 검수 카메라가 정면을 유지합니다. 동작 클립이나 관절 각도를 줄여서 의상 문제를 숨기지 않습니다. 렌더는 외관 검수 자료이며 발 접지 물리나 모든 동작의 충돌을 보증하지 않습니다.
