#!/bin/bash

echo "🚀 Starting Copilot CLI Web Application..."
echo ""

# Check if Copilot CLI is installed
if ! command -v copilot &> /dev/null
then
    echo "❌ Copilot CLI is not installed or not in PATH"
    echo "Please install it first: https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli"
    exit 1
fi

echo "✅ Copilot CLI found"
echo ""

# Start Backend
echo "📦 Starting Backend API..."
cd Backend
dotnet restore > /dev/null 2>&1
dotnet run &
BACKEND_PID=$!
echo "✅ Backend API started (PID: $BACKEND_PID)"
echo ""

# Wait for backend to be ready
sleep 5

# Start Frontend
echo "📦 Installing Frontend dependencies..."
cd ../Frontend
npm install > /dev/null 2>&1
echo "✅ Dependencies installed"
echo ""

echo "🎨 Starting Frontend..."
npm run dev &
FRONTEND_PID=$!
echo "✅ Frontend started (PID: $FRONTEND_PID)"
echo ""

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "✨ Application is ready!"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "🌐 Frontend: http://localhost:5173"
echo "🔧 Backend:  http://localhost:5000"
echo ""
echo "Press Ctrl+C to stop all services"
echo ""

# Wait for user interrupt
trap "echo ''; echo '🛑 Stopping services...'; kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; exit 0" INT
wait
