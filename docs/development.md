# 개발 환경

REPOPocketCartTurret의 첫 커밋 `e788f0741f08436f0349089e68ca3bd48d0f3d1c`과 같은 범위의 셋업이다. 플러그인 로딩과 Harmony 초기화만 포함하며 난이도 진행, 몬스터 능력, 인지 판정은 구현하지 않는다.

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

로딩 시 `HellParty v0.1.0 loaded.`가 기록된다. 이번 셋업에서는 게임을 실행하거나 프로필에 자동 배포하지 않는다.

## 패키징

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Pack-Thunderstore.ps1 -NoRestore
```

`artifacts/HellParty-0.1.0.zip`을 생성하며 프로필에 배포하지 않는다. `thunderstore/icon.png`는 이전 프로젝트에서 재사용한 임시 개발용 아이콘이다. 공개 배포 전 교체한다. DLL 외 게임·Unity·BepInEx 어셈블리는 패키지에 포함하지 않는다.

## 검증과 다음 단계

이번 범위의 검증은 오프라인 복원, Debug/Release 컴파일, PowerShell·JSON·XML 구문과 패키지 내용 확인이다. 빈 프로젝트를 통과시키기 위한 형식적인 단위 테스트는 추가하지 않는다. 아직 능력 구현이나 단위 테스트 실행 결과는 없다.

다음 구현부터 순수 규칙을 Unity와 분리하고, 인지 여부·쿨타임·소환 상한·보상 선택 같은 규칙에 실제 단위 테스트를 추가한다. 게임 객체 생성, RPC, 바닐라 참가자 화면·피해는 별도 실제 멀티플레이 검증 대상으로 둔다. 기획 문서는 `docs/`에서 계속 관리한다.
