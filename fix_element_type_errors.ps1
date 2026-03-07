$files = Get-ChildItem -Path "src/Match3.Core.Tests" -Recurse -Filter "*.cs"
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $original = $content
    
    $content = $content -replace "ElementType\.Normal", "ElementType.Item1"
    $content = $content -replace "ElementType\.Bomb", "ElementType.Item1"
    
    if ($content -ne $original) {
        Set-Content $file.FullName $content -Encoding UTF8
        Write-Host "Updated $($file.Name)"
    }
}
