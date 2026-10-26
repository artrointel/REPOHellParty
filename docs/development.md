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

실행 스크립트와 로그 작업은 기본 Steam 경로와 Thunderstore `REPO` 프로필을 사용한다. 다른 프로필로 바꾸면 `Directory.Repo.props`뿐 아니라 `scripts/Start-RepoDev.ps1`과 `.vscode/tasks.json`의 실행·로그 경로도 맞춘다.

로딩 시 `HellParty v0.2.0 loaded.`가 기록된다. 스테이지 준비 후 현재 스테이지와 모드 난이도가 로그에 기록된다.

## Debug와 Release

옵션 없는 Debug와 Release 모두 [공용 난이도](metadata/difficulty.md)를 사용한다. `min(5, 1 + max(0, levelsCompleted) / 3)`으로 stage1/4/7/10/13에서 난이도 1/2/3/4/5가 된다. 난이도 0은 순정 기준이며 자연 진행에는 없다. `DifficultyService.Current`는 모든 능력과 이벤트의 공용 조회 지점이고, `StageRuntime.TryAttach`는 난이도에 맞는 능력을 연결한다.

기본 빌드에는 테스트용 몬스터·무기 자동 생성, 강제 난이도, 전용 단축키, 솔로 납치 우회, Upscream 테스트 예외와 스토커 이동 진단 로그가 없다. 프로필의 과거 `[Debug.*]` 설정도 읽지 않는다. 테스트 코드는 `src/HellParty/Debugging/`에 분리하고 **Debug 빌드에서 `-TestScenario CleanupCrew`를 명시할 때만** 컴파일한다. Release에서 테스트 옵션을 요청하면 빌드 전에 거부한다. 납치는 테스트 빌드에서도 생존자 2명 이상을 요구한다.

기본 두 구성의 차이는 컴파일 최적화와 디버깅 정보다. 실제 멀티플레이에는 Release를 권장한다. 옵션 없는 Debug에서도 게임 진행·능력 조건은 동일하며 Mono 디버거를 연결할 수 있다. 런타임의 로드·스테이지 난이도 로그와 실제 오류·경고는 유지한다.

`scripts/Start-RepoDev.ps1`은 선택한 구성을 빌드·배포하고 게임을 실행한다. 기본 구성은 Debug이며 `-Configuration Release`로 일반 플레이용 빌드를 선택한다. 이미 배포한 빌드는 `-NoBuild`로 실행한다. 게임 창은 표시되며 `-EnableDebugger`를 사용해도 디버거 연결을 기다리며 게임을 멈추지 않는다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1 -Configuration Release
```

청소부 테스트 빌드만 준비하거나 바로 빌드·배포·실행하려면 다음을 사용한다. 게임을 실행 중이면 배포·새 실행을 거부한다.

```powershell
# 빌드만: 설치된 DLL과 실행 중인 게임은 변경하지 않는다.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Dev.ps1 -Offline -TestScenario CleanupCrew
# 게임 종료 후 테스트 빌드·배포·실행
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1 -TestScenario CleanupCrew
# 테스트 종료 후 기본 Debug로 복귀: 옵션을 생략하고 다시 빌드·배포한다.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1
```

테스트 DLL은 `src/HellParty/bin/Debug/Scenarios/CleanupCrew/netstandard2.1/HellParty.dll`에 별도로 생성된다. 기본 Debug의 `bin/Debug/netstandard2.1`과 Release 산출물은 덮어쓰지 않는다. 직접 MSBuild를 사용할 때는 `-p:HellPartyTestScenario=CleanupCrew`를 전달하며 기본값은 `None`이다. `-NoBuild`는 이미 배포된 DLL을 실행하는 옵션이므로 테스트 옵션과 함께 사용할 수 없고, 정상 빌드로 되돌리는 기능도 아니다. 예전 테스트 DLL을 배포했다면 옵션 없는 빌드를 다시 배포해야 한다. 과거 멘탈리스트 테스트 안내는 실행 스크립트에서 제거했다.

청소부 테스트는 호스트의 각 실제 스테이지 난이도를 **5**로 고정하고 시작 플레이어 근처에 샷건·수면총 각 1개와 청소부 1마리를 생성한다. 저장된 `levelsCompleted`를 수정하지 않는다. 장비 생성과 몬스터 위치 탐색을 분리하고, 청소부는 빈 NavMesh와 연결 경로·몸체 공간·시야를 확인해 배치한다. 공간이 없으면 플레이어 이동을 따라 최대 30초간 재시도하므로 트럭 밖으로 나와 확인한다. 순정 인지·공격·폭발 타이머와 부화는 그대로 사용한다. 테스트 안내는 `TEST BUILD`·`TEST CleanupCrew` 로그에서 확인한다. 플레이어 간 전달 검증에는 2명이 필요하며 바닐라 참가자는 추가 모드를 설치하지 않는다.

VS Code에서는 `HellParty: Start Cleanup Crew test` 태스크 또는 `HellParty: Cleanup Crew test + attach` 실행 구성을 선택한다. 다른 테스트를 추가할 때도 별도 시나리오 소스·컴파일 심볼·빌드 옵션으로 연결하고, 기본 Debug 및 Release에 시나리오 타입이나 난이도 우회가 없는지 검사한다. 테스트 시나리오 안에서만 장비·몬스터 준비와 진단 로그를 작성한다.

자동 검사는 `tests/HellParty.Tests`에 유지한다. 몬스터별 실제 확인 문서는 [플레이테스트 폴더](playtests/README.md)에 모아 자연 생성 개체와 실제 진행도를 기준으로 작성하며, 바닐라 참가자에게 테스트 플러그인을 요구하지 않는다.

## 패키징

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Pack-Thunderstore.ps1 -NoRestore
```

`artifacts/HellParty-0.2.0.zip`을 생성하며 프로필에 배포하지 않는다. 패키징은 Release 구성만 허용하며 Release의 컴파일 경고도 오류로 처리한다. `assets/branding/hellparty-icon-source.png`가 검토한 카툰 아이콘 원본이며, `scripts/New-PackageIcon.ps1`은 이를 256×256 패키지 아이콘으로 축소 출력한다. 외형 참고 출처와 생성 프롬프트는 [아이콘 제작 자료](../assets/branding/README.md)에 보관한다. 참고 스크린샷·모델 파일이나 폰트 파일은 배포하지 않는다.

`scripts/Test-Package.ps1`은 ZIP 경로를 추출하지 않고 파일 목록·중복·UTF-8·manifest·PNG 크기와 서명·Release DLL 버전·원본 해시를 확인한다. 루트의 manifest·README·CHANGELOG·icon과 `BepInEx/plugins/HellParty/HellParty.dll`만 허용한다. 임시 ZIP 검증에 성공한 경우에만 최종 ZIP을 교체한다. DLL 외 게임·Unity·BepInEx 어셈블리, 개인 설정·로그·PDB는 포함하지 않는다. 패키지 구조는 [Thunderstore 공식 가이드](https://wiki.thunderstore.io/mods/creating-a-package)를 따른다.

배포 후보를 한 번에 검증하려면 아래 명령을 사용한다. 설치된 게임 DLL이 없으면 계약 검사를 건너뛰지 않고 실패한다. 다른 게임 경로는 `-GameAssembly`로 지정한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-Release.ps1 -Offline
```

Debug·Release 빌드, 규칙·게임 계약, 패키지 생성·검사, 잘못된 패키지의 거부 검사를 순서대로 실행한다. 성공하면 ZIP과 `artifacts/HellParty-0.2.0.release.json`에 ZIP·DLL·게임 어셈블리의 SHA-256을 기록한다. 보고서는 실제 멀티플레이를 미검증으로 표시한다. 공개 전 실제 확인 항목은 [배포 준비 문서](release-readiness.md)를 따른다.

## 검증과 다음 단계

### 네트워크 오브젝트 수명과 지연 RPC

`Native/NetworkLifetime.cs`는 로컬에서 보내는 두 종류의 `PhotonView.RPC` 호출 전에 방 접속·ViewID·현재 등록 객체의 일치를 확인한다. 이미 등록 해제된 View로는 보내지 않는다. 다른 참가자의 발송이나 정상 발송 후 네트워크를 이동 중인 메시지까지 취소하는 기능은 아니다.

Bella 미끼·부착 인형, Crown 정리, Chef 변장 해제, Mentalist 반격총, 웨이브 Gramophone 및 가스 생성 실패의 오브젝트 정리는 공통 제거 경로를 사용한다. 잡기를 먼저 해제하고 순정 위치 RPC로 화면 밖으로 이동시킨 뒤 기본 1초 동안 기존 View를 유지한다. 유효한 늦은 잡기 요청에는 해제를 다시 전달하고 유예를 연장하되 총 3초를 넘기지 않는다. 제거 중 재집기·잡기 링크와 지연 코루틴의 추가 전송을 막고 마지막에 네트워크 삭제한다. 코어 보상을 주는 정상 몬스터 사망·순정 가스 생성용 파괴에는 이 무효과 제거 경로를 대신 사용하지 않는다.

로컬 설치된 SharedHealthPacks 1.0.0의 `ItemHealthPackUpdatePatch.Postfix`는 회복팩 사용 여부를 확인하지 않고 `ItemHealthPack.OnDestroy` 때 `SemiFunc.PlayerGetAll()`의 전원에게 `HealOther`를 호출한다. 미사용 팩과 장면 정리에서도 회복 요청이 발생할 수 있다. 네이티브 파괴 콜백 전체를 예외 복구 가능한 범위로 감싸 이 경우의 회복을 막고, `PlayerHealth.HealOther`도 사망·비활성·미등록 대상을 제외한다. 정상 사용된 팩의 플레이 중 공유 회복 동작은 유지한다. SharedHealthPacks DLL 자체는 수정하거나 제거하지 않는다.

호스트만 HellParty를 설치한 경우, 다른 참가자의 SharedHealthPacks 또는 다른 모드가 오래된 ViewID로 보내는 `HealOtherRPC`·잡기 메시지에는 이 발송 방어가 적용되지 않는다. 특히 로그의 `Remote called. By: #02 'Ara'`는 그 참가자가 발송했다는 의미다. 원격 모드의 정리 시 회복 버그는 해당 참가자 측에서도 수정되어야 한다. 수신 경고를 숨기거나 사라진 ViewID를 가짜 객체로 재등록하지 않는다. 실제 멀티플레이에서 장면 전환과 제거 직전 Grab을 확인해야 한다.

스테이지마다 반복된 `RPC_SetWalkieColor`·`RPC_CreateRemoteTalkieOnRightArm`·`RPC_SetLocalAvatarViewId` 오류는 대상 View가 없다는 경고와 다르다. View는 있으나 해당 RPC를 처리할 컴포넌트·메서드가 없다는 뜻이다. 2026-10-24 재검사에서 순정 게임과 현재 호스트 프로필의 플러그인 DLL 총 34개에 세 이름의 수신 메서드·직접 송신 문자열이 없었다. [Omniscye WalkieTalkies의 공개 소스](https://thunderstore.io/c/repo/p/Omniscye/WalkieTalkies/source/)에서 `WalkieController`가 뒤의 두 RPC를 송신·수신하는 것을 확인했다. 공개 소스는 구버전이며 `RPC_SetWalkieColor`는 포함하지 않으므로, 세 오류 모두의 정확한 송신 버전·참가자는 참가자 설치 목록과 대조해야 한다. 호스트에는 이 무전기 모드가 설치되어 있지 않으므로 참가자 측 설치 여부를 먼저 확인한다. 순정 참가자를 유지하려면 송신 측 무전기 모드를 비활성화하고 새 로비에서 확인한다. 무전기 모드를 사용하려면 필요한 수신 측에도 호환 버전이 있어야 한다. HellParty에 가짜 수신 RPC를 추가해 오류만 지우지 않는다.

`AllocateViewID() ... room (user 0)`는 수신 RPC 누락과 달리 방 소유 ViewID 공간의 고갈이다. 설치된 PUN의 기본 `MAX_VIEW_IDS`는 1000이며, 방 ID는 1~999를 사용한다. 순정 디스폰은 `EnemyParent.EnableObject`만 비활성화하므로 등록 객체와 ViewID를 반납하지 않는다. 웨이브·부화는 `SummonedEnemySpawnTicket`의 전용 풀로 완전 퇴장 후 2초가 지난 동일 프리팹을 재사용한다. 스테이지와 용도가 같은 개체만 허용하고 예약 시 즉시 대기 플래그를 세워 중복 대여를 막는다. 순정 Director 목록·Gnome 인덱스를 보존하고 기존 스폰 RPC로 새 생명을 시작하므로 바닐라 참가자에 추가 RPC가 필요 없다.

`RoomViewBudget`는 `VanillaCatalog.Spawn`의 두 경로에서 네트워크 생성 전에 방 View 점유와 프리팹의 자식 View까지 검사한다. 순정 생성용으로 32개를 남기며 PUN의 ID 체계나 제한값을 바꾸지 않는다. 가스 생성도 같은 경로를 사용한다. 용량 부족은 전용 예외로 구분해 웨이브는 3초 후 재시도하고 부화는 취소한다. 이는 PUN이 프리팹 일부의 ID만 할당한 뒤 실패하는 경로에 들어가지 않도록 한다. 다른 시스템의 살아 있는 객체를 지우거나 기존 순정 몬스터를 풀에 편입하지 않는다. [Photon 공식 문서](https://doc.photonengine.com/pun/current/gameplay/instantiation)의 ViewID 한계·수명 설명을 참고한다.

플레이 중에는 빌드만 수행하고 실행 중인 게임과 설치된 DLL은 변경하지 않는다. 새 수명 관리 코드의 검증은 배포·재시작 이후에 수행한다. 사라진 View에 대한 두 종류의 잡기 경고만으로 해당 오브젝트 종류를 확정할 수는 없다.

`tests/HellParty.Tests`는 Unity를 로드하지 않는 .NET 9 콘솔 테스트 러너다. 실제 규칙 소스를 연결해 단계 경계, Apex Predator 분신·복귀·재소환, Animal 거리·쿨타임·분화 수명, Birthday Boy의 연속 체류·10m 경계·일반/피격/사망 파열 보상·실제 피해/치명타 구분·사망 파열 추가 생성 억제와 중복 방지, Bella의 해금·경고 단계 누적/대기/중단, Bowtie 형제·복수의 해금·10초 수명·교체·공격별/사망별 중복 소환 방지, Chef의 해금·20m 경계·1초 재시도·변장/해제/공격 전환, 파괴 시 처치와 중복 파괴·Grab 경합 방지, 변장 중 시야 검사 간격과 시야 상실 시 비공격 배회 복귀, 공격 회복 중 타깃 유지와 순정 추격 종료·사망에 따른 재변장 제한, 본체 체력·Frog 유효 내구 비율 및 반복 변신에서의 부분 피해 유지를 검사한다. 자율 인형의 최근접 대상·동률 선택, 관찰 시 공격/이동 중단, Grab 대기와 화면 시야 경계, 100 체력·피해 누적·중복 사망 방지, 체력 비율 가격과 정수 표시 반올림·가격 15% 이하 생존·누적 오차 방지도 검사한다. Huntsman 납치의 거리·단계·생존자 수·주기 피해·해제 후 쿨타임·전역 슬롯 경쟁, Cleanup Crew의 단계·끈적이는 헤드 1.5m 경계·분리 시점부터 3초 쿨타임·Grab 해제·폭발 후 재부착 차단·다음 투척 초기화·폭발 중복·헤드 재사용·소환 수명도 검사한다. Mono.Cecil로 두 구성의 임시 테스트 코드 제거, 실제 진행도 계산과 설치된 게임의 패치 지점도 검사한다. 실패 시 프로세스가 실패 코드로 종료된다. `dotnet test`/VSTest 프로젝트는 아니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Dev.ps1 -Offline
```

추가로 공용 난이도의 stage1/4/7/10/13 경계와 음수·최대 정수 입력, 블라인드의 3초 반복·타깃 상실/교체·재스폰 초기화, 웨이브의 20m 영역·3초 예고·자세 회피·초당 노움 2마리·20초 종료·3m 최소 거리·순정 자세 동기화 계약을 검사한다. 게임 객체 생성, 원격 클라이언트 동기화·피해·가스와 시각적 위치는 이 테스트로 증명하지 않는다. 실제 플레이 확인 항목은 [Bowtie](playtests/bowtie-playtest.md), [Bella](playtests/bella-playtest.md), [Birthday Boy](playtests/birthday-boy-playtest.md), [Animal](playtests/animal-playtest.md), [Apex Predator](playtests/apex-playtest.md) 플레이테스트를 따른다. 기획 문서는 `docs/`에서 계속 관리한다.


Trudge 확인은 [소집 플레이테스트](playtests/trudge-playtest.md), Loom·Robe 확인은 [Loom·Robe 플레이테스트](playtests/loom-robe-playtest.md)를 따른다.
