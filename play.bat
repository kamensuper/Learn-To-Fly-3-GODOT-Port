@echo off
cd /d "%~dp0"
"%~dp0..\Tools\Godot\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe" --path "%~dp0" StartScreen.tscn %*
