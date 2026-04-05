# 06_Backend 멀티플레이 흐름 요약

## 씬 흐름
- `Main_menu`(시작): 로그인 → 시작하기 → 호스트/참가 선택
- `TestMain`(게임씬): 네트워크 플레이 실제 진행

## 동작 순서
1. `RoomLauncher`가 런타임에 생성되고 메뉴 씬(`Main_menu`)을 기준으로 동작한다.
2. 메뉴에서 호스트를 선택하면 4자리 숫자 코드로 세션을 생성한다.
3. 메뉴에서 참가를 선택하면 입력한 4자리 숫자 코드의 세션으로 접속한다.
4. 세션 시작 시 `TestMain` 씬을 네트워크 게임 씬으로 로드한다.
5. 서버(호스트)는 `06_Backend/Player.prefab`(BackendPlayerNetworkAdapter 포함)을 플레이어별로 스폰한다.
6. 스폰된 플레이어는 `02_Player` 스크립트를 adapter를 통해 로컬/원격 상태에 맞게 활성화한다.

## 체크 포인트
- `Main_menu`, `TestMain`이 Build Settings에 포함되어 있어야 한다.
- NetworkProjectConfig PrefabTable에 `06_Backend/Player.prefab`이 등록되어 있어야 한다.
