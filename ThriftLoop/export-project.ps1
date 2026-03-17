# Generates a tree and dumps code contents of FILTERED files into a text file, then opens in Chrome
param(
    [string[]]$Filters = @(),
    [string[]]$Excludes = @()
)

$ExcludeFolders = @('bin', 'obj', '.git', '.vs', 'node_modules', 'lib', 'Migrations', 'uploads', 'Properties')
$IncludeExtensions = @('.cs', '.cshtml', '.json', '.html', '.css', '.js') 

$OutputFile = "$PWD\Project_Export.txt"

# 1. Initialize file and write the header
$header = "Project Export`n"
if ($Filters.Count -gt 0) { $header += "Filters (Path or Content): $($Filters -join ', ')`n" }
if ($Excludes.Count -gt 0) { $header += "Excludes: $($Excludes -join ', ')`n" }

Set-Content -Path $OutputFile -Value "Project Structure: $PWD`n$header`n=========================================`n" -Encoding UTF8

# 2. Function to build the tree
function Export-Tree {
    param([string]$Path = '.', [string]$Indent = "")
    
    $items = Get-ChildItem -Path $Path -Force -ErrorAction SilentlyContinue | 
             Where-Object { $ExcludeFolders -notcontains $_.Name -and -not $_.Name.StartsWith('.') } |
             Sort-Object -Property @{e={$_.PSIsContainer}; Descending=$true}, Name
    
    $count = $items.Count
    for ($i = 0; $i -lt $count; $i++) {
        $item = $items[$i]
        $isLast = ($i -eq $count - 1)
        $branch = if ($isLast) { "+-- " } else { "|-- " }
        
        $line = "$Indent$branch$($item.Name)"
        Add-Content -Path $OutputFile -Value $line -Encoding UTF8
        
        if ($item.PSIsContainer) {
            $extension = if ($isLast) { "    " } else { "|   " }
            Export-Tree -Path $item.FullName -Indent ($Indent + $extension)
        }
    }
}

Write-Host "`nGenerating Tree..." -ForegroundColor Cyan
Export-Tree

# 3. Append Content Header
Add-Content -Path $OutputFile -Value "`n`n=========================================`nFILE CONTENTS`n=========================================`n" -Encoding UTF8

Write-Host "`nExtracting file contents (Scanning for keywords)..." -ForegroundColor Cyan

# 4. Deep Filter Logic (Path + Content Search)
$allFiles = Get-ChildItem -Path . -Recurse -File -Force -ErrorAction SilentlyContinue |
            Where-Object {
                $relativePath = $_.FullName.Substring($PWD.Path.Length + 1).Replace('\', '/')
                
                # Basic validation
                $isValidExt = $IncludeExtensions -contains $_.Extension
                $isSystemDir = $false
                foreach ($dir in $ExcludeFolders) {
                    if ($relativePath -match "^$dir/" -or $relativePath -match "/$dir/") { $isSystemDir = $true; break }
                }

                if (-not $isValidExt -or $isSystemDir) { return $false }

                # Load content for deep scanning
                $fileContent = Get-Content -Path $_.FullName -Raw -ErrorAction SilentlyContinue

                # A. Filter Logic (Checks Path OR File Content)
                $passFilter = ($Filters.Count -eq 0)
                if ($Filters.Count -gt 0) {
                    foreach ($f in $Filters) {
                        $fNorm = $f.Replace('\', '/')
                        # Check if keyword is in the Path
                        $inPath = $relativePath.IndexOf($fNorm, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
                        # Check if keyword is inside the file content
                        $inContent = $false
                        if ($fileContent) {
                            $inContent = $fileContent.IndexOf($f, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
                        }

                        if ($inPath -or $inContent) { $passFilter = $true; break }
                    }
                }

                # B. Exclude Logic
                $failExclude = $false
                if ($Excludes.Count -gt 0) {
                    foreach ($e in $Excludes) {
                        if ($relativePath.IndexOf($e.Replace('\', '/'), [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { 
                            $failExclude = $true; break 
                        }
                    }
                }

                $passFilter -and -not $failExclude
            }

# 5. Append Contents
$exportedCount = 0
foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($PWD.Path.Length + 1).Replace('\', '/')
    Add-Content -Path $OutputFile -Value "`n`n--- $relativePath ---`n" -Encoding UTF8
    
    $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
    $textToWrite = if ([string]::IsNullOrWhiteSpace($content)) { "[Empty File]" } else { $content }

    Add-Content -Path $OutputFile -Value $textToWrite -Encoding UTF8
    $exportedCount++
}

Write-Host "Export completed! $exportedCount files mentioned your keywords." -ForegroundColor Green

# 6. Open in Chrome
Start-Process "chrome.exe" "file:///$($OutputFile.Replace('\','/'))"