@echo off
chcp 65001 >nul
setlocal ENABLEEXTENSIONS

rem 切换到项目目录（相对于当前批处理文件所在路径）
cd /d "%~dp0FileSearchTool"

echo [1/3] 正在关闭已运行的实例...
taskkill /IM FileSearchTool.exe /F >nul 2>nul
timeout /t 1 /nobreak >nul

echo [2/3] 正在还原并编译项目...
dotnet build
if errorlevel 1 (
  echo 编译失败，请检查错误信息。
  pause
  exit /b 1
)

set "EXE=bin\Debug\net6.0-windows\FileSearchTool.exe"
if not exist "%EXE%" set "EXE=bin\Release\net6.0-windows\FileSearchTool.exe"

if not exist "%EXE%" (
  echo 未找到编译生成的可执行文件：%EXE%
  echo 请确认项目已成功生成。
  pause
  exit /b 2
)

echo [3/3] 正在启动应用程序...
start "" "%EXE%"

echo 完成。窗口已启动。
exit /b 0
