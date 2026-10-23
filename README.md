# REPO HellParty

R.E.P.O.에 스테이지별 누적 난이도, 몬스터 특수 능력과 글로벌 이벤트를 추가하는 호스트 전용 모드다.

## 기본 조건

- 호스트만 설치하고 바닐라 참가자에게 플러그인·에셋·설정 변경을 요구하지 않는다.
- 순정 프리팹·애니메이션·RPC를 사용해 이동·공격·피해·보상을 전달한다.
- 난이도 계산과 능력 판정은 호스트에서 수행한다. 실제 바닐라 참가자 화면과 피해·수집 결과는 게임 안에서 확인한다.

## 난이도 진행

첫 스테이지부터 **난이도 1**로 시작하며, 3스테이지를 완료할 때마다 다음 단계로 상승한다.

| 현재 스테이지 | 헬파티 난이도 |
| --- | --- |
| 1~3 | 1 |
| 4~6 | 2 |
| 7~9 | 3 |
| 10~12 | 4 |
| 13 이상 | 5 — 최대 |

계산식은 `min(5, 1 + max(0, levelsCompleted) / 3)`이며 순정 완료 스테이지 수를 사용한다. 실패·재시도만으로 추가 상승시키지 않고, 이어 하는 런에도 같은 계산을 적용한다. 0단계는 순정 동작 기준으로 남기며 일반 진행에서는 사용하지 않는다. Debug 전용 시나리오가 켜져 있으면 그 시나리오의 강제 난이도가 우선한다. 자세한 기준은 [공용 난이도](docs/metadata/difficulty.md)를 따른다.

## 구현된 몬스터 능력

해금 단계와 한 줄 요약은 [특수 능력 기획표](docs/monster-abilities.md), 세부 수치·예외는 [몬스터별 문서](docs/monsters/README.md)에서 관리한다.

- [Apex Predator](docs/monsters/apex-predator.md): 분신1·2·3은 난이도 1·3·5에 최대 1·2·3마리를 유지한다. 본체 Crown과 사망 보상, 15m 거리 이탈 시 순정 애니메이션 복귀를 사용한다.
- [Animal](docs/monsters/animal.md): 난이도 2의 독가스는 현재 타깃이 3m 이내일 때 대상 위치에 10초 쿨타임으로 생성한다. 난이도 3의 분화는 사망 0.5초 후 체력 50%인 개체 2마리를 생성하며 각각 20초 안에 처치해야 코어를 얻는다.
- [Birthday Boy](docs/monsters/birthday-boy.md): 난이도 1·2·3에 풍선강화1·2·3을 해금한다. 근접 자동 파열·일반 파열의 Gnome, 자동 파열 어그로, 본체 피격 시 타깃을 공유하는 주변 풍선 일괄 파열을 적용한다.
- [Bella](docs/monsters/bella.md): 난이도 1의 자폭은 탑승 Clown Doll을 벨에 연동한다. 난이도 3의 Tiny 미끼는 새 Grab마다 공격을 유도하고 15m 이상 이탈하면 제거한다. 본체 사망 시 남은 물품은 수집 보상으로 전환하며 탑승 인형 폭발은 Bella에게 피해를 주지 않는다.
- [Bowtie](docs/monsters/bowtie.md): 난이도 2의 형제와 4의 복수로 무코어 Elsa를 소환한다. 순정 변신 후 공격하고 10초 후 애니메이션으로 디스폰하며, 자신을 소환한 Bowtie의 바람 공격을 무시한다.
- [Chef](docs/monsters/chef.md)·[Upscream](docs/monsters/upscream.md): 난이도 1부터 공유 체력·내구도의 수집품 변장을 사용한다. Chef는 Frog, 자연 생성 Upscream은 제외 목록을 적용한 Big 수집품 24종 중 하나를 사용한다. 시야 상실 5초 후 배회 복귀, Grab 해제 후 순정 공격, 환경 파괴 시 코어 보상, 드론·노움 제외를 적용한다. 다른 능력과 웨이브가 소환한 Upscream은 변장하지 않는다.
- [Loom](docs/monsters/loom.md)·[Robe](docs/monsters/robe.md): 난이도 1부터 각 개체가 [스토커](docs/monsters/stalking-doll.md) 1마리를 동반한다. 스토커 체력은 100이며 기본 분노와 난이도 3의 폭주를 보유한다.
- [Peeper](docs/monsters/peeper.md): 난이도 1의 유효 타격마다 Gnome, 난이도 2부터 30% 확률의 추가 Banger를 소환한다. 둘을 합쳐 본체별 누적 10마리로 제한한다.
- [Mentalist](docs/monsters/mentalist.md): 난이도 2부터 타깃이 없을 때 총격 피해·기절을 막고 레이저총으로 공격자의 당시 위치를 향해 2초 뒤 반격한다.
- [Huntsman](docs/monsters/huntsman.md): 난이도 2부터 공격·추적 중 7m 내 최근접 플레이어를 납치한다. 3초 후부터 1초마다 피해 2를 주며, 운반 중 사격·피해자 소음 반응·피해자의 본체 Grab을 차단한다. 피격·넘어짐 등에 해제되고 5초 쿨타임을 적용한다. 난이도 4의 죽음의전술은 사망 시 확보 가능한 Human Grenade를 핀을 뽑아 던진다.
- [Cleanup Crew](docs/monsters/cleanup-crew.md): 난이도 1의 부화는 대상이 있는 헤드 폭발 2초 후 무코어 Spewer를 생성한다. 난이도 5의 부화2는 Upscream으로 대체하며, 10초 수명 또는 던지기 적용 시 디스폰한다.
- [Reaper](docs/monsters/reaper.md): 난이도 2의 전력질주는 추적 대상이 10m 이상 멀어지면 Headman으로 전환해 빠르게 접근하고 3m 이내에 Reaper로 돌아온다. 쿨타임은 사용부터 10초이며 체력·대상·코어 보상을 이어받는다.
- [Shadow Child](docs/monsters/shadow-child.md): 난이도 1의 [블라인드](docs/metadata/blind.md)는 같은 대상을 유지하면 3초마다 당시 시야 높이에 순정 독가스를 생성한다. 가스는 생성 지점에 남는다.

[Clown](docs/monsters/clown.md)은 난이도 3의 레이저스태프소환과 난이도 5의 레이저쉴드를 사용한다. 타깃이 없을 때 총격 피해·기절을 차단하고 2초 뒤 고정 조준한 Staff로 반격한다. [Loom](docs/monsters/loom.md)은 난이도 4부터 납치를 사용하며 Huntsman과 장애물 검사·전역 납치 슬롯을 공유한다. 능력 기획이 없는 몬스터는 순정 상태를 유지한다.

- [Headman](docs/monsters/headman.md): 난이도 4의 보호본능은 타깃이 없을 때 20m 내 동료의 타깃 또는 동료를 공격한 플레이어를 공유받는다.
- [Trudge](docs/monsters/trudge.md): 난이도 2부터 내려찍기마다 1초 후 순정 Tick·Gnome·Banger 중 무작위 2마리를 소환하고 마지막 추적 대상을 전달한다. 소환수에는 특수능력과 시간 제한을 부여하지 않는다.

## 글로벌 이벤트

난이도 1부터 [몬스터 웨이브](docs/events/monster-wave.md)를 사용한다. 가치품 파괴 후 고정 Gramophone을 생성하고, 3초 뒤에도 반경 20m 안에 서 있는 플레이어가 있으면 사각지대 소환을 시작한다. 모두 웅크리거나 기어가면 취소한다.

초기 Rugrat·Upscream·Headgrab은 3초에, 노움은 4~19초에 초당 2마리씩 요청한다. 위치는 모든 플레이어로부터 3m 초과 거리를 지키고, 캐시를 재검증해 우선 사용하거나 최대 240회 추출한 후보를 최근접 플레이어까지의 거리순으로 검사한다. 재탐색은 1초 간격이다. 전체 20초에는 예고가 포함되며 종료 시 몬스터를 디스폰하고 Gramophone을 순정 연출로 파괴한다. 예고 취소는 조용히 제거하며 새 후보가 취소되어도 기존 웨이브는 유지한다.

## 개발과 테스트

[개발 환경](docs/development.md)에서 SDK·빌드·배포·실행·패키징과 Debug 설정을 확인한다. 전용 Debug 시나리오는 Headman·Clown·Reaper·Upscream·Huntsman·Cleanup Crew이며 실제 활성 대상은 프로필 설정에 따라 달라진다. 종합 테스트는 여섯 시나리오를 모두 끄거나 Release로 실행한다. 종료된 몬스터 테스트의 안내를 현재 자동 생성 시나리오로 사용하지 않는다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Dev.ps1 -Offline
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Dev.ps1 -Configuration Release -NoRestore
```

자동 검사는 규칙과 설치된 게임의 호출 계약을 확인한다. Unity 내 소환·애니메이션·화면 위치·원격 피해는 각 문서의 플레이테스트 항목으로 별도 확인한다. 빌드·테스트 통과를 실제 멀티플레이 검증 완료로 표시하지 않는다.
