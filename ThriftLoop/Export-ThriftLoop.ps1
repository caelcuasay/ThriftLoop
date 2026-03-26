<#
.SYNOPSIS
    ThriftLoop Feature‑Aware Exporter – updated with FeatureList functionality.

.DESCRIPTION
    Exports only the files relevant to a given feature by tracing outward dependencies.

.PARAMETER Feature
    Smart feature filter for full export.

.PARAMETER FeatureList
    NEW: Instead of exporting, lists all files that WOULD be exported for the specified 
    feature(s) directly in the console. Respects -Excludes.

.PARAMETER Filters
    Legacy raw keyword filter.

.PARAMETER Excludes
    Exclude files whose relative path contains any of these strings.

.PARAMETER ListFeatures
    Scan Controllers and list all auto‑detected feature names.

.PARAMETER NoOpen
    Skip opening the output file in Chrome after export.

.PARAMETER CleanComments
    Replace Unicode box‑drawing characters with hyphens.

.EXAMPLE
    .\Export-ThriftLoop.ps1 -FeatureList 'Order'
    
    .\Export-ThriftLoop.ps1 -FeatureList 'Order' -Excludes '.css'
#>

param(
    [string[]]$Feature  = @(),
    [string[]]$Filters  = @(),
    [string[]]$Excludes = @(),
    [string[]]$FeatureList = @(), # New Parameter
    [switch]$ListFeatures,
    [switch]$NoOpen,
    [switch]$CleanComments
)

# Configuration
$ExcludeFolders    = @('bin','obj','.git','.vs','node_modules','lib','Migrations','uploads','Properties')
$IncludeExtensions = @('.cs','.cshtml','.json','.html','.css','.js')
$OutputFile        = "$PWD\Project_Export.txt"
$AlwaysInclude     = @(
    'Program.cs',
    'ApplicationDbContext.cs',
    'BaseController.cs',
    '_Layout.cshtml',
    '_Navbar.cshtml',
    '_ViewImports.cshtml',
    '_ViewStart.cshtml'
)

# Helper: relative path
function Get-RelativePath([string]$fullPath) {
    $fullPath.Substring($PWD.Path.Length + 1).Replace('\', '/')
}

# Helper: exclude folders test
function Test-ExcludedPath([string]$relativePath) {
    foreach ($dir in $ExcludeFolders) {
        if ($relativePath -match "(^|/)$([regex]::Escape($dir))(/|$)") { return $true }
    }
    return $false
}

# Get all project files
function Get-AllProjectFiles {
    Get-ChildItem -Path . -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object {
            ($IncludeExtensions -contains $_.Extension) -and
            (-not (Test-ExcludedPath (Get-RelativePath $_.FullName)))
        }
}

# Feature detection from controller names
function Get-DetectedFeatures {
    Get-ChildItem -Recurse -Filter '*Controller.cs' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne 'BaseController.cs' } |
        ForEach-Object { $_.BaseName -replace 'Controller$', '' } |
        Sort-Object -Unique
}

# Feature resolver – outward dependency trace
function Resolve-FeatureFiles {
    param([string[]]$FeatureNames)

    $allFiles   = Get-AllProjectFiles
    $collected  = [System.Collections.Generic.HashSet[string]]::new(
                      [System.StringComparer]::OrdinalIgnoreCase)

    foreach ($featureName in $FeatureNames) {
        $escaped = [regex]::Escape($featureName)

        $anchors = $allFiles | Where-Object { $_.Name -match "(?i)$escaped" }
        $viewFiles = $allFiles | Where-Object {
            (Get-RelativePath $_.FullName) -match "(?i)Views/$escaped/"
        }
        $anchorSet = @($anchors) + @($viewFiles) | Sort-Object FullName -Unique

        foreach ($a in $anchorSet) { [void]$collected.Add($a.FullName) }

        $referencedTypes = [System.Collections.Generic.HashSet[string]]::new(
                               [System.StringComparer]::OrdinalIgnoreCase)

        foreach ($anchor in $anchorSet) {
            $content = Get-Content $anchor.FullName -Raw -ErrorAction SilentlyContinue
            if (-not $content) { continue }

            $suffixPattern = '\b(I?[A-Z][a-zA-Z0-9]+(?:Service|Repository|Dto|DTO|ViewModel|ViewModels|Context|DbContext)s?)\b'
            foreach ($m in [regex]::Matches($content, $suffixPattern)) {
                [void]$referencedTypes.Add($m.Value)
            }

            $paramPattern = '(?<!\w)([A-Z][a-z][a-zA-Z0-9]+)\s+[a-z_][a-zA-Z0-9_]*\s*[,\)\{]'
            foreach ($m in [regex]::Matches($content, $paramPattern)) {
                [void]$referencedTypes.Add($m.Groups[1].Value)
            }

            $genericPattern = '<\s*([A-Z][a-zA-Z0-9]+)\s*>'
            foreach ($m in [regex]::Matches($content, $genericPattern)) {
                [void]$referencedTypes.Add($m.Groups[1].Value)
            }
        }

        foreach ($typeName in $referencedTypes) {
            foreach ($file in $allFiles) {
                $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
                $cleanBase = $baseName -replace '\.cshtml$', ''
                if ($cleanBase -ieq $typeName -or $baseName -ieq $typeName) {
                    [void]$collected.Add($file.FullName)
                }
            }
        }
    }

    foreach ($infraName in $AlwaysInclude) {
        $match = $allFiles | Where-Object { $_.Name -ieq $infraName } | Select-Object -First 1
        if ($match) { [void]$collected.Add($match.FullName) }
    }

    $allFiles | Where-Object { $collected.Contains($_.FullName) }
}

# ---------- LIST FEATURES ----------
if ($ListFeatures) {
    $detected = Get-DetectedFeatures
    Write-Host ''
    Write-Host '  Detected Features (from Controllers)' -ForegroundColor Cyan
    Write-Host '  --------------------------------------------------' -ForegroundColor DarkGray
    foreach ($f in $detected) {
        Write-Host ('    -Feature ' + $f) -ForegroundColor Yellow
    }
    Write-Host ''
    exit
}

# ---------- NEW: FEATURE LIST ONLY MODE ----------
if ($FeatureList.Count -gt 0) {
    Write-Host ''
    Write-Host "Listing files for Feature(s): $($FeatureList -join ', ')" -ForegroundColor Cyan
    $filesToList = @(Resolve-FeatureFiles -FeatureNames $FeatureList)
    
    # Apply excludes to the list
    if ($Excludes.Count -gt 0) {
        $filesToList = @(
            $filesToList | Where-Object {
                $rel  = Get-RelativePath $_.FullName
                $fail = $false
                foreach ($e in $Excludes) {
                    if ($rel.IndexOf($e.Replace('\','/'), [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                        $fail = $true; break
                    }
                }
                -not $fail
            }
        )
    }

    if ($filesToList.Count -eq 0) {
        Write-Host "No files found for the specified feature(s)." -ForegroundColor Yellow
    } else {
        foreach ($file in $filesToList) {
            Write-Host ("  > " + (Get-RelativePath $file.FullName)) -ForegroundColor Green
        }
        Write-Host "`nTotal: $($filesToList.Count) file(s)." -ForegroundColor DarkGray
    }
    exit
}

# ---------- RESOLVE FILES FOR EXPORT ----------
$resolvedFiles = @()
$modeLabel     = ''

if ($Feature.Count -gt 0) {
    $modeLabel = 'Feature (Smart Trace): ' + ($Feature -join ', ')
    $resolvedFiles = @(Resolve-FeatureFiles -FeatureNames $Feature)
    Write-Host ''
    Write-Host ('Mode: Feature Trace — ' + ($Feature -join ', ')) -ForegroundColor Cyan
} elseif ($Filters.Count -gt 0) {
    $modeLabel = 'Filter (Path or Content): ' + ($Filters -join ', ')
    Write-Host ''
    Write-Host ('Mode: Keyword Filter — ' + ($Filters -join ', ')) -ForegroundColor Cyan
    $allFiles  = Get-AllProjectFiles
    $resolvedFiles = @(
        $allFiles | Where-Object {
            $rel     = Get-RelativePath $_.FullName
            $content = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
            $pass    = $false
            foreach ($f in $Filters) {
                $inPath    = $rel.IndexOf($f, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
                $inContent = $content -and ($content.IndexOf($f, [System.StringComparison]::OrdinalIgnoreCase) -ge 0)
                if ($inPath -or $inContent) { $pass = $true; break }
            }
            $pass
        }
    )
} else {
    $modeLabel = 'Full Export (no filter)'
    Write-Host ''
    Write-Host 'Mode: Full Export' -ForegroundColor Cyan
    $resolvedFiles = @(Get-AllProjectFiles)
}

# Apply excludes for Export
if ($Excludes.Count -gt 0) {
    $resolvedFiles = @(
        $resolvedFiles | Where-Object {
            $rel  = Get-RelativePath $_.FullName
            $fail = $false
            foreach ($e in $Excludes) {
                if ($rel.IndexOf($e.Replace('\','/'), [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $fail = $true; break
                }
            }
            -not $fail
        }
    )
}

# ---------- WRITE OUTPUT ----------
$projectName = Split-Path $PWD -Leaf

Set-Content -Path $OutputFile -Value ('Project: ' + $PWD) -Encoding UTF8
Add-Content -Path $OutputFile -Value ($projectName + ' Export') -Encoding UTF8
Add-Content -Path $OutputFile -Value ('Mode: ' + $modeLabel) -Encoding UTF8
if ($Excludes.Count -gt 0) {
    Add-Content -Path $OutputFile -Value ('Exclude: ' + ($Excludes -join ', ')) -Encoding UTF8
}
Add-Content -Path $OutputFile -Value '' -Encoding UTF8
Add-Content -Path $OutputFile -Value '=========================================' -Encoding UTF8

function Export-FilteredTree {
    param(
        [System.Collections.Generic.HashSet[string]]$IncludedSet,
        [string]$CurrentPath = $PWD.Path,
        [string]$Indent = ''
    )

    $items = Get-ChildItem -Path $CurrentPath -Force -ErrorAction SilentlyContinue |
             Where-Object { $ExcludeFolders -notcontains $_.Name -and -not $_.Name.StartsWith('.') } |
             Sort-Object -Property @{Expression={$_.PSIsContainer}; Descending=$true}, Name

    $relevantItems = $items | Where-Object {
        if ($_.PSIsContainer) {
            $fp = $_.FullName
            foreach ($p in $IncludedSet) {
                if ($p.StartsWith($fp + '\', [System.StringComparison]::OrdinalIgnoreCase) -or
                    $p.StartsWith($fp + '/', [System.StringComparison]::OrdinalIgnoreCase)) {
                    return $true
                }
            }
            return $false
        } else {
            return $IncludedSet.Contains($_.FullName)
        }
    }

    $count = @($relevantItems).Count
    for ($i = 0; $i -lt $count; $i++) {
        $item   = $relevantItems[$i]
        $isLast = ($i -eq $count - 1)
        $branch = if ($isLast) { '+-- ' } else { '|-- ' }

        Add-Content -Path $OutputFile -Value ($Indent + $branch + $item.Name) -Encoding UTF8

        if ($item.PSIsContainer) {
            $ext = if ($isLast) { '    ' } else { '|   ' }
            Export-FilteredTree -IncludedSet $IncludedSet -CurrentPath $item.FullName -Indent ($Indent + $ext)
        }
    }
}

Write-Host 'Generating filtered tree...' -ForegroundColor DarkGray
$includedSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($f in $resolvedFiles) { [void]$includedSet.Add($f.FullName) }

Export-FilteredTree -IncludedSet $includedSet

Add-Content -Path $OutputFile -Value '' -Encoding UTF8
Add-Content -Path $OutputFile -Value '=========================================' -Encoding UTF8
Add-Content -Path $OutputFile -Value 'FILE CONTENTS' -Encoding UTF8
Add-Content -Path $OutputFile -Value '=========================================' -Encoding UTF8

foreach ($file in $resolvedFiles) {
    $rel     = Get-RelativePath $file.FullName
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $body    = if ([string]::IsNullOrWhiteSpace($content)) { '[Empty File]' } else { $content }

    if ($CleanComments) {
        $body = $body -replace '[\u2500-\u257F]', '-'
    }

    Add-Content -Path $OutputFile -Value '' -Encoding UTF8
    Add-Content -Path $OutputFile -Value ('--- ' + $rel + ' ---') -Encoding UTF8
    Add-Content -Path $OutputFile -Value $body -Encoding UTF8
}

Write-Host ('Export complete — ' + $resolvedFiles.Count + ' file(s) exported.') -ForegroundColor Green
Write-Host ('Output : ' + $OutputFile) -ForegroundColor DarkGray

if (-not $NoOpen) {
    $uri = [System.Uri]::new($OutputFile).AbsoluteUri
    Start-Process 'chrome.exe' $uri
}