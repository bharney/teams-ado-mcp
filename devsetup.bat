@echo off
REM Teams-ADO MCP Development Setup CLI
REM This script provides a convenient way to run the development setup CLI

cd /d "%~dp0"
dotnet run --project DevSetupCli -- %*
