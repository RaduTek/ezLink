@echo off
rem Builds ezLink.sln with the Visual Studio MSBuild (same toolchain as EasyMusicSync).
rem Usage: build.cmd [Debug|Release]
setlocal
set CONFIG=%1
if "%CONFIG%"=="" set CONFIG=Debug

set VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
set MSBUILD=
if exist "%VSWHERE%" (
  for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set MSBUILD=%%i
)
if "%MSBUILD%"=="" (
  echo MSBuild not found. Install Visual Studio with the .NET desktop workload.
  exit /b 1
)

"%MSBUILD%" "%~dp0ezLink.sln" /p:Configuration=%CONFIG% /v:minimal /nologo
exit /b %ERRORLEVEL%
