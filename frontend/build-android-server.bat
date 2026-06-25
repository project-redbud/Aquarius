@echo off
echo ==========================================
echo  Aquarius Android Build - PRODUCTION
echo ==========================================

cd /d "%~dp0"

if not exist "src\environments\environment.server.ts" (
  echo.
  echo ERROR: src\environments\environment.server.ts not found!
  echo Create it with your production server URL:
  echo   export const environment = {
  echo     production: true,
  echo     apiBase: 'https://your-domain.com'
  echo   };
  echo.
  pause
  exit /b 1
)

echo.
echo [1/3] Copying server environment...
copy /Y src\environments\environment.server.ts src\environments\environment.prod.ts >nul

echo [2/3] Building Angular...
call npm run build -- --configuration production
if %errorlevel% neq 0 goto :error

echo [3/3] Copying Capacitor config...
copy /Y capacitor.config.json android\app\src\main\assets\capacitor.config.json >nul 2>nul

echo [4/4] Syncing with Capacitor...
call npx cap sync android
if %errorlevel% neq 0 goto :error

echo.
echo ==========================================
echo  Build complete! Opening Android Studio...
echo ==========================================
call npx cap open android
goto :end

:error
echo.
echo BUILD FAILED!
pause
exit /b 1

:end
