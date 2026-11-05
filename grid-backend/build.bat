@echo off
cd /d "%~dp0.."
echo Building SCP grid backend...
cargo build --release --manifest-path grid-backend\Cargo.toml
if errorlevel 1 pause & exit /b 1
copy /y target\release\scp-grid-backend.exe scp-grid-backend.exe
echo Built scp-grid-backend.exe
pause
