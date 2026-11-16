@echo off
rem Builds bin\MsgOptimizer.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem No SDK, Visual Studio or NuGet needed.
setlocal
cd /d "%~dp0"

set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\csc.exe" set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
if not exist "%FW%\csc.exe" (
  echo csc.exe not found. Install the .NET Framework 4.8 runtime: https://dotnet.microsoft.com/download/dotnet-framework/net48
  exit /b 1
)

if not exist bin mkdir bin
"%FW%\csc.exe" /nologo /codepage:65001 /target:winexe /optimize+ /out:bin\MsgOptimizer.exe ^
  /lib:"%FW%\WPF" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:System.Net.Http.dll /r:System.Web.Extensions.dll /r:System.Security.dll ^
  /r:UIAutomationClient.dll /r:UIAutomationTypes.dll /r:WindowsBase.dll ^
  /resource:src\app.png,app.png ^
  src\*.cs
if errorlevel 1 exit /b 1

rem GUI exe: "start /wait" makes cmd wait for it and pick up its exit code.
start "" /wait bin\MsgOptimizer.exe --selftest
if errorlevel 1 (
  echo Self-test failed.
  exit /b 1
)
echo Built bin\MsgOptimizer.exe
