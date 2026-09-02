using System;
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

        private void HandleInventoryChanged() => RefreshAll();

        private void RefreshAll()
        {
            RefreshInventoryList();
            RefreshCommanderSlots();
            RefreshCommanderStats();
        }
    }
}
