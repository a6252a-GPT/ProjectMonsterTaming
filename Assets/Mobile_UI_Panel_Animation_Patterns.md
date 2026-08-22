# 모바일 UI 패널 등장 연출 패턴

## 1. 기본 추천 패턴

몬스터 성장·관리처럼 화면을 크게 사용하는 패널은 다음 조합을 기본 연출로 사용한다.

**배경 딤 페이드 + 패널 팝인 + 짧은 콘텐츠 순차 등장**

```text
배경 딤 페이드인
        ↓
패널이 살짝 위로 이동하며 확대
        ↓
제목·아이콘·내용이 짧게 순차 등장
```

### 권장 수치

| 요소 | 시작값 | 종료값 | 시간 | Ease |
|---|---:|---:|---:|---|
| 배경 딤 | Alpha 0 | Alpha 0.55 | 0.12초 | OutCubic |
| 패널 크기 | 0.96 | 1.0 | 0.28초 | OutCubic |
| 패널 위치 | 아래 16px | 원래 위치 | 0.28초 | OutCubic |
| 콘텐츠 순차 등장 | Alpha 0 | Alpha 1 | 0.12~0.16초 | OutCubic |

닫기 연출은 등장보다 짧게 설정한다.

- 배경 딤 페이드아웃: 약 0.12~0.16초
- 패널 축소 및 이동: 약 0.15~0.2초
- 닫을 때는 `Ease.InCubic` 사용

## 2. 패널 유형별 추천 패턴

| 패널 유형 | 추천 연출 |
|---|---|
| 몬스터 성장·관리 패널 | 페이드 + 스케일 0.96 → 1.0 |
| 보상 획득 팝업 | 스케일 0.85 → 1.0 + 약한 OutBack |
| 하단 선택창 | 아래에서 위로 슬라이드 |
| 우측 상세 패널 | 오른쪽에서 슬라이드 |
| 경고·확인 팝업 | 짧은 스케일 팝인 |
| 이벤트 배너 | 아래에서 위로 슬라이드 + 페이드 |

## 3. 현재 게임에 적용할 추천안

몬스터 성장·관리 화면에는 과한 바운스보다 안정적인 팝인 연출을 사용한다.

```text
1. 배경 딤 페이드인       0.12초
2. 패널 이동 및 확대      0.28초
3. 아이콘 등장             0.15초
4. 스탯·버튼 등장          0.05초 간격으로 순차 표시
```

레벨업 완료나 보상 획득처럼 강조가 필요한 순간에만 아이콘 또는 보상 슬롯에 약한 `Ease.OutBack`을 적용한다.

## 4. DOTween 구현 예시

```csharp
panel.localScale = Vector3.one * 0.96f;
panel.anchoredPosition = originalPosition + new Vector2(0f, -16f);
canvasGroup.alpha = 0f;

Sequence sequence = DOTween.Sequence();

sequence.Join(canvasGroup.DOFade(1f, 0.16f));
sequence.Join(panel
    .DOScale(1f, 0.28f)
    .SetEase(Ease.OutCubic));
sequence.Join(panel
    .DOAnchorPos(originalPosition, 0.28f)
    .SetEase(Ease.OutCubic));
```

콘텐츠를 순차 등장시킬 때는 짧은 간격을 사용한다.

```csharp
foreach (RectTransform item in contentItems)
{
    item.localScale = Vector3.one * 0.96f;
    item.gameObject.SetActive(true);

    item.DOScale(1f, 0.14f)
        .SetEase(Ease.OutCubic)
        .SetDelay(index * 0.05f);

    index++;
}
```

## 5. 사용 시 주의사항

- 패널을 열 때마다 긴 애니메이션을 재생하지 않는다.
- 일반 패널은 0.25~0.3초 안에 내용을 확인할 수 있게 한다.
- 큰 바운스나 반복 흔들림은 보상·레벨업 등 강조가 필요한 경우에만 사용한다.
- 애니메이션 중에도 닫기 버튼이나 뒤로가기를 막지 않는 것이 좋다.
- 모바일 저사양 기기를 고려해 UI 애니메이션은 30~60fps에서 안정적으로 동작해야 한다.
- 모션 감소 옵션을 추가할 경우 슬라이드·스케일을 줄이고 페이드 중심으로 전환한다.
- 애니메이션만으로 상태 변화를 전달하지 말고 텍스트, 색상, 사운드, 필요 시 햅틱을 함께 사용한다.

## 6. 최종 선택

이번 프로젝트의 기본 패널 등장 효과는 다음으로 통일한다.

> **배경 딤 페이드 + 패널 0.96 스케일에서 1.0으로 확대 + 16px 위로 이동 + OutCubic**

보상 획득 및 레벨업 완료 연출에는 위 기본 패턴에 아이콘의 약한 `OutBack` 효과만 추가한다.

## 참고

- [Apple Human Interface Guidelines - Motion](https://developer.apple.com/design/human-interface-guidelines/motion)
- [Apple Human Interface Guidelines - Accessibility](https://developer.apple.com/design/human-interface-guidelines/accessibility)
