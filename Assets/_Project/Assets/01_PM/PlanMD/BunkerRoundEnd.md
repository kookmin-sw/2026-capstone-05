**벙커 기준 라운드 종료 페널티 계획**

**Summary**
- 라운드 종료 페널티는 일반 실내가 아니라 **벙커 내부 여부**만 기준으로 판정한다.
- `PlayerCondition`에는 `IsInBunker`를 별도 상태로 둔다.
- 일반 `IndoorsTrigger`/`SetIndoors`는 추위 증감용으로만 유지하고, 라운드 종료 생존 판정에는 사용하지 않는다.
- 스폰/리스폰 위치가 벙커 내부이므로 `IsInBunker` 초기값은 `true`다.

**Key Changes**
- `PlayerCondition`에 벙커 상태를 추가한다.
  - `private bool isInBunker = true;`
  - `public bool IsInBunker => isInBunker;`
  - `public void SetInBunker(bool value)`
- 기존 `isIndoors`/`SetIndoors(bool)`는 그대로 두고 추위 로직에만 사용한다.
- `BunkerEnterInteractable`에서 벙커로 들어가는 텔레포트 성공 후 `SetInBunker(true)`를 호출한다.
- `BunkerExitInteractable`에서 벙커 밖으로 나가는 텔레포트 성공 후 `SetInBunker(false)`를 호출한다.
- `PlayerRespawn.SpawnAtSpawner()` 완료 후 `SetInBunker(true)`를 호출한다.
- `BackendRoundManager`의 라운드 종료 페널티 판정은 `PlayerCondition.IsInBunker == false`만 본다.

**Round End Flow**
- `EndRoundRoutine()` 시작 시 `IsRoundRunning = false`로 벙커 입장 상호작용을 막는다.
- 벙커 문이 열려 있다면 벙커 문만 닫는다. 일반 문은 건드리지 않는다.
- 벙커 문/입장 상태가 닫힌 직후 한 프레임 대기한다.
- 그 다음 `IsInBunker == false`인 플레이어만 페널티 대상으로 확정한다.
- 대상 플레이어는 저장 전에 인벤토리/퀵슬롯을 비우고, 다음 사망 시 아이템 드랍을 막는다.
- 죽음 자체는 기존 추위 데미지 로직에 맡긴다.

**Test Plan**
- 스폰/리스폰 직후: `IsInBunker == true`.
- 벙커 입장 성공 후: `IsInBunker == true`.
- 벙커 퇴장 성공 후: `IsInBunker == false`.
- 일반 집/실내 진입: 추위는 줄어도 `IsInBunker`는 바뀌지 않음.
- 라운드 종료 시 일반 집 안에 있음: `IsInBunker == false`라서 페널티 대상.
- 라운드 종료 시 벙커 안에 있음: 인벤 유지.
- 페널티 대상이 추위로 사망: 아이템 드랍 없음, 빈 인벤 저장.

**Assumptions**
- 벙커 판정은 `BunkerEnterInteractable`, `BunkerExitInteractable`, `PlayerRespawn`만 갱신한다.
- 일반 `IndoorsTrigger`는 라운드 종료 생존 판정에서 제외한다.
- 공유 골드는 삭제하지 않는다.
