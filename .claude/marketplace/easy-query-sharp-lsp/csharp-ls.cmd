@echo off
rem Starts csharp-ls for the repository passed as %1, speaking LSP over stdio.
rem The .NET 10 SDK lives in %USERPROFILE%\.dotnet (the dotnet on PATH is an 11 preview that global.json rejects),
rem so both the tool host and MSBuild must resolve that SDK. Nothing but LSP traffic may reach stdout.
setlocal
set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
cd /d "%~1" || exit /b 1
dotnet tool restore >nul 2>&1
dotnet tool run csharp-ls -- --solution EasyQuerySharp.slnx
