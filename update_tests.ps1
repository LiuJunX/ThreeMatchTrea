$files = Get-ChildItem -Path "src/Match3.Core.Tests" -Recurse -Filter "*.cs"
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    if ($content -match "TileType") {
        $content = $content -replace "TileType\.Red", "ElementType.Item1"
        $content = $content -replace "TileType\.Green", "ElementType.Item2"
        $content = $content -replace "TileType\.Blue", "ElementType.Item3"
        $content = $content -replace "TileType\.Yellow", "ElementType.Item4"
        $content = $content -replace "TileType\.Purple", "ElementType.Item5"
        $content = $content -replace "TileType\.Orange", "ElementType.Item6"
        $content = $content -replace "TileType\.Rainbow", "ElementType.Universal"
        $content = $content -replace "TileType\.None", "ElementType.None"
        $content = $content -replace "TileType\.Hole", "ElementType.None"
        $content = $content -replace "TileType\.Wall", "ElementType.None"
        $content = $content -replace "TileType", "ElementType"
        Set-Content $file.FullName $content -Encoding UTF8
        Write-Host "Updated $($file.Name)"
    }
}
