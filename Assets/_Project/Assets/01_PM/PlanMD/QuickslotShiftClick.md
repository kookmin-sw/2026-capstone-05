# 퀵슬롯 포함 보유 아이템 조회/소비 공용화

## Summary
- 총 발사와 상점 판매가 현재 그리드 인벤토리만 기준으로 아이템 수량을 확인/소비해서, 총알이 퀵슬롯에 있으면 없는 것으로 판정된다.
- 새 공용 스크립트를 추가해 “플레이어 보유 아이템”을 그리드 인벤토리 + 퀵슬롯 합산 기준으로 조회한다.
- 총 발사, 상점 판매 목록/판매 처리, 퀘스트 탄약 확인을 이 공용 API로 교체한다.

## Key Changes
- 새 스크립트 `PlayerItemInventoryQuery`를 추가한다.
  - 위치: `Assets/_Project/Scripts/01_PM/GridInventory/Inventory/PlayerItemInventoryQuery.cs`
  - public static API:
    - `CountItem(ItemData itemData)`: 그리드 인벤토리와 퀵슬롯의 총 수량 반환
    - `HasItem(ItemData itemData, int amount = 1)`: 합산 수량 기준 보유 여부
    - `TryConsumeItem(ItemData itemData, int amount = 1)`: 합산 수량 기준 소비
    - `BuildItemCounts()`: 상점 판매 목록용 `Dictionary<ItemData, int>` 반환
  - 그리드 인벤토리는 같은 `ItemInstance`가 여러 셀을 차지하므로 `HashSet<ItemInstance>`로 중복 카운트를 막는다.
  - 퀵슬롯 4칸도 함께 세되, 같은 `ItemInstance`가 양쪽에 동시에 참조되는 비정상 상태가 있어도 중복 카운트하지 않는다.

- 소비 규칙을 명확히 둔다.
  - 소비 전 `CountItem`으로 전체 수량이 충분한지 먼저 확인한다.
  - 소비 순서는 그리드 인벤토리 먼저, 부족분은 퀵슬롯에서 소비한다.
  - 퀵슬롯 스택이 줄면 `RefreshSlotVisual(index)`를 호출하고, 0이 되면 `RemoveItemFromSlot(index)`로 제거한다.
  - 소비 실패 시에는 사전 수량 체크 때문에 부분 소비가 일어나지 않게 한다.

- 기존 호출부를 교체한다.
  - `FirearmWeaponBehaviour`: 탄약 확인과 발사 시 탄약 소비를 `PlayerItemInventoryQuery.HasItem/TryConsumeItem`으로 교체한다.
  - `ShopController.RefreshSellCatalog`: 판매 목록 수량 계산을 `BuildItemCounts()`로 교체해 퀵슬롯 아이템도 판매 목록에 뜨게 한다.
  - `ShopController.HandleSellItemClicked`: 판매 시 `PlayerItemInventoryQuery.TryConsumeItem`으로 교체해 퀵슬롯 보유분도 판매 가능하게 한다.
  - `DemoRoundMissionHUD`: 총/탄약 퀘스트 확인의 중복 퀵슬롯 조회 로직을 `PlayerItemInventoryQuery.HasItem`으로 정리한다.

## Test Plan
- 총알이 그리드 인벤토리에만 있을 때 총이 정상 발사되고 탄약이 1개 줄어드는지 확인한다.
- 총알이 퀵슬롯에만 있을 때도 총이 정상 발사되고 퀵슬롯 스택/아이콘이 갱신되는지 확인한다.
- 총알이 인벤토리와 퀵슬롯에 나뉘어 있을 때 합산 수량으로 발사 가능 여부가 판단되는지 확인한다.
- 상점 판매 탭에서 퀵슬롯에만 있는 아이템도 판매 목록에 표시되고, 판매 시 퀵슬롯에서 제거/감소되는지 확인한다.
- 판매 수량이 합산 보유량보다 많으면 판매가 실패하고 아이템 수량이 유지되는지 확인한다.
- `dotnet build Assembly-CSharp.csproj` 또는 Unity 컴파일 오류가 없는지 확인한다.

## Assumptions
- “인벤에 몇 개 있는지”는 플레이어가 들고 다니는 보유품 전체, 즉 그리드 인벤토리 + 퀵슬롯 합산으로 정의한다.
- 장착 중인 퀵슬롯 아이템도 퀵슬롯 보유품으로 간주한다.
- 구매 배치 로직은 그리드 인벤토리에 배치하는 기존 UX를 유지하고, 이번 변경은 조회/판매/소비 기준만 확장한다.
