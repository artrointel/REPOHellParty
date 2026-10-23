# 몬스터 인지 상태 구현 검토

현재 저장소의 런타임 코드와 설치된 게임 어셈블리 계약을 기준으로 정리한다. 과거 공개 모드 소스에서 추정했던 범용 `SetChaseTarget` 호출을 모든 몬스터에 공통 적용하지 않는다. 실제 스킬은 각 순정 클래스의 타깃 필드와 이벤트에 연결한다.

기획상의 인지는 [인지 상태 메타데이터](../metadata/recognition-state.md)에 따라 **타깃 참조를 보유한 상태**다. 플레이어 시야·거리·실제 공격 상태는 스킬에 명시된 경우에만 추가한다.

## 실제 연결

| 몬스터 | 현재 구현이 사용하는 정보 |
| --- | --- |
| Animal | `EnemyAnimal.playerTarget`; 독가스는 타깃과의 3m 조건을 추가한다. |
| Headman | 자신의 `Enemy.TargetPlayerAvatar`와 실제 추적 상태를 확인하고, 타깃이 없으면 20m 내 동료의 타깃 또는 동료 피격의 플레이어 발생 문맥을 이어받는다. |
| Trudge | `EnemySlowWalker.targetPlayer`의 마지막 유효 대상을 보관한다. 발동은 대상 보유나 피해 여부 대신 `SlowWalkerAttack`의 실제 내려찍기 첫 타격 이벤트다. |
| Reaper | `EnemyRunner.targetPlayer`와 순정 추적 상태; 10m 이상에서 Headman으로 전환하고 3m 이내에 Reaper로 복귀한다. |
| Shadow Child | `EnemyThinMan.playerTarget`; 블라인드의 공통 타이머 규칙을 사용한다. |
| Mentalist | `EnemyFloater.targetPlayer == null`일 때 반사쉴드를 허용한다. 타깃 보유 조건의 반대다. |
| Clown | `EnemyBeamer`의 실제 Attack·플레이어 대상 Melee 진입에서 Staff를 소환한다. 레이저쉴드는 `playerTarget == null`일 때 총격 피해·기절을 차단하고 반격한다. |
| Loom | `EnemyShadow.playerTarget`이 있고 Follow·Slap 상태일 때 납치한다. Huntsman과 납치 직전 시야 ray 차폐 검사·전역 납치 슬롯을 공유한다. |
| Bowtie | `EnemyBowtie.playerTarget`과 실제 바람 공격 시작 이벤트를 함께 사용한다. 복수는 사망 이벤트다. |
| Peeper | `EnemyCeilingEye.targetPlayer`와 실제 체력 감소 확인을 사용한다. |
| Bella | 순정 벨 이벤트에 연동하며 Grab 시 `EnemyTricycle.playerTarget`을 잡은 플레이어로 지정한다. 경고 중 타깃이 비어 있을 수 있다. |
| Birthday Boy | 일반·자동 파열과 피격·사망 이벤트가 발동 조건이다. 피격 지원 소환은 `EnemyBirthdayBoy.playerTarget`을 우선 공유한다. |
| Huntsman | `EnemyHunter.State.Aim`·`Shoot`, 또는 플레이어의 시야 Transform을 추적 중인 `InvestigateWalk`를 납치 조건으로 사용한다. 단순 소음 조사 이동은 제외한다. |
| Cleanup Crew | 본체가 아닌 `EnemyBombThrowerHead.playerTarget`을 폭발 직전에 저장한다. |

## 타깃 보유와 별도의 발동 조건

- Apex Predator의 분신과 Crown은 본체 스폰 시점부터 시작하며 인지를 기다리지 않는다.
- Chef와 자연 생성 Upscream의 변장은 배회·대기 중 플레이어 탐색과 수집품 Grab·파괴·시야 상실로 제어한다. 20m·ray는 변장 조건이며 순정 추격 종료 조건을 대신하지 않는다.
- Loom·Robe의 스토커 동반은 본체의 실제 등장에, 스토커의 분노·폭주는 Grab·유효 타격에 연결한다.
- Animal 분화·Bowtie 복수·Huntsman 죽음의전술은 사망 시 발동한다.
- 몬스터 웨이브는 가치품 파괴와 3초 후 플레이어 자세를 사용하며 몬스터 인지와 무관하다.

## 타이머와 검증 범위

타깃 교체만으로 모든 스킬의 쿨타임을 일괄 초기화하지 않는다. 블라인드는 동일 타깃 연속 유지 시간이므로 타깃 교체·상실 때 다시 3초를 세지만, Animal 독가스는 기존 10초 쿨타임을 유지한다. 이미 생성된 가스·소환수의 수명도 개별 명세를 따른다.

`tests/HellParty.Tests`는 순정 필드·메서드·RPC 인증 계약과 Unity 없이 실행 가능한 상태·타이머 규칙을 검사한다. 실제 원격 시야·이동·피해는 별도 플레이테스트로 확인한다. 난이도와 시작 스테이지는 [공용 난이도](../metadata/difficulty.md)를 따른다.
