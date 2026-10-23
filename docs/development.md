# 개발 환경

REPOPocketCartTurret의 첫 커밋 `e788f0741f08436f0349089e68ca3bd48d0f3d1c`을 기반으로 개발 환경을 준비했고, 첫 기능으로 공용 난이도 모듈과 Apex Predator 능력을 구현했다. 현재 구현 목록은 [몬스터별 문서](monsters/README.md)의 구현 상태를 따른다. 공용 난이도는 stage1부터 1이며, [몬스터 웨이브](events/monster-wave.md)와 Reaper의 [전력질주](monsters/reaper.md)와 Shadow Child의 [블라인드](metadata/blind.md)도 구현되어 있다.

## 도구와 참조

- .NET 9 SDK, C# 12, `netstandard2.1`.
- BepInEx.Core 5.4.21 및 종속 HarmonyX.
- UnityEngine.Modules 2022.3.62.
- R.E.P.O.GameLibs.Steam 0.4.4-ngd.0.
- Linkoid.Repo.Plugin.Build 0.2.2.
- BepInEx.Analyzers 1.0.8, BepInEx.AssemblyPublicizer.MSBuild 0.4.3.

버전은 기준 커밋과 동일하게 고정했다. `global.json`은 안정 버전 .NET 9 SDK의 최신 feature band 사용을 허용한다. 로컬에 준비한 SDK는 9.0.319다. 게임 참조 패키지는 컴파일용이며 실제 게임이나 BepInEx를 설치하지 않는다.

현재 PC에서는 이전 프로젝트의 `.dotnet` SDK와 `.nuget/packages`를 이 저장소로 복사해 준비했다. 이전 저장소를 수정하거나 런타임 종속성으로 사용하지 않는다. 두 폴더와 `.dotnet-home`, 빌드 산출물, 개인 경로 설정은 Git에서 제외된다.

새 PC에서는 .NET 9 SDK를 PATH 또는 `.dotnet/dotnet.exe`에 설치하고 온라인 복원을 먼저 실행한다. 오프라인 복원은 이미 채워진 패키지 캐시가 필요하다.

## 빌드

저장소 루트에서 실행한다. 기본 빌드는 게임 프로필에 파일을 복사하지 않는다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Dev.ps1
```

현재 준비된 캐시로 네트워크 없이 복원·빌드하려면:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Dev.ps1 -Offline
```

복원 후에는 `-NoRestore`, Release 빌드에는 `-Configuration Release`를 사용한다. 출력은 `src/HellParty/bin/<Configuration>/netstandard2.1/HellParty.dll`이다.

## 개발 프로필과 디버깅

`Directory.Repo.props.example`을 `Directory.Repo.props`로 복사하고 Steam 게임 경로, Thunderstore의 `ProfileName`, `BepInExDirectory`를 맞춘다. 현재 로컬 설정은 Steam 기본 경로와 Thunderstore `REPO` 프로필이며 출력 하위 폴더는 `HellParty`다. Gale 예제도 제공한다.

명시적으로 프로필에 DLL/PDB를 배포하려면 게임을 종료한 뒤 실행한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Dev.ps1 -NoRestore -Deploy
```

VS Code의 기본 빌드 작업은 빌드만 수행한다. 별도 `Build + Deploy` 작업과 게임 시작 작업은 명시적으로 배포한다. F5는 Mono 디버거 `127.0.0.1:10000`에 연결하며 권장 Unity 확장이 필요하다.

실행 스크립트와 로그 작업은 기준 커밋처럼 기본 Steam 경로와 Thunderstore `REPO` 프로필을 사용한다. 다른 프로필로 바꾸면 `Directory.Repo.props`뿐 아니라 `scripts/Start-RepoDev.ps1`과 `.vscode/tasks.json`의 실행·로그 경로도 맞춘다.

로딩 시 `HellParty v0.1.0 loaded.`가 기록된다. 스테이지 준비 후 현재 스테이지와 모드 난이도가 로그에 기록된다.

## Debug와 Release

`src/HellParty/Debug/`와 호출부는 `#if DEBUG`로 분리하며 Release에는 테스트 생성·강제 난이도·전용 키가 포함되지 않는다. `DifficultyService.Current`는 모든 능력과 이벤트가 사용하는 공용 조회 지점이고, `StageRuntime.TryAttach`는 난이도에 맞는 능력을 연결한다.

Release와 모든 시나리오를 끈 Debug는 [공용 난이도](metadata/difficulty.md)를 사용한다. `min(5, 1 + max(0, levelsCompleted) / 3)`으로 stage1/4/7/10/13에서 난이도 1/2/3/4/5가 된다. 난이도 0은 순정 기준이며 자연 진행에는 없다.

실행 대상은 프로필의 `BepInEx/config/artrointel.HellParty.cfg`에 저장된 값이 결정한다. 코드상 초기 기본값은 여덟 시나리오 모두 true지만 우선순위에 따라 실행하며 기존 프로필 설정을 덮어쓰지 않는다. 이번 테스트 프로필은 Loom·Robe만 활성화했다. 설정 변경 후 게임을 재시작한다.

| 설정 | 활성 시 동작 |
| --- | --- |
| `[Debug.LoomRobe] Enabled` | 최우선이며 난이도 5로 Loom·Robe 각 1마리와 샷건·수면총을 준비한다. 테스트 Loom만 솔로 납치를 허용하며 F8로 해제한다. 스토커는 실제 동반 생성 경로를 사용한다. |
| `[Debug.Trudge] Enabled` | LoomRobe가 꺼졌을 때 실행하며 난이도 5로 Trudge 1마리와 샷건·수면총을 준비한다. |
| `[Debug.Headman] Enabled` | Trudge가 꺼졌을 때 실행한다. 난이도 5로 Headman·순정 Animal 동료 각 1마리와 샷건·수면총을 준비한다. |
| `[Debug.Clown] Enabled` | Headman이 꺼졌을 때 난이도 5로 Clown 1마리와 샷건·수면총을 준비하고 다른 시나리오·전용 키를 중단한다. 순정 AI는 유지한다. |
| `[Debug.Reaper] Enabled` | Headman·Clown이 꺼졌을 때 난이도 2, Reaper 1마리와 샷건·수면총을 준비한다. 다른 세 시나리오와 전용 키를 실행하지 않는다. 순정 타깃 획득과 AI는 유지한다. |
| `[Debug.Upscream] Enabled` | Headman·Clown·Reaper가 꺼져 있을 때 난이도 5, 변장 허용 Upscream 1마리와 샷건·수면총을 준비한다. 켜져 있으면 아래 두 시나리오와 전용 키는 실행하지 않는다. |
| `[Debug.CleanupCrew] Enabled` | Headman·Clown·Reaper·Upscream이 꺼진 상태에서 Cleanup Crew를 준비한다. 난이도는 처음 5이며 F9로 1/5를 전환하고 F10은 인지 요청, F11은 활성 헤드의 폭발 예고다. |
| `[Debug.Huntsman] Enabled` | Headman·Clown·Reaper·Upscream이 꺼진 상태에서 Huntsman을 준비한다. F6은 테스트 개체의 생존자 2명 조건 우회(기본 OFF), F7은 순정 조준 요청, F8은 납치 해제다. Cleanup Crew가 꺼져 있으면 난이도 5를 강제한다. |

Cleanup Crew와 Huntsman은 함께 실행할 수 있다. 두 몬스터를 시작 위치 20m 안의 유효 NavMesh에서 각각 준비하고 무기는 중복 생성하지 않는다. F9는 공용 난이도에 적용되므로 1단계에서 Huntsman은 새 납치를 시작하지 않는다. 실패한 몬스터 위치 탐색과 무기 생성은 분리한다. 상세 조작은 [Huntsman](huntsman-playtest.md), [Cleanup Crew](cleanup-crew-playtest.md), [Upscream](upscream-playtest.md)의 테스트 문서를 따른다.

Headman 테스트는 [Headman 플레이테스트](headman-playtest.md)를 따른다. Clown 테스트는 [Clown 플레이테스트](clown-playtest.md)를 따른다. Reaper 자동 생성과 전력질주 확인 순서는 [Reaper 플레이테스트](reaper-playtest.md)를 따른다. 이 시나리오는 Shadow Child를 추가 생성하지 않으며 Reaper는 블라인드 대신 전력질주를 사용한다.

종합 테스트와 새 난이도 진행 확인에는 다음 여덟 설정을 모두 끈다. Release는 이 설정과 관계없이 실제 진행도를 사용한다.

```ini
[Debug.LoomRobe]
Enabled = false

[Debug.Trudge]
Enabled = false

[Debug.Headman]
Enabled = false

[Debug.Clown]
Enabled = false

[Debug.Reaper]
Enabled = false

[Debug.Upscream]
Enabled = false

[Debug.CleanupCrew]
Enabled = false

[Debug.Huntsman]
Enabled = false
```

Apex Predator·Animal·Birthday Boy·Bella·Bowtie·Chef·스토커·Peeper·Mentalist의 과거 전용 임시 자동 생성은 종료했다. 일반 생성 개체로 각 능력을 확인한다. 다른 Debug 시나리오가 켜져 있으면 해당 강제 난이도가 이 몬스터들에게도 적용된다.

`scripts/Start-RepoDev.ps1`은 Debug를 빌드·배포하고 게임을 실행한다. `-Configuration Release`로 실제 진행도를 확인할 수 있으며, 이미 배포한 빌드는 `-NoBuild`로 실행한다. 게임 창은 `WindowStyle Normal`로 표시한다. 일반 테스트에 디버거가 필요하지 않으며 `-EnableDebugger`나 F5를 사용해도 연결을 기다리며 게임을 정지하지 않는다.

```powershell
# Debug: 실행 전 게임 종료, 위 프로필 설정 확인
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1

# 실제 진행도 기반 Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1 -Configuration Release
```

임시 테스트 코드는 Debug 디렉터리에 추가한다. 순정 진행도·저장 데이터에 강제 난이도를 기록하지 않는다.

## 패키징

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Pack-Thunderstore.ps1 -NoRestore
```

`artifacts/HellParty-0.1.0.zip`을 생성하며 프로필에 배포하지 않는다. 패키징은 Release 구성만 허용한다. `thunderstore/icon.png`는 이전 프로젝트에서 재사용한 임시 개발용 아이콘이다. 공개 배포 전 교체한다. DLL 외 게임·Unity·BepInEx 어셈블리는 패키지에 포함하지 않는다.

## 검증과 다음 단계

### 네트워크 오브젝트 수명과 지연 RPC

`Native/NetworkLifetime.cs`는 로컬에서 보내는 두 종류의 `PhotonView.RPC` 호출 전에 방 접속·ViewID·현재 등록 객체의 일치를 확인한다. 이미 등록 해제된 View로는 보내지 않는다. 다른 참가자의 발송이나 정상 발송 후 네트워크를 이동 중인 메시지까지 취소하는 기능은 아니다.

Bella 미끼·부착 인형, Crown 정리, Chef 변장 해제, Mentalist 반격총, 웨이브 Gramophone 및 가스 생성 실패의 오브젝트 정리는 공통 제거 경로를 사용한다. 잡기를 먼저 해제하고 순정 위치 RPC로 화면 밖으로 이동시킨 뒤 기본 1초 동안 기존 View를 유지한다. 유효한 늦은 잡기 요청에는 해제를 다시 전달하고 유예를 연장하되 총 3초를 넘기지 않는다. 제거 중 재집기·잡기 링크와 지연 코루틴의 추가 전송을 막고 마지막에 네트워크 삭제한다. 코어 보상을 주는 정상 몬스터 사망·순정 가스 생성용 파괴에는 이 무효과 제거 경로를 대신 사용하지 않는다.

로컬 설치된 SharedHealthPacks 1.0.0의 `ItemHealthPackUpdatePatch.Postfix`는 회복팩 사용 여부를 확인하지 않고 `ItemHealthPack.OnDestroy` 때 `SemiFunc.PlayerGetAll()`의 전원에게 `HealOther`를 호출한다. 미사용 팩과 장면 정리에서도 회복 요청이 발생할 수 있다. 네이티브 파괴 콜백 전체를 예외 복구 가능한 범위로 감싸 이 경우의 회복을 막고, `PlayerHealth.HealOther`도 사망·비활성·미등록 대상을 제외한다. 정상 사용된 팩의 플레이 중 공유 회복 동작은 유지한다. SharedHealthPacks DLL 자체는 수정하거나 제거하지 않는다.

호스트만 HellParty를 설치한 경우, 다른 참가자의 SharedHealthPacks 또는 다른 모드가 오래된 ViewID로 보내는 `HealOtherRPC`·잡기 메시지에는 이 발송 방어가 적용되지 않는다. 특히 로그의 `Remote called. By: #02 'Ara'`는 그 참가자가 발송했다는 의미다. 원격 모드의 정리 시 회복 버그는 해당 참가자 측에서도 수정되어야 한다. 수신 경고를 숨기거나 사라진 ViewID를 가짜 객체로 재등록하지 않는다. 실제 멀티플레이에서 장면 전환과 제거 직전 Grab을 확인해야 한다.

스테이지마다 반복된 `RPC_SetWalkieColor`·`RPC_CreateRemoteTalkieOnRightArm`·`RPC_SetLocalAvatarViewId` 오류는 대상 View가 없다는 경고와 다르다. View는 있으나 해당 RPC를 처리할 컴포넌트·메서드가 없다는 뜻이다. 보고 당시 순정 게임 및 호스트 프로필의 플러그인 DLL 총 35개에서 세 이름의 수신 메서드와 직접적인 송신 문자열을 검사했으나 찾지 못했다. 참가자 측 무전기 관련 모드의 전용 통신으로 추정하며, 정확한 모드명은 참가자 설치 목록과 대조해야 한다. HellParty에 가짜 수신 RPC를 추가해 오류만 지우지 않는다. 송수신 모드의 설치·버전·호스트 전용 지원 여부를 맞추거나 송신 측 기능을 조정해야 한다.

플레이 중에는 수정 빌드만 준비하고 설치된 DLL과 실행 프로세스는 변경하지 않는다. 따라서 빌드 후에도 현재 세션에서 발생한 View 645의 `GrabLinkRPC`·`GrabPointSyncRPC` 경고는 새 수명 관리 코드의 검증 결과로 취급하지 않는다. 다음 배포·게임 재시작 이후 별도로 재검증한다. 해당 View가 어떤 오브젝트였는지는 이 두 경고만으로 확정할 수 없다.

`tests/HellParty.Tests`는 Unity를 로드하지 않는 .NET 9 콘솔 테스트 러너다. 실제 규칙 소스를 연결해 단계 경계, Apex Predator 분신·복귀·재소환, Animal 거리·쿨타임·분화 수명, Birthday Boy의 연속 체류·10m 경계·일반/피격/사망 파열 보상·실제 피해/치명타 구분·사망 파열 추가 생성 억제와 중복 방지, Bella의 해금·경고 단계 누적/대기/중단, Bowtie 형제·복수의 해금·10초 수명·교체·공격별/사망별 중복 소환 방지, Chef의 해금·20m 경계·1초 재시도·변장/해제/공격 전환, 파괴 시 처치와 중복 파괴·Grab 경합 방지, 변장 중 시야 검사 간격과 시야 상실 시 비공격 배회 복귀, 공격 회복 중 타깃 유지와 순정 추격 종료·사망에 따른 재변장 제한, 본체 체력·Frog 유효 내구 비율 및 반복 변신에서의 부분 피해 유지를 검사한다. 자율 인형의 최근접 대상·동률 선택, 관찰 시 공격/이동 중단, Grab 대기와 화면 시야 경계, 100 체력·피해 누적·중복 사망 방지, 체력 비율 가격과 정수 표시 반올림·가격 15% 이하 생존·누적 오차 방지도 검사한다. Huntsman 납치의 거리·단계·생존자 수·주기 피해·해제 후 쿨타임·전역 슬롯 경쟁, Cleanup Crew의 단계·폭발 중복·헤드 재사용·소환 수명도 검사한다. Mono.Cecil로 Huntsman Debug 시나리오와 Release 제외, 실제 진행도 계산과 설치된 게임의 패치 지점도 검사한다. 실패 시 프로세스가 실패 코드로 종료된다. `dotnet test`/VSTest 프로젝트는 아니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Dev.ps1 -Offline
```

추가로 공용 난이도의 stage1/4/7/10/13 경계와 음수·최대 정수 입력, 블라인드의 3초 반복·타깃 상실/교체·재스폰 초기화, 웨이브의 20m 영역·3초 예고·자세 회피·초당 노움 2마리·20초 종료·3m 최소 거리·순정 자세 동기화 계약을 검사한다. 게임 객체 생성, 원격 클라이언트 동기화·피해·가스와 시각적 위치는 이 테스트로 증명하지 않는다. 실제 플레이 확인 항목은 [Bowtie](bowtie-playtest.md), [Bella](bella-playtest.md), [Birthday Boy](birthday-boy-playtest.md), [Animal](animal-playtest.md), [Apex Predator](apex-playtest.md) 플레이테스트를 따른다. 기획 문서는 `docs/`에서 계속 관리한다.

현재 테스트 시나리오는 위 Debug 설정을 기준으로 선택한다. Loom·Robe의 스토커 동반과 다른 몬스터 능력은 선택된 공용 난이도를 사용하며, 실제 진행도 검증에는 전용 시나리오를 모두 끄거나 Release를 사용한다.

Trudge 테스트는 [소집 플레이테스트](trudge-playtest.md)를 따른다. Trudge가 켜져 있으면 아래의 모든 기존 시나리오와 전용 키를 중단한다.

현재 테스트 확인 순서는 [Loom·Robe 플레이테스트](loom-robe-playtest.md)를 따른다. LoomRobe가 켜져 있으면 이전 시나리오와 전용 키를 중단한다.
