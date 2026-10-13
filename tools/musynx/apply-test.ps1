param([ValidateSet('Install','Restore','Verify')][string]$Mode = 'Verify')
$ErrorActionPreference = 'Stop'
$experimentRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\artifacts\musynx-test'))
$manifest = Get-Content -LiteralPath (Join-Path $experimentRoot 'installation.json') -Raw | ConvertFrom-Json
$gameRoot = [IO.Path]::GetFullPath($manifest.gameRoot)
$gameExe = Join-Path $gameRoot 'MUSYNX.exe'
if ($Mode -ne 'Verify') {
    if (Get-Process -Name MUSYNX -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $gameExe }) {
        throw '请先关闭 Steam 原版 MUSYNX，再进行安装或恢复。'
    }
}
$items = foreach ($entry in $manifest.files) {
    $target = [IO.Path]::GetFullPath((Join-Path $gameRoot $entry.relativePath))
    if (-not $target.StartsWith($gameRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid target path' }
    $original = Join-Path $experimentRoot ('original\' + $entry.relativePath)
    $replacement = Join-Path $experimentRoot ('replacement\' + $entry.relativePath)
    $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash -ne $entry.originalSha256) { throw "备份校验失败：$original" }
    if ((Get-FileHash -LiteralPath $replacement -Algorithm SHA256).Hash -ne $entry.replacementSha256) { throw "替换文件校验失败：$replacement" }
    if ($actual -ne $entry.originalSha256 -and $actual -ne $entry.replacementSha256) {
        throw "游戏文件已变化，可能更新过版本；停止覆盖：$target"
    }
    [pscustomobject]@{ Target=$target; Original=$original; Replacement=$replacement; Entry=$entry; Actual=$actual }
}
if ($Mode -eq 'Verify') {
    $items | ForEach-Object { [pscustomobject]@{File=$_.Entry.relativePath;State=if($_.Actual -eq $_.Entry.replacementSha256){'TEST installed'}else{'Original'}} }
    return
}
# All paths, hashes and backups are checked before the first game-file mutation.
$written = @()
try {
    foreach ($item in $items) {
        $source = if ($Mode -eq 'Install') { $item.Replacement } else { $item.Original }
        $written += $item
        Copy-Item -LiteralPath $source -Destination $item.Target -Force
        $expected = if ($Mode -eq 'Install') { $item.Entry.replacementSha256 } else { $item.Entry.originalSha256 }
        if ((Get-FileHash -LiteralPath $item.Target -Algorithm SHA256).Hash -ne $expected) { throw '复制后的校验失败' }
    }
} catch {
    foreach ($item in $written) {
        $previous = if ($item.Actual -eq $item.Entry.originalSha256) { $item.Original } else { $item.Replacement }
        Copy-Item -LiteralPath $previous -Destination $item.Target -Force
    }
    throw
}
Write-Output "$Mode 成功：3 个文件；蜡烛人 [TEST] / 4 键简单 / 24 秒。"
