# 플레이테스트

실제 게임에서 몬스터 행동·피해·보상과 바닐라 참가자 동기화를 확인하는 문서다. 자동 검사 코드는 저장소의 `tests/`에서, 빌드·실행 방법은 [개발 환경](../development.md)에서 관리한다.

기본 Debug와 Release는 임시 몬스터·무기 생성이나 강제 난이도를 사용하지 않는다. 청소부는 `-TestScenario CleanupCrew`로 별도 테스트 Debug를 빌드해 빠르게 준비할 수 있으며, 해당 코드가 기본 빌드에 포함되지 않도록 검사한다. 각 몬스터의 실제 해금 조건은 [기획표](../monster-abilities.md)와 [상세 기획](../monsters/README.md)을 따른다.

| 대상 | 확인 문서 |
| --- | --- |
| Apex Predator | [분신과 Crown](apex-playtest.md) |
| Animal | [독가스와 분화](animal-playtest.md) |
| Bella | [자폭과 미끼](bella-playtest.md) |
| Birthday Boy | [풍선과 Gnome](birthday-boy-playtest.md) |
| Shadow Child | [블라인드](blind-playtest.md) |
| Bowtie | [형제와 복수](bowtie-playtest.md) |
| Chef | [Frog 변장](chef-playtest.md) |
| Cleanup Crew | [끈적이는 헤드와 부화](cleanup-crew-playtest.md) |
| Clown | [레이저 Staff와 쉴드](clown-playtest.md) |
| Elsa | [고립 대상 암살과 즉시 해제](elsa-playtest.md) |
| Headman | [보호본능](headman-playtest.md) |
| Huntsman | [납치와 죽음의전술](huntsman-playtest.md) |
| Loom·Robe | [스토커 동반과 Loom 납치](loom-robe-playtest.md) |
| Mentalist | [반사쉴드](mentalist-playtest.md) |
| Peeper | [지원 몬스터 소환](peeper-playtest.md) |
| Reaper | [전력질주](reaper-playtest.md) |
| Stalking Doll | [관찰·추적·분노와 물리 이동](stalking-doll-playtest.md) |
| Trudge | [소집](trudge-playtest.md) |
| Upscream | [Big 수집품 변장](upscream-playtest.md) |

[몬스터 웨이브의 실제 테스트](../events/monster-wave.md#실제-테스트)와 [공개 전 통합 확인](../release-readiness.md#공개-전-실제-플레이-확인)도 함께 수행한다.
