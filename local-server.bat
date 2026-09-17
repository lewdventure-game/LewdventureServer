@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"

set "COMPOSE=docker compose -f deploy\compose\compose.yaml -f deploy\compose\compose.local.yaml"
set "COMMAND=%~1"
if "%COMMAND%"=="" set "COMMAND=start"

if /i "%COMMAND%"=="start" goto start
if /i "%COMMAND%"=="stop" goto stop
if /i "%COMMAND%"=="restart" goto restart
if /i "%COMMAND%"=="logs" goto logs
if /i "%COMMAND%"=="status" goto status
if /i "%COMMAND%"=="reset" goto reset

echo Использование: local-server.bat [start^|stop^|restart^|logs^|status^|reset]
echo   start    собрать и поднять api + MongoDB, дождаться готовности (по умолчанию)
echo   stop     остановить, данные Mongo сохраняются
echo   restart  пересобрать api из текущего кода и перезапустить
echo   logs     логи api в реальном времени
echo   status   состояние контейнеров и health
echo   reset    остановить и удалить данные Mongo и кэш конфигов
exit /b 2

:start
call :ensure_credentials || exit /b 1
call :ensure_docker || exit /b 1
call :ensure_ports || exit /b 1
echo [local] Сборка и запуск api и MongoDB, первый запуск займёт несколько минут...
%COMPOSE% up -d --build --wait --wait-timeout 300
if errorlevel 1 goto failed
call :wait_ready || goto failed
call :print_info
exit /b 0

:restart
call :ensure_credentials || exit /b 1
call :ensure_docker || exit /b 1
echo [local] Пересборка api...
%COMPOSE% up -d --build --wait --wait-timeout 300 api
if errorlevel 1 goto failed
call :wait_ready || goto failed
call :print_info
exit /b 0

:stop
call :ensure_docker || exit /b 1
%COMPOSE% down
exit /b %errorlevel%

:reset
call :ensure_docker || exit /b 1
choice /c YN /m "Удалить локальные данные Mongo и кэш конфигов"
if errorlevel 2 exit /b 1
%COMPOSE% down -v
exit /b %errorlevel%

:logs
call :ensure_docker || exit /b 1
%COMPOSE% logs -f --tail 200 api
exit /b %errorlevel%

:status
call :ensure_docker || exit /b 1
%COMPOSE% ps
curl -s http://127.0.0.1:9090/health
echo.
exit /b 0

:failed
echo.
echo [local] Не удалось поднять сервер. Последние логи api:
%COMPOSE% logs --tail 80 api
exit /b 1

:ensure_credentials
if exist "google-credentials.json" exit /b 0
if defined GOOGLE_CREDENTIALS_FILE if exist "%GOOGLE_CREDENTIALS_FILE%" exit /b 0
echo [local] Нет google-credentials.json в корне репозитория: без него конфиги не импортируются из Google Sheets.
echo [local] Положите ключ в корень или задайте GOOGLE_CREDENTIALS_FILE.
exit /b 1

:ensure_docker
docker info >nul 2>&1
if not errorlevel 1 exit /b 0
if not exist "%ProgramFiles%\Docker\Docker\Docker Desktop.exe" (
  echo [local] Docker не запущен и Docker Desktop не найден.
  exit /b 1
)
echo [local] Запуск Docker Desktop...
start "" "%ProgramFiles%\Docker\Docker\Docker Desktop.exe"
for /l %%i in (1,1,60) do (
  timeout /t 3 /nobreak >nul
  docker info >nul 2>&1 && exit /b 0
)
echo [local] Docker Desktop не запустился за 3 минуты.
exit /b 1

:ensure_ports
%COMPOSE% ps --status running --services 2>nul | findstr /x "api" >nul && exit /b 0
netstat -ano | findstr /r /c:"127.0.0.1:5000 .*LISTENING" /c:"0.0.0.0:5000 .*LISTENING" /c:"\[::\]:5000 .*LISTENING" /c:"\[::1\]:5000 .*LISTENING" >nul
if errorlevel 1 exit /b 0
echo [local] Порт 5000 уже занят другим процессом, например dotnet run сервера. Остановите его и повторите.
exit /b 1

:wait_ready
echo [local] Ожидание загрузки конфигов...
for /l %%i in (1,1,60) do (
  curl -sf http://127.0.0.1:9090/health/ready >nul 2>&1 && exit /b 0
  timeout /t 2 /nobreak >nul
)
echo [local] Сервер не стал готов за 2 минуты.
curl -s http://127.0.0.1:9090/health
echo.
exit /b 1

:print_info
echo.
echo [local] Сервер готов.
curl -s http://127.0.0.1:9090/health/ready
echo.
echo.
echo   API для Unity     http://localhost:5000   (в Unity: RunMode / Server / Local)
echo   Swagger           http://localhost:5000/swagger
echo   Health            http://127.0.0.1:9090/health
echo   MongoDB           mongodb://lewdventure_app:local-app-password@127.0.0.1:27017/?replicaSet=rs0^&authSource=lewdventure_local^&directConnection=true
echo   Публикация        curl -X POST -H "X-Config-Key: local-config-key" http://localhost:5000/api/config/publish
echo.
echo   Логи: local-server.bat logs    Остановить: local-server.bat stop
exit /b 0
