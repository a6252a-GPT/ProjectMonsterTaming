using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    public sealed partial class EquipmentPageController
    {
        // ---------------------------------------------------------------
        // 이벤트 핸들러 / 새로 그리기
        // ---------------------------------------------------------------

        private void HandleInventoryChanged()
        {
            if (!isActiveAndEnabled || pendingInventoryRefresh != null)
            {
                return;
            }

            // 월드 드랍·보상 정산은 같은 프레임에 여러 장비를 연속 저장할 수 있다. 모든 변경이 반영된
            // 다음 프레임에 현재 필터·등급 정렬 기준으로 한 번만 다시 바인딩해 중간 순서가 남지 않게 한다.
            pendingInventoryRefresh = StartCoroutine(RefreshInventoryAfterDataChange());
        }

        private IEnumerator RefreshInventoryAfterDataChange()
        {
            yield return null;
            pendingInventoryRefresh = null;
            if (isActiveAndEnabled)
            {
                RefreshAll();
            }
        }

        private void RefreshAll()
        {
            RefreshInventoryList();
            RefreshCommanderSlots();
            RefreshCommanderStats();
        }
    }
}
