# Reaper 전력질주 플레이테스트

Reaper의 [전력질주](monsters/reaper.md)를 확인한다. 기존 Reaper 블라인드 테스트를 대체하며, Shadow Child의 블라인드는 자연 생성 개체로 별도 확인한다.

## Debug 준비

프로필의 `BepInEx/config/artrointel.HellParty.cfg`에서 다음과 같이 선택한 뒤 게임을 재시작한다.

```ini
[Debug.Headman]
Enabled = false

[Debug.Clown]
Enabled = false

[Debug.Reaper]
Enabled = true

[Debug.Upscream]
Enabled = false

[Debug.CleanupCrew]
Enabled = false

[Debug.Huntsman]
Enabled = false
```

Clown 테스트를 끄면 이 시나리오가 우선하며 난이도를 **2**로 유지한다. 이전 `Debug.Blind` 설정은 더 이상 사용하지 않는다. 시작 위치 4~20m 안의 연결된 NavMesh에 Reaper 1마리와 시작 지점 근처에 샷건·수면총 각 1개를 준비한다. 몬스터 위치 탐색에 실패해도 무기는 독립적으로 생성한다. 별도 테스트 키나 강제 타깃은 없으며 순정 인지·추격을 유지한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1
```

## 확인 항목

1. 로그가 난이도 2이고 `DEBUG Reaper: requested ...` 생성 요청이 한 번 나오는지 확인한다. 이전 테스트 몬스터가 추가 생성되지 않아야 한다.
2. Reaper의 순정 인지를 유도한다. 타깃이 없거나 10m 미만이면 전력질주가 없어야 한다. 추적 대상과의 거리가 정확히 10m 이상이면 Headman으로 전환하고 빠르게 따라와야 한다. Reaper에서 독가스가 나오면 안 된다.
3. 대상에게 정확히 3m 이내 접근하면 Reaper로 돌아와 같은 플레이어에 대한 순정 추격·공격을 이어가야 한다. 출발 위치로 되돌아가거나 체력을 회복하면 안 된다.
4. 복귀 후 다시 거리를 벌려 사용 시점부터 10초 전에 재발동하지 않는지 확인한다. 10초 후 10m 이상이면 다시 사용할 수 있어야 한다. Headman 상태에서는 쿨타임이 지나도 추가 Headman을 생성하지 않아야 한다.
5. Reaper 상태에서 비치명 피해를 주고 변신시킨 뒤 Headman에게도 피해를 준다. 왕복 변신으로 체력이 초기화되지 않는지 확인한다. 수면총 기절은 각 모습에서 유지되고 기절 중 3m 복귀가 발생하면 남은 시간이 이어져야 한다.
6. Headman 상태에서 처치하면 그 위치 근처에 Reaper 기준 코어가 정상 횟수로 나오고 두 몸체의 코어가 중복되지 않아야 한다. Reaper 상태의 정상 처치와 비교한다. 숨겨진 개체가 혼자 재등장하지 않아야 한다.
7. Headman이 추적하던 플레이어가 죽거나 퇴장하면 Reaper로 복귀하고 다른 플레이어에게 임의로 돌진하지 않아야 한다.
8. 바닐라 참가자가 공격 대상인 경우에도 모델 교체·추격·3m 복귀·피격·기절·코어를 확인한다. 첫 전환의 순정 등록·등장 지연과 빠른 이동 중 위치 튐·NavMesh 오류도 확인한다.

정상 진행으로 돌아가려면 Headman·Clown·Reaper·Upscream·CleanupCrew·Huntsman 여섯 시나리오를 모두 끄거나 Release를 사용한다. Release에는 전용 테스트 생성·강제 난이도 코드가 없다.
