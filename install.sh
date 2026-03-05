#!/bin/bash
# TodoBot - Linux / macOS installer
# Ishga tushirish: chmod +x install.sh && ./install.sh

set -e

echo ""
echo "======================================"
echo "     TodoBot - Installer"
echo "     Linux / macOS"
echo "======================================"
echo ""

# dotnet tekshirish
if ! command -v dotnet &> /dev/null; then
    echo "[ERROR] .NET SDK topilmadi!"
    echo "   https://dotnet.microsoft.com/download dan yuklab o'rnating."
    echo ""
    echo "   Tezkor o'rnatish:"
    echo "   curl -sSL https://dot.net/v1/dotnet-install.sh | bash"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "[OK] .NET SDK topildi: $DOTNET_VERSION"

# OS aniqlash
OS="$(uname -s)"
case "$OS" in
    Darwin) OS_NAME="macOS"; BIN_DIR="/usr/local/bin" ;;
    Linux)  OS_NAME="Linux";  BIN_DIR="$HOME/.local/bin" ;;
    *)      OS_NAME="$OS";    BIN_DIR="$HOME/.local/bin" ;;
esac

INSTALL_DIR="$HOME/.todobot"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/TodoBot.csproj"

echo "[INFO] OS: $OS_NAME"
echo "[INFO] Install dir: $INSTALL_DIR"
echo "[INFO] Buyruq: $BIN_DIR/todosharp"
echo ""

# Loyihani tekshirish
if [ ! -f "$PROJECT_FILE" ]; then
    echo "[ERROR] TodoBot.csproj topilmadi: $PROJECT_FILE"
    exit 1
fi

# [1/6] Build
echo "[1/6] Build qilinmoqda..."
dotnet publish "$PROJECT_FILE" -c Release -o "$INSTALL_DIR"
echo "[OK] Build muvaffaqiyatli!"
echo ""

# [2/6] todosharp management script
echo "[2/6] todosharp launcher yaratilmoqda..."
mkdir -p "$BIN_DIR"

cat > "$BIN_DIR/todosharp" << 'TODOSHARP_EOF'
#!/bin/bash
INSTALL_DIR="$HOME/.todobot"
PID_FILE="$INSTALL_DIR/todobot.pid"
LOG_FILE="$INSTALL_DIR/todobot.log"
DLL="$INSTALL_DIR/TodoBot.dll"

is_running() {
    [ -f "$PID_FILE" ] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null
}

start_bot() {
    if is_running; then
        echo "[!] TodoBot allaqachon ishlamoqda (PID: $(cat "$PID_FILE"))"
        return
    fi
    [ ! -f "$DLL" ] && echo "[ERROR] TodoBot.dll topilmadi: $DLL" && return 1
    cd "$INSTALL_DIR"
    nohup dotnet "$DLL" > "$LOG_FILE" 2>&1 &
    echo $! > "$PID_FILE"
    sleep 2
    if is_running; then
        echo "[OK] TodoBot ishga tushdi (PID: $(cat "$PID_FILE"))"
    else
        echo "[ERROR] TodoBot ishga tushmadi! 'todosharp log' bilan tekshiring"
        rm -f "$PID_FILE"
    fi
}

stop_bot() {
    if ! is_running; then
        echo "[!] TodoBot ishlamayapti"
        return
    fi
    local pid=$(cat "$PID_FILE")
    kill "$pid" 2>/dev/null
    sleep 1
    kill -0 "$pid" 2>/dev/null && kill -9 "$pid" 2>/dev/null
    rm -f "$PID_FILE"
    echo "[OK] TodoBot toxtatildi"
}

case "${1:-}" in
    start)   start_bot ;;
    stop)    stop_bot ;;
    restart) stop_bot; sleep 2; start_bot ;;
    status)
        if is_running; then
            echo "[OK] TodoBot ishlamoqda (PID: $(cat "$PID_FILE"))"
        else
            echo "[!] TodoBot ishlamayapti"
        fi
        ;;
    log)
        [ -f "$LOG_FILE" ] && tail -50 "$LOG_FILE" || echo "[!] Log fayl topilmadi"
        ;;
    run)
        [ ! -f "$DLL" ] && echo "[ERROR] TodoBot.dll topilmadi" && exit 1
        cd "$INSTALL_DIR"
        exec dotnet "$DLL"
        ;;
    *)
        echo ""
        echo "  todosharp - TodoBot boshqaruv paneli"
        echo ""
        echo "  todosharp start    - Botni ishga tushirish (fon)"
        echo "  todosharp stop     - Botni toxtatish"
        echo "  todosharp restart  - Qayta ishga tushirish"
        echo "  todosharp status   - Bot holatini korish"
        echo "  todosharp log      - Loglarni korish"
        echo "  todosharp run      - Oldingi rejimda ishga tushirish"
        echo ""
        ;;
esac
TODOSHARP_EOF

chmod +x "$BIN_DIR/todosharp"
echo "[OK] $BIN_DIR/todosharp yaratildi"

# [3/6] PATH tekshirish
echo "[3/6] PATH tekshirilmoqda..."
if [ "$OS_NAME" = "Linux" ]; then
    if ! echo "$PATH" | grep -q "$BIN_DIR"; then
        SHELL_RC=""
        if [ -f "$HOME/.zshrc" ]; then
            SHELL_RC="$HOME/.zshrc"
        elif [ -f "$HOME/.bashrc" ]; then
            SHELL_RC="$HOME/.bashrc"
        fi

        if [ -n "$SHELL_RC" ]; then
            if ! grep -q ".local/bin" "$SHELL_RC" 2>/dev/null; then
                echo 'export PATH="$HOME/.local/bin:$PATH"' >> "$SHELL_RC"
                echo "[OK] PATH qoshildi: $SHELL_RC"
            fi
        fi
    fi
fi
echo "[OK] PATH tayyor"

# [4/6] Desktop yorliq
echo "[4/6] Desktop yorliq - Linux/macOS uchun shart emas"

# [5/6] Uninstall script
echo "[5/6] Uninstaller yaratilmoqda..."
cat > "$INSTALL_DIR/uninstall.sh" << EOF
#!/bin/bash
echo "TodoBot ochirilmoqda..."
$BIN_DIR/todosharp stop 2>/dev/null
rm -f "$BIN_DIR/todosharp"
rm -rf "$INSTALL_DIR"
echo "TodoBot ochirildi!"
EOF
chmod +x "$INSTALL_DIR/uninstall.sh"
echo "[OK] Uninstaller: $INSTALL_DIR/uninstall.sh"

# [6/6] Sozlamalar
echo ""
echo "======================================"
echo "  [6/6] Sozlamalar"
echo "======================================"
echo ""

echo "Telegram bot tokenini kiriting"
echo "(BotFather dan olingan token)"
echo ""
printf "Bot token: "
read BOT_TOKEN

if [ -z "$BOT_TOKEN" ]; then
    echo "[!] Token kiritilmadi - bot ishga tushganda soraydi"
else
    echo "[OK] Token qabul qilindi"
fi

echo ""
echo "======================================"
echo "  TodoBot muvaffaqiyatli ornatildi!"
echo "======================================"
echo ""
echo "  Boshqarish:"
echo "    todosharp start    - Ishga tushirish"
echo "    todosharp stop     - Toxtatish"
echo "    todosharp restart  - Qayta tushirish"
echo "    todosharp status   - Holatni korish"
echo "    todosharp log      - Loglarni korish"
echo ""
echo "  Ochirish: ~/.todobot/uninstall.sh"
echo ""

if ! echo "$PATH" | grep -q "$BIN_DIR"; then
    echo "[!] Yangi terminal oching yoki quyidagini bajaring:"
    echo "   source ~/.bashrc  yoki  source ~/.zshrc"
    echo ""
fi

# Avtomatik ishga tushirish
echo "Bot ishga tushirilmoqda..."
echo ""

if [ -n "$BOT_TOKEN" ]; then
    export TELEGRAM_BOT_TOKEN="$BOT_TOKEN"
fi

"$BIN_DIR/todosharp" start
