@echo off
chcp 65001 > nul
rem sign.cmd signs PowerShell scripts and PE files, including WLX binaries.
setlocal EnableDelayedExpansion

rem --- Параметры по умолчанию ------------------------------------------------
rem Отпечатки код-подписывающих сертификатов (SHA-1, hex, регистр не важен).
set "THUMBPRINT_SHA1=1e50512341554b70671f922f4a027d4ef035626c"
set "THUMBPRINT_SHA256=9fff57ffc87eea6b34cd196dba95436c217baebf"

rem Хранилище сертификата: CurrentUser\My (без прав админа) или LocalMachine\My.
set "STORE=CurrentUser"

rem Путь к signtool.exe (Windows SDK). Можно переопределить через окружение.
if not defined SIGNTOOL set "SIGNTOOL=C:\Proj\jCjS2\_dest_dir\utils\signTools\signtool.exe"

rem Серверы метки времени (RFC 3161). Пустые значения отключают таймстемпинг.
rem   TMS1 — для SHA1-подписи
rem   TMS2 — для SHA256-подписи (с явным alg=sha256)
set "TMS1=http://timestamp.digicert.com/"
set "TMS2=http://timestamp.digicert.com?alg=sha256"

rem --- Разбор аргументов -----------------------------------------------------
set "TARGET=%~1"
if "%TARGET%"=="" goto :usage
if not exist "%TARGET%" (
    echo [sign] ERROR: файл не найден: %TARGET%
    exit /b 2
)

rem --- Определение типа файла по расширению ---------------------------------
set "EXT=%~x1"
set "EXT=%EXT:~1%"
for %%i in ("!EXT!") do set "EXT=%%~i"

set "KIND="
for %%e in (ps1) do if /i "!EXT!"=="%%e" set "KIND=ps"
for %%e in (exe dll msi cab wlx wlx64) do if /i "!EXT!"=="%%e" set "KIND=pe"

if "!KIND!"=="" (
    echo [sign] ERROR: неподдерживаемое расширение '.%EXT%'
    echo        поддерживаются: .ps1 .exe .dll .msi .cab .wlx .wlx64
    exit /b 3
)

echo [sign] Подпись файла: %TARGET%
echo        тип: !KIND! / .%EXT%
echo        сертификаты: SHA1=!THUMBPRINT_SHA1!
echo                    SHA256=!THUMBPRINT_SHA256!
echo.

rem --- Подпись ---------------------------------------------------------------
if "!KIND!"=="ps" goto :sign_ps
if "!KIND!"=="pe" goto :sign_pe
goto :eof

rem ============================================================================
rem  PowerShell-скрипты (.ps1): ОДНА подпись SHA256 + метка времени TMS1.
rem  Set-AuthenticodeSignature использует Authenticode-протокол меток времени
rem  (НЕ RFC 3161), который НЕ понимает query-параметры в URL. Поэтому для .ps1
rem  берётся TMS1 (без ?alg=sha256) — сам хэш SHA256 задаётся через
rem  -HashAlgorithm SHA256, а сервер метки от DigiCert возвращает SHA256-метку
rem  по умолчанию. TMS2 (?alg=sha256) предназначен ТОЛЬКО для signtool /tr.
rem ============================================================================
:sign_ps
echo [sign] --- подпись: SHA256 ---
powershell -NoProfile -Command "$ErrorActionPreference='Stop'; $c = Get-ChildItem Cert:\%STORE%\My -CodeSigningCert -ErrorAction SilentlyContinue | Where-Object { $_.Thumbprint -eq '%THUMBPRINT_SHA256%' }; if (-not $c) { Write-Host '[sign] ERROR: SHA256-сертификат не найден'; exit 1 }; $params = @{ FilePath='%TARGET%'; Certificate=$c; HashAlgorithm='SHA256' }; if ('%TMS1%' -ne '') { $params.TimestampServer = '%TMS1%' }; $r = Set-AuthenticodeSignature @params; $r | Format-List Status, StatusMessage, TimeStamperCertificate, SignerCertificate; if ($r.Status -ne 'Valid') { exit 1 }"
if errorlevel 1 (
    echo [sign] ERROR: SHA256-подпись PowerShell не удалась.
    exit /b 5
)
echo.
echo [sign] OK: PowerShell-скрипт подписан (SHA256 + метка времени).
goto :verify

rem ============================================================================
rem  PE-файлы (.exe .dll .msi .cab .wlx .wlx64): два вызова signtool.exe.
rem
rem  Метки времени — ДВА разных протокола:
rem    sign_t1 = /t  TMS1  — Authenticode-протокол (для SHA1-подписи)
rem                          НЕ понимает query-параметры; /td НЕ используется.
rem    sign_t2 = /tr TMS2  — RFC 3161 (для SHA256-подписи)
rem                          понимает ?alg=sha256; требует /td sha256.
rem
rem  Подпись 1 (SHA1):   /sha1 SHA1  /fd sha1            %sign_t1%
rem  Подпись 2 (SHA256): /sha1 SHA256 /fd sha256 /as     /td sha256 %sign_t2%
rem  /as — добавить подпись к уже существующей (append signature).
rem ============================================================================
:sign_pe
if not exist "%SIGNTOOL%" (
    echo [sign] ERROR: signtool.exe не найден: %SIGNTOOL%
    exit /b 6
)

rem Ключи метки времени по протоколам.
set "sign_t1=/t %TMS1%"
set "sign_t2=/tr %TMS2%"

echo [sign] --- подпись 1/2: SHA1 (Authenticode timestamp^) ---
set "ARGS1=sign /sha1 %THUMBPRINT_SHA1% /fd sha1"
if defined TMS1 set "ARGS1=!ARGS1! %sign_t1%"
"%SIGNTOOL%" !ARGS1! "%TARGET%"
if errorlevel 1 (
    echo [sign] ERROR: signtool SHA1 завершился с ошибкой (код %errorlevel%^).
    exit /b 7
)

echo.
echo [sign] --- подпись 2/2: SHA256 (RFC 3161 timestamp, append^) ---
set "ARGS2=sign /sha1 %THUMBPRINT_SHA256% /fd sha256 /td sha256 /as"
if defined TMS2 set "ARGS2=!ARGS2! %sign_t2%"
"%SIGNTOOL%" !ARGS2! "%TARGET%"
if errorlevel 1 (
    echo [sign] ERROR: signtool SHA256 завершился с ошибкой (код %errorlevel%^).
    exit /b 7
)
echo.
echo [sign] OK: PE-файл подписан двумя подписями.
goto :verify

rem ============================================================================
rem  Проверка результата.
rem ============================================================================
:verify
echo.
echo [sign] Проверка подписи:
if "!KIND!"=="ps" (
    powershell -NoProfile -Command "$s = Get-AuthenticodeSignature '%TARGET%'; Write-Host 'Status:' $s.Status; Write-Host 'Signer:' $s.SignerCertificate.Subject; Write-Host 'TimeStamper:' $s.TimeStamperCertificate.Subject"
) else (
    "%SIGNTOOL%" verify /pa /all "%TARGET%"
)
if errorlevel 1 (
    echo [sign] WARN: проверка вернула ошибку — смотрите вывод выше.
    exit /b 8
)
echo.
echo [sign] Done.
endlocal
goto :eof

:usage
echo Использование: sign.cmd ^<файл^>
echo.
echo   файл — путь к подписываемому файлу (.ps1 .exe .dll .msi .cab)
echo.
echo Логика:
echo   .ps1               — одна подпись SHA256 + метка времени TMS2
echo   PE-файлы           — двойная подпись (SHA1+TMS1, SHA256+TMS2^)
echo.
echo Примеры:
echo   sign.cmd .scan-pkg.ps1
echo   sign.cmd app.exe
echo.
echo Переменные окружения:
echo   SIGNTOOL         — путь к signtool.exe (по умолчанию %SIGNTOOL%)
echo   STORE            — хранилище сертификата (по умолчанию %STORE%)
echo   THUMBPRINT_SHA1  — отпечаток SHA1-сертификата
echo   THUMBPRINT_SHA256— отпечаток SHA256-сертификата
echo   TMS1             — сервер метки времени для SHA1 (пусто = без метки)
echo   TMS2             — сервер метки времени для SHA256 (пусто = без метки)
endlocal
exit /b 1
