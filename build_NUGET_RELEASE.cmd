@echo off
cls

.paket\paket.bootstrapper.exe prerelease
if errorlevel 1 (
  exit /b %errorlevel%
)

.paket\paket.exe restore -v
if errorlevel 1 (
  exit /b %errorlevel%
)

packages\FAKE\tools\FAKE.exe build.fsx "target=Release" "NugetKey=b9eae88e-bb2d-423e-bcf6-6cde8ea1cebe" "github-user=vaskir@gmail.com"  "github-pw=vaskir2011"
