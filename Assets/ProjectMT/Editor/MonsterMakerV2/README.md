# Monster Maker V2

## 목적

V2는 기존 3열 제작 흐름과 기능을 유지하면서 화면 배치와 스타일을 UI Builder에서
조절할 수 있도록 다시 만든 독립 EditorWindow다. 현재 `Assets`에서 컴파일되는 유일한
Monster Maker이며, `MonsterMakerDraft`, Validator, Writer, Runtime Profile 같은 공용 계약을 직접 사용한다.

- 기본 메뉴: `JC Tool/Monster/Monster Maker`
- V1 보관본: `LegacyArchives/MonsterMakerV1_20260830` (`Assets` 밖, 비컴파일)
- 최소 크기: 1418 × 760
- 배치: Catalog 230px + 제작 데이터 430px + 가변 Live Preview
- 제작 데이터: V1과 같은 1~10번 순서
- 편집 경계: 영구 Draft와 분리된 `DontSave` 작업 사본

## 구조

    MonsterMakerV2/
      MonsterMakerV2NativeWindow*.cs       창, Catalog, 편집, Preview, 프로필 요약
      MonsterMakerV2AdjustmentWindow*.cs   V2 전용 좌표·VFX 보정과 격리 3D Preview
      State/MonsterMakerV2State.cs         작업 사본, 복구, 저장, 외부 변경 방어
      Views/MonsterMakerV2AuthoringView*.cs 1~10 제작 UI와 전용 조작 UX
      Adapters/MonsterMakerV2PreviewAdapter.cs 공용 3D Preview 엔진 연결
      Workshops/MonsterWorkshopV2Window*.cs 기본공격·공격 액티브·효과형 통합 조립소
      Workshops/MonsterBasicAttackWorkshopSession.cs V2 기본공격 저장·검증·배정 세션
      Workshops/MonsterBasicAttackWorkshopPreviewV2.cs V2 기본공격 판정·VFX/SFX·FEEL Preview
      UI/MonsterMakerV2NativeWindow.uxml   UI Builder용 3열 구조
      UI/MonsterMakerV2NativeWindow.uss    폭, 간격, 색상, 상태 표현
      UI/MonsterMakerV2AdjustmentWindow.uxml/.uss  UI Builder용 보정 창
      UI/MonsterWorkshopV2Window.uxml/.uss UI Builder용 통합 조립소

`PreviewRenderUtility`가 필요한 렌더 표면만 `IMGUIContainer`를 사용한다. 나머지 Catalog,
제작 입력, 프로필 요약, 작업대, 검증 결과는 UI Toolkit이다.

## 주요 작업 흐름

- Catalog 44종 검색, 기본순/등급순, 초상화·등급·Draft 상태, 선택 복원
- Catalog 접기/펼치기와 운영 목록 밖의 저장 Draft를 여는 `원본 열기`
- 새 Monster, 기존 Draft 열기, 작업 사본 편집, Undo/Redo, 초기 복원, 변경 폐기
- 모델·공격 기준점·피격 중심과 VFX 위치를 전용 3D 조절 창에서 직접 조정
- 패시브에 필요한 수치만 한국어로 노출하고 레벨 1→200 결과 요약
- `6. 스킬`은 `패시브 / 액티브` 동급 탭이며 각 탭의 `패시브 사용 / 액티브 사용`을 독립 적용한다.
- 패시브·기본공격·공격/효과형 액티브는 원시 ObjectField 대신 저장 프리셋 선택/변경 메뉴를 사용한다.
  액티브 종류는 선택한 프로필이 결정하므로 Maker에서 공격형/효과형을 다시 선택하지 않는다.
- 기본공격·공격 액티브·효과형 조립은 하나의 `MonsterWorkshopV2Window`에서 탭만 전환한다.
  세 탭은 저장 목록, 분리 작업 사본, 복제, 새 저장, 현재 저장, Maker 배정, Preview 문법을 공유한다.
- 공격 액티브 VFX/SFX 계약은 현재 Step 공격 형태에서 실제 실행 가능한 발생 시점·기준 위치·반복·
  부착·종료 옵션만 보여 주고, 기본공격 고급 계약도 전부 한글 이름을 사용한다.
- 기본공격·공격/효과 액티브 VFX가 `사용` 상태인데 Prefab만 비어 있으면 실제 계약의 발생 시점·
  기준 위치·부착·유지 시간에 맞춰 색상형 임시 표식을 재생한다. 이 표식은 3D Preview 안에서만
  생성되는 `HideAndDontSave` 오브젝트이며 Draft·Profile·Runtime에는 저장되지 않는다.
- 액티브 Profile Step·효과 묶음은 작업 사본을 열거나 프리셋을 바꿀 때 자동 동기화한다.
  공격 액티브 Runtime은 '저장하고 공격 액티브 게임 자산 갱신' 한 버튼으로만 반영하며,
  미완료 VFX/SFX 결정은 저장 전에 화면과 하단 검증에 정확한 항목으로 표시한다.
- 기본공격 조립소, Runtime 동기화 상태, 계약 기반 VFX/SFX 결정, 타이밍 게이지+정확한 숫자 입력,
  SFX 미리듣기, VFX 재생·보정, 원본 보존용 전용 래퍼 생성
- 공격 Motion, Recipe Marker 1개 정리, Breath 지속시간 예외
- 대기·이동·사망 Motion과 사망 VFX/SFX
- Preview 환경·카메라·모션·피해·판정범위와 모델·공격·피격 기준점 토글/좌표 확인, 프로필 요약, 검증, 저장, 전투 반영
- Preview 안쪽 전투 상태, 재생/기록 2줄·각 3등분 작업 제어, 상태/전투 2줄·같은 3열 모션, 1:1 검증/반영, 실행할 때만 확장되는 하단 검증 로그
- 하단 모션은 가로 ScrollView를 사용하지 않는다. 상태 3종은 고정 3등분하고, 기본공격들은
  `공격 1..N`과 `랜덤`으로 줄여 액티브 버튼까지 같은 폭으로 한 줄 배치한다. 실제 Clip명은 Tooltip에 보존한다.
  액티브가 없는 몬스터는 액티브 칸을 숨겨 기본공격 버튼이 남는 폭을 전부 사용한다.
- 쉬운 한글 선택지와 폭을 넘지 않는 Catalog 검색·행·선택 표시
- Catalog 검색·정렬·하단 작업 버튼은 줄어들지 않는 고정 높이를 사용하고 목록은 전용 프레임 안에서만 그린다.
- 기본공격 VFX 수명·시작점·속도·위치·회전·크기를 한 창에서 편집
- 모델·공격 기준점·피격 중심, 기본공격·피격/사망 VFX 보정 버튼은 V1 팝업을 열지 않고
  `MonsterMakerV2AdjustmentWindow`만 연다. 숫자 입력과 노란 3D 핸들은 즉시 같은 Preview에
  반영하고 `적용`할 때만 V2 작업 사본 콜백을 호출하며 `취소`·Esc·창 닫기는 값을 버린다.
- VFX 창은 위치·회전·크기·유지 시간·Prefab 내부 시작점·재생 속도를 함께 편집한다.
  속도 게이지는 로그 축 `1배` 중심이며 128배 같은 직접 입력도 범위를 확장해 보존한다.

## 안전 계약

- 일반 필드 편집은 영구 Draft를 직접 바꾸지 않는다.
- 세 조립소의 작업 Profile은 `HideInHierarchy | DontSave`만 사용한다. `NotEditable`이 포함되는
  `HideAndDontSave`는 Preview 오브젝트에만 허용하며, 저장 프리셋을 불러온 작업 사본은 즉시 편집 가능해야 한다.
- 창 직렬화값과 `SessionState`에 작업 사본을 중복 복구하고, 복구된 Dirty는 저장·폐기 전까지 유지한다.
- `원본 저장`을 눌렀을 때만 작업 사본을 영구 Draft에 복사한다.
- 로드 뒤 원본 GUID·경로·ID·파일 지문이 바뀌면 저장을 거부한다.
- `전투 반영`은 공용 `MonsterMakerValidator`와 `MonsterMakerAssetWriter`를 사용한다.
- Vendor 모델/VFX 원본은 수정하지 않는다. 전용 VFX 래퍼는 사용자가 버튼을 눌렀을 때만 생성한다.
- Editor 회귀 검사는 영구 Draft 저장, 전투 반영, Scene 저장을 실행하지 않는다.
- V1 구현과 당시 테스트 원본은 `LegacyArchives/MonsterMakerV1_20260830`에 보관하며 활성 프로젝트에서는 컴파일·메뉴 등록하지 않는다.
- 독립 조립소 메뉴는 이전 Maker 배정 대상을 이어받지 않으며, 폐기 시 작업 JSON뿐 아니라
  불러온 자산 경로도 함께 지워 다음 실행에서 빈 사본과 저장 자산이 섞이지 않게 한다.

## 유지보수

- 열 폭·간격·색상·고정 영역은 UXML/USS에서 조절한다.
- UXML이 USS를 직접 참조하므로 UI Builder와 실제 창이 같은 스타일 원본을 사용한다.
- 실제 EditorWindow는 이전 V2 USS만 `Remove`한 뒤 UXML을 다시 복제해
  도메인 리로드에도 같은 USS가 정확히 1개만 남도록 한다.
- `rootVisualElement.styleSheets.Clear()`는 Editor 기본 폰트·테마까지 제거해 실제 창의
  글자가 사라지므로 사용하지 않는다.
- 세 열 제목의 시작선, 제작 입력의 142px 라벨 열, 같은 그룹 버튼의 동일 폭을 시각 정렬 기준으로 유지한다.
- 하단 작업대는 카드 사이 8px, 작업 제어·모션의 52px 행 라벨과 3열 버튼, 검증/반영 1:1 폭을 공통 정렬 기준으로 유지한다.
- 프로필 능력치는 2열로 유지하고 각 카드·입력·버튼은 최소 창 크기에서도 서로 겹치지 않아야 한다.
- 조건부 제작 흐름은 `MonsterMakerV2AuthoringView` partial 파일에 둔다.
- 작업 사본·저장·복구 규칙은 `MonsterMakerV2State`에만 둔다.
- 3D Preview 엔진 자체는 공용 구현을 Adapter로 호출한다.
- 변경 후 `MonsterMakerV2ParityTests`와 `MonsterMakerSafetyContractTests`를 함께 실행한다.
- 조립소 변경 후 `MonsterWorkshopV2Tests`와 기본공격·공격/효과 액티브 계약·Maker E2E를 함께 실행한다.
- 조립소 목록의 사용 수는 Draft 전체를 한 번만 순회해 계산하고, 작업 사본 `SessionState` 저장은
  입력 중 0.25초 단위로 합쳐 처리한다. 입력 반응을 위해 필드 변경마다 전체 탐색·직렬화를 반복하지 않는다.
- UI 완료 보고 전 실제 EditorWindow를 최종 캡처해 겹침·잘림·정렬·공백을 눈검수한다.
- 보정 창은 `MonsterMakerV2AdjustmentWindow.uxml/.uss`를 UI Builder에서 직접 열어 조절하고,
  3D 렌더와 PositionHandle만 `IMGUIContainer`에 둔다.

## UI Builder로 열기

1. Project 창에서 `UI/MonsterMakerV2NativeWindow.uxml`을 더블클릭하거나 UI Builder의
   `File > Open`으로 해당 파일을 연다.
2. 빈 문서의 Library에서 UXML을 끌어놓으면 편집 대상이 아닌 `TemplateContainer`가 되므로
   사용하지 않는다.
3. `StyleSheets`에 `MonsterMakerV2NativeWindow.uss`가 1개 표시되는지 확인한다.
4. Viewport는 최소 `1418 × 760`, 실제 검수 크기 `1496 × 1110`에서 확인한다.

## 검증 기록

2026-08-30 기준 V2·Maker 안전성·신화/전설 E2E·기본공격·공격/효과 액티브·VFX 계약·범용 스킬 EditMode 합계 139/139와 Unity Compile/Console Error 0을 확인했다.
실제 열린 1496×1093 창의 해석 배치는 Catalog 230px, 제작 430px, Preview 804px이며 제작 헤더 버튼 4개와 공격/액티브 줄, 검증/반영 버튼이 각 패널 경계를 넘지 않았다.
저장되지 않은 `pango_01` 작업 사본은 전체 검사 뒤에도 Dirty=true와 동일 SHA-256을 유지했다.
Windows Graphics Capture가 Unity 창에서 `SetIsBorderRequired 0x80004002`를 반환했지만
Unity 창 좌표 기반 대체 화면 복사로 최종 하단 작업대 PNG와 요소 좌표를 확인했다.
최종 캡처는 `MonsterMakerV2_LowerWorkspaceAligned_20260830.png`이며 작업 제어 6개,
상태/전투 모션 6개와 검증/반영 버튼의 열 정렬을 함께 검수했다.
좌표·VFX 보정 창은 `MonsterMakerV2_PositionAdjustment_20260830.png`,
`MonsterMakerV2_VfxAdjustment_20260830.png`으로 다시 캡처했다. 첫 캡처에서 UI Toolkit
컨테이너 원점을 반영하지 않아 핸들이 기준점보다 위로 밀린 회귀를 발견했고, 창 좌표를
`Handles.SetCamera`에 적용한 뒤 노란 핸들과 선택 점이 정확히 겹치는 것을 재확인했다.
`MonsterMakerV2_AdjustmentFunctionalProbe_20260830.txt`에는 적용 1회, 취소 0회,
VFX 6개 값, 재생 제어, 초기값 복원, 영구 Draft·Scene 무변경 결과를 남겼다.

통합 조립소 V2는 2026-08-30 기준 전용 회귀 `9/9`을 포함해 V1 분리·기본공격·공격
액티브·효과형·Maker 스킬 계약 집중 EditMode 총 `116/116`을 통과했다. 기본공격은 V2 전용
Session/Preview를 사용하고 Maker 배정 완료는 `MonsterWorkshopAssignmentEvents` 공용 경계로 수신한다.
Preview Scene은 각 Preview owner의 등록/해제로 보존하며, 라이브 버튼 재생 뒤 시작 Scene 수로 복귀한다.

V1 창 타입 5종은 활성 Editor Assembly에서 `0`이고 원본은 `Assets` 밖 레거시 보관본에 남았다.
`6. 스킬`의 패시브·액티브 탭은 실제 1496px 창에서 373px 행을 183/182px로 나눠 정렬되며,
패시브 탭에는 `패시브 사용`만, 액티브 탭에는 `액티브 사용`만 생성되는 것을 UI Toolkit
메타데이터로 확인했다. 기존 공통 사용값은 최초 로드 시 패시브 사용으로 이관하고, 실제 액티브
프로필이 있던 전설·신화 Draft만 액티브 사용을 함께 켠다. 비활성 탭의 프리셋·모션·연출값은
보존하되 Validator와 Writer는 활성 탭만 전투에 반영한다.

2026-08-30 임시 VFX Preview 계약은 기본공격 `15/15`, 공격 액티브 Maker E2E `6/6`,
효과형 Runtime PlayMode `4/4`, 통합 조립소 `9/9`을 통과했다. `사용 + Prefab 없음`은
Preview 표식으로 확인되지만 기존 Validator의 `VFX 누락` 경고와 저장 차단은 그대로 유지된다.
실제 Prefab이 연결되면 같은 경로에서 실제 VFX만 재생한다. Unity 활성 Scene은 `00_Entry`,
dirty=false를 유지했다.

2026-08-30 저장 프리셋 편집 잠금 회귀는 `gale_dance`를 실제 V2 조립소에 불러와 표시 이름을
UI 입력으로 수정하고 `현재 프리셋 저장` 버튼이 활성화되는 경로로 재현·검증한다. 기본공격·공격
액티브·효과형의 비영구 작업 Profile은 모두 `DontSave`를 유지하면서 `NotEditable`은 포함하지 않는다.
조립소 `9/9`, 기본공격 상용 준비 `15/15`, 공격 액티브 Maker E2E `6/6`, 효과형 Profile `5/5`,
Maker V2 패리티 `15/15`, 효과형 Runtime PlayMode `4/4`로 연결 회귀 총 `54/54`를 통과했고,
Unity Console Error 0과 `00_Entry` Scene dirty=false를 유지했다.

2026-08-30 하단 모션 재생부는 공격 Clip명이 길어질 때 생기던 가로 스크롤과 우측 액티브 잘림을
제거했다. 공격 개수+랜덤+액티브가 동일 폭으로 자동 축소되고, 실제 Clip명은 각 버튼 Tooltip에서
확인한다. 액티브 미사용 시에는 해당 버튼과 빈 칸을 함께 숨긴다. 실제 1496×1108 EditorWindow에서
상태 3종은 143~144px, 공격 3종+랜덤은 106~107px로 균등 배치되고 전투 줄이 패널 안에서 닫히는
것을 UI Toolkit 좌표로 확인했다. Windows Graphics Capture는 기존과 같은 `0x80004002`를 반환해
새 PNG 대신 라이브 요소 좌표와 가로 ScrollView 부재를 최종 근거로 사용했다.
