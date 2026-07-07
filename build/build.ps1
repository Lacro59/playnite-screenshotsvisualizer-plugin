param(
    [string]$ConfigurationName, 
    [string]$OutDir,
    [string]$SolutionDir 
)

function Get-RepoRelativePath {
    param(
        [string]$Path,
        [string]$SolutionDir
    )

    $repoRoot = (Resolve-Path (Join-Path $SolutionDir "..")).Path
    $resolved = (Resolve-Path $Path).Path

    if ($resolved.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
        return $resolved.Substring($repoRoot.Length).TrimStart('\')
    }

    return $resolved
}

function Write-PackagedArtifact {
    param(
        [string]$Label,
        [string]$Path,
        [string]$SolutionDir
    )

    $displayPath = Get-RepoRelativePath -Path $Path -SolutionDir $SolutionDir
    Write-Host ("  {0,-5} {1}" -f $Label, $displayPath)
}

$PlaynitePaths = @(
    "C:\Playnite_dev", "C:\Projects\Playnite_dev",
    "D:\Playnite_dev", "D:\Projects\Playnite_dev",
    "G:\Playnite_dev", "G:\Projects\Playnite_dev",
    "F:\Playnite_dev", "F:\Projects\Playnite_dev"
)

$PlaynitePath = $null

foreach ($path in $PlaynitePaths) {
    if (Test-Path -Path $path) {
        $PlaynitePath = $path
        break
    }
}

if ($null -eq $PlaynitePath) {
    Write-Host "No Playnite path valid found"
} 
else {
    $ToolboxPath = (Join-Path $PlaynitePath "toolbox.exe")
    $OutDirPath = (Join-Path $OutDir "..")

    if ($ConfigurationName -eq "debug-release") {
		if (Test-Path $ToolboxPath) {
			$string = & $ToolboxPath "pack" $OutDir $OutDirPath

            if ($string -match '"([^"]+\.pext)"') {
                $fullPath = (Resolve-Path $Matches[1]).Path
                $fileName = Split-Path -Path $fullPath -Leaf
                $fileNameWithoutExt = [System.IO.Path]::GetFileNameWithoutExtension($fileName)
                
                $zipPath = Join-Path $OutDirPath ($fileNameWithoutExt + ".zip")
                if (Test-Path $zipPath) {
                    Remove-Item $zipPath -Force
                }
                Compress-Archive -Path $fullPath -DestinationPath $zipPath

                Write-Host "  Packaging"
                Write-PackagedArtifact -Label "PEXT" -Path $fullPath -SolutionDir $SolutionDir
                Write-PackagedArtifact -Label "ZIP" -Path $zipPath -SolutionDir $SolutionDir
            }
            else {
                Write-Host "Packaging failed: $string"
            }
		} 
		else {
			Write-Host "toolbox.exe not found."
		}		
	}

    if ($ConfigurationName -eq "release") {
        $Version = ""

        foreach ($Line in Get-Content (Join-Path $SolutionDir "extension.yaml")) {
            if ($Line -imatch "Version:") {
                $Version = $Line
            }
        }

        $Manifest = (Join-Path $SolutionDir "..\manifest\")
        $YmlFile = Get-ChildItem -Path $Manifest -Filter *.yaml | Select-Object -First 1
        $Manifest = (Join-Path $Manifest $YmlFile.Name)

        $Result = Get-Content $Manifest

        if ($Result -imatch $Version) {
            if (Test-Path $ToolboxPath) {
                $packResult = & $ToolboxPath "pack" $OutDir $OutDirPath

                if ($packResult -match '"([^"]+\.pext)"') {
                    Write-Host "  Packaging"
                    Write-PackagedArtifact -Label "PEXT" -Path $Matches[1] -SolutionDir $SolutionDir
                }
                else {
                    Write-Host "Packaging failed: $packResult"
                }

                $verifyResult = & $ToolboxPath "verify" "installer" $Manifest
                if ($verifyResult -imatch "Installer manifest passed verification") {
                    Write-Host "  Manifest OK"
                } else {
                    Write-Host $verifyResult
                }
            } 
			else {
                Write-Host "toolbox.exe not found."
            }
        } 
		else {
            Write-Host "Manifest does not contain the actual version"
        }
    }
}