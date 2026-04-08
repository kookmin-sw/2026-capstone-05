# 그리드 인벤토리 시스템 설정 가이드

## 1. 아이템 데이터 생성 및 설정 (ItemDetails)
1. Project 창 빈 공간 우클릭 -> `Create > Inventory > Item` 클릭하여 아이템 데이터(ScriptableObject) 생성
2. 인스펙터 창에서 다음 정보들을 설정:
   - **Name**: 아이템 이름 (예: 사과, 검)
   - **Max Stack**: 한 슬롯에 겹칠 수 있는 최대 개수 (기본값: 1)
   - **Width / Height**: 인벤토리 그리드에서 아이템이 차지하는 칸 수 (예: 1x1, 가로 1x세로 2 등)
   - **Id**: 고유 식별자 (우측 점 3개 메뉴 `ContextMenu` - "Assign New Guid"를 통해 재할당 가능)
   - **Icon**: 인벤토리 UI에 표시될 아이템 이미지 (Sprite)
   - **Description**: 아이템 설명

## 2. UI 설정 (UI Toolkit 기반)
이 시스템은 uGUI(Canvas)가 아닌 **UI Toolkit**으로 제작되었습니다.
1. 씬에 빈 GameObject를 생성 (예: `InventoryUI`)
2. `UIDocument` 컴포넌트 추가
   - **Panel Settings**: 프로젝트에 있는 UI Panel Settings 에셋 할당
   - **Source Asset**: `Inventory.uxml` 에셋 할당
3. 동일한 GameObject에 `InventoryView.cs` 스크립트 추가
4. 인스펙터 설정:
   - **Document**: 방금 추가한 UIDocument 연결
   - **Style Sheet**: `Inventory.uss` 에셋 연결
   - **Panel Name**: 창 상단에 표시될 헤더 이름 (기본값: "Inventory")

## 3. 인벤토리 메인 연결 (Inventory)
1. 플레이어나 별도의 관리 오브젝트에 `Inventory.cs` 컴포넌트를 추가
2. 인스펙터 설정:
   - **View**: 위 2번에서 설정한 `InventoryView` 컴포넌트를 드래그 앤 드롭
   - **Starting Items**: 게임 시작 시 들고 시작할 초기 아이템 목록(`ItemDetails`)을 리스트에 추가

## 4. 조작 및 연동 (입력)
인벤토리 On/Off는 `InventoryView.cs`에서 `Tab` 키로 하드코딩되어 있습니다. 별도 외부 입력 설정 없이 바로 동작합니다.
- **Tab 키 누름**: 인벤토리 창 표시/숨김 토글
- **열렸을 때 (Opened)**:
  - 마우스 커서 활성화 및 잠금 해제 (`Cursor.visible = true`, `CursorLockMode.None`)
  - 씬 내의 `PlayerInputHandler`를 자동으로 찾아 `SetInputActive(false)`를 호출해 플레이어 조작 차단
- **닫혔을 때 (Closed)**:
  - 마우스 커서 숨김 및 잠금 (`Cursor.visible = false`, `CursorLockMode.Locked`)
  - `PlayerInputHandler` 조작 다시 활성화

## 5. 핵심 작동 방식 비교
### 5.1 그리드 크기 차지 (Grid-based)
- 디아블로 혹은 타르코프처럼 아이템의 `Width`와 `Height` 옵션에 따라 그리드의 여러 칸을 차지합니다.

### 5.2 드래그 앤 드롭 기능
- 아이템 아이콘을 좌클릭하여 드래그하고, 원하는 그리드 위치에 놓을 수 있습니다.
- 배치 가능한 빈 공간이 있으면 그대로 들어갑니다.

### 5.3 아이템 스태킹 (Stacking) / 합치기
- 동일한 Id의 아이템이고 `maxStack` 값이 1 이상이라면 겹쳐서 수량을 합칠 수 있습니다.

### 5.4 스왑 (Swapping)
- 이미 아이템이 있는 자리에 다른 아이템을 덮어씌우면, 칸 계산 판정 후 두 아이템의 위치가 서로 뒤바뀝니다(Swap).

## 6. 중요 참고 & 디버깅 포인트
- **UI 사이즈 자동 계산**: 인벤토리 칸의 가로 개수는 기본 8칸으로 세팅되며, 총 Capacity 개수 대비 열 수에 맞춰 세팅됩니다.
- **아이템 최대 크기 주의**: 인벤토리 폭(8칸)을 초과하는 Width를 가진 아이템은 넣거나 배치할 때 에러가 발생하거나 드롭되지 않을 수 있습니다.
- **`PlayerInputHandler` 선택사항**: 씬에 `PlayerInputHandler`가 없어도 인벤토리는 정상 동작합니다. 단, 존재할 경우 UI를 열고 닫을 때 자동으로 플레이어의 움직임을 멈추거나 재개합니다.
- UI Toolkit 에러 발생 시 UI Builder 에디터로 `Inventory.uxml`을 열어 `container`나 `inventory-window` 등 지정된 클래스/이름 구조가 손상되지 않았나 점검하세요.
