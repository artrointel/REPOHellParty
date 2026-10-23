# Upscream

0단계는 순정 상태 그대로 진행한다. 난이도 **1(stage1)**부터 **변장(미믹)**을 부여한다. [Chef의 기획](chef.md)과 동일한 내구도·해제 조건을 사용하며, 공격은 Upscream의 순정 AI를 사용한다.

호스트만 모드를 설치하고 바닐라 참가자와 완벽하게 호환되는 능력만 기획한다.

## 변장(미믹)

- 변장 외형은 [지정된 Valuables 목록](https://repo-2025horror.fandom.com/wiki/Valuables)의 Big 수집품 중 하나를 매 변장마다 균등 무작위로 선택한다. **코어 구슬(Enemy Valuable), 모든 Surplus, Telescope, Unicorn Horn은 제외**한다. Chef는 계속 Frog로 변장한다.
- 후보 24종: Big Vase, Chunky Vase, Diamond Display, Scream Doll, Television, Barrel, Big Sample, Creature Leg, Flamethrower, Guitar, Icepick, Ice Saw, Sample Cooler, Baby Head, Egg, Gem Burger, Golden Swirl, Museum Boombox, Uranium Mug Deluxe, Cauldron Box, Cube of Knowledge, Forever Candle, Master Potion, Spider Potion. 현재 맵에 한정하지 않고 순정 리소스에서 선택한다. Icepick은 위키의 Big 분류를 따르되 실제 게임의 Medium 리소스 경로를 사용한다.
- 배회·대기 중 1초마다 재시도한다. 본체 반경 20m 안의 살아 있는 플레이어에게 장애물 없는 ray가 도달하면 본체 위치 근처에 무작위 Big 수집품을 생성하고 기존 Upscream을 숨긴다. 방향 제한은 없다.
- 본체를 새로 만드는 방식이 아니다. 기존 본체의 순정 스폰·디스폰과 위치 동기화를 사용하며, 변장 중 자동 재등장은 차단한다.
- 변장 중에도 변장 수집품을 기준으로 1초마다 20m·시야 조건을 검사한다. 보이는 살아 있는 플레이어가 없는 상태가 연속 5초 이어지면 변장 수집품을 제거하고 공격 대상 없이 배회로 복귀한다. 중간에 다시 보이면 시야 상실 타이머를 초기화한다.
- 변장 수집품을 Grab하면 해당 플레이어를 공격 대상으로 삼는다. 다음 프레임에 Grab을 해제하고 변장 수집품을 제거한 뒤, 기존 체력을 유지한 Upscream이 순정 인지 예고·접근·공격 과정을 수행한다. 공격 상태로 바로 건너뛰지 않는다.
- 공격 대상이 살아 있고 순정 추격이 이어지는 동안에는 재변장하지 않는다. 공격 후 Idle 회복은 대상 상실로 보지 않는다. 대상 사망·비활성화 또는 순정 추격/공격 종료의 Leave 전환 후에는 배회로 돌아간다. 변장용 20m·ray 조건으로 추격을 임의로 종료하지 않는다.
- 본체 체력과 변장 수집품의 유효 내구도 비율을 공유한다. 가치 15~100%를 내구도 0~100%로 환산하며, 피해·회복·반복 변신에서의 비율과 정수 반올림 처리는 Chef와 동일하다. 본체의 기본 체력은 순정을 사용한다.
- 무기·다른 물체·지형·몬스터 등으로 변장 수집품이 파괴되면 Upscream 처치로 판정하고 순정 코어를 드랍한다. Grab과 파괴가 겹쳐도 처치 보상은 한 번만 발생한다.
- 변장 수집품은 드론 부착과 노움의 가치품 공격 대상에서 제외한다. Grab 시 제거되므로 수집 보상으로 남지 않으며, 획득 가능 금액에도 추가하지 않는다. 일반 수집품에는 적용하지 않는다.
- 사망·스테이지 전환 시 변장 오브젝트를 정리한다.

## 소환된 Upscream의 예외

**다른 몬스터의 능력이나 몬스터 웨이브로 소환된 Upscream에는 변장 능력을 부여하지 않는다.** 청소부 부화2의 대상 계승·10초 수명·던지기 직후 디스폰·코어 미드랍 규칙은 그대로 유지한다. 웨이브 Upscream도 기존 타깃·웨이브 종료 처리를 유지한다.

공용 소환 기록(`SummonedEnemySpawnTicket`)은 실제 스폰이 끝나도 보관하여 소환 출신 여부를 계속 구분한다. Release는 이 기록이 있는 Upscream을 모두 제외한다. Debug 테스트 개체만 명시적인 예외로 변장을 허용하며, 그 예외 판정 코드는 Release에서 제외한다.

## 구현과 검증

순정 코드명은 `EnemyUpscream`이다. Chef와 `ChefMimic`·`ChefMimicFrog`·내구도·시야 상태 로직을 공유하고, Upscream용 AI 어댑터에서 `PlayerNotice → GoToPlayer → Attack`을 사용한다. 참가자 전용 코드나 사용자 정의 RPC는 추가하지 않는다.

자동 검사는 단계·소환 출신 제외·Debug 예외의 Release 제외, 기존 공용 변장 상태·체력 규칙을 검사한다. 실제 변장·코어·드론·노움·원격 참가자 동기화는 [Upscream 플레이테스트](../upscream-playtest.md)로 확인한다. 실제 게임의 회귀 확인은 플레이테스트 항목을 따른다.

단계별 요약은 [난이도별 기획표](../monster-abilities.md)에서 관리한다.
