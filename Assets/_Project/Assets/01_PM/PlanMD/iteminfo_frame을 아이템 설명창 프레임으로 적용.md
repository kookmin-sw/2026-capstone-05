# `iteminfo_frame`을 아이템 설명창 프레임으로 적용

## Summary
- `Assets/Resources/UI/ItemDescriptionPanel.uss`만 수정해서 기존 갈색/베이지 설명창 박스를 `iteminfo_frame.png` 이미지 프레임으로 교체한다.
- 인벤토리/상자 UI가 `popup.png`를 적용한 방식과 동일하게 USS `background-image` 패턴을 사용한다.
- UXML 구조와 `ItemDescriptionPanelController` 동작은 그대로 둔다.

## Key Changes
- `.item-description-box` 스타일을 인벤토리/상자 패널 방식과 맞춘다.
  - `background-image: url("project://database/Assets/_Project/Assets/04_UI/UI/iteminfo_frame.png?fileID=21300000&guid=21391bbb8a8d1b44cabac7fd0476611e&type=3#iteminfo_frame");`
  - `background-color: transparent;`
  - `border-width: 0;`
  - 기존 `border-color`, `border-radius` 제거
- 기존 팝업 폭 `520px`와 표시 위치는 유지한다.
- 프레임 안쪽에 내용이 들어가도록 `.item-description-box`, `.item-description-header`, `.item-description-body`의 padding/margin을 조정한다.
- 어두운 프레임에 맞춰 텍스트 색상을 흰색/연회색 계열로 조정한다.
- 기존 갈색 헤더 배경은 제거해서 이미지 프레임 디자인이 가려지지 않게 한다.

## Test Plan
- 인벤토리 아이템 hover 시 설명창에 `iteminfo_frame`이 적용되는지 확인한다.
- 상자/루팅 UI의 아이템 hover에서도 같은 설명창 프레임이 뜨는지 확인한다.
- 아이템명, 가격, 수량, 설명, 아이콘, 모양 미리보기가 프레임 안쪽에 들어가고 잘 읽히는지 확인한다.
- 기존 hover 표시/숨김, 드래그 중 숨김 동작이 깨지지 않는지 확인한다.

## Assumptions
- 사용할 파일은 `Assets/_Project/Assets/04_UI/UI/iteminfo_frame.png`가 맞다.
- 인벤토리/상자 UI의 이미지 프레임 적용 방식, 즉 USS `background-image` + transparent background + border 0 패턴을 따른다.
- 기존 dirty 파일인 FMOD cache, 폰트 SDF asset, Quickslot USS 변경분은 건드리지 않는다.
