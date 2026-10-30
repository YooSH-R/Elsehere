# 남자 Base의 행인 인스턴스

`LittleMung_Passerby_Male.prefab`은 `LittleMung_Base_Male_170.prefab`의 Prefab Variant입니다. 1.7m Base의 몸·얼굴 메시, Avatar, 표정·립싱크 연결과 이동 Animator를 상속합니다. 원본 Base는 편집하지 않습니다.

- 프리팹: `Assets/Prefabs/Characters/Mannequin/Npc/LittleMung_Passerby_Male.prefab`
- 외형 확인 씬: `Assets/Scenes/Sandbox/Mannequin/LittleMung_Passerby_Male_Outfit.unity`
- 상의: `Visual/Wardrobe/UpperSlot/Suit_Navy_Upper`
- 바지·신발 자리: `Visual/Wardrobe/PantsSlot`, `ShoesSlot`
- 머리 자리: Animator의 `Head` 본 아래 `HairSlot`

현재 실제 3D 부품이 있는 상의만 장착했습니다. 머리·바지·신발 자리는 비어 있습니다. 참고 PNG는 3D 부품이 아니므로 자동으로 장착되지 않습니다. 다른 캐릭터에서 임의로 잘라 낸 부품은 사용하지 않습니다. 상의는 사용자 생성 FBX의 형상을 유지하고 착용 좌표와 Base 본 가중치를 별도 메시로 저장했습니다. `PasserbyParts.json`은 이 상의의 장착 데이터이며 Base를 수정하는 데이터가 아닙니다.

새 머리는 `HairSlot` 아래에 붙여 위치·회전·크기를 맞춥니다. 바지와 신발이 스킨 메시라면 전용 슬롯에 두는 것 외에 Base의 bones/rootBone과 bindpose를 맞춰야 합니다. 파일을 드래그하는 것만으로 다른 골격의 스킨 메시가 자동 호환되지는 않습니다.

행인 행동은 기존 `Passerby`, `NpcIdentity`, 마을 행인 설정과 `CharacterAnimationDriver`를 사용합니다. 플레이어 입력·이동 컴포넌트는 이 Variant에서만 비활성화됩니다. 서버 NPC 코드는 기존 설계대로 씬 인스턴스에서 지정합니다. 배치할 때 `player`, `dialogue`, `offerView`를 해당 씬 객체로 연결해야 합니다. 검수 씬에서는 외형·표정만 확인하도록 행인 접근 행동을 비활성화했습니다. Production 씬에는 배치하지 않았습니다.

부모 Base 변경은 오버라이드하지 않은 몸·얼굴·표정·Animator 설정에 전파됩니다. 상의 메시와 가중치는 별도 에셋이므로 Base 체형 또는 본의 기준 자세를 바꾸면 착용 상태를 다시 검수해야 합니다. 모든 동작에서 옷 관통이 없다는 보장은 아닙니다.

상의 착용 시 `BodyUnderSuit` 머티리얼은 옷으로 덮인 몸통·팔 부위만 알파 마스크로 가립니다. Base 메시·UV·블렌드셰이프는 그대로 공유하며, 얼굴·손 영역의 UV는 보호합니다. 머티리얼을 원래 `MannequinSkin`으로 되돌리면 몸 전체가 다시 보입니다. 옷을 교체할 때는 가리는 부위도 새 옷에 맞게 갱신해야 합니다. 머티리얼의 피부 틴트 기능은 유지합니다.

## 현재 검수 범위

이 프리팹은 외형 검수용 후보입니다. Base 연결 검사는 의상의 정지 핏·변형 품질 합격을 뜻하지 않습니다. 어깨·소매·밑단의 실제 착용 핏은 아직 완료되지 않았습니다.

`restrict_upper_weights.py`는 기존 장착 JSON의 정점·면·UV를 유지하면서 재킷 영향 본을 몸통·목·어깨·위팔·아래팔로 제한합니다. 다리·손가락·머리 본에 의상이 끌려가는 문제를 방지하는 작업이며, 의상 형태 자체를 맞추는 기능은 아닙니다. Blender 백그라운드에서 `--source <장착 JSON> --measurement <Base 측정 JSON> --output <결과 폴더>`로 실행합니다. 원본 입력 경로와 출력 경로를 분리해야 합니다.

자동 렌더에서는 `PASSERBY_SHOW_BODY=1`로 몸 숨김을 끄고 실제 관통·핏을 확인할 수 있습니다. `PASSERBY_REST_ONLY=1`은 정지 방향 비교만 출력합니다. 이 옵션들은 저장된 프리팹·씬을 바꾸지 않습니다.
