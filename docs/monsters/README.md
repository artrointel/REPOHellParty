# 몬스터별 상세 기획

순정 몬스터 29종의 특수 능력 상세 기획을 개별 Markdown 문서로 관리한다. 기획이 없는 몬스터는 미정 상태로 두고, 앞으로 전달되는 기획을 해당 문서와 [난이도별 기획표](../monster-abilities.md)에 함께 반영한다.

모든 능력은 호스트만 설치하고 바닐라 참가자와 완벽히 호환되는 것을 필수 조건으로 한다.

공통 발동 조건은 [인지 상태 메타데이터](../metadata/recognition-state.md)를 참조한다. 몬스터별 문서에 예외가 있으면 해당 명세를 따른다.

난이도는 [공용 진행 규칙](../metadata/difficulty.md)에 따라 stage1부터 1로 시작한다. 자연 진행에서 0단계를 사용하지 않으며, 기존 능력의 해금 단계는 유지한다.

## 구현 상태

- 구현됨: 자연 생성 Elsa의 암살, Headman의 보호본능, Trudge의 소집, Clown의 레이저스태프소환·레이저쉴드, Loom의 납치, Animal, Apex Predator, Bella, Birthday Boy, Bowtie, Chef, Cleanup Crew, Huntsman, Loom·Robe의 스토커 동반, Mentalist, Peeper, Reaper의 전력질주와 Shadow Child의 블라인드, 자연 생성 Upscream의 변장, 독립 스토커.
- 현재 기획표의 능력은 구현되었으며 실제 바닐라 동반 플레이 검증은 각 플레이테스트 문서를 따른다.
- 그 밖의 순정 몬스터: 별도 능력 기획 미정이며 순정 동작 유지.

구현·자동 검사 통과와 실제 바닐라 참가자 검증은 구분한다. 각 상세 문서와 플레이테스트에 확인 범위를 기록한다. 두 빌드 구성 모두 실제 진행도를 사용하며 임시 테스트 시나리오는 제거했다. 실행은 [개발 환경](../development.md#debug와-release)을 따른다.

## 순정 몬스터 목록

- [Animal](animal.md)
- [Apex Predator](apex-predator.md)
- [Banger](banger.md)
- [Bella](bella.md)
- [Birthday Boy](birthday-boy.md)
- [Bowtie](bowtie.md)
- [Chef](chef.md)
- [Cleanup Crew](cleanup-crew.md)
- [Clown](clown.md)
- [Elsa](elsa.md)
- [Gambit](gambit.md)
- [Gnome](gnome.md)
- [Headgrab](headgrab.md)
- [Headman](headman.md)
- [Heart Hugger](heart-hugger.md)
- [Hidden](hidden.md)
- [Huntsman](huntsman.md)
- [Loom](loom.md)
- [Mentalist](mentalist.md)
- [Oogly](oogly.md)
- [Peeper](peeper.md)
- [Reaper](reaper.md)
- [Robe](robe.md)
- [Rugrat](rugrat.md)
- [Shadow Child](shadow-child.md)
- [Spewer](spewer.md)
- [Tick](tick.md)
- [Trudge](trudge.md)
- [Upscream](upscream.md)

## 독립 추가 몬스터

- [자율 Scream Doll — Stalking Doll (가칭)](stalking-doll.md): 난이도 1부터 Loom·Robe에 동반 등장하며 분노를 기본 탑재하고, 난이도 3부터 일반 공격의 유효 타격으로 분노하는 폭주를 보유한다.
