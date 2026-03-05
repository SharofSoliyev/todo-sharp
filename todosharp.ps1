# todosharp - TodoBot boshqaruv paneli
# Usage: todosharp start|stop|restart|status|log|run

param(
    [Parameter(Position=0)]
    [string]$Command = ""
)

$installDir = Join-Path $HOME ".todobot"
$pidFile = Join-Path $installDir "todobot.pid"
$logFile = Join-Path $installDir "todobot.log"
$dll = Join-Path $installDir "TodoBot.dll"

function Test-BotRunning {
    if (-not (Test-Path $pidFile)) { return $false }
    $savedPid = (Get-Content $pidFile -ErrorAction SilentlyContinue).Trim()
    if (-not $savedPid) { return $false }
    try {
        Get-Process -Id ([int]$savedPid) -ErrorAction Stop | Out-Null
        return $true
    } catch {
        Remove-Item $pidFile -ErrorAction SilentlyContinue
        return $false
    }
}

function Start-Bot {
    if (Test-BotRunning) {
        $savedPid = (Get-Content $pidFile).Trim()
        Write-Host "[!] TodoBot allaqachon ishlamoqda (PID: $savedPid)" -ForegroundColor Yellow
        return
    }
    if (-not (Test-Path $dll)) {
        Write-Host "[ERROR] TodoBot.dll topilmadi: $dll" -ForegroundColor Red
        return
    }
    $runScript = Join-Path $installDir "run-bot.cmd"
    Set-Content -Path $runScript -Value "@echo off`r`ndotnet `"$dll`" > `"$logFile`" 2>&1" -Encoding ASCII

    $proc = Start-Process cmd -ArgumentList "/c `"$runScript`"" -WorkingDirectory $installDir -WindowStyle Hidden -PassThru
    Set-Content -Path $pidFile -Value $proc.Id
    Start-Sleep -Seconds 2

    if (Test-BotRunning) {
        Write-Host "[OK] TodoBot ishga tushdi (PID: $($proc.Id))" -ForegroundColor Green
    } else {
        Write-Host "[ERROR] TodoBot ishga tushmadi! 'todosharp log' bilan tekshiring" -ForegroundColor Red
        Remove-Item $pidFile -ErrorAction SilentlyContinue
    }
}

function Stop-Bot {
    if (-not (Test-BotRunning)) {
        Write-Host "[!] TodoBot ishlamayapti" -ForegroundColor Yellow
        return
    }
    $savedPid = (Get-Content $pidFile).Trim()
    & taskkill /t /f /pid $savedPid 2>$null | Out-Null
    Remove-Item $pidFile -ErrorAction SilentlyContinue
    Write-Host "[OK] TodoBot toxtatildi" -ForegroundColor Green
}

switch ($Command.ToLower()) {
    "start" {
        Start-Bot
    }
    "stop" {
        Stop-Bot
    }
    "restart" {
        Stop-Bot
        Start-Sleep -Seconds 2
        Start-Bot
    }
    "status" {
        if (Test-BotRunning) {
            $savedPid = (Get-Content $pidFile).Trim()
            Write-Host "[OK] TodoBot ishlamoqda (PID: $savedPid)" -ForegroundColor Green
        } else {
            Write-Host "[!] TodoBot ishlamayapti" -ForegroundColor Yellow
        }
    }
    "log" {
        if (Test-Path $logFile) {
            Get-Content $logFile -Tail 50
        } else {
            Write-Host "[!] Log fayl topilmadi" -ForegroundColor Yellow
        }
    }
    "run" {
        if (-not (Test-Path $dll)) {
            Write-Host "[ERROR] TodoBot.dll topilmadi: $dll" -ForegroundColor Red
            return
        }
        Set-Location $installDir
        & dotnet $dll
    }
    default {
        Write-Host ""
        Write-Host "  todosharp - TodoBot boshqaruv paneli" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "  todosharp start    - Botni ishga tushirish (fon)" -ForegroundColor White
        Write-Host "  todosharp stop     - Botni toxtatish" -ForegroundColor White
        Write-Host "  todosharp restart  - Qayta ishga tushirish" -ForegroundColor White
        Write-Host "  todosharp status   - Bot holatini korish" -ForegroundColor White
        Write-Host "  todosharp log      - Loglarni korish" -ForegroundColor White
        Write-Host "  todosharp run      - Oldingi rejimda ishga tushirish" -ForegroundColor White
        Write-Host ""
    }
}
