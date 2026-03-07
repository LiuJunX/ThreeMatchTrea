$files = Get-ChildItem -Path "src/Match3.Core.Tests" -Recurse -Filter "*.cs"
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $original = $content
    
    $content = $content -replace "TileType\.Normal", "ElementType.Item1"
    $content = $content -replace "TileType\.Bomb", "ElementType.Item1"
    $content = $content -replace "ElementTypesCount", "TileTypesCount"
    # Be careful with ElementTypes replacement, only if it's a property of Snapshot
    $content = $content -replace "snapshot\.ElementTypes", "snapshot.TileTypes"
    
    if ($content -ne $original) {
        Set-Content $file.FullName $content -Encoding UTF8
        Write-Host "Updated $($file.Name)"
    }
}
