$files = Get-ChildItem -Path "src/Match3.Core.Tests" -Recurse -Filter "*.cs"
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $original = $content
    
    # Regex to match new StandardMatchProcessor(arg1, arg2)
    # Allows for whitespace and potentially complex args
    $pattern = 'new StandardMatchProcessor\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)'
    $replacement = 'new StandardMatchProcessor($1, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), $2)'
    
    $content = [Regex]::Replace($content, $pattern, $replacement)
    
    if ($content -ne $original) {
        Set-Content $file.FullName $content -Encoding UTF8
        Write-Host "Updated $($file.Name)"
    }
}
