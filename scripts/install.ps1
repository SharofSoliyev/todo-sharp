#!/usr/bin/env pwsh
# TodoBot - Cross-platform installer (Windows / macOS / Linux)
# Windows:  powershell -ExecutionPolicy Bypass -File install.ps1
# macOS/Linux: pwsh install.ps1

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "     TodoBot - Installer" -ForegroundColor Cyan
Write-Host "     Windows / macOS / Linux" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# dotnet tekshirish va kerak bo'lsa o'rnatish
$dotnetVersion = $null
try { $dotnetVersion = & dotnet --version 2>$null } catch {}
if (-not $dotnetVersion) {
    Write-Host "[!] .NET SDK topilmadi - avtomatik o'rnatilmoqda..." -ForegroundColor Yellow
    Write-Host ""

    if ($env:OS -eq "Windows_NT") {
        # Windows: winget orqali o'rnatish
        $hasWinget = $null
        try { $hasWinget = & winget --version 2>$null } catch {}

        if ($hasWinget) {
            Write-Host "    winget orqali .NET 8 SDK o'rnatilmoqda..." -ForegroundColor Cyan
            & winget install Microsoft.DotNet.SDK.8 --accept-source-agreements --accept-package-agreements
            if ($LASTEXITCODE -ne 0) {
                Write-Host "[ERROR] .NET SDK o'rnatilmadi!" -ForegroundColor Red
                Write-Host "  Qo'lda o'rnating: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
                exit 1
            }
        } else {
            # winget yo'q - dotnet-install.ps1 skripti orqali
            Write-Host "    dotnet-install skripti orqali o'rnatilmoqda..." -ForegroundColor Cyan
            $installScript = Join-Path $env:TEMP "dotnet-install.ps1"
            try {
                [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
                Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript -UseBasicParsing
                & powershell -ExecutionPolicy Bypass -File $installScript -Channel 8.0
            } catch {
                Write-Host "[ERROR] .NET SDK yuklab olinmadi!" -ForegroundColor Red
                Write-Host "  Qo'lda o'rnating: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
                exit 1
            }
        }

        # PATH yangilash (joriy sessiya uchun)
        $dotnetPaths = @(
            (Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"),
            (Join-Path $env:ProgramFiles "dotnet"),
            (Join-Path $HOME ".dotnet")
        )
        foreach ($p in $dotnetPaths) {
            if ((Test-Path (Join-Path $p "dotnet.exe")) -and ($env:PATH -notlike "*$p*")) {
                $env:PATH = "$p;$env:PATH"
            }
        }
    } else {
        # macOS / Linux: dotnet-install.sh skripti
        Write-Host "    dotnet-install skripti orqali o'rnatilmoqda..." -ForegroundColor Cyan
        try {
            & bash -c "curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0"
        } catch {
            Write-Host "[ERROR] .NET SDK o'rnatilmadi!" -ForegroundColor Red
            exit 1
        }
        $dotnetHome = Join-Path $HOME ".dotnet"
        if ($env:PATH -notlike "*$dotnetHome*") {
            $env:PATH = "${dotnetHome}:$env:PATH"
        }
    }

    # Qayta tekshirish
    $dotnetVersion = $null
    try { $dotnetVersion = & dotnet --version 2>$null } catch {}
    if (-not $dotnetVersion) {
        Write-Host "[ERROR] .NET SDK o'rnatilmadi yoki PATH da topilmadi!" -ForegroundColor Red
        Write-Host "  Qo'lda o'rnating: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
        exit 1
    }
    Write-Host ""
    Write-Host "[OK] .NET SDK o'rnatildi: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "[OK] .NET SDK: $dotnetVersion" -ForegroundColor Green
}

# OS aniqlash
$osIsWindows = ($env:OS -eq "Windows_NT")
$installDir = Join-Path $HOME ".todobot"

Write-Host "[INFO] Install dir: $installDir" -ForegroundColor Cyan
Write-Host ""

# Build
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$projectFile = Join-Path $repoRoot "src" "TodoBot.csproj"

if (-not (Test-Path $projectFile)) {
    Write-Host "[ERROR] TodoBot.csproj topilmadi!" -ForegroundColor Red
    exit 1
}

Write-Host "[1/6] Build qilinmoqda..." -ForegroundColor Yellow
& dotnet publish $projectFile -c Release -o $installDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Build xatolik!" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Build tayyor!" -ForegroundColor Green
Write-Host ""

# ===== INSTALL =====
if ($osIsWindows) {

    # 2. Launcher - todosharp.cmd
    Write-Host "[2/6] todosharp launcher yaratilmoqda..." -ForegroundColor Yellow
    $cmdPath = Join-Path $installDir "todosharp.cmd"
    $cmdContent = "@echo off`r`npowershell -ExecutionPolicy Bypass -File `"%~dp0todosharp.ps1`" %*"
    Set-Content -Path $cmdPath -Value $cmdContent -Encoding ASCII
    Write-Host "[OK] todosharp.cmd yaratildi" -ForegroundColor Green

    # 3. PATH
    Write-Host "[3/6] PATH yangilanmoqda..." -ForegroundColor Yellow
    $userPath = [Environment]::GetEnvironmentVariable("PATH", [EnvironmentVariableTarget]::User)
    if (-not $userPath) {
        [Environment]::SetEnvironmentVariable("PATH", $installDir, [EnvironmentVariableTarget]::User)
        Write-Host "[OK] PATH ga qoshildi" -ForegroundColor Green
    } elseif ($userPath -notlike "*todobot*") {
        [Environment]::SetEnvironmentVariable("PATH", "$userPath;$installDir", [EnvironmentVariableTarget]::User)
        Write-Host "[OK] PATH ga qoshildi" -ForegroundColor Green
    } else {
        Write-Host "[OK] PATH da allaqachon bor" -ForegroundColor Green
    }

    # 4. Desktop shortcut
    Write-Host "[4/6] Desktop yorliq..." -ForegroundColor Yellow
    try {
        $desktop = [Environment]::GetFolderPath("Desktop")
        $ws = New-Object -ComObject WScript.Shell
        $lnk = $ws.CreateShortcut((Join-Path $desktop "TodoBot.lnk"))
        $lnk.TargetPath = (Get-Command dotnet).Source
        $lnk.Arguments = "`"$installDir\TodoBot.dll`""
        $lnk.WorkingDirectory = $installDir
        $lnk.Description = "Telegram To-Do Bot"
        $lnk.Save()
        Write-Host "[OK] Desktop yorliq yaratildi" -ForegroundColor Green
    } catch {
        Write-Host "[SKIP] Desktop yorliq yaratilmadi" -ForegroundColor Yellow
    }

    # 5. Uninstaller
    Write-Host "[5/6] Uninstaller..." -ForegroundColor Yellow
    $uninstallPath = Join-Path $installDir "uninstall.cmd"
    $unContent = @"
@echo off
chcp 65001 >nul
echo TodoBot ochirilmoqda...
del "%USERPROFILE%\Desktop\TodoBot.lnk" >nul 2>&1
rmdir /s /q "$installDir" >nul 2>&1
echo TodoBot ochirildi!
pause
"@
    Set-Content -Path $uninstallPath -Value $unContent -Encoding UTF8
    Write-Host "[OK] uninstall.cmd yaratildi" -ForegroundColor Green

    $todosharpPath = Join-Path $installDir "todosharp.ps1"

} else {
    # ===== macOS / Linux =====

    # 2. Launcher - todosharp
    Write-Host "[2/6] todosharp launcher yaratilmoqda..." -ForegroundColor Yellow
    $isMac = $false
    try { $isMac = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX) } catch {}
    if ($isMac) {
        $binDir = "/usr/local/bin"
    } else {
        $binDir = Join-Path $HOME ".local" "bin"
    }
    if (-not (Test-Path $binDir)) {
        New-Item -ItemType Directory -Path $binDir -Force | Out-Null
    }
    $launcherPath = Join-Path $binDir "todosharp"

    $todosharpBash = @'
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
'@

    Set-Content -Path $launcherPath -Value $todosharpBash
    & chmod +x $launcherPath 2>$null
    Write-Host "[OK] $launcherPath yaratildi" -ForegroundColor Green

    # 3. PATH
    Write-Host "[3/6] PATH tekshirilmoqda..." -ForegroundColor Yellow
    if (-not $isMac -and -not ($env:PATH -like "*$binDir*")) {
        $bashrc = Join-Path $HOME ".bashrc"
        if (Test-Path $bashrc) {
            $rc = Get-Content $bashrc -Raw -ErrorAction SilentlyContinue
            if ($rc -and ($rc -notlike "*/.local/bin*")) {
                Add-Content -Path $bashrc -Value "`nexport PATH=`"`$HOME/.local/bin:`$PATH`""
                Write-Host "[OK] ~/.bashrc yangilandi" -ForegroundColor Green
            }
        }
    }
    Write-Host "[OK] PATH tayyor" -ForegroundColor Green

    # 4. Skip (no desktop shortcut on Linux)
    Write-Host "[4/6] Desktop yorliq - Linux/macOS uchun shart emas" -ForegroundColor Yellow

    # 5. Uninstaller
    Write-Host "[5/6] Uninstaller..." -ForegroundColor Yellow
    $uninstallPath = Join-Path $installDir "uninstall.sh"
    Set-Content -Path $uninstallPath -Value "#!/bin/bash`nrm -f `"$binDir/todosharp`"`nrm -rf `"$installDir`"`necho TodoBot ochirildi!"
    & chmod +x $uninstallPath 2>$null
    Write-Host "[OK] uninstall.sh yaratildi" -ForegroundColor Green

    $todosharpPath = $launcherPath
}

# ===== [6/6] SOZLAMALAR =====
Write-Host ""
Write-Host "[6/6] Sozlamalar..." -ForegroundColor Yellow
Write-Host ""
Write-Host "Telegram bot tokenini kiriting" -ForegroundColor Cyan
Write-Host "(BotFather dan olingan token)" -ForegroundColor Cyan
Write-Host ""
Write-Host -NoNewline "Bot token: " -ForegroundColor Cyan
$botToken = Read-Host

if ([string]::IsNullOrWhiteSpace($botToken)) {
    Write-Host "[!] Token kiritilmadi - bot ishga tushganda soraydi" -ForegroundColor Yellow
} else {
    Write-Host "[OK] Token qabul qilindi" -ForegroundColor Green
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "  TodoBot muvaffaqiyatli ornatildi!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Boshqarish:" -ForegroundColor White
Write-Host "    todosharp start    - Ishga tushirish" -ForegroundColor White
Write-Host "    todosharp stop     - Toxtatish" -ForegroundColor White
Write-Host "    todosharp restart  - Qayta tushirish" -ForegroundColor White
Write-Host "    todosharp status   - Holatni korish" -ForegroundColor White
Write-Host "    todosharp log      - Loglarni korish" -ForegroundColor White
Write-Host ""
Write-Host "  Ochirish: $uninstallPath" -ForegroundColor White
Write-Host ""
if ($osIsWindows) {
    Write-Host "  [!] Yangi terminal oching - PATH uchun" -ForegroundColor Yellow
    Write-Host ""
}

# Avtomatik ishga tushirish
Write-Host "Bot ishga tushirilmoqda..." -ForegroundColor Cyan
Write-Host ""

if (-not [string]::IsNullOrWhiteSpace($botToken)) {
    $env:TELEGRAM_BOT_TOKEN = $botToken
}

& $todosharpPath start
