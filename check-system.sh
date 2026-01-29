#!/bin/bash

echo "🔍 Copilot CLI Web App - 系統檢查"
echo "=================================="
echo ""

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

check_command() {
    if command -v $1 &> /dev/null; then
        echo -e "${GREEN}✓${NC} $1 已安裝: $($1 --version 2>&1 | head -1)"
        return 0
    else
        echo -e "${RED}✗${NC} $1 未安裝"
        return 1
    fi
}

# Check .NET
echo "1. 檢查 .NET SDK..."
if check_command dotnet; then
    version=$(dotnet --version)
    major=$(echo $version | cut -d. -f1)
    if [ $major -ge 10 ]; then
        echo -e "   ${GREEN}版本符合要求 (>= 10.0)${NC}"
    else
        echo -e "   ${YELLOW}警告: 需要 .NET 10.0 或更新版本${NC}"
    fi
fi
echo ""

# Check Node.js
echo "2. 檢查 Node.js..."
if check_command node; then
    version=$(node --version | cut -d'v' -f2 | cut -d. -f1)
    if [ $version -ge 18 ]; then
        echo -e "   ${GREEN}版本符合要求 (>= 18.0)${NC}"
    else
        echo -e "   ${YELLOW}警告: 建議使用 Node.js 18.0 或更新版本${NC}"
    fi
fi
echo ""

# Check npm
echo "3. 檢查 npm..."
check_command npm
echo ""

# Check Copilot CLI
echo "4. 檢查 Copilot CLI..."
if check_command copilot; then
    echo "   測試 Copilot CLI..."
    if timeout 10 copilot -p "test" > /dev/null 2>&1; then
        echo -e "   ${GREEN}✓ Copilot CLI 正常運作${NC}"
    else
        echo -e "   ${YELLOW}⚠ Copilot CLI 可能需要登入${NC}"
        echo "   執行: gh auth login"
    fi
else
    echo -e "   ${RED}請安裝 Copilot CLI:${NC}"
    echo "   gh extension install github/gh-copilot"
fi
echo ""

# Check GitHub CLI
echo "5. 檢查 GitHub CLI..."
if check_command gh; then
    echo "   檢查認證狀態..."
    if gh auth status &> /dev/null; then
        echo -e "   ${GREEN}✓ 已登入 GitHub${NC}"
    else
        echo -e "   ${YELLOW}⚠ 未登入 GitHub${NC}"
        echo "   執行: gh auth login"
    fi
fi
echo ""

# Check Backend build
echo "6. 檢查後端專案..."
if [ -f "Backend/CopilotApi.csproj" ]; then
    echo -e "   ${GREEN}✓${NC} 專案檔案存在"
    if [ -d "Backend/bin/Debug/net10.0" ]; then
        echo -e "   ${GREEN}✓${NC} 已編譯"
    else
        echo -e "   ${YELLOW}⚠${NC} 尚未編譯，執行: cd Backend && dotnet build"
    fi
else
    echo -e "   ${RED}✗${NC} 找不到專案檔案"
fi
echo ""

# Check Frontend
echo "7. 檢查前端專案..."
if [ -f "Frontend/package.json" ]; then
    echo -e "   ${GREEN}✓${NC} package.json 存在"
    if [ -d "Frontend/node_modules" ]; then
        echo -e "   ${GREEN}✓${NC} 依賴已安裝"
    else
        echo -e "   ${YELLOW}⚠${NC} 尚未安裝依賴，執行: cd Frontend && npm install"
    fi
else
    echo -e "   ${RED}✗${NC} 找不到 package.json"
fi
echo ""

# Check ports
echo "8. 檢查端口占用..."
if lsof -Pi :5000 -sTCP:LISTEN -t >/dev/null 2>&1 ; then
    echo -e "   ${YELLOW}⚠${NC} Port 5000 已被占用"
else
    echo -e "   ${GREEN}✓${NC} Port 5000 可用"
fi

if lsof -Pi :5173 -sTCP:LISTEN -t >/dev/null 2>&1 ; then
    echo -e "   ${YELLOW}⚠${NC} Port 5173 已被占用"
else
    echo -e "   ${GREEN}✓${NC} Port 5173 可用"
fi
echo ""

# Summary
echo "=================================="
echo "檢查完成！"
echo ""
echo "如果所有檢查都通過，執行以下命令啟動應用："
echo "  ./start.sh"
echo ""
echo "或手動啟動："
echo "  終端 1: cd Backend && dotnet run"
echo "  終端 2: cd Frontend && npm run dev"
echo ""
