# SyncChanges.ps1
# This script stages all changes, commits them with a descriptive message, and pushes to GitHub.

$commitMessage = "Update NixTraceability: Improved calibration region selection and OCR validation flow"

Write-Host "--- Git Sync Started ---" -ForegroundColor Cyan

# 1. Add all changes
Write-Host "Staging changes..." -ForegroundColor Yellow
git add .

# 2. Commit changes
Write-Host "Committing changes..." -ForegroundColor Yellow
git commit -m "$commitMessage"

# 3. Push to main
Write-Host "Pushing to GitHub (origin main)..." -ForegroundColor Yellow
git push origin main

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nSUCCESS: Your changes have been pushed to GitHub!" -ForegroundColor Green
} else {
    Write-Host "`nERROR: Git sync failed. Please check the error messages above." -ForegroundColor Red
}

Write-Host "`nPress any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
