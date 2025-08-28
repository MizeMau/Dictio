@echo off
setlocal enabledelayedexpansion

REM Run twitch token and save output to a temp file
twitch token -u -s "chat:read chat:edit" > token_output.txt 2>&1

REM Print all lines to the console
type token_output.txt

REM Read the token line from the file
set "TOKEN="
for /f "delims=" %%L in ('findstr /c:"User Access Token:" token_output.txt') do set "LINE=%%L"

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
