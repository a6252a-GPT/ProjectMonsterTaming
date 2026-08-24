param(
    [string]$Path = "Assets\ProjectMT\03_Features\Quest\Prefabs\PF_QuestMissionPanel.prefab"
)

$raw = Get-Content -Path $Path -Raw

# Split into documents. Each document starts with "--- !u!<classId> &<fileId>\n<TypeName>:\n..."
$docHeaderRegex = [regex]'(?m)^--- !u!(?<class>\d+) &(?<id>-?\d+)\r?\n(?<type>\w+):\r?\n'
$matches = $docHeaderRegex.Matches($raw)

$objects = @{}

for ($i = 0; $i -lt $matches.Count; $i++) {
    $m = $matches[$i]
    $startBody = $m.Index + $m.Length
    $endBody = if ($i + 1 -lt $matches.Count) { $matches[$i+1].Index } else { $raw.Length }
    $body = $raw.Substring($startBody, $endBody - $startBody)

    $id = $m.Groups['id'].Value
    $class = $m.Groups['class'].Value
    $type = $m.Groups['type'].Value

    $objects[$id] = [pscustomobject]@{
        Id = $id
        Class = $class
        Type = $type
        Body = $body
    }
}

Write-Host "Total objects parsed: $($objects.Count)"

function Get-Field {
    param($body, $fieldName)
    $rx = [regex]::new("(?m)^  $fieldName`: (.*)$")
    $mm = $rx.Match($body)
    if ($mm.Success) { return $mm.Groups[1].Value.Trim() }
    return $null
}

function Get-FileIdFromInline {
    param($inline)
    if ($inline -match '\{fileID:\s*(-?\d+)') { return $matches[1] }
    return $null
}

function Get-ChildrenIds {
    param($body)
    $rx = [regex]'(?ms)^  m_Children:\r?\n((?:  - \{fileID: -?\d+\}\r?\n)*)'
    $mm = $rx.Match($body)
    $ids = @()
    if ($mm.Success) {
        $listBlock = $mm.Groups[1].Value
        $idRx = [regex]'\{fileID:\s*(-?\d+)\}'
        foreach ($im in $idRx.Matches($listBlock)) {
            $ids += $im.Groups[1].Value
        }
    }
    return $ids
}

# Build helper caches
$goCache = @{}   # fileId(GameObject) -> name/isActive/componentIds
$rtCache = @{}   # fileId(RectTransform) -> gameObjectId/fatherId/childrenIds

foreach ($key in $objects.Keys) {
    $obj = $objects[$key]
    if ($obj.Type -eq 'GameObject') {
        $name = Get-Field $obj.Body 'm_Name'
        $isActive = Get-Field $obj.Body 'm_IsActive'
        $goCache[$key] = [pscustomobject]@{ Name = $name; IsActive = $isActive }
    } elseif ($obj.Type -eq 'RectTransform' -or $obj.Type -eq 'Transform') {
        $goInline = Get-Field $obj.Body 'm_GameObject'
        $fatherInline = Get-Field $obj.Body 'm_Father'
        $goId = Get-FileIdFromInline $goInline
        $fatherId = Get-FileIdFromInline $fatherInline
        $childrenIds = Get-ChildrenIds $obj.Body
        $rtCache[$key] = [pscustomobject]@{ GameObjectId = $goId; FatherId = $fatherId; ChildrenIds = $childrenIds }
    }
}

Write-Host "GameObjects: $($goCache.Count), Transforms: $($rtCache.Count)"

# Build reverse map: GameObjectId -> TransformId (its own RectTransform)
$goToTransform = @{}
foreach ($key in $rtCache.Keys) {
    $goId = $rtCache[$key].GameObjectId
    if ($goId) { $goToTransform[$goId] = $key }
}

function Get-GameObjectName {
    param($goId)
    if ($goCache.ContainsKey($goId)) { return $goCache[$goId].Name }
    return $null
}

# Export caches to global scope via files for reuse in later script invocations (dot-sourcing this file each time)
$global:__objects = $objects
$global:__goCache = $goCache
$global:__rtCache = $rtCache
$global:__goToTransform = $goToTransform

Write-Host "Parsing complete. Caches stored in global scope."
