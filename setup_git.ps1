# setup_git.ps1
# This script initializes the git repository and makes the initial commit.

Write-Host "Initializing Git repository..." -ForegroundColor Cyan
git init

Write-Host "Adding files..." -ForegroundColor Cyan
git add .

Write-Host "Making initial commit..." -ForegroundColor Cyan
git commit -m "Initial commit from Antigravity AI"

Write-Host ""
Write-Host "SUCCESS: Your project is now a local Git repository." -ForegroundColor Green
Write-Host ""
Write-Host "TO PUSH TO GITHUB:" -ForegroundColor Yellow
Write-Host "1. Create a NEW repository on GitHub (e.g., https://github.com/USER/NixTraceability.git)"
Write-Host "2. Copy the URL and run the following commands:"
Write-Host "   git remote add origin YOUR_REPOSITORY_URL"
Write-Host "   git branch -M main"
Write-Host "   git push -u origin main"
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
