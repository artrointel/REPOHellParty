# 개발 환경

REPOPocketCartTurret의 첫 커밋 `e788f0741f08436f0349089e68ca3bd48d0f3d1c`을 기반으로 개발 환경을 준비했고, 첫 기능으로 공용 난이도 모듈과 Apex Predator 능력을 구현했다. 다른 몬스터의 능력과 공통 인지 판정 연결은 아직 구현하지 않았다.

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

- `src/HellParty/Debug/`는 Debug 구성에서만 컴파일한다. 호출부도 `#if DEBUG`로 분리한다.
- Release는 완료 스테이지 수로 난이도를 계산한다. `min(5, max(0, levelsCompleted) / 3)`을 사용하므로 stage1은 0, stage4는 1, stage7은 2, stage10은 3, stage13은 4, stage16 이상은 5다.
- Debug는 Bowtie 테스트를 위해 난이도를 5로 강제한다. 순정 `levelsCompleted`나 저장 진행도는 변경하지 않는다. 실제 스테이지 시작마다 시작 위치 근처 Bowtie·샷건·수면총을 각각 하나씩 생성한다. 테스트용 Bella·Birthday Boy·Animal·오리 생성과 임시 능력 추적 로그는 제거한 상태다.
- `DifficultyService`는 공용 난이도 조회 지점이며, `StageRuntime.TryAttach`는 향후 몬스터 능력을 연결할 지점이다.
- `scripts/Start-RepoDev.ps1`은 기본적으로 Debug를 빌드·배포하고 게임을 실행한다. `-Configuration Release`로 실제 진행 모드를 실행할 수 있다. 복원을 먼저 수행해야 하며, 이미 배포한 빌드를 시작할 때는 `-NoBuild`를 사용한다.
- 게임은 `WindowStyle Normal`로 표시한다. 일반 플레이 테스트는 디버거 연결 없이 실행할 수 있으며, `-EnableDebugger`나 F5를 사용해도 디버거 연결을 기다리며 시작을 정지하지 않는다.

```powershell
# Debug로 시작 (실행 전 게임 종료)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1

# 실제 진행도 기반 Release 시작
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-RepoDev.ps1 -Configuration Release
```

임시 테스트 코드는 `Debug/`에 추가한다. Release에 테스트 코드를 추가하거나 순정 진행도에 강제 난이도 값을 기록하지 않는다.

## 패키징

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Pack-Thunderstore.ps1 -NoRestore
```

`artifacts/HellParty-0.1.0.zip`을 생성하며 프로필에 배포하지 않는다. 패키징은 Release 구성만 허용한다. `thunderstore/icon.png`는 이전 프로젝트에서 재사용한 임시 개발용 아이콘이다. 공개 배포 전 교체한다. DLL 외 게임·Unity·BepInEx 어셈블리는 패키지에 포함하지 않는다.

## 검증과 다음 단계

`tests/HellParty.Tests`는 Unity를 로드하지 않는 .NET 9 콘솔 테스트 러너다. 실제 규칙 소스를 연결해 단계 경계, Apex Predator 분신·복귀·재소환, Animal 거리·쿨타임·분화 수명, Birthday Boy의 연속 체류·10m 경계·일반/피격/사망 파열 보상·실제 피해/치명타 구분·최근 풍선 보상과 중복 방지, Bella의 해금·경고 단계 누적/대기/중단, Bowtie 형제·복수의 해금·10초 수명·교체·공격별/사망별 중복 소환 방지를 검사한다. Mono.Cecil로 Debug 전용 Bowtie 시나리오, Release 정상 난이도 계산과 설치된 게임의 패치 지점도 검사한다. 실패 시 프로세스가 실패 코드로 종료된다. `dotnet test`/VSTest 프로젝트는 아니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Dev.ps1 -Offline
```

게임 객체 생성, 원격 클라이언트 동기화·피해·가스와 시각적 위치는 이 테스트로 증명하지 않는다. 실제 플레이 확인 항목은 [Bowtie](bowtie-playtest.md), [Bella](bella-playtest.md), [Birthday Boy](birthday-boy-playtest.md), [Animal](animal-playtest.md), [Apex Predator](apex-playtest.md) 플레이테스트를 따른다. 기획 문서는 `docs/`에서 계속 관리한다.
