param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "0.1.8",
    [switch]$SelfContained,
    [switch]$FrameworkDependent,
    [switch]$SkipInstaller,
    [string]$CertificatePath,
    [string]$CertificatePassword,
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$SignToolPath,
    [string]$InnoSetupCompilerPath
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "Version must be numeric, for example 0.1.8 or 1.2.3.4."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "EdgePeek\EdgePeek.csproj"
$artifactRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactRoot "publish\EdgePeek-$Version-$Runtime"
$portableZip = Join-Path $artifactRoot "EdgePeek-$Version-$Runtime-portable.zip"
$installerPath = Join-Path $artifactRoot "EdgePeekSetup-$Version-$Runtime.exe"
$installerScript = Join-Path $repoRoot "installer\EdgePeek.iss"

function Find-SignTool {
    param([string]$RequestedPath)

    if ($RequestedPath) {
        return $RequestedPath
    }

    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path $kitsRoot) {
        $candidate = Get-ChildItem -Path $kitsRoot -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -like "*\x64\signtool.exe" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($candidate) {
            return $candidate.FullName
        }
    }

    return $null
}

function Find-InnoSetupCompiler {
    param([string]$RequestedPath)

    if ($RequestedPath) {
        return $RequestedPath
    }

    $command = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

function Invoke-CodeSign {
    param(
        [string]$TargetPath,
        [string]$ToolPath
    )

    if (!$CertificatePath) {
        Write-Warning "Skipping signing for $TargetPath because -CertificatePath was not provided."
        return
    }

    if (!$ToolPath) {
        throw "signtool.exe was not found. Install Windows SDK or pass -SignToolPath."
    }

    $arguments = @(
        "sign",
        "/fd", "SHA256",
        "/tr", $TimestampUrl,
        "/td", "SHA256",
        "/f", $CertificatePath
    )

    if ($CertificatePassword) {
        $arguments += @("/p", $CertificatePassword)
    }

    $arguments += $TargetPath
    & $ToolPath @arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Signing failed for $TargetPath."
    }

    & $ToolPath verify /pa /v $TargetPath
    if ($LASTEXITCODE -ne 0) {
        throw "Signature verification failed for $TargetPath."
    }
}

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
if (Test-Path $portableZip) {
    Remove-Item -LiteralPath $portableZip -Force
}
if (Test-Path $installerPath) {
    Remove-Item -LiteralPath $installerPath -Force
}

$selfContainedValue = if ($FrameworkDependent) { "false" } else { "true" }
$publishArguments = @(
    "publish", $projectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", $selfContainedValue,
    "-o", $publishDir,
    "-p:PublishSingleFile=true",
    "-p:Version=$Version",
    "-p:AssemblyVersion=$Version",
    "-p:FileVersion=$Version",
    "-p:InformationalVersion=$Version"
)

Write-Host "Publishing EdgePeek $Version for $Runtime..."
dotnet @publishArguments

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

Get-ChildItem -Path $publishDir -Recurse -Include *.pdb,*.xml -File | Remove-Item -Force

$signTool = Find-SignTool -RequestedPath $SignToolPath
$exePath = Join-Path $publishDir "EdgePeek.exe"
Invoke-CodeSign -TargetPath $exePath -ToolPath $signTool

Write-Host "Creating portable zip..."
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portableZip -Force

if (!$SkipInstaller) {
    $innoCompiler = Find-InnoSetupCompiler -RequestedPath $InnoSetupCompilerPath
    if ($innoCompiler) {
        Write-Host "Building installer with Inno Setup..."
        & $innoCompiler `
            "/DAppVersion=$Version" `
            "/DSourceDir=$publishDir" `
            "/DOutputDir=$artifactRoot" `
            $installerScript

        if ($LASTEXITCODE -ne 0) {
            throw "Inno Setup build failed."
        }

        Invoke-CodeSign -TargetPath $installerPath -ToolPath $signTool
    }
    else {
        Write-Warning "Inno Setup compiler was not found. Install Inno Setup 6 or pass -InnoSetupCompilerPath to build the installer."
    }
}

Write-Host ""
Write-Host "Release artifacts:"
Write-Host "  Portable zip: $portableZip"
if (Test-Path $installerPath) {
    Write-Host "  Installer:     $installerPath"
}
