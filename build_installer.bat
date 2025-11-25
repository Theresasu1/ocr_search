@echo off
chcp 65001 >nul
echo ========================================
echo OCR文件搜索工具 - 安装程序生成器
echo ========================================
echo.

:: 1. 清理旧的发布文件
echo [1/4] 清理旧的发布文件...
if exist "FileSearchTool\bin\Release" (
    rmdir /s /q "FileSearchTool\bin\Release"
)

:: 2. 发布项目（Release模式，单文件发布）
echo [2/4] 发布项目（Release模式）...
dotnet publish FileSearchTool\FileSearchTool.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=false ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:PublishReadyToRun=true

if errorlevel 1 (
    echo.
    echo [错误] 项目发布失败！
    pause
    exit /b 1
)

echo.
echo [3/4] 项目发布成功！
echo.

:: 3. 检查 Inno Setup 是否存在
set INNO_SETUP_PATH=E:\alldata\InnoSetup6\ISCC.exe

if not exist "%INNO_SETUP_PATH%" (
    echo [错误] 未找到 Inno Setup 编译器：
    echo %INNO_SETUP_PATH%
    echo.
    echo 请确认 Inno Setup 已安装在 E:\alldata\InnoSetup6 目录
    pause
    exit /b 1
)

:: 4. 使用 Inno Setup 编译安装程序
echo [4/4] 生成安装程序...
"%INNO_SETUP_PATH%" setup.iss

if errorlevel 1 (
    echo.
    echo [错误] 安装程序生成失败！
    pause
    exit /b 1
)

echo.
echo ========================================
echo ✓ 安装程序生成成功！
echo ========================================
echo.
echo 安装程序位置：
echo %cd%\FileSearchTool_Setup_v5.0.0.exe
echo.

:: 打开输出目录
if exist "FileSearchTool_Setup_v5.0.0.exe" (
    explorer /select,"FileSearchTool_Setup_v5.0.0.exe"
)

pause
