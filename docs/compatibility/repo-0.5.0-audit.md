# R.E.P.O. 0.5.0 호환성 점검

이 문서는 수정 전 조사 기록이다. 이후 구현·자동 검증 결과는 [0.5.0 업데이트 결과](repo-0.5.0-upgrade.md)를 따른다.

점검일: 2026-10-30. 브랜치: main. **현재 HellParty 0.2.1 빌드는 0.5.0 호환성 수정이 필요하다.** 이번 작업은 검토이며 런타임 코드·패키지 버전·설치 프로필은 수정하지 않았다. 게임 실행과 멀티플레이 검증도 수행하지 않았다.

## 기준

- 공식 업데이트: [R.E.P.O. v0.5.0 — The Sunstone Update](https://steamcommunity.com/app/3241660/announcements/). 신규 맵·Pony·다국어·게임패드 외에 래그돌 이동, 음성 재생·재접속, 아이템과 가치품 처리도 변경됐다.
- 설치된 Steam 빌드: `26294889`.
- 설치된 `Assembly-CSharp.dll`: 6,040,576 bytes.
- SHA-256: `AA6B1CE0DEEF8D2B7104A3722864C4BA94C107257726471F8E44B45C99E36888`.
- 컴파일 참조는 여전히 `R.E.P.O.GameLibs.Steam 0.4.4-ngd.0`, Unity 참조 패키지는 `2022.3.62`다. 따라서 기존 참조로 빌드가 성공해도 새 게임에서 실행 가능함을 의미하지 않는다.

## 확인된 HellParty 수정 대상

| 우선순위 | 변경 | 영향 및 대응 방향 |
| --- | --- | --- |
| 최우선 | `PhysGrabObjectImpactDetector.EnemyInvestigate`가 `(float)`와 `(float, Vector3)` 오버로드를 갖는다. | `HuntsmanSoundSourceScope.TargetMethods`가 이름만으로 검색한다. 사용 중인 HarmonyX의 `AccessTools.Method`는 이 경우 인자 없는 오버로드도 찾지 못하면 `AmbiguousMatchException`을 던진다. 초기화 실패 시 Plugin이 자체 패치 전체를 해제하므로 모드 전체가 비활성화될 수 있다. 정확한 시그니처를 지정하고 필요한 두 경로를 검토해야 한다. |
| 높음 | `PlayerTumble.TumbleSet(bool, bool)` → `(bool, bool, bool _reset = false)` | 헌트맨·룸 공용 납치 경로의 기존 바이너리 호출이 존재하지 않는다. 새 참조로 재컴파일하고 새로운 상태 리비전 동기화 규칙을 유지해야 한다. |
| 높음 | `PhysGrabObjectImpactDetector.DestroyObject(bool)` → `(bool effects = true, bool forced = false)` | Chef/Upscream 변장, 스토커 사망, 오리·Animal 가스 매개체, 웨이브 음악 오브젝트 파괴 등에 쓰는 기존 호출이 존재하지 않는다. 새 인자의 의도를 보존해 갱신해야 한다. |
| 높음 | `ItemGrenadeHuman.Initialize()` 제거, 초기화가 `Awake()`에 배치됨 | 헌트맨 죽음의전술에서 기존 Initialize 호출을 제거하고 생성·준비 대기·Spawn 순서를 새 코드에 맞춰 검증해야 한다. |
| 높음 | `TTSVoice.TTSSpeakNow(string, bool)` → `(string, bool, string localeCode)` | 싱글플레이 TTS 호출이 변경됐다. 영문 안내에는 명시적인 영문 locale을 전달하는 방향이 적절하다. |
| 높음 | `PlayerAvatar.ChatMessageSendRPC`에 `_localeCode` 인자 추가 | 현재 안내는 문장과 속삭임 여부만 송신한다. 새 RPC가 요구하는 문자열 인자가 빠져 순정 참가자 TTS가 실패한다. 공개 함수 `ChatMessageSend`에도 locale 인자가 추가됐다. |
| 검토 | `PhysGrabObject.SetPositionRPC`가 호스트에서만 실제 위치 처리를 수행하도록 변경됨 | 클라운 스태프, Animal 가스 매개체, NetworkLifetime의 직접 All RPC는 더 이상 각 참가자에서 같은 함수 본문을 실행하지 않는다. 호스트의 `SetPositionLogic`이 PhotonTransformView.Teleport로 전달하는 경로를 사용하며, 파괴 전 원격 위치 반영 순서를 실제로 재검증해야 한다. 메시지 정보 인자 추가 자체는 Photon이 공급하므로 인자 누락 RPC 오류로 분류하지 않는다. |
| 검토 | GrabLinkRPC·TumbleSetRPC 등에 상태 리비전 인자 및 권한 검사 변경 | GrabLinkRPC를 직접 생성해 보내는 코드는 확인되지 않았으며 현재 공통 파라미터를 사용하는 패치가 즉시 깨진다고 확정할 수는 없다. 납치·헤드 부착·강제 Grab 해제·삭제 중 지연 RPC는 새 상태 전환 순서에서 확인해야 한다. |

위 네 개 직접 메서드 참조(Initialize, DestroyObject, TumbleSet, TTSSpeakNow)는 최신 Release DLL의 총 278개 게임 멤버 참조를 새 어셈블리와 비교해 확인했다. 일반화된 제네릭 PrefabRef 참조는 정상 해석되는 것으로 재확인했다. 반사 FieldRef의 이름·타입 누락은 해당 스캔에서 발견되지 않았다. 이것은 런타임 동작이나 모든 Harmony 삽입 계약이 유지됨을 보증하지 않는다.

## 검사 결과

- 기본 Debug, Release, CleanupCrew 테스트 Debug는 기존 0.4.4 참조로 빌드 성공, 경고·오류 0개.
- `scripts/Test-Dev.ps1 -Offline -RequireGameAssembly`: **실패**. 새 TTS 시그니처 검사에서 중단됐다.
- 기존 테스트 러너를 복제한 진단용 실행에서 assertion 실패 후에도 검사하도록 하여 추가 변동을 조사했다. 기존 테스트 소스와 정상 러너의 실패 처리는 변경하지 않았다.
- 진단 결과: 기존 검사 1,117개 조건 충족, 2개 실패. TTS 인자 수, TumbleSetRPC의 기존 MasterAndOwnerOnlyRPC 검사다. 후자는 새 게임에서 MasterOnlyRPC로 변경됐다. 정상 테스트 통과로 취급하지 않는다.
- 진단 중 EnemyInvestigate의 단일 메서드 선택도 새 오버로드 때문에 예외가 발생했다. 진단 복제본만 오버로드들을 확인하도록 바꿔 나머지 검사를 완료했다.
- 기존 게임 계약 검사는 메서드 전체 참조·이름만으로 찾는 반사 호출을 모두 검사하지 않으므로, 향후 새 메서드 참조/오버로드 계약을 정식 검사에 추가할 필요가 있다.

로컬 증거:

- `artifacts/game-0.5-audit-tests.log`: 정상 러너의 실패.
- `artifacts/game-0.5-contract-diagnostics.log`: 이어서 조사한 진단 결과. 정상 테스트 통과 로그가 아님.
- `artifacts/game-0.5-member-audit.json`, `.log`: 최신 로컬 빌드 및 설치 DLL들의 게임 멤버 참조 비교.
- `artifacts/decompiled/0.5.0/`: 설치된 새 게임에서 읽은 관련 타입 코드.
- `artifacts/compat-audit/`: 참조 검사에 사용한 임시 도구. `artifacts`는 Git 관리 제외 영역이다.

## 현재 테스트 프로필의 다른 모드

최신 HellParty 빌드 1개와 프로필에 설치된 관리형 DLL 35개, 총 36개를 검사했다. 버전은 마켓 패키지 버전이 아니라 DLL의 AssemblyVersion이다. DLL이 없는 빈 디렉터리는 설치·작동하는 모드로 판정하지 않았다.

| 모드/라이브러리 | 로컬 DLL 버전 | 확인 결과 |
| --- | --- | --- |
| MenuLib | 2.5.4.0 | 기존 `MenuManager.MenuEffectClick` 5인자 호출이 사라졌다. 새 게임은 마지막에 `bool rumble = true`가 추가된 6인자다. 업데이트 필요. |
| REPOLib | 4.0.0.0 | `SteamManager.instance`, `SteamManager.developerMode` 필드 참조가 사라졌다. 새 게임의 SteamManager는 static class다. 업데이트 필요. |
| PocketCartTurret | 0.1.0.0 / 0.1.1.0 | 검사한 직접 게임 멤버 참조의 누락은 발견되지 않음. 실제 카트·지뢰·소유권·총기 변화에 대한 런타임 검증은 필요. |
| RaiseToUpgrade | 0.1.0.0 두 설치본 | 검사한 직접 참조 누락은 발견되지 않음. SetPositionRPC 동작 변경은 재검증 대상. |
| SharedHealthPacks, Minimap, REPOConfig, ReturnToLobby, MorePlayers, PublicSaves, Korean_Chat | 설치된 DLL 기준 | 검사한 직접 참조 누락은 발견되지 않음. 반사·Harmony·에셋·상호작용까지 호환된다는 의미는 아님. |

프로필에 HellParty가 `HellParty/HellParty.dll`(0.2.1.0)과 `Artrointel-HellParty/HellParty/HellParty.dll`(0.2.0.0) 양쪽에 존재한다. PocketCartTurret·RaiseToUpgrade도 로컬 배포본과 모드 관리자 배포본이 함께 있다. BepInEx가 어떤 버전을 선택하는지 혼동하지 않도록 실제 업데이트 시 설치 경로를 정리해야 한다. 이번 점검에서는 삭제·배포하지 않았다.

최신 마켓 버전이 이 문제를 이미 해결했는지는 이번 로컬 검사로 확인하지 않았다. MenuLib/REPOLib는 현재 설치본의 업데이트가 필요하다는 결론이다.

## 권장 작업 순서

1. 새 게임용 컴파일 참조와 개발 빌드 환경을 준비한다.
2. Harmony 검색을 명확하게 수정해 초기화부터 정상화한다.
3. 위 직접 호출과 locale 포함 TTS를 갱신한다. 새 Tumble 권한·리비전 계약을 우회하지 않는다.
4. 새 함수 계약을 정식 검사에 추가하고 테스트가 전체 통과하도록 한다. 이전 가정을 무조건 삭제해 통과시키지 않는다.
5. 일반 Debug/Release에서 시작 알림·TTS, 오리와 가스, 납치, 수류탄, 변장 파괴, 스토커 사망, 웨이브 종료를 우선 확인한다. 클라운·멘탈리스트의 총기/스태프와 새 맵 NavMesh도 포함한다.
6. 모드 없는 참가자와 위치·피해·Grab·스폰·디스폰을 확인한 뒤 새 버전 ZIP을 만든다.

신규 Pony는 별도 능력 기획 대상이다. 새 몬스터가 추가됐다는 이유만으로 기존 몬스터 스폰 보정이나 특수능력을 자동 변경하지 않는다. Headgrab 실험 브랜치도 구 참조를 사용하므로 main 호환성 수정 후 별도 갱신이 필요하다.
