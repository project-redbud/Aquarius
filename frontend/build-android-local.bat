@echo off
echo ==========================================
echo  Aquarius Android Build - LOCALHOST
echo  API: http://192.168.5.14:5185
echo ==========================================

cd /d "%~dp0"

echo.
echo [1/3] Copying local environment...
copy /Y src\environments\environment.local.ts src\environments\environment.ts >nul

echo [2/3] Building Angular...
call npm run build -- --configuration production
if %errorlevel% neq 0 goto :error

echo [3/3] Syncing with Capacitor...
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
