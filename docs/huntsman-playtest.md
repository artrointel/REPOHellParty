# Huntsman 납치 플레이테스트

아래 전용 Debug 테스트는 `[Debug.Headman]
Enabled = false

[Debug.Clown]
Enabled = false

[Debug.Reaper] Enabled = false`, `[Debug.Upscream] Enabled = false`와 해당 시나리오의 `Enabled = true`를 설정한 뒤 재시작하여 선택한다. 실제 실행 대상은 프로필 설정을 따르며 [개발 환경](development.md#debug와-release)에서 우선순위를 확인한다.

구현·Debug/Release 빌드 및 자동 검사를 완료했다. 아래 항목은 실제 게임에서 확인할 회귀 검사 목록이다. 바닐라 참가자를 포함한 실제 동기화 확인이 필요하다.

## Debug 테스트 준비

`[Debug.Headman]
Enabled = false

[Debug.Clown]
Enabled = false

[Debug.Reaper] Enabled = false`, `[Debug.Upscream] Enabled = false`, `[Debug.Huntsman] Enabled = true`로 선택한다. Cleanup Crew도 true면 둘을 각각 1마리씩 생성하며, Huntsman만 테스트하려면 `[Debug.CleanupCrew] Enabled = false`로 설정한다. 아래 F6·F7·F8은 Huntsman용이고 Cleanup Crew는 F9·F10·F11을 사용한다. F9로 공용 난이도를 1로 내린 동안에는 Huntsman이 새 납치를 시작하지 않으므로 함께 확인하려면 5로 돌린다. 기존 프로필 값이 코드상 기본값보다 우선한다.

호스트만 HellParty를 설치한다. 게임을 종료한 상태에서 저장소 루트에서 실행한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1
```

- Debug의 기본 설정은 `[Debug.Huntsman] Enabled = true`다. 프로필의 `BepInEx/config/artrointel.HellParty.cfg`에서 변경하고 게임을 다시 시작한다.
- 켜져 있으면 각 일반 스테이지에서 난이도를 5로 유지하고 시작 위치 20m 이내의 유효 NavMesh 위치에 Huntsman 1마리를 준비한다. 트럭 내부에 NavMesh가 없으면 바깥에서 찾는다. 샷건과 수면총은 시작 위치에 각각 하나 생성하며 두 시나리오가 함께 켜져 있어도 중복 생성하지 않는다.
- 몬스터 위치를 찾지 못해도 무기는 별도로 생성하며, 실패 원인을 로그로 남긴다. 테스트 몬스터의 순정 AI·인지·사격은 차단하지 않는다.
- **F6:** 테스트로 생성한 Huntsman만 생존자 2명 조건을 우회한다. 기본 OFF다. 혼자 확인할 때만 켠다. 거리·공격 상태·쿨타임·동시 1명 제한은 그대로다.
- **F7:** 순정 접촉 반응을 호출하여 테스트 Huntsman이 호스트를 조준하도록 요청한다. 스폰 연출·기절 등 순정 조건에서는 즉시 반응하지 않을 수 있다. 납치하려면 7m 안으로 접근해야 한다.
- **F8:** 테스트 Huntsman의 납치를 강제로 해제한다.
- 종합 테스트는 Headman·Clown·Reaper·Upscream·CleanupCrew·Huntsman 네 시나리오의 `Enabled`를 모두 false로 바꾸거나 Release를 사용한다. stage1부터 난이도 1인 실제 진행도를 사용하고 몬스터·무기를 자동 생성하지 않는다. Release에는 위 설정·키·난이도 강제·생존자 조건 우회 코드가 포함되지 않는다.

## 확인 순서

1. 혼자 F6을 끈 상태에서 F7 후 7m 안으로 접근해도 납치되지 않는지 확인한다. F6을 켜면 공격·추적 중 납치되어야 한다.
2. 호스트와 바닐라 참가자 1명 이상으로 F6 OFF 상태를 확인한다. 가장 가까운 생존자만 등에 고정되고 몸체는 반대 방향, 카메라는 자유 시선이어야 한다. 원격 참가자가 납치되는 경우도 확인한다.
3. 납치 순간 피해 없이 3초 후부터 1초마다 2 피해를 받는지 확인한다. 수동 일어나기와 제삼자가 납치 피해자를 Grab하는 것으로 운반이 풀리거나 흔들리지 않아야 한다.
4. 다른 플레이어가 샷건으로 Huntsman을 타격하거나 수면총으로 넘어뜨리면 해제되어야 한다. 본체 사망·디스폰·피해자 사망·퇴장·장면 전환에서도 고정과 전역 슬롯이 남지 않아야 한다.
5. 해제 후 정상 회복·이동·충돌·Grab이 가능한지 확인한다. 동일 Huntsman은 5초가 지나기 전에 재납치하면 안 된다.
6. 벽 뒤의 플레이어는 7m 안이어도 납치하면 안 된다. 운반 중 벽에 접근해 등 위치가 막히면 해제되어야 한다. 피해자가 지형에 갇히지 않는지 확인한다.
7. Huntsman 여러 마리가 있어도 동시에 1명만 납치하는지 확인한다. 납치 중 다른 생존자만 죽은 경우에는 피해자 납치가 유지되어야 한다.
8. Release에서 stage1~3(난이도 1)의 Huntsman은 추가 납치가 없고 stage4(난이도 2)부터 적용되는지 확인한다. 소음 위치만 조사하는 이동은 납치 조건이 아니어야 한다.
9. 납치 피해자가 말하거나 물체·총기로 소리를 내도 납치한 Huntsman이 그 소리 때문에 뒤돌아 조준·조사하지 않는지 확인한다. 같은 위치 근처에서 다른 플레이어가 낸 소리에는 정상적으로 반응해야 한다. 다른 Huntsman·몬스터는 납치 피해자의 소리도 정상 감지하고, 납치 해제 후에는 운반하던 Huntsman도 다시 감지해야 한다.
10. 납치 중 다른 플레이어가 소리를 내어도 Huntsman이 총을 발사하면 안 된다. 이동·배회와 납치 주기 피해는 유지되며, 해제 후에는 다시 순정 사격이 가능해야 한다. 납치하지 않은 다른 Huntsman의 사격도 정상이어야 한다.
11. 납치 피해자가 자신을 운반하는 Huntsman을 Grab하면 즉시 잡기가 해제되고 본체가 흔들리거나 잡힌 상태로 남으면 안 된다. 이미 잡고 있던 중 납치된 경우도 확인한다. 다른 플레이어는 Huntsman을 Grab할 수 있어야 하며, 납치 해제 후에는 피해자도 정상 Grab이 가능해야 한다. 호스트·바닐라 참가자를 각각 납치하여 확인한다.

12. 난이도 5인 현재 Huntsman Debug에서 샷건으로 본체를 처치하면 사망 위치에서 Human Grenade가 핀을 뽑고 던져져야 한다. 한 사망에서 하나만 나오는지, 본체 코어도 정상 드랍되는지 확인한다. 기절·일반 디스폰은 수류탄을 만들면 안 된다.
13. 바닐라 참가자가 폭발 전 수류탄을 Grab하고 인벤토리에 넣어 확보할 수 있는지 확인한다. Grab만 유지하면 1.5~3초 지연 후 폭발하고, 수납하면 핀·타이머가 초기화되어야 한다. 확보한 수류탄의 다음 스테이지 보존·재사용·폭발 후 소모도 확인한다.
14. 정상 진행의 stage1~9(난이도 1~3)에서는 죽음의전술이 없어야 하고 stage10(난이도 4)부터 적용되어야 한다. 서로 다른 Huntsman과 같은 본체의 새 생명에서 각각 한 번씩 발동하는지 확인한다.

상세 사양은 [Huntsman](monsters/huntsman.md)을 따른다. 자동 검사는 다음 명령으로 실행한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Dev.ps1 -Offline
```
