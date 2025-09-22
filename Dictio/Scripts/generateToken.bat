@echo off
setlocal enabledelayedexpansion

REM Run twitch token command and capture output
set "LINE="
for /f "delims=" %%L in ('twitch token -u -s "chat:read chat:write chat:edit moderator:read:followers user:read:chat user:read:emotes" 2^>^&1') do (
    echo %%L
    echo %%L | findstr /c:"User Access Token:" >nul
    if not errorlevel 1 (
        set "LINE=%%L"
    )
)

if defined LINE (
    REM Remove everything up to the colon and space
    set "TOKEN=!LINE:*: =!"
    set "TWITCH_TOKEN=!TOKEN!"

    REM Set as user environment variable (persistent)
    setx TwitchChat "!TWITCH_TOKEN!" >nul

    echo.
    echo Captured token: !TWITCH_TOKEN!
    echo Token has been saved to your user environment variables.
) else (
    echo.
    echo Token line not found.
)

pause
