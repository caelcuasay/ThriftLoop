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
if ($Filters.Count -gt 0) { $header += "Filters: $($Filters -join ', ')`n" }
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

Write-Host "`nExtracting file contents..." -ForegroundColor Cyan

# 4. Filter and Exclude Logic
$allFiles = Get-ChildItem -Path . -Recurse -File -Force -ErrorAction SilentlyContinue |
            Where-Object {
                $normalizedPath = $_.FullName.Replace('/', '\')
                $isValidExt = $IncludeExtensions -contains $_.Extension
                $isNotSystemDir = $true
                foreach ($dir in $ExcludeFolders) {
                    if ($normalizedPath -match "\\$dir\\") { $isNotSystemDir = $false; break }
                }

                $matchesFilter = ($Filters.Count -eq 0)
                if ($Filters.Count -gt 0) {
                    foreach ($f in $Filters) {
                        if ($normalizedPath.IndexOf($f.Replace('/', '\'), [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                            $matchesFilter = $true; break
                        }
                    }
                }

                $isExcluded = $false
                if ($Excludes.Count -gt 0) {
                    foreach ($e in $Excludes) {
                        if ($normalizedPath.IndexOf($e.Replace('/', '\'), [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                            $isExcluded = $true; break
                        }
                    }
                }
                
                $isValidExt -and $isNotSystemDir -and $matchesFilter -and -not $isExcluded
            }

# 5. Append Contents (FIXED LOGIC)
$exportedCount = 0
foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($PWD.Path.Length + 1).Replace('\', '/')
    Add-Content -Path $OutputFile -Value "`n`n--- $relativePath ---`n" -Encoding UTF8
    
    $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
    
    # Check content status before writing
    if ([string]::IsNullOrWhiteSpace($content)) {
        $textToWrite = "[Empty File]"
    } else {
        $textToWrite = $content
    }

    Add-Content -Path $OutputFile -Value $textToWrite -Encoding UTF8
    $exportedCount++
}

Write-Host "Export completed! $exportedCount files exported." -ForegroundColor Green

# 6. Open in Chrome
Start-Process "chrome.exe" "file:///$($OutputFile.Replace('\','/'))"


# Commands: .\export-project.ps1 -Excludes -Filter