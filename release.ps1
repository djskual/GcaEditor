param(
    [Parameter(Mandatory = $true)]
    [string]$Tag
)

$ErrorActionPreference = "Stop"

# -----------------------------
# Config
# -----------------------------
$ProjectPath  = ".\GcaEditor\GcaEditor.csproj"
$PublishDir   = ".\GcaEditor\bin\Release\net8.0-windows\win-x64\publish"
$ArtifactsDir = ".\.artifacts"
$ZipName      = "GcaEditor_$Tag" + "_win-x64.zip"
$ZipPath      = Join-Path $ArtifactsDir $ZipName
$ReleaseNotes = ".\RELEASE_NOTES.md"

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
    Fail "GitHub CLI is not authenticated. Run: gh auth login"
}

Write-Step "Checking git repository"
git rev-parse --is-inside-work-tree | Out-Null
if ($LASTEXITCODE -ne 0) {
    Fail "Current folder is not inside a git repository."
}

Write-Step "Checking working tree"
$gitStatus = git status --porcelain
if ($gitStatus) {
    Fail "Working tree is not clean. Commit or stash changes before running the release script."
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
if(-not (Test-Path $ArtifactsDir)) {
    New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null
    (Get-Item $ArtifactsDir).Attributes += 'Hidden'
}

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

# -----------------------------
# Publish
# -----------------------------
Write-Step "Publishing application"
dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) {
    Fail "dotnet publish failed."
}

if (-not (Test-Path $PublishDir)) {
    Fail "Publish directory not found: $PublishDir"
}

Write-Step "Writing git-tag.txt"
Set-Content -Path (Join-Path $PublishDir "git-tag.txt") -Value $Tag -NoNewLine

# -----------------------------
# Zip
# -----------------------------
Write-Step "Creating zip"
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath
if (-not (Test-Path $ZipPath)) {
    Fail "Zip was not created: $ZipPath"
}

# -----------------------------
# GitHub Release
# -----------------------------
Write-Step "Creating GitHub release"
gh release create $Tag $ZipPath --title "GcaEditor $Tag" --notes-file $ReleaseNotes
if ($LASTEXITCODE -ne 0) {
    Fail "Failed to create GitHub release."
}

# -----------------------------
# Cleanup
# -----------------------------
Write-Step "Deleting local zip"
Remove-Item $ZipPath -Force

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
Write-Host "Release notes have been reset."