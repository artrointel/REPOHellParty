# R.E.P.O. 0.5.0 업데이트 결과

2026-10-30, main. **코드 갱신과 자동 검증 완료, 실제 게임·순정 참가자 플레이테스트 대기.** [수정 전 조사](repo-0.5.0-audit.md)의 확인된 호출 문제들을 반영했다. 공개 버전 번호는 아직 0.2.1이며 이번 작업에서 커밋·배포·업로드하지 않았다.

## 빌드 기준

- 구버전 GameLibs/Unity NuGet 참조 대신 설치된 게임의 `REPO_Data/Managed`를 직접 참조한다. 게임 기준은 0.5.0, Steam build 26294889, Unity 6000.3.17f1이다.
- 기존 BepInEx와 HarmonyX를 사용하며 REPOLib·MenuLib을 추가하지 않았다.
- 내부 멤버는 빌드 시 생성한 publicized 사본으로 참조한다. 원본 게임을 변경하지 않으며 게임·Unity·Photon DLL을 배포물에 포함하지 않는다.
- 기본 Debug/Release는 일반 플레이용이다. CleanupCrew 테스트 시나리오는 기존처럼 명시한 별도 Debug 빌드에만 들어간다. Headgrab 실험은 다른 브랜치에 보존한다.

## 반영한 수정

| 영역 | 변경 |
| --- | --- |
| 초기화 | 헌트맨 소리 감지 패치가 실제 방출을 담당하는 `EnemyInvestigate(float, Vector3)`를 정확히 선택하도록 수정했다. |
| 납치 | 새 `TumbleSet`의 reset 인자를 명시했다. 상태 리비전과 MasterOnlyRPC는 순정 코드에 맡긴다. |
| 가치품 파괴 | 새 2인자 DestroyObject 시그니처로 컴파일되며 기존 효과 여부와 기본 forced=false를 유지한다. |
| 사망 수류탄 | 사라진 Initialize 호출을 제거했다. 순정 Awake 초기화와 기존 생성 준비 대기 후 Spawn을 사용한다. |
| TTS | 멀티플레이 ChatMessageSendRPC와 싱글플레이 TTSSpeakNow에 `en-US` 언어 코드를 전달한다. 문구와 속도는 기존 안내를 유지한다. |
| 위치 동기화 | 스태프·가스·오브젝트 제거에서 SetPositionRPC를 참가자 전체에 직접 보내지 않고 호스트의 순정 Teleport 동기화를 사용한다. |
| 가스 위치 | Animal/블라인드 가스 매개체를 처음부터 실제 방출 위치에 생성해 원격 파괴가 첫 위치 스냅샷보다 먼저 도착하는 경우에도 위치가 맞도록 한다. |
| 스태프 조준 | 생성 전에 프리팹의 발사구 오프셋과 최종 조준 회전을 계산해 최초 네트워크 생성부터 올바른 위치·방향을 갖게 한다. |
| Unity API | Rigidbody.linearVelocity와 FindObjectsByType을 사용한다. 검색 순서는 기존 InstanceID 정렬을 유지한다. |

몬스터 능력 해금 단계·수치·밸런스는 바꾸지 않았다. 신규 Pony 능력도 추가하지 않았다.

## 자동 검증

- 기본 Debug, Release, CleanupCrew 테스트 Debug: 경고·오류 0개.
- 규칙·게임 코드 계약·빌드 검사: **1,135개 통과**.
- 새 검사에는 세 빌드의 게임·Unity·Photon 멤버 참조 존재, Harmony 대상과 이름으로 주입하는 인자 존재, 검사하는 게임과 실제 컴파일 게임의 MVID 일치가 포함된다.
- 제거된 구 TTS/수류탄 참조를 의도적으로 전달하면 새 참조 검사가 거부하는 것도 확인했다.
- 게임 DLL 경로가 없을 때 빌드가 명확한 오류로 거부되는 것을 확인했다.
- 설치 게임 리소스 목록 1,537개 중 코드에 명시된 아이템·가치품·몬스터 경로 **31개 전부 존재**. 실제 프리팹 로드나 NavMesh 동작 검증을 의미하지 않는다.
- 로컬 테스트 ZIP 구성·해시 검사 및 변조 패키지 거부 검사 **10개 통과**. 게임 어셈블리가 패키지에 들어가지 않는다.

재현: [개발 환경](../development.md)의 빌드·`Test-Dev.ps1 -Offline -RequireGameAssembly`를 따른다. 참조 검사는 `tests/HellParty.Tests/GameReferenceAudit.cs`에서 수행한다. 로컬 로그는 `artifacts/game-0.5-tests.log`, `artifacts/game-0.5-package-tests.log`, `artifacts/game-0.5-resource-audit.log`다.

로컬 검증용 파일은 `artifacts/compatibility-0.5.0/HellParty-local-test.zip`이며 **새 공개 릴리즈로 업로드하면 안 된다**. 내부 메타데이터는 기존 0.2.1이고 검증용으로만 생성했다. 기존 `artifacts/HellParty-0.2.1.zip`은 덮어쓰지 않았다. DLL·게임·검증용 ZIP의 해시는 같은 폴더의 `verification.json`에 기록한다.

## 다음 실제 확인

[0.5.0 플레이테스트](../playtests/repo-0.5.0-playtest.md)를 먼저 최소 구성(BepInExPack + HellParty)에서 수행한다. 이후 평소 사용하는 외부 모드를 단계적으로 추가한다. 구 REPOLib/MenuLib의 게임 코드 참조 문제는 헬파티 수정으로 해결되지 않는다.

미설치 참가자와 위치·피해·Grab·관전·스폰·디스폰을 확인한 뒤 공개 버전 번호·변경 기록·릴리즈 노트를 확정하고 새 ZIP을 생성한다. 자동 검사 통과를 실제 멀티플레이 검증으로 기록하지 않는다.
