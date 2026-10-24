# 배포 준비 계획과 검증

이 문서는 로컬 배포 후보를 만드는 순서와 공개 전 실제 플레이 확인을 관리한다. 공개 업로드·커밋·태그 생성은 별도 작업이다. 버전은 기존 `0.1.0`을 유지한다.

## 1. 런타임과 개발 환경

- [x] 임시 스폰·무기·강제 난이도·단축키·솔로 납치 우회를 제거한다.
- [x] 최근 밸런스와 스테이지 경계가 코드·규칙 검사·기획표에 일치하도록 한다.
- [x] 플러그인 초기화 실패 시 부분 적용된 자체 Harmony 패치를 해제하고 실패를 로그에 남긴다.
- [x] 일반 개발 검사와 설치된 게임 호출 계약이 반드시 필요한 배포 검증을 구분한다.

## 2. 사용자 문서와 패키지

- [x] 개발용 manifest 설명을 실제 기능 설명으로 바꾼다.
- [x] 이전 프로젝트의 임시 아이콘을 HellParty 전용 아이콘으로 교체한다.
- [x] 설치·제거·호스트 전용 조건·문제 제보 절차를 패키지 README에 추가한다.
- [x] ZIP의 파일 목록·UTF-8·아이콘·manifest·버전·DLL 일치를 자동 검사한다.
- [x] 검증 명령 하나로 규칙·게임 계약·Release 패키지와 해시 보고서를 생성한다.

## 3. 검증과 배포 후보

- [x] Debug·Release 빌드와 자동 규칙·설치된 게임 계약 검사를 실행한다.
- [x] 정상 ZIP과 잘못된 패키지의 거부 동작을 검사한다.
- [x] 문서의 로컬 링크와 변경 파일 형식을 확인한다.
- [x] 최종 ZIP과 SHA-256 검증 보고서를 `artifacts/`에 생성한다.

이번 자동 검증 결과는 규칙·게임 호출 계약 **816개**, 정상·변조 패키지 **10개**, 로컬 문서 링크 **255개** 통과다. Debug와 Release 모두 경고·오류가 없었다. 호환되지 않는 게임 어셈블리 입력 시 테스트 러너가 오류 코드 1로 종료하고, 필수 게임 DLL 누락 시 빌드 전에 거부하는 동작도 확인했다. 실행 로그는 `artifacts/release-readiness-verified.log`와 `artifacts/release-package-negative.log`에 남겼다.

## 공개 전 실제 플레이 확인

자동 검사는 Unity 플레이와 원격 화면을 대신하지 않는다. 아래 항목은 최종 패키지를 새 프로필에 로컬 설치해 확인하고 결과를 기록한다. 기존 개발 프로필에서의 성공을 새 ZIP 설치 검증으로 간주하지 않는다.

- [ ] 호스트의 새 프로필에 BepInEx와 ZIP만 설치하고 중복 HellParty DLL 없이 실행한다.
- [ ] 모드 미설치 참가자가 접속하고 호스트·참가자가 동일한 스폰·피해·보상을 확인한다.
- [ ] stage1/4/7/10/13에서 난이도 1/2/3/4/5를 확인한다. 이어하기와 다음 스테이지도 확인한다.
- [ ] [Loom·Robe·스토커](playtests/loom-robe-playtest.md), [공용 납치](metadata/abduction.md), [몬스터 웨이브](events/monster-wave.md)의 회귀 항목을 확인한다.
- [ ] 플레이어 사망·퇴장·스테이지 전환 후 납치·고정 물품·소환수가 이전 상태로 남지 않는지 확인한다.
- [ ] 장시간 플레이 후 로그에 반복되는 HellParty 예외나 NavMesh·RPC 오류가 없는지 확인한다.

기존 플레이에서 사용자가 확인한 스토커 계단·벽 접촉 수정은 [스토커 플레이테스트](playtests/stalking-doll-playtest.md)에 기록했다. 나머지 실제 플레이 항목은 실행하지 않은 채 완료로 표시하지 않는다.

## 검증 명령과 산출물

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-Release.ps1 -Offline
```

게임 경로가 다르면 `-GameAssembly`에 설치된 `Assembly-CSharp.dll`을 지정한다. 게임 DLL을 찾지 못하면 배포 검증은 실패하며 검사 생략을 성공으로 기록하지 않는다. 일반 개발용 `Test-Dev.ps1`만 게임 계약 검사를 생략할 수 있고 그 경우 경고를 출력한다.

- ZIP: `artifacts/HellParty-0.1.0.zip`.
- 보고서: `artifacts/HellParty-0.1.0.release.json`.
- 공개용 소개문: [0.1.0 릴리즈 노트](releases/0.1.0.md). 검증 성공 시 `artifacts/HellParty-0.1.0.release-notes.md`에도 복사한다.
- ZIP 체크섬: `artifacts/HellParty-0.1.0.zip.sha256`.
- 보고서는 ZIP·플러그인·검사한 게임 어셈블리의 SHA-256과 자동 검증 범위를 기록한다. 실제 멀티플레이는 별도 미완료 상태다.
- `scripts/Test-Package.ps1`로 기존 ZIP을 재검사할 수 있다. 최종 산출물을 만들거나 검사할 때 프로필에 DLL을 배포하거나 게임을 실행하지 않는다.

패키지 형식의 기준은 [Thunderstore 공식 문서](https://wiki.thunderstore.io/mods/creating-a-package)다. 공개 업로드 전에 같은 버전이 이미 게시되어 있다면 버전을 올려야 한다. 업로드한 패키지는 수정할 수 없다는 [공식 업데이트 규칙](https://wiki.thunderstore.io/mods/updating-a-package)을 따른다.
