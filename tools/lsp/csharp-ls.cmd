@echo off
rem Starts csharp-ls (pinned in dotnet-tools.json) for the repository passed as %1, speaking LSP over stdio.
rem Nothing but LSP traffic may reach stdout.
rem - The .NET 10 SDK lives in %USERPROFILE%\.dotnet (the dotnet on PATH is an 11 preview that global.json rejects),
rem   so both the tool host and MSBuild must resolve that SDK.
rem - Configuration=Lsp: the language server evaluates projects as if built in a configuration nobody builds, so it
rem   never loads (and locks) bin\Debug\EasyQuerySharp.Generator.dll, which the test projects use as an analyzer.
setlocal
set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "Configuration=Lsp"
cd /d "%~1" || exit /b 1
dotnet tool restore >nul 2>&1
dotnet tool run csharp-ls -- --solution EasyQuerySharp.slnx
