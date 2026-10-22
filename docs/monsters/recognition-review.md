# 몬스터 인지 상태 구현 검토

이 문서는 개별 몬스터 기획이 아닌 공통 기술 검토 기록이다. 현재 게임 어셈블리 분석이나 실행 검증 결과가 아니라 공개 모드의 제작자 소스를 확인한 결과다. 대상 게임 버전에서 멤버와 호출 순서를 다시 확인해야 한다.

확정된 기획 정의는 [인지 상태 메타데이터](../metadata/recognition-state.md)를 따른다. 인지는 순정의 시각 감지나 특정 행동 상태가 아니라 **타깃 참조를 보유한 상태**다.

## 공개 소스에서 확인한 구조

- `EnemyVision.VisionTrigger(int playerID, PlayerAvatar player)`를 가로채 시각 감지를 차단하는 코드가 있다. 이 코드는 `VisionTriggered[playerID]`와 `VisionsTriggered[playerID]`를 초기화한다. 플레이어별 시각 감지 정보를 다루는 지점임을 확인할 수 있다.
- 공통 `Enemy`에는 `SetChaseTarget(PlayerAvatar)`, `TargetPlayerAvatar`, `CurrentState`를 사용하는 코드가 있다.
- `EnemyAnimal`과 `EnemyDuck` 등은 별도의 `playerTarget`을 사용하는 패치가 있다. 다른 몬스터에는 `targetPlayer`나 `currentTarget`처럼 다른 필드도 사용된다.
- 오리는 `EnemyDuck.State.Roam`, `EnemyDuck.State.AttackStart`와 `UpdateState`를 사용하는 코드가 있다. 발견과 공격 개시가 동일한 사건이라고 가정하면 안 된다.
- Huntsman의 코드명인 `EnemyHunter`는 `State.Aim`, `investigatePointTransform`, 접촉·집기 대상도 참조한다. 시각 감지 하나만으로 모든 몬스터를 처리할 수 있다고 볼 근거는 없다.

출처: 제작자 공개 소스 [EnemyVisionPatch.cs](https://github.com/proferabg/RepoAdminMenu/blob/main/Patches/EnemyVisionPatch.cs), [NoTargetPatches.cs](https://github.com/proferabg/RepoAdminMenu/blob/main/Patches/NoTargetPatches.cs).

## 권장 구현 방향

몬스터별 순정 타깃 참조를 모드의 공통 별칭 `chaseTarget`에 연결하고, null 여부만으로 공통 인지 상태를 판정한다. 아래 내용은 구현 방향이며, 실제 게임 버전에서의 필드 연결은 아직 검증하지 않았다.

- `SetChaseTarget` 호출 사실만으로 판정하지 않고 실제 저장된 타깃 참조를 읽는다. 다른 경로에서 타깃이 할당·해제되는 경우도 반영한다.
- 몬스터별 타깃 필드의 non-null 여부를 판정 기준으로 사용한다. 미리 고른 대상이나 시야에서 놓친 대상도 참조가 남아 있으면 기획상 인지 상태에 해당한다.
- 플레이어가 몬스터를 바라보는 것과 몬스터가 플레이어를 인지하는 것을 구분한다. `EnemyOnScreen.GetOnScreen`이라는 이름만으로 인지 전환이라고 해석하지 않는다.
- 호스트에서 미인지 → 인지 전환을 감지한다. 같은 상태를 매 프레임 확인하더라도 발동·타이머를 중복 시작하지 않는다.
- Animal은 `EnemyAnimal.playerTarget`을 공통 타깃에 연결할 후보로 확인한다. 단일 타깃 필드를 쓰지 않는 몬스터는 동등한 타깃 보유 정보를 별도로 연결한다.
- 오리의 분신은 인지 조건의 예외로 본체 스폰 즉시 시작한다. Crown은 능력 부여 시 생성한다.

## 아직 확정하지 않은 기획

- 앞으로 추가할 스킬의 최초 발동 시점은 개별 명세에서 정한다. Animal 독가스는 현재 타깃이 3m 이내일 때 발동하고, 분화는 원본 사망 시 발동한다. 오리의 분신은 인지와 무관하게 본체 스폰 즉시 시작한다.
- 인지 해제 시 이미 생성된 효과를 어떻게 처리할지. 신규 발동은 인지 상태를 조건으로 한다.
- 인지를 잃었다가 다시 확보했을 때 쿨타임을 유지·정지·초기화할지.

위 항목은 인지 시작 조건과 별개의 규칙이다. 현재 기획에서 임의로 확정하지 않는다.
