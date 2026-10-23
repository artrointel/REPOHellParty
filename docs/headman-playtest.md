# Headman 보호본능 플레이테스트

Debug의 전용 테스트 프로필은 **난이도 5**를 강제한다. 시작 위치 근처의 연결된 NavMesh에 Headman 1마리, Headman 반경 20m 내 빈 위치에 특수능력이 없는 Animal 1마리를 준비한다. 샷건·수면총도 시작 지점에 각 1개 생성한다. 동료 위치를 찾지 못하면 경고를 기록하며 자연 생성 동료로 확인할 수 있다.

프로필의 `BepInEx/config/artrointel.HellParty.cfg`에서 `[Debug.Headman] Enabled = true`로 설정한다. Headman·Clown·Reaper·Upscream·CleanupCrew·Huntsman은 false로 설정한다. Headman은 가장 높은 우선순위를 가지며 다른 전용 테스트 생성과 키를 실행하지 않는다. 순정 시야나 AI를 상시 차단하지 않는다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1
```

1. 난이도 5와 `DEBUG Headman: requested ...`, 동료 Animal 위치 로그를 확인한다. Clown 등 이전 전용 테스트 몬스터가 추가 생성되지 않아야 한다.
2. Headman에게 먼저 보이지 않도록 접근해 20m 안의 동료 Animal만 플레이어를 발견하게 한다. 약 0.5초 이내 Headman이 같은 플레이어를 추적해야 한다.
3. Headman이 타깃이 없는 상태에서 20m 안의 동료에게 피해를 준다. 공격한 플레이어를 추적해야 한다. 치명타·총격·근접·물체 충돌을 각각 확인한다. 출처 없는 환경 피해는 임의의 플레이어를 지목하지 않아야 한다.
4. A를 추적 중인 Headman 주변에서 B가 동료를 공격해도 A를 유지해야 한다. 순정 추적이 끝난 뒤에는 다른 동료의 타깃을 다시 공유받을 수 있어야 한다.
5. 동료가 정확히 20m 이내·밖에 있는 경우를 비교한다. 벽 뒤라도 거리 조건을 만족하면 공유해야 한다. 본체 기절·스폰·디스폰 중에는 강제로 추적 상태로 바뀌면 안 된다.
6. Reaper 전력질주의 Headman 몸체에는 보호본능이 부여되지 않고 원래 Reaper 타깃을 유지해야 한다.
7. 바닐라 참가자가 동료의 타깃 또는 공격자인 경우에도 같은 추적·이동·공격 결과를 확인한다. 자동 검사는 실제 멀티플레이 검증을 대체하지 않는다.

일반 진행으로 돌아가려면 Headman을 포함한 여섯 Debug 시나리오를 모두 끄거나 Release를 사용한다.
