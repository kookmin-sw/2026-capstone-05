# 06_Backend 멀티플레이 진입 흐름

## 씬 역할
- `Start`: 방 코드 입력 후 Host/Client 시작 선택 화면
- `Main1`(게임씬): 실제 네트워크 플레이 씬

## RoomLauncher 동작
1. 부팅 시 `RoomLauncher`를 런타임에서 자동 생성(`DontDestroyOnLoad`).
2. `Start` 씬에서만 방 코드 기반 멀티플레이 UI를 표시한다.
3. `Start`에서는 방 코드를 입력하고
   - `호스트로 시작`
   - `클라이언트로 참가`
   두 경로 중 하나를 선택한다.
4. Host/Client 시작 시 `Main1` 씬을 네트워크 게임 씬으로 로드한다.
5. 플레이어 입장 시 `playerPrefab`을 `Runner.Spawn`으로 생성하여 입력 권한 플레이어가 직접 컨트롤한다.

## 필수 설정
- `NetworkProjectConfig.PrefabTable`에 플레이어 프리팹(기본 탐색 이름: `PlayerCharacter`, `Player`)이 등록되어 있어야 한다.
- `Start`, `Main1`이 Build Settings에 포함되어 있어야 한다.
