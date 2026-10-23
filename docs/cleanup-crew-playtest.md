# Cleanup Crew 부화 플레이테스트

아래 전용 Debug 테스트는 `[Debug.Trudge]
Enabled = false

[Debug.Headman]
Enabled = false

[Debug.Clown]
Enabled = false

[Debug.Reaper] Enabled = false`, `[Debug.Upscream] Enabled = false`와 해당 시나리오의 `Enabled = true`를 설정한 뒤 재시작하여 선택한다. 실제 실행 대상은 프로필 설정을 따르며 [개발 환경](development.md#debug와-release)에서 우선순위를 확인한다.

구현과 Debug/Release 빌드·자동 검사를 완료했다. 아래 항목은 실제 게임에서 확인할 회귀 검사 목록이다. 호스트만 HellParty를 설치하고 바닐라 참가자로도 확인한다.

## 실행과 Debug 키

게임을 종료한 뒤 저장소 루트에서 실행한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1
```

Reaper·Upscream을 끄고 Cleanup Crew와 Huntsman을 켠 Debug는 난이도 5와 시작 위치 20m 이내의 각 1마리, 샷건·수면총 각 1개를 준비한다. Huntsman을 false로 두면 Cleanup Crew만 생성한다. 두 몬스터의 생성 지점은 최소 3m 떨어뜨리고 각각 독립적으로 탐색한다. 유효한 NavMesh가 없는 트럭 내부는 피하며, 몬스터 위치 탐색이 실패해도 무기는 따로 생성한다.

- **F9:** 난이도 5(부화2·Upscream)와 1(부화·Spewer)을 전환한다. 다음 폭발부터 적용하며 이미 예약된 소환은 폭발 시점의 종류를 유지한다.
- **F10:** 테스트 Cleanup Crew가 호스트를 순정 인지 경로로 발견하도록 요청한다. 순정 스폰·기절·이미 진행 중인 공격 조건은 유지하므로 반응하지 않으면 연출이 끝난 뒤 다시 누른다.
- **F11:** 이미 분리되어 활성화된 헤드의 폭발 예고를 시작한다. 즉시 소환하는 키가 아니며 순정 폭발 연출 후 2초를 기다린다. 활성 헤드가 없으면 로그로 안내한다.

프로필의 `BepInEx/config/artrointel.HellParty.cfg`에서 `[Debug.CleanupCrew]`와 `[Debug.Huntsman]`의 `Enabled = true`가 기본값이다. 둘 다 켜면 두 몬스터가 함께 생성된다. 하나만 테스트하려면 다른 몬스터의 설정을 false로 바꾸고 재시작한다. Huntsman 키는 F6(1인 조건 우회)·F7(조준 요청)·F8(납치 해제)이다. F9는 공용 난이도를 바꾸므로 1단계에서는 Huntsman이 새 납치를 시작하지 않는다. 함께 테스트하려면 다시 5단계로 돌린다. 종합 테스트에서는 Trudge·Headman·Clown·Reaper·Upscream·CleanupCrew·Huntsman 네 설정을 모두 false로 바꾸거나 Release를 사용한다. Release에는 자동 생성·키·강제 난이도 코드가 포함되지 않는다.

## 확인 항목

1. 기본 5단계에서 자연 공격 또는 F10 후 헤드를 기다린다. 실제 폭발 2초 뒤 폭발 위치 또는 가까운 유효 바닥에 Upscream 1마리만 나타나야 한다.
2. F9으로 1단계로 전환하고 다음 공격에서는 Spewer 1마리만 나타나는지 확인한다. 2~4단계는 Spewer 유지, 0단계는 추가 소환 없음도 Release의 정상 진행에서 확인한다.
3. 두 플레이어가 있는 상황에서 헤드가 A를 추적할 때 본체의 타깃을 B로 바꾼다. 소환수의 최초 대상은 폭발 당시 헤드의 대상 A여야 한다. 이후 순정 AI의 자연스러운 대상 변경은 허용한다.
4. 헤드 하나의 폭발은 소환 한 번만 발생해야 한다. 같은 본체가 다음 헤드를 생성한 경우에는 다시 소환되어야 한다. 여러 본체의 서로 다른 헤드는 각각 판정한다.
5. 공격 후 조기 디스폰하지 않은 소환수는 실제 스폰부터 10초가 되면 공격·추적·기절 중에도 순정 사라짐 연출로 디스폰되어야 한다. Spewer가 얼굴에 부착된 경우에는 부착 연출도 종료되고 다시 활성화되면 안 된다. 소환 전에 기다린 2초는 수명에 포함하지 않는다.
6. 만료 전에 처치해도 코어가 나오면 안 된다. 10초 만료와 처치가 겹치거나 장시간 기다려도 보상·재등장이 발생하면 안 된다.
7. 폭발 대상이 없으면 소환하지 않아야 한다. 대기 중 대상 사망·퇴장은 예약 취소, 소환 후 대상 사망·퇴장은 디스폰이어야 한다. 스테이지 전환 후 이전 예약이 발생하면 안 된다.
8. 폭발 후 본체를 처치하더라도 이미 저장된 대상이 유효하면 예약이 이어져야 한다. 벽·계단·소품 주변에서도 소환수가 이동하며, NavMesh 오류나 제자리 고정이 없어야 한다. 유효한 보정 지점이 없으면 경고 후 해당 소환만 취소한다.
9. 부화2 Upscream이 플레이어를 던지는 순간 10초 이전이어도 순정 디스폰을 시작해야 한다. 던져진 플레이어의 이동과 착지 피해는 이어져야 하며, 같은 공격의 중복 이벤트로 다시 던지면 안 된다. 공격하기 전에 대상이 사망·퇴장한 경우에는 던지지 않아야 한다. 호스트와 바닐라 참가자 모두 확인한다. Spewer와 일반 Upscream에는 이 공격 후 조기 디스폰 규칙이 적용되지 않아야 한다.

자동 검사는 다음 명령으로 실행한다. 실제 시각 연출·원격 동기화·플레이어 피해까지 증명하는 검사는 아니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Dev.ps1 -Offline
```

상세 사양은 [Cleanup Crew](monsters/cleanup-crew.md)를 따른다.
