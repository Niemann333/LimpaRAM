@echo off
REM Compila o LimpaRAM.exe usando o compilador C# que ja vem no Windows (.NET Framework 4.x)
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Compilador nao encontrado. Instale o .NET Framework 4.8.
    pause
    exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:app.manifest /out:LimpaRAM.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll LimpaRAM.cs

if exist LimpaRAM.exe (
    echo.
    echo Pronto! LimpaRAM.exe criado nesta pasta.
) else (
    echo.
    echo Falha na compilacao.
)
pause
