param(
    [Parameter(Mandatory = $true)]
    [string]$Tag
)

$ErrorActionPreference = "Stop"

# -----------------------------
# Config
# -----------------------------
$EditorProjectPath  = ".\GcaEditor\GcaEditor.csproj"
$UpdaterProjectPath = ".\GcaUpdater\GcaUpdater.csproj"

$EditorPublishDir   = ".\GcaEditor\bin\Release\net8.0-windows\publish\win-x64"
$UpdaterOutDir      = ".\.artifacts\updater-publish"

$ArtifactsDir       = ".\.artifacts"
$ZipName            = "GcaEditor_$Tag" + "_win-x64.zip"
$ZipPath            = Join-Path $ArtifactsDir $ZipName
$ReleaseNotes       = ".\RELEASE_NOTES.md"

$UpdaterExeName     = "Updater.exe"
$UpdaterExePath     = Join-Path $UpdaterOutDir $UpdaterExeName
$EditorUpdaterPath  = Join-Path $EditorPublishDir $UpdaterExeName

# -----------------------------
# Helpers
# -----------------------------
function Fail($msg) {
    Write-Host ""
    Write-Host "ERROR: $msg" -ForegroundColor Red
    exit 1
}

function Ensure-Command($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        Fail "$name is not installed or not in PATH."
    }
}

function Write-Step($msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

function Remove-IfExists($path) {
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
    }
}

# -----------------------------
# Start
# -----------------------------
Write-Host ""
Write-Host "GcaEditor release script" -ForegroundColor Green
Write-Host "Tag: $Tag" -ForegroundColor Yellow

# -----------------------------
# Checks
# -----------------------------
Write-Step "Checking required tools"
Ensure-Command git
Ensure-Command dotnet
Ensure-Command gh

Write-Step "Checking GitHub CLI authentication"
gh auth status | Out-Null
if ($LASTEXITCODE -ne 0) {
    Fail "GitHub CLI is not authenticated.`nRun: gh auth login"
}

Write-Step "Checking git repository"
git rev-parse --is-inside-work-tree | Out-Null
if ($LASTEXITCODE -ne 0) {
    Fail "Current folder is not inside a git repository."
}

Write-Step "Checking working tree"
$gitStatus = git status --porcelain
if ($gitStatus) {
    Fail "Working tree is not clean.`nCommit or stash changes before running the release script."
}

Write-Step "Checking project files"
if (-not (Test-Path $EditorProjectPath)) {
    Fail "Editor project not found: $EditorProjectPath"
}
if (-not (Test-Path $UpdaterProjectPath)) {
    Fail "Updater project not found: $UpdaterProjectPath"
}

Write-Step "Checking release notes"
if (-not (Test-Path $ReleaseNotes)) {
    Fail "Release notes file not found: $ReleaseNotes"
}
$notesContent = Get-Content $ReleaseNotes -Raw
if ([string]::IsNullOrWhiteSpace($notesContent)) {
    Fail "RELEASE_NOTES.md is empty."
}

Write-Step "Checking that tag does not already exist"
$localTag = git tag --list $Tag
if ($localTag) {
    Fail "Tag '$Tag' already exists locally."
}
$remoteTag = git ls-remote --tags origin "refs/tags/$Tag"
if ($remoteTag) {
    Fail "Tag '$Tag' already exists on remote."
}

# -----------------------------
# Create and push tag
# -----------------------------
Write-Step "Creating annotated tag"
git tag -a $Tag -m "Release $Tag"
if ($LASTEXITCODE -ne 0) {
    Fail "Failed to create git tag."
}

Write-Step "Pushing tag"
git push origin $Tag
if ($LASTEXITCODE -ne 0) {
    Fail "Failed to push tag."
}

# -----------------------------
# Prepare artifacts folder
# -----------------------------
Write-Step "Preparing artifacts folder"
if (-not (Test-Path $ArtifactsDir)) {
    New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null
    (Get-Item $ArtifactsDir).Attributes += 'Hidden'
}

Remove-IfExists $ZipPath
Remove-IfExists $UpdaterOutDir
Remove-IfExists $EditorPublishDir

# -----------------------------
# Publish GcaEditor
# -----------------------------
Write-Step "Publishing GcaEditor"
dotnet publish $EditorProjectPath -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) {
    Fail "dotnet publish failed for GcaEditor."
}

if (-not (Test-Path $EditorPublishDir)) {
    Fail "Editor publish directory not found: $EditorPublishDir"
}

Write-Step "Writing git-tag.txt"
Set-Content -Path (Join-Path $EditorPublishDir "git-tag.txt") -Value $Tag -NoNewLine

# -----------------------------
# Publish GcaUpdater (single file)
# -----------------------------
Write-Step "Publishing GcaUpdater"
dotnet publish $UpdaterProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $UpdaterOutDir

if ($LASTEXITCODE -ne 0) {
    Fail "dotnet publish failed for GcaUpdater."
}

if (-not (Test-Path $UpdaterExePath)) {
    Fail "Updater executable not found: $UpdaterExePath"
}

# -----------------------------
# Inject Updater.exe into GcaEditor publish
# -----------------------------
Write-Step "Copying Updater.exe into GcaEditor publish folder"
Copy-Item $UpdaterExePath $EditorUpdaterPath -Force

if (-not (Test-Path $EditorUpdaterPath)) {
    Fail "Failed to copy Updater.exe into editor publish folder."
}

# -----------------------------
# Zip
# -----------------------------
Write-Step "Creating zip"
Compress-Archive -Path "$EditorPublishDir\*" -DestinationPath $ZipPath

if (-not (Test-Path $ZipPath)) {
    Fail "Zip was not created: $ZipPath"
}

# -----------------------------
# GitHub Release
# -----------------------------
Write-Step "Creating GitHub release"
gh release create $Tag $ZipPath `
    --title "GcaEditor $Tag" `
    --notes-file $ReleaseNotes `
    --latest

if ($LASTEXITCODE -ne 0) {
    Fail "Failed to create GitHub release."
}

# -----------------------------
# Cleanup
# -----------------------------
Write-Step "Deleting local zip"
Remove-IfExists $ZipPath

Write-Step "Deleting updater publish temp folder"
Remove-IfExists $UpdaterOutDir

Write-Step "Resetting RELEASE_NOTES.md"
@"
## Added

## Improved

## Fixed

## Cleanup
"@ | Set-Content $ReleaseNotes

Write-Host ""
Write-Host "Release completed successfully." -ForegroundColor Green
Write-Host "Tag: $Tag"
Write-Host "Zip content: GcaEditor publish + Updater.exe + git-tag.txt"
Write-Host "Release notes have been reset."