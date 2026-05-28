# 아이템 설명창 표시 루트 수정 계획

## 요약
- 씬에 둔 `ItemDetailUI`의 `UIDocument`를 공식 설명창 루트로 사용하게 고칩니다.
- 시작 시 설명창을 코드에서 무조건 숨겨 왼쪽 위 생짜 텍스트가 보이지 않게 합니다.
- hover/drag 때는 이 UIDocument 안의 패널을 찾아 하단 중앙 고정 설명창으로 표시합니다.
- 씬에 `ItemDetailUI`가 없어도 기존처럼 런타임 복제 방식으로 fallback 동작하게 유지합니다.

## 주요 변경
- `ItemDescriptionPanelController`에 `UIDocument` 참조를 추가합니다.
  - `Awake`/`OnEnable`에서 `UIDocument.rootVisualElement` 안의 `item-description-layer`를 찾아 캐시합니다.
  - 캐시 직후 `style.display = DisplayStyle.None`을 코드로 강제 적용해서 USS 적용 여부와 상관없이 시작 시 숨깁니다.
  - `ItemDetailUI`에 이 컨트롤러를 붙이면 그 인스턴스가 전역 설명창 루트가 됩니다.

- `Show(item, owner)` 동작 우선순위를 바꿉니다.
  - 1순위: 씬에 등록된 `ItemDescriptionPanelController`의 UIDocument 패널 사용
  - 2순위: 등록된 인스턴스가 없을 때만 현재 아이템 UI 루트에 런타임 패널 복제
  - 이로써 인벤토리 UIDocument 안쪽에 갇히지 않고, 사용자가 만든 전체 화면 `ItemDetailUI`에서 표시됩니다.

- UXML/USS 안정성을 보강합니다.
  - UXML의 기본 텍스트가 노출되지 않도록 컨트롤러가 inline `display: none`을 강제합니다.
  - 하단 중앙 고정에 필요한 핵심 스타일은 USS에 두되, `layer`의 `position`, `left/right/top/bottom`, `justify-content`, `align-items`, `padding-bottom`은 코드에서도 보정합니다.
  - 설명창 내부는 입력을 먹지 않도록 `PickingMode.Ignore`를 재귀 적용합니다.

- 수동 UIDocument 사용 규칙을 명확히 합니다.
  - `ItemDetailUI` GameObject에는 `UIDocument`와 `ItemDescriptionPanelController`를 함께 둡니다.
  - `UIDocument.sourceAsset`은 `ItemDescriptionPanel.uxml`을 사용합니다.
  - 별도 Canvas는 사용하지 않습니다.

## 테스트
- 게임 시작 직후 왼쪽 위에 `0 Gold`, `수량`, 설명 텍스트가 보이지 않아야 합니다.
- 인벤토리 아이템에 마우스를 올리면 하단 중앙 고정 위치에 박스형 설명창이 떠야 합니다.
- 드래그 중에도 같은 설명창이 유지되어야 합니다.
- 빈 슬롯으로 이동하거나 드래그를 끝내면 설명창이 사라져야 합니다.
- 퀵슬롯/루팅/인벤토리 모두 같은 `ItemDetailUI` 설명창을 사용해야 합니다.
- `dotnet build Assembly-CSharp.csproj --no-restore`로 컴파일 오류가 없어야 합니다.

## 가정
- 현재 씬의 `ItemDetailUI` GameObject를 실제 설명창 전용 UIDocument로 사용합니다.
- 위치는 기존 요청대로 커서 추적이 아니라 화면 하단 중앙 고정입니다.
