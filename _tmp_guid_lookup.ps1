$dataDir = "Assets\ProjectMT\03_Features\Quest\Data"
$questDir = Get-ChildItem -Path $dataDir -Directory | Where-Object {
    (Get-ChildItem -Path $_.FullName -Filter "QD_*.asset" -ErrorAction SilentlyContinue | Select-Object -First 1) -ne $null
} | Select-Object -First 1 -ExpandProperty FullName

$metaFiles = Get-ChildItem -Path $questDir -Filter "*.meta"

$guidToName = @{}
foreach ($f in $metaFiles) {
    $content = Get-Content $f.FullName -Raw
    if ($content -match 'guid:\s*([0-9a-f]{32})') {
        $guid = $matches[1]
        $name = $f.Name -replace '\.meta$', ''
        $guidToName[$guid] = $name
    }
}

$catalogContent = Get-Content "Assets\ProjectMT\03_Features\Quest\Data\QuestCatalog.asset" -Raw
$rx = [regex]'- \{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}'
$catalogGuids = @()
foreach ($m in $rx.Matches($catalogContent)) {
    $catalogGuids += $m.Groups[1].Value
}

$idx = 0
foreach ($g in $catalogGuids) {
    $name = "UNKNOWN($g)"
    if ($guidToName.ContainsKey($g)) { $name = $guidToName[$g] }
    Write-Host ([string]::Format("{0}: {1}", $idx, $name))
    $idx++
}
