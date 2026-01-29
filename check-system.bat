@echo off
echo 🔍 Copilot CLI Web App - 系統檢查
echo ==================================
echo.

REM Check .NET
echo 1. 檢查 .NET SDK...
where dotnet >nul 2>nul
if %errorlevel% equ 0 (
    echo ✓ .NET 已安裝
    dotnet --version
) else (
    echo ✗ .NET 未安裝
    echo   下載: https://dotnet.microsoft.com/download
)
echo.

REM Check Node.js
echo 2. 檢查 Node.js...
where node >nul 2>nul
if %errorlevel% equ 0 (
    echo ✓ Node.js 已安裝
    node --version
) else (
    echo ✗ Node.js 未安裝
    echo   下載: https://nodejs.org/
)
echo.

REM Check npm
echo 3. 檢查 npm...
where npm >nul 2>nul
if %errorlevel% equ 0 (
    echo ✓ npm 已安裝
    npm --version
) else (
    echo ✗ npm 未安裝
)
echo.

REM Check Copilot CLI
echo 4. 檢查 Copilot CLI...
where copilot >nul 2>nul
if %errorlevel% equ 0 (
    echo ✓ Copilot CLI 已安裝
    copilot --version
) else (
    echo ✗ Copilot CLI 未安裝
    echo   安裝: gh extension install github/gh-copilot
)
echo.

REM Check GitHub CLI
echo 5. 檢查 GitHub CLI...
where gh >nul 2>nul
if %errorlevel% equ 0 (
    echo ✓ GitHub CLI 已安裝
    gh --version
    echo   檢查認證: gh auth status
) else (
    echo ✗ GitHub CLI 未安裝
    echo   下載: https://cli.github.com/
)
echo.

REM Check Backend
echo 6. 檢查後端專案...
if exist "Backend\CopilotApi.csproj" (
    echo ✓ 專案檔案存在
    if exist "Backend\bin\Debug\net10.0" (
        echo ✓ 已編譯
    ) else (
        echo ⚠ 尚未編譯
        echo   執行: cd Backend ^&^& dotnet build
    )
) else (
    echo ✗ 找不到專案檔案
)
echo.

REM Check Frontend
echo 7. 檢查前端專案...
if exist "Frontend\package.json" (
    echo ✓ package.json 存在
    if exist "Frontend\node_modules" (
        echo ✓ 依賴已安裝
    ) else (
        echo ⚠ 尚未安裝依賴
        echo   執行: cd Frontend ^&^& npm install
    )
) else (
    echo ✗ 找不到 package.json
)
echo.

REM Check ports
echo 8. 檢查端口占用...
netstat -ano | findstr :5000 >nul
if %errorlevel% equ 0 (
    echo ⚠ Port 5000 已被占用
) else (
    echo ✓ Port 5000 可用
)

netstat -ano | findstr :5173 >nul
if %errorlevel% equ 0 (
    echo ⚠ Port 5173 已被占用
) else (
    echo ✓ Port 5173 可用
)
echo.

echo ==================================
echo 檢查完成！
echo.
echo 如果所有檢查都通過，執行以下命令啟動應用：
echo   start.bat
echo.
echo 或手動啟動：
echo   終端 1: cd Backend ^&^& dotnet run
echo   終端 2: cd Frontend ^&^& npm run dev
echo.
pause
