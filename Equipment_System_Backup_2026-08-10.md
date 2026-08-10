# 장비(Equipment) 시스템 작업 내용 백업 (2026-08-10 기준, `a6252a` 브랜치)

이 문서는 `a6252a` 브랜치(커밋 `850e3ce "0810_장비시스템2일차"`)에 구현되어 있는 장비 시스템 전체를
백업/기록용으로 정리한 것입니다. 이후 `develop` 기준 새 브랜치에 이 내용을 옮겨서 다시 적용합니다.

원본 기획 문서 이름: `17_능력치_성장_장비_계산_규칙` (파일로 저장되어 있지 않고, 코드 주석에 규칙이
그대로 반영되어 있습니다. 아래 "설계 규칙" 섹션이 그 내용을 코드 기준으로 재구성한 것입니다).

---

## 1. 전체 구조 요약

```
Assets/ProjectMT/
├─ 02_Shared/Equipment/                 (저장 데이터 전용, 최소 의존성)
│  ├─ EquipmentPart.cs                  부위 enum (6종)
│  ├─ EquipmentGrade.cs                 등급 enum (5종)
│  ├─ EquipmentOptionType.cs            랜덤 추가 옵션 enum (13종)
│  ├─ EquipmentOptionRollData.cs        옵션 1건(종류+확정값) 저장용
│  ├─ EquipmentInstanceData.cs          보유 장비 1개(고유 인스턴스) 저장용
│  └─ EquipmentSaveData.cs              보유 목록 + 부위별 장착 ID 저장 원본(+View)
│
├─ 02_Shared/GameData/
│  ├─ GameProgressData.cs               (수정) equipment 필드 + Acquire/Equip/Unequip 반영
│  └─ SaveService.cs                    (수정) CurrentDataVersion 5 → 6
│
├─ 03_Features/Equipment/
│  ├─ Runtime/
│  │  ├─ EquipmentPart.cs               부위 표시 이름·드랍 확률(기획 정보)
│  │  ├─ EquipmentGrade.cs              등급 표시 이름·색상·드랍확률·능력치예산·옵션배율/개수
│  │  ├─ EquipmentStatType.cs           최종 능력치 13종 + 부위별 핵심 능력치 산출 테이블
│  │  ├─ EquipmentOptionInfo.cs         랜덤 옵션 13종의 기준값·표시이름·능력치 변환
│  │  ├─ EquipmentRandomOptionRoller.cs 등급별 랜덤 옵션 N개 굴림(중복 페널티 포함)
│  │  ├─ EquipmentDropRoller.cs         드랍 6개 굴림(부위·등급·랜덤옵션까지 확정된 인스턴스 생성)
│  │  ├─ EquipmentBaseItemDefinition.cs 카탈로그에 등록하는 부위별 베이스 아이템 1개
│  │  ├─ EquipmentCatalog.cs            베이스 아이템→부위+등급 조합 EquipmentDefinition 생성
│  │  ├─ EquipmentDefinition.cs         부위+등급이 고정하는 정보(아이콘·이름·핵심능력치)
│  │  ├─ EquipmentInventoryRuntime.cs   보유/장착 조회 + 획득/장착/해제 요청 파사드(정적)
│  │  ├─ CommanderEquipmentStats.cs     군단장 능력치 13종 구조체 + 합산 계산기
│  │  ├─ EquipmentPageController.cs     장비창 UI 전체 컨트롤러(가장 큰 파일)
│  │  ├─ EquipmentInventorySwipeHandler.cs  인벤토리 스크롤뷰 드래그/휠 → 페이지 전환
│  │  └─ EquipmentTestAcquireButton.cs  테스트용 "장비 획득" 버튼(드랍 6개 즉시 지급)
│  │
│  ├─ Editor/
│  │  ├─ EquipmentPagePrefabBuilder.cs  프리팹에 UI 오브젝트를 실제로 생성/저장하는 에디터 도구
│  │  └─ ProjectMT.Features.Equipment.Editor.asmdef
│  │
│  ├─ Data/EquipmentCatalog.asset       실제 카탈로그 에셋(부위 6개 베이스 아이템 등록됨)
│  ├─ Prefabs/PF_CommanderEquipmentPage.prefab   장비창 UI 프리팹
│  └─ Art/RenderTextures/...            군단장 미리보기용 렌더텍스처(장비와 직접 관련 없음)
│
└─ 03_Features/MainBattle/
   ├─ Runtime/MainBattleSceneRoot.cs     (수정) EquipmentPageController를 씬 조립 시 Configure
   └─ UI/MainBattleManagementUiController.cs  (기존에 이미 있던 열기/닫기 로직, 이번 작업과 무관하게
                                                develop에도 이미 동일하게 존재함 - 아래 6절 참고)
```

---

## 2. 설계 규칙 (문서 "17_능력치_성장_장비_계산_규칙" 반영)

### 2.1 부위(6종)와 핵심 능력치 분배

| 부위 | 표시 이름 | 핵심 능력치 분배(등급별 예산 % 를 아래 비율로 분배) |
|---|---|---|
| Weapon | 무기 | 공격력 100% |
| Helmet | 투구 | 체력 70% / 방어력 20% / 공격력 10% |
| Armor | 갑옷 | 방어력 70% / 체력 20% / 공격력 10% |
| Boots | 하의 | 체력 40% / 방어력 40% / 공격력 20% |
| Glove | 장갑 | 고정표(2.2절) - 치명타 확률/피해 |
| Ring | 장신구 | 고정표(2.2절) - 공격속도/이동속도 |

등급별 핵심 능력치 예산(%, "군단장 기본 스탯 대비"): Common 3 / Rare 5 / Epic 8 / Legendary 12 / Mythic 18.

### 2.2 장갑·장신구 고정표 (등급별, Common→Mythic 순)

| 부위 | 능력치 | Common | Rare | Epic | Legendary | Mythic |
|---|---|---|---|---|---|---|
| 장갑 | 치명타 확률(%p) | 1 | 2 | 3 | 4 | 5 |
| 장갑 | 치명타 피해(%p) | 5 | 10 | 15 | 20 | 25 |
| 장신구 | 공격속도(기본 대비 %) | 2 | 4 | 6 | 8 | 10 |
| 장신구 | 이동속도(기본 대비 %) | 1 | 2 | 3 | 4 | 5 |

### 2.3 등급(5종) 공통 표

| 등급 | 표시 이름 | 색상 | 드랍확률(%) | 랜덤옵션 배율 | 랜덤옵션 개수 |
|---|---|---|---|---|---|
| Common | 일반 | 초록 | 68 | 1.0 | 1 |
| Rare | 희귀 | 파란 | 20 | 1.5 | 1 |
| Epic | 영웅 | 노란 | 8 | 2.2 | 2 |
| Legendary | 전설 | 보라 | 3 | 3.2 | 3 |
| Mythic | 신화 | 빨간 | 1 | 4.5 | 4 |

부위는 6종 중 균등 확률(1/6)로 뽑힘. 드랍 1회 = 6개 동시 지급(부위·등급 각각 독립 롤).

### 2.4 랜덤 추가 옵션 13종 (부위/등급과 무관하게 인스턴스마다 독립적으로 확정)

| 옵션 | 기준값 | 단위 |
|---|---|---|
| 공격력 | 2 | 기본 스탯 대비 % |
| 방어력 | 2 | 기본 스탯 대비 % |
| 체력 | 2 | 기본 스탯 대비 % |
| 공격속도 | 1 | 기본 스탯 대비 % |
| 이동속도 | 0.5 | 기본 스탯 대비 % |
| 치명타 확률 | 1 | %p 절대값 |
| 치명타 피해 | 5 | %p 절대값 |
| 스킬 피해 | 2 | % 절대값 |
| 보스 피해 | 2 | % 절대값 |
| 일반 몬스터 피해 | 2 | % 절대값 |
| 스킬 쿨타임 감소 | 1 | %p 절대값 |
| 방어 관통률 | 2 | %p 절대값 |
| 피해 감소율 | 1 | %p 절대값 |

**확정값 공식**: `옵션 확정값 = 옵션 기준값 × 등급 배율(2.3절) × Random(0.8, 1.2)`

**추첨 방식**: 슬롯마다(등급별 개수, 2.3절) 13종 중 하나를 매번 독립적으로 뽑음(같은 옵션 중복 가능).
단, 이미 2번 뽑힌 옵션은 다음 추첨부터 가중치가 30%씩 누적 감소(`0.7^max(0, count-1)`).

옵션은 최초 획득 시 확정되어 저장되며, 재접속해도 다시 굴리지 않음.

### 2.5 능력치 상한(캡)

| 능력치 | 상한 |
|---|---|
| 치명타 확률 | 75 |
| 치명타 피해 | 300 |
| 공격속도 보너스(%) | 50 |
| 이동속도 보너스(%) | 30 |
| 스킬 쿨타임 감소 | 40 |
| 방어 관통률 | 80 |
| 피해 감소율 | 70 |

### 2.6 최종 능력치 = 군단장 기본값 + 장비 보너스

- 공격력/체력/방어력/공격속도/이동속도: 핵심 능력치 + 랜덤 옵션이 전부 "기본값 대비 %"로 누적된 뒤,
  `기본값 × (1 + 누적% / 100)`로 최종값 계산.
- 나머지(치확/치피/스킬·보스·일반몬스터 피해/쿨감/방관/피해감소): 절대값이 그대로 누적(캡 적용).
- **장비 능력치는 장착한 군단장에게만 적용되며, 몬스터·편성 부대 능력치에는 절대 영향을 주지 않는다**
  (계산이 완전히 분리된 별도 경로 - `CommanderEquipmentStatsCalculator`).

---

## 3. 인벤토리/저장 방식

- 기존(구버전, develop 브랜치)에는 "부위+등급" 조합으로 스택(`EquipmentStack`, 수량만 관리)했지만,
  **랜덤 옵션 때문에 아이템마다 능력치가 달라져서 더 이상 스택으로 취급하지 않는다.** 보유 장비는
  전부 `EquipmentInstanceData`(고유 ID + 부위 + 등급 + 랜덤옵션 목록)로 개별 저장.
- 최대 보유 수량 100개(`EquipmentSaveData.MaxTotalQuantity`). 초과분은 조용히 버림(임시 규칙).
- 부위별 장착 슬롯 6개(`equippedInstanceIds[6]`, 인덱스 = `(int)EquipmentPart`).
- `GameProgressData`에 `EquipmentSaveData` 필드로 포함되어 **세이브 파일에 영구 저장**됨
  (세이브 데이터 버전 6). "저장 데이터 초기화" 디버그 기능을 쓰면 장비도 함께 초기화됨.
- 변경 요청은 전부 `GameProgressChange`(불변 스냅샷) → `GameProgressData.TryApply()` → 저장 순서로
  처리됨(다른 진행 데이터와 동일한 패턴).
  - `GameProgressChange.AcquireEquipment(List<EquipmentInstanceData>)` - 드랍 6개 추가
  - `GameProgressChange.EquipItem(instanceId)` - 장착(같은 부위 기존 장착은 자동 교체)
  - `GameProgressChange.UnequipItem(part)` - 해제

---

## 4. UI 동작 (`EquipmentPageController.cs`)

- **참조 탐색**: 프리팹 내부 구조가 바뀌어도 안전하도록, 고정 경로 대신 이름 기반 재귀 탐색(`FindDeep`)
  으로 모든 UI 오브젝트를 찾음. 인스펙터에 직접 연결하지 않아도 동작.
- **인벤토리 슬롯 20개**(`InventorySlot_01~20`): 슬롯 오브젝트는 항상 활성 상태로 두고, 아이템이 있을
  때만 아이콘 영역을 켜고 "+" 표시를 끔. 아이콘은 부위별 대표 스프라이트(군단장 장착 슬롯에 이미 있는
  아이콘)를 재사용. 테두리는 목업에 있던 등급별 완성 프레임(`ItemFrame_01_Normal_Green/Blue/Yellow/
  Plum/Red`)을 복제해서 그대로 재사용(런타임 tint 아님).
- **페이지 넘김**: 보유 수량이 슬롯보다 많을 수 있어 페이지 단위(20개씩)로 표시.
  - 버튼 방식: `InventoryPagingBar`(이전/다음 버튼 + "N / M" 라벨) - 프리팹에 실제 오브젝트로 저장됨.
  - 스크롤 방식: `PF_InventoryScroll View`(사용자가 직접 추가한 ScrollRect)에 `EquipmentInventorySwipeHandler`
    를 붙여서 위/아래 드래그 또는 마우스 휠 스크롤로도 페이지 전환. 스크롤 방식이 활성화되면 버튼은
    자동으로 숨김(라벨은 유지).
- **필터 탭**: 전체/무기/방패(투구)/방어구/장신구/신발(하의) - 목업 5분류를 6부위에 대응(장갑은 전용
  탭 없이 "전체"에서만 노출 - 기획 확인 필요 항목으로 안내됨).
- **정렬**: 클릭할 때마다 등급 내림차순 ↔ 오름차순 전환.
- **상세 정보 텍스트 분리**: 한 칸에 다 몰아넣으면 줄이 넘쳐서 다른 UI와 겹쳐 보이는 문제가 있어,
  - `SelectedItemStat`(왼쪽) = 기본 옵션(핵심 능력치, 부위+등급 고정값)
  - `SelectedItemRandomOptionStat`(오른쪽) = 추가 랜덤 옵션
  두 칸으로 분리 표시. 추가 랜덤 옵션 표기는 전부 "%"로 통일(과거 "%p" 표기 제거).
- **장착 버튼**: 선택된 장비가 미장착이면 "장착", 이미 장착 중이면 "해제"로 텍스트 전환.
- **군단장 장착 슬롯 6개**(WeaponSlot 등): 인벤토리 빈 슬롯과 동일한 "+" 표시 방식으로 미장착 상태 표현.
- **능력치 카드(StatGrid) + 총전투력(CommanderSummary)**: 카드가 6개뿐이라 현재 6개 능력치만 표시
  가능(공격력/체력/방어력/공격속도/이동속도/치명타 확률). 나머지 7개(치피/스킬·보스·일반몬스터 피해/
  쿨감/방관/피해감소)는 카드가 늘어나면 매핑을 추가해야 함(`ResolveStatType` 참고).
  총전투력 공식은 기획 확정 전 임시 가중치(`CommanderEquipmentStats.EstimatePower()`).

---

## 5. 장비 획득 경로

- **테스트 전용**: `GetEquipmentButton` 오브젝트에 붙는 `EquipmentTestAcquireButton` - 누르면 즉시
  드랍 6개 지급(원래는 원정대 10·15·20...스테이지 클리어 보상으로 지급될 예정이나 그 연결은 아직 안 됨).
- 실제 스테이지 클리어 → 장비 드랍 연결은 **아직 구현되지 않음**(별도 작업 필요).

---

## 6. UI 연결 지점 (중요 - develop 브랜치에 이미 존재하는 부분)

`develop` 브랜치를 확인한 결과, 아래 연결은 **이미 develop에도 존재**하고 `a6252a`와 100% 동일함
(diff 없음). 즉 이 부분은 새로 만들 필요가 없고, 그대로 활용하면 된다:

- `MainBattleManagementUiController.cs`(`03_Features/MainBattle/UI/`): `equipmentButton`,
  `equipmentPage`(GameObject), `equipmentCloseButton` 필드 + `OpenEquipmentPage()` /
  `ToggleEquipmentPage()` / `CloseEquipmentPage()` 로직이 이미 완성되어 있음.
- `PF_ManagementUI.prefab`: `Buttons/EquipmentButton`과 `Pages` 하위에 `PF_CommanderEquipmentPage`
  중첩 프리팹 인스턴스가 이미 배치되어 있고, 위 컨트롤러 필드에도 이미 연결되어 있음
  (`equipmentPage: {fileID: 9190012153527788526}` 등).

**develop에만 있고 a6252a 작업으로 새로 추가/교체해야 하는 부분**은 다음 3가지뿐이다:

1. `GameProgressData.cs` / `SaveService.cs` - equipment 필드·Acquire/Equip/Unequip 반영 로직 추가
   (순수 추가(additive) diff, 기존 코드 삭제 없음).
2. `MainBattleSceneRoot.cs` - `EquipmentPageController.Configure()` 호출 추가(순수 추가 diff).
3. **`PF_CommanderEquipmentPage.prefab`의 실제 내용물** - develop에는 이 경로에 원래 있어야 할 장비
   UI가 아니라 **캐릭터 손가락 뼈대(스켈레톤) 데이터가 잘못 들어가 있었다**(develop 저장소 자체의
   실수로 추정, `a6252a` 작업과 무관). `.meta`의 guid(`4189301c28cd86d4ea1bcc140841d844`)는 양쪽이
   동일하므로, `PF_ManagementUI.prefab`의 기존 참조를 그대로 두고 이 프리팹의 내용만
   교체하면 자동으로 정상 연결된다.

또한 `03_Features/Equipment/Runtime/*.cs` 전체(`EquipmentPart`, `EquipmentGrade`,
`EquipmentPageController`, `EquipmentInventoryRuntime`, `EquipmentCatalog`, `EquipmentDefinition`,
`EquipmentDropRoller`, `EquipmentTestAcquireButton`, `EquipmentStatType`, `CommanderEquipmentStats`,
`EquipmentBaseItemDefinition`)는 develop에도 이름이 같은 "구버전"(스택 기반, 랜덤옵션 없음)이 이미
있으나, 이번 작업으로 완전히 새로 작성된 "인스턴스 기반 + 랜덤옵션 + 20슬롯" 버전으로 전면 교체된다.
이 파일들은 Equipment 폴더 밖에서 참조되는 곳이 전혀 없음을 확인했으므로(다른 시스템에 영향 없음)
안전하게 통째로 교체 가능하다. `EquipmentStack.cs`(구버전 스택 클래스)는 더 이상 쓰이지 않으므로 삭제.

---

## 7. 알려진 문제 / 이번에 고치는 것

- **공용 팝업 프레임 중첩 문제**: `PF_CommanderEquipmentPage.prefab`이 루트에서
  `PF_UIStandard_PopupMedium.prefab`을 중첩 프리팹(Nested Prefab Instance)으로 물고 있었다. 이 공용
  프레임은 `PF_CommanderGrowthPage.prefab`도 똑같이 물고 있어서, 장비창 프리팹을 저장할 때 실수로
  공용 프레임 쪽에 변경이 새어나가면 성장창까지 같이 깨지는 사고가 있었다.
  → **해결**: `PrefabUtility.UnpackPrefabInstance(..., PrefabUnpackMode.Completely, ...)`로 장비창
  프리팹 내부의 중첩 링크를 완전히 끊어서(겉모습·좌표·컴포넌트는 그대로 유지) 더 이상 공용 프레임과
  어떤 링크도 공유하지 않는 완전히 독립된 오브젝트 트리로 만든다. (에디터 도구
  `EquipmentPagePrefabBuilder.UnpackSharedBase()`)
- **`EquipmentPageController`가 프리팹 자체에 저장되어 있지 않던 문제**: 씬에만 임시로 붙어있어서
  새로 씬을 열 때마다 로직이 동작하지 않는 경우가 있었음 → 프리팹 루트에 없으면 자동으로 추가하고
  카탈로그까지 연결하도록 처리(`EnsureEquipmentPageController()`).

---

## 8. 아직 안 된 것 / 추후 작업 필요

- 실제 스테이지 클리어 보상으로 장비 드랍이 연결되어 있지 않음(테스트 버튼만 존재).
- StatGrid 카드가 6개뿐이라 13종 능력치 중 6개만 화면에 표시됨(나머지는 계산은 되지만 카드 UI가 없음).
- 총전투력 공식이 임시 가중치(기획 확정 수치 아님).
- 장갑 부위는 필터 탭이 없어 "전체"에서만 보임(전용 탭 필요 여부는 기획 확인 필요).
- 인벤토리 슬롯 20개도 보유 최대 100개보다 적어 페이지 넘김이 필요한 구조(정상 동작하지만, 슬롯을
  더 늘리는 것도 가능).

---

## 9. 참고 - a6252a 브랜치의 관련 커밋

- `025ac5e` 0809_장비창작업1일차
- `e60993a` 0809_장비창작업1일차(1)
- `850e3ce` 0810_장비시스템2일차 (← 이 문서 기준 최신 상태)
