# 목표 UI Sorting Order 변경 계획

## Summary
- 오른쪽 위 목표/미션 HUD는 `DemoRoundMissionHUD`가 런타임에 생성하는 `Canvas` 기반 UI로 확인됨.
- 요청한 “order”는 `Canvas.sortingOrder`로 해석하고, 현재 값 `30`을 `-1`로 변경한다.

## Key Change
- `DemoRoundMissionHUD`의 `CanvasSortingOrder` 상수를 `30`에서 `-1`로 변경한다.
- UI 위치, 텍스트, 표시 조건, 미션 진행 로직은 건드리지 않는다.

## Test Plan
- `DemoRoundMissionHUD` 생성 시 `canvas.sortingOrder == -1`인지 확인.
- 게임 화면에서 오른쪽 위 미션/힌트 UI가 더 낮은 렌더 순서로 표시되는지 확인.
- 인벤토리/퀵슬롯/상점/루팅 UI의 기존 `sortingOrder` 동작이 바뀌지 않았는지 확인.

## Assumptions
- “오른쪽 위 목표 ui”는 `Assets/_Project/Scripts/04_UI/Ingame UI/DemoRoundMissionHUD.cs`의 미션 HUD를 의미한다.
- “order”는 Unity `Canvas.sortingOrder`를 의미한다.
