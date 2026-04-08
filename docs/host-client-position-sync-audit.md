# Host/Client 위치 동기화 미적용 원인 분석 (Fusion)

## 결론 요약
현재 프로젝트는 **접속/스폰/로컬-리모트 분기까지는 구현되어 있지만**, 실제 위치 동기화에 필요한 Fusion 입력/시뮬레이션 루프가 비어 있어 플레이어 이동이 네트워크 상태로 전파되지 않습니다.

핵심적으로 아래 3가지가 동시에 문제입니다.

1. `RoomLauncher.OnInput()`이 비어 있어 클라이언트 입력이 네트워크로 전달되지 않음.
2. `PlayerController`가 `MonoBehaviour` 기반 로컬 `Update()`에서만 `CharacterController.Move()`를 수행함.
3. `Player.prefab`의 `NetworkObject`에 `NetworkedBehaviours`가 비어 있어(= `NetworkTransform`/커스텀 `NetworkBehaviour` 없음) 상태 복제가 일어나지 않음.

---

## 요소별 점검 결과

### 1) 스크립트: RoomLauncher
- `OnPlayerJoined`에서 `runner.Spawn(...)` + `SetPlayerObject(...)`는 정상으로 보이며, 스폰 자체는 이뤄지고 있습니다.
- 하지만 `OnInput(NetworkRunner runner, NetworkInput input)`가 완전히 빈 구현입니다.
- 따라서 클라이언트 입력이 Fusion 시뮬레이션으로 전달되지 않습니다.

**영향**: 클라이언트에서 움직여도 서버/다른 클라이언트가 같은 입력을 재생산할 근거가 없습니다.

### 2) 스크립트: PlayerController
- `PlayerController`는 `MonoBehaviour`이며 `Update()`에서 직접 `Controller.Move(currentVelocity * Time.deltaTime)`를 호출합니다.
- 즉 이동이 Fusion `FixedUpdateNetwork()` 경로를 타지 않습니다.

**영향**: 각 클라이언트가 자기 로컬 오브젝트만 로컬 프레임 기준으로 움직이고, 네트워크 상태로 정규화되지 않습니다.

### 3) 프리팹: Player.prefab
- `NetworkObject`는 붙어 있으나 `NetworkedBehaviours: []` 상태입니다.
- 네트워크 변환 동기화(`NetworkTransform` 또는 커스텀 `NetworkBehaviour`/`[Networked]`) 담당 컴포넌트가 없습니다.

**영향**: 스폰 후 transform 자체를 복제할 채널이 없습니다.

### 4) 로컬/리모트 분기 구조
- `PlayerAdapter` + `PlayerNetworkSetup` + `IPlayerNetworkConfigurable`로 입력 활성화/비활성화, 1P/3P 분기는 잘 되어 있습니다.
- 다만 이 구조는 **표시/입력 권한 분리**이고, **위치 네트워크 동기화 자체를 대신해주지는 않습니다**.

**영향**: "로컬만 조작 가능"은 성립해도 "원격 동기화"는 별도 구현이 필요합니다.

### 5) 씬/런너 설정 관점
- `StartGame` 호출은 구현되어 있고 씬 로드도 동작합니다.
- 그러나 입력 수집/전달(`ProvideInput`, `OnInput`) + 네트워크 이동 시뮬레이션(`NetworkBehaviour.FixedUpdateNetwork`)이 연결되지 않음.

---

## 근본 해결책 (권장 순서)

### 해결책 A (권장): Fusion 표준 입력-시뮬레이션 구조로 전환

1. **입력 struct 추가**
   - 예: `struct PlayerNetworkInput : INetworkInput`에 move/look/jump/sprint/action 비트 포함.

2. **`RoomLauncher.OnInput()` 구현**
   - `runner.TryGetPlayerObject(runner.LocalPlayer, out var obj)`로 로컬 플레이어 오브젝트를 찾고,
   - `PlayerInputHandler` 값을 읽어 `input.Set(playerInput)` 수행.

3. **이동을 NetworkBehaviour로 이동**
   - 새 `PlayerNetworkMotor : NetworkBehaviour` 작성.
   - `FixedUpdateNetwork()`에서 `GetInput<PlayerNetworkInput>(out var input)`을 읽고 이동/중력/점프 계산.
   - 최종 이동은 Fusion 친화 경로(`NetworkCharacterController` 또는 `NetworkTransform` + 수동 동기화) 사용.

4. **Player.prefab 네트워크 컴포넌트 구성**
   - `NetworkObject` 유지.
   - `NetworkedBehaviours`에 위 `PlayerNetworkMotor`(및 필요 시 `NetworkCharacterController`)가 실제 등록되도록 prefab 저장.

5. **기존 `PlayerController` 역할 분리**
   - 네트워크 이동 책임은 `PlayerNetworkMotor`로 이관.
   - `PlayerController`는 카메라/애니메이션/상태머신(표현) 위주로 축소.

### 해결책 B (임시): Transform 강제 복제
- 빠른 확인용으로 `NetworkTransform`만 추가해 호스트 권한 transform을 복제할 수는 있으나,
- 입력 예측/리샘플링/권한 모델을 제대로 맞추지 않으면 지연/튐/역보정 문제가 남습니다.
- 프로덕션 기준으로는 A안을 권장합니다.

---

## 컴포넌트/프리팹/씬/스크립트 체크리스트

### 스크립트
- [ ] `RoomLauncher.OnInput()` 구현 완료
- [ ] `PlayerNetworkInput` 정의
- [ ] `PlayerNetworkMotor.FixedUpdateNetwork()` 구현
- [ ] `PlayerController`의 로컬 `Move` 중복 제거 또는 네트워크 권한 조건 분기

### 프리팹 (`Assets/_Project/Prefabs/02_Player/Player.prefab`)
- [ ] `NetworkObject` 유지
- [ ] `NetworkedBehaviours`에 네트워크 이동 컴포넌트 포함
- [ ] `CharacterController`와 네트워크 이동 컴포넌트 충돌 없는지 확인

### 씬
- [ ] 플레이어 프리팹이 `NetworkProjectConfig` PrefabTable에 등록되어 있는지 확인
- [ ] Host/Client 모두 동일한 빌드/씬/PrefabTable 사용 확인

### 런타임 검증
- [ ] Host에서 클라이언트 플레이어 이동이 보이는지
- [ ] Client에서 Host 플레이어 이동이 보이는지
- [ ] 양쪽에서 점프/중력/회전이 동일하게 재현되는지
- [ ] 패킷 지연 환경에서 순간이동/튐 여부 확인

---

## 왜 지금 증상이 정확히 발생하는가 (사용자 증상과 매칭)

사용자 설명:
- "호스트/클라이언트 접속 OK"
- "카메라 배정 OK"
- "서로 조작해서 위치 변환 시 동기화 안 됨"

프로젝트 상태:
- 접속/스폰/카메라/로컬 분기는 존재함.
- 하지만 네트워크 입력/네트워크 이동 동기화 루프가 없음.

즉, 현재 증상은 **구조적으로 100% 재현 가능한 정상(버그) 결과**입니다. 접속 성공과 위치 동기화 성공은 별개이며, 후자가 아직 구현되지 않은 상태입니다.
