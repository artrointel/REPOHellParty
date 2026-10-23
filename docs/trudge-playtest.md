# Trudge 소집 플레이테스트

Debug에서 `[Debug.Trudge] Enabled = true`로 설정한다. LoomRobe·Headman·Clown·Reaper·Upscream·CleanupCrew·Huntsman의 Enabled는 false로 둔다. LoomRobe가 꺼져 있으면 Trudge 시나리오가 우선하며 스테이지가 바뀌어도 난이도 **5**를 유지한다.

시작 지점 4~20m 안의 연결된 NavMesh에 Trudge 1마리, 시작 지점 근처에 샷건·수면총 각 1개를 준비한다. 무기는 몬스터 위치 탐색과 독립적으로 생성한다. Headman과 테스트 동료 Animal은 추가 생성하지 않는다. 순정 인지·공격 AI를 유지한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1
```

1. 난이도 5와 `DEBUG Trudge: requested ...` 로그를 확인한다.
2. 플레이어를 발견하게 한 뒤 내려찍기를 유도한다. 피격 여부와 관계없이 첫 타격 판정 약 3초 후 Tick·Gnome·Banger 중 2마리가 나타나야 한다. 같은 종류 2마리도 가능하다.
3. 반복 내려찍기마다 한 쌍만 생성되어야 한다. 대상 A를 추적하던 Trudge의 소환수는 A를 공유받아야 한다. Tick의 Grab 빔 반응 등 순정 고유 행동은 유지한다.
4. 소환수에는 헬파티 특수능력이 없어야 하고 시간 만료로 사라지면 안 된다. 처치·Banger 자폭은 정상 처리하고, 사망 후 재등장하지 않아야 한다.
5. 내려찍기 직후 Trudge를 처치해도 이미 예약한 소환은 완료되어야 한다. 스테이지 전환 시에는 이전 예약이 취소되어야 한다.
6. 바닐라 참가자와 생성·이동·타깃·공격·사망 결과를 함께 확인한다. 자동 검사는 실제 게임 검증을 대체하지 않는다.

일반 진행으로 돌아가려면 여덟 Debug 시나리오를 모두 끄거나 Release로 실행한다. 상세 규칙은 [Trudge 기획](monsters/trudge.md)을 따른다.
