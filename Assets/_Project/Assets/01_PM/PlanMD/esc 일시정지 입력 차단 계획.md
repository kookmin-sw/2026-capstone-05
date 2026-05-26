**ESC 일시정지 입력 차단 계획**

**요약**
- ESC 일시정지는 **로컬 플레이어 입력만 멈추는 방식**으로 처리합니다.
- 멀티플레이 세션, 서버 로직, 다른 플레이어, UI 애니메이션은 계속 동작합니다.
- 현재 문제의 핵심은 `PlayerInputActions`만 꺼지고 `PlayerController.Update()`의 카메라/이동 루프가 계속 도는 점이므로, 게임플레이 루프 자체를 일시정지 상태에서 막습니다.

**주요 변경**
- [PlayerController.cs](<C:\Users\robor\Documents\GitHub\NUNBORA\Assets\_Project\Scripts\02_Player\PlayerController.cs>)에서 `PauseMenuManager.IsAnyUIOpen()`이 true면 게임플레이 업데이트를 중단합니다.
  - 카메라 회전, 이동 FSM, 중력, `Controller.Move`, 자세 전환, 아이템 사용을 막습니다.
  - 일시정지 진입 시 `currentVelocity`도 0으로 만들어 관성처럼 미끄러지는 현상을 막습니다.
- [RoomLauncher.cs](<C:\Users\robor\Documents\GitHub\NUNBORA\Assets\_Project\Scripts\06_Backend\RoomLauncher.cs>)의 `OnInput`에서 일시정지 또는 UI 오픈 중이면 빈 입력을 전송합니다.
  - 네트워크 입력에 이전 프레임의 이동/시점/공격 값이 새는 것을 막습니다.
- [PlayerInputHandler.cs](<C:\Users\robor\Documents\GitHub\NUNBORA\Assets\_Project\Scripts\02_Player\PlayerInputHandler.cs>)의 기존 `SetInputActive(false)`와 입력 리셋 구조는 유지하되, UI가 닫힌 뒤에만 정상 입력으로 복귀하도록 정리합니다.

**일시정지 중 막아야 할 것**
- 마우스 시점 회전, 카메라 반동 복귀, 뷰모델 흔들림
- 이동, 달리기, 점프, 앉기, 중력 이동, 남아 있는 속도
- 공격/아이템 사용, 조준, 상호작용, 홀드 상호작용
- 아이템 줍기, 문 열기, 상점/루팅 상호작용
- 퀵슬롯 숫자키, 재장전/회전 같은 게임플레이 단축키
- 네트워크 이동/시점/액션 입력 전송
- 메뉴 뒤쪽의 상호작용 하이라이트와 안내 프롬프트 갱신

**테스트**
- ESC 화면에서 마우스를 움직여도 카메라가 움직이지 않아야 합니다.
- 이동/달리기/점프 중 ESC를 눌러도 플레이어가 멈춰야 합니다.
- 낙하나 이동 중 ESC를 눌렀을 때 미끄러지지 않아야 합니다.
- 일시정지 메뉴 버튼, 설정창, 확인창은 정상 클릭되어야 합니다.
- 재개 후 커서가 다시 잠기고 입력이 정상 복구되어야 합니다.
- 멀티플레이에서는 내 일시정지가 다른 플레이어나 서버 진행을 멈추지 않아야 합니다.

**가정**
- ESC 일시정지는 멀티플레이 기준으로 로컬 입력만 차단합니다.
- `Time.timeScale = 0`은 사용하지 않습니다.
- 기존 인벤토리/상점/루팅 UI도 `PauseMenuManager.IsAnyUIOpen()` 기준으로 게임플레이 입력을 막습니다.
