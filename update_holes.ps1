$files = @("src/Match3.Core.Tests/Systems/Physics/HoleGravityTests.cs", "src/Match3.Core.Tests/Systems/Physics/RealtimeRefillSystemTests.cs")
foreach ($file in $files) {
    $path = Join-Path (Get-Location) $file
    $content = Get-Content $path -Raw -Encoding UTF8
    # Replace assignment
    $content = [Regex]::Replace($content, 'state\.Holes\[([^\]]+)\] = true', 'state.Cells[$1] = CellKind.Void')
    # Replace snapshot check
    $content = [Regex]::Replace($content, 'snapshot\.Holes\[([^\]]+)\]', 'snapshot.Cells[$1] == CellKind.Void')
    $content = $content -replace 'snapshot.Holes.Length', 'snapshot.Cells.Length'
    Set-Content $path $content -Encoding UTF8
    Write-Host "Updated $file"
}
