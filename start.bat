@echo off
echo 🚀 Starting Copilot CLI Web Application...
echo.

REM Check if Copilot CLI is installed
where copilot >nul 2>nul
if %errorlevel% neq 0 (
    echo ❌ Copilot CLI is not installed or not in PATH
    echo Please install it first: https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli
    pause
    exit /b 1
)

echo ✅ Copilot CLI found
echo.

REM Start Backend
echo 📦 Starting Backend API...
cd Backend
start "Backend API" dotnet run
echo ✅ Backend API started
echo.

REM Wait for backend to be ready
timeout /t 5 /nobreak >nul

REM Start Frontend
echo 📦 Installing Frontend dependencies...
cd ..\Frontend
call npm install >nul 2>nul
echo ✅ Dependencies installed
echo.

echo 🎨 Starting Frontend...
start "Frontend" npm run dev
echo ✅ Frontend started
echo.

echo ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
echo ✨ Application is ready!
echo ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
echo.
echo 🌐 Frontend: http://localhost:5173
echo 🔧 Backend:  http://localhost:5000
echo.
echo Press any key to stop all services...
pause >nul

REM Stop services
taskkill /FI "WindowTitle eq Backend API*" /T /F >nul 2>nul
taskkill /FI "WindowTitle eq Frontend*" /T /F >nul 2>nul
echo.
echo 🛑 Services stopped
