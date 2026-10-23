# Clown 플레이테스트

Debug 빌드에서 프로필의 `BepInEx/config/artrointel.HellParty.cfg`를 다음과 같이 설정하고 게임을 재시작한다.

```ini
[Debug.Headman]
Enabled = false

[Debug.Clown]
Enabled = true

[Debug.Reaper]
Enabled = false

[Debug.Upscream]
Enabled = false

[Debug.CleanupCrew]
Enabled = false

[Debug.Huntsman]
Enabled = false
```

Headman 테스트를 끄면 Clown 시나리오가 우선하며 스테이지와 무관하게 난이도 **5**를 유지한다. 시작 위치 4~20m 내 연결된 NavMesh에 Clown 1마리, 시작 지점 근처에 샷건·수면총 각 1개를 준비한다. 몬스터 위치 탐색에 실패해도 무기는 독립적으로 생성한다. 순정 타깃 획득·AI는 유지한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1
```

1. `Stage ... difficulty 5`와 `DEBUG Clown: requested ...` 로그를 확인한다. 다른 테스트 몬스터가 추가 생성되지 않아야 한다.
2. Clown에게 아직 발견되지 않은 상태에서 총을 쏜다. 피해·기절이 차단되고 공격자를 향한 Staff가 나타나 2초 후 발사해야 한다. 산탄 한 발의 여러 탄환이 Staff 여러 개를 만들면 안 된다.
3. Staff 생성 후 옆으로 이동한다. 조준은 처음 위치에 고정되어야 한다. 이미 공격 대상이 있는 Clown은 총격에 정상적으로 피해·기절을 받아야 한다.
4. 순정 레이저 공격과 플레이어 대상 근접 공격을 유도한다. 공격마다 Staff 1개가 생성되어야 하며 물체 대상 근접 공격에는 생성하지 않아야 한다.
5. 고정 Staff를 반복해서 잡아 강제 해제·피해 10을 확인한다. 고정 중 파괴·추출이 안 되고 발사 종료 후 제거되어야 한다. 물리 반동 경고도 확인한다.
6. 대기 중·발사 중 Staff가 있는 상태에서 본체를 죽인다. 발사를 멈추고 떨어진 Staff가 수집·추출 가능해야 한다. 원래 발사 종료 시간이 지나도 남아 있어야 한다. 이미 사라진 Staff는 다시 생성되면 안 된다.
7. 모드 없는 참가자가 공격자·Grab 시도자인 경우에도 조준·발사·피해·기절 차단·보상 결과를 확인한다. 실제 Unity 플레이는 자동 검증과 별도로 수행한다.

정상 진행으로 돌아가려면 여섯 Debug 시나리오를 모두 끄거나 Release를 사용한다. Release에는 테스트 생성·난이도 강제 코드가 없다.
