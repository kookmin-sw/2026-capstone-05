# 상점 구매가/판매가를 ItemData.price 기준으로 통일

## Summary
- `ItemData.price`를 아이템의 판매가로 사용한다.
- 상점 판매 탭 가격은 `ItemData.price` 그대로 표시/정산한다.
- 상점 구매 탭 가격은 `ItemData.price * 2`로 표시/정산한다.
- 기존 상점 설정의 `ShopItemEntry.BuyPrice` 값은 더 이상 최종 구매가로 쓰지 않고, 아이템 데이터 기준 가격으로 덮어쓴다.

## Key Changes
- `ShopController.OpenShop()`에서 상점 아이템 목록을 렌더링하기 전에 구매가를 정규화한다.
  - `BuyPrice = Mathf.Max(0, item.ItemData.price * 2)`
  - `ItemData == null`인 항목은 안전하게 제외하거나 기존 null 방어 흐름에 맞춰 무시한다.
- `ShopController.RefreshSellCatalog()`에서는 판매 가격을 `ItemData.price`로 세팅한다.
  - 기존 `BuyPrice / 2` 계산과 기본 `10G` fallback 제거
  - 판매용 `ShopItemEntry.BuyPrice = Mathf.Max(0, itemData.price)`
- `ShopView`는 구조 변경 없이 그대로 둔다.
  - 구매 목록/구매 팝업/구매 정산은 정규화된 `BuyPrice`를 사용한다.
  - 판매 목록/판매 팝업/판매 정산도 판매 카탈로그에 들어간 `BuyPrice`를 사용한다.
- `ShopItemEntry`에 새 필드는 추가하지 않는다.
  - 현재 구조에서는 `BuyPrice`를 “현재 모드에서 표시/정산할 가격”으로 계속 사용한다.

## Test Plan
- 구매 탭에서 아이템 가격이 `ItemData.price * 2`로 표시되는지 확인한다.
- 구매 확정 시 차감 골드가 표시 가격과 같은지 확인한다.
- 판매 탭에서 아이템 가격이 `ItemData.price`로 표시되는지 확인한다.
- 판매 확정 시 지급 골드가 `ItemData.price * quantity`인지 확인한다.
- 구매 후 배치 취소/환불 경로도 `ItemData.price * 2` 기준으로 복구되는지 확인한다.
- `dotnet build Assembly-CSharp.csproj`로 컴파일 오류가 없는지 확인한다.

## Assumptions
- `ItemData.price`는 플레이어가 상점에 팔 때 받는 판매가다.
- 플레이어가 상점에서 살 때의 구매가는 판매가의 정확히 2배다.
- `price = 0`인 아이템은 구매가/판매가 모두 `0G`로 처리한다.
