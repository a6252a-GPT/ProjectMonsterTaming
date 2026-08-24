. .\_tmp_parse_prefab.ps1

$objects = $global:__objects
$goCache = $global:__goCache
$rtCache = $global:__rtCache
$goToTransform = $global:__goToTransform

function Get-Field2 {
    param($body, $fieldName)
    $rx = [regex]::new("(?m)^  $fieldName`: (.*)$")
    $mm = $rx.Match($body)
    if ($mm.Success) { return $mm.Groups[1].Value.Trim() }
    return $null
}

function Get-AllDescendants {
    param($transformId, $depth = 0, $maxDepth = 20)
    $result = @()
    if ($depth -gt $maxDepth) { return $result }
    if (-not $rtCache.ContainsKey($transformId)) { return $result }
    $rt = $rtCache[$transformId]
    foreach ($childId in $rt.ChildrenIds) {
        if (-not $rtCache.ContainsKey($childId)) { continue }
        $childRt = $rtCache[$childId]
        $goId = $childRt.GameObjectId
        $name = Get-GameObjectName $goId
        $result += [pscustomobject]@{ GameObjectId = $goId; TransformId = $childId; Name = $name; Depth = $depth }
        $result += Get-AllDescendants -transformId $childId -depth ($depth+1) -maxDepth $maxDepth
    }
    return $result
}

function Find-AncestorNameContains {
    param($transformId, $substrings)
    $current = $transformId
    $visited = @{}
    while ($current -and $rtCache.ContainsKey($current) -and -not $visited.ContainsKey($current)) {
        $visited[$current] = $true
        $goId = $rtCache[$current].GameObjectId
        $name = Get-GameObjectName $goId
        foreach ($s in $substrings) {
            if ($name -and $name -like "*$s*") { return $name }
        }
        $current = $rtCache[$current].FatherId
    }
    return $null
}

function Get-ComponentsOfGameObjectBody {
    param($goBody)
    $rx = [regex]'(?m)^  - component: \{fileID: (-?\d+)\}'
    $ids = @()
    foreach ($m in $rx.Matches($goBody)) { $ids += $m.Groups[1].Value }
    return $ids
}

function Describe-GameObject {
    param($goId, $label = "")
    if (-not $goId -or -not $objects.ContainsKey($goId)) { Write-Host "  [$label] (not found: $goId)"; return }
    $goObj = $objects[$goId]
    $name = Get-Field2 $goObj.Body 'm_Name'
    $isActive = Get-Field2 $goObj.Body 'm_IsActive'
    $compIds = Get-ComponentsOfGameObjectBody $goObj.Body
    Write-Host "  [$label] Name=$name IsActive=$isActive GoId=$goId"
    foreach ($cid in $compIds) {
        if (-not $objects.ContainsKey($cid)) { continue }
        $c = $objects[$cid]
        if ($c.Type -eq 'MonoBehaviour') {
            $color = Get-Field2 $c.Body 'm_Color'
            $script = Get-Field2 $c.Body 'm_Script'
            if ($color) {
                Write-Host "      MonoBehaviour(Image-like) Color=$color Script=$script"
            }
            $interactable = Get-Field2 $c.Body 'm_Interactable'
            if ($interactable) {
                $colorsBlockRx = [regex]'(?ms)m_Colors:\r?\n(.*?)(?:\r?\n  m_)'
                $cm = $colorsBlockRx.Match($c.Body)
                $colorsBlock = if ($cm.Success) { $cm.Groups[1].Value } else { "" }
                Write-Host "      Button Interactable=$interactable"
                if ($colorsBlock) {
                    $flat = $colorsBlock -replace "[\r\n]+", ' | '
                    Write-Host "      Colors: $flat"
                }
            }
        } elseif ($c.Type -eq 'RectTransform') {
            $anchorMin = Get-Field2 $c.Body 'm_AnchorMin'
            $anchorMax = Get-Field2 $c.Body 'm_AnchorMax'
            $sizeDelta = Get-Field2 $c.Body 'm_SizeDelta'
            $anchoredPos = Get-Field2 $c.Body 'm_AnchoredPosition'
            Write-Host "      RectTransform AnchorMin=$anchorMin AnchorMax=$anchorMax SizeDelta=$sizeDelta AnchoredPos=$anchoredPos"
        }
    }
}

# Find all GameObjects named ListItem_Mission_XX
$listItems = @()
foreach ($goId in $goCache.Keys) {
    $n = $goCache[$goId].Name
    if ($n -match '^ListItem_Mission_(\d+)$') {
        $listItems += [pscustomobject]@{ GoId = $goId; Name = $n; Index = [int]$matches[1] }
    }
}

Write-Host "Found $($listItems.Count) ListItem_Mission_* GameObjects"

foreach ($li in $listItems) {
    $transformId = $goToTransform[$li.GoId]
    $ancestor = Find-AncestorNameContains -transformId $transformId -substrings @('Daily', 'Weekly')
    Write-Host "=== $($li.Name) (GoId=$($li.GoId)) Ancestor=$ancestor ==="
}

Write-Host ""
Write-Host "=========================================="
Write-Host "DEEP DUMP OF SPECIFIC SLOTS"
Write-Host "=========================================="

$targets = @(
    @{ Label = "Daily_06 (BUGGY-dark, GrowthDungeon 1회, not claimed)"; GoId = "9104024788662396738" },
    @{ Label = "Daily_05 (correct-dark, claimed)"; GoId = "6533124992960516955" },
    @{ Label = "Daily_07 (correct-bright)"; GoId = "4912891989512622190" },
    @{ Label = "Weekly_05 (BUGGY-dark, MonsterAscension)"; GoId = "5396681744456153222" },
    @{ Label = "Weekly_06 (BUGGY-dark, EquipmentEnhance)"; GoId = "4127736061365398148" },
    @{ Label = "Weekly_04 (correct-bright)"; GoId = "549947713335401790" }
)

foreach ($t in $targets) {
    Write-Host ""
    Write-Host "------ $($t.Label) ------"
    $rootGoId = $t.GoId
    Describe-GameObject -goId $rootGoId -label "ROOT"
    $rootTransformId = $goToTransform[$rootGoId]
    $descendants = Get-AllDescendants -transformId $rootTransformId
    foreach ($d in $descendants) {
        $indent = "  " * ($d.Depth + 1)
        Write-Host "$indent- $($d.Name) [depth=$($d.Depth)]"
        Describe-GameObject -goId $d.GameObjectId -label $d.Name
    }
}
