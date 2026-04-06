# 멀티플레이 이동/카메라 문제 진단 (Fusion)

## 관찰된 현상
- 입력은 `RoomLauncher.OnInput()`까지 정상 전달된다.
- 플레이어 프리팹의 애니메이션은 입력에 반응한다.
- 하지만 실제 월드 이동(Transform/CharacterController)과 카메라 체감 제어는 정상 동작하지 않는다.

## 코드 기준 원인 요약

### 1) 입력 소비 시점과 이동 실행 시점이 서로 다른 루프에 있음
- 네트워크 입력 수신/주입은 `PlayerAdapter.FixedUpdateNetwork()`에서 수행한다.
- 실제 이동(`CharacterController.Move`)과 시점 회전(`HandleLook`)은 `PlayerController.Update()`에서 수행한다.

즉, **입력은 Fusion Tick 루프(FixedUpdateNetwork), 이동은 Unity 프레임 루프(Update)** 에서 처리되어 멀티에서 타이밍이 어긋난다.

### 2) InputAuthority와 StateAuthority가 동시에 이동을 수행하도록 되어 있음
- `AuthorityStateController.Apply()`에서 `shouldMove = isStateAuthority || isLocalPlayer`로 되어 있다.
- 이 설정은 비-상태권한 로컬 클라이언트도 이동을 직접 수행하게 만든다.
- 그러나 최종 Transform은 상태권한(서버/호스트) 스냅샷이 덮어쓰므로, 로컬에서는 "움직이는 것 같다가 되돌아감" 또는 "거의 안 움직이는" 현상이 생긴다.

### 3) 카메라 피치 보정 활성 조건이 실제 상태와 엇갈릴 수 있음
- `LocalCameraBinder.Apply()`에서 `PlayerController`가 켜져 있으면 `RemotePitchCompensator`를 끈다.
- 현재 구조에서는 비-상태권한 로컬도 `PlayerController`를 켜기 때문에, 네트워크 보간/스냅으로 루트 회전이 흔들릴 때 피치 보정 경로가 비활성화될 수 있다.

## 왜 "애니메이션만 정상"처럼 보이나?
- 애니메이션 파라미터는 로컬 입력값을 기반으로 갱신되기 쉽고 즉시 반응한다.
- 반면 실제 위치/회전은 네트워크 권한과 동기화 결과가 최종값이다.
- 그래서 입력/애니메이션은 맞는데 이동/카메라만 비정상처럼 보이는 전형적인 분리 증상이 발생한다.

## 우선 적용 권장사항
1. **권한 분리 원칙 고정**
   - 상태권한만 실제 이동/회전을 확정하도록 정리.
   - 입력권한 측 즉시성은 Fusion 예측(CharacterController Network sample 방식)으로 처리.
2. **입력 소비와 이동 적용 루프 통일**
   - 가능하면 `FixedUpdateNetwork` 기준으로 이동/회전 로직을 통합.
3. **카메라 보정 조건 재정의**
   - "PlayerController enabled 여부" 대신 "상태권한/예측 사용 여부" 기준으로 피치 보정 온오프.

## 빠른 검증 체크리스트
- 서버(상태권한) 로그에서 `Server propagate`의 `pos`/`yaw`가 실제로 변화하는지.
- 비-상태권한 로컬에서 `PlayerController`를 일시 비활성했을 때 되돌림 현상이 사라지는지.
- `FixedUpdateNetwork` 한 루프에서 입력 읽기-이동 적용-네트워크 동기화가 일관되게 일어나는지.
