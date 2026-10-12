@echo off
rem ==========================================================================
rem  Makes this folder a git repository with the whole history of the HMI, or
rem  brings it up to the newest commits Claude left in _history\HMI.bundle.
rem  Double-click it after a batch of Claude's (whenever you want the history
rem  in Visual Studio's Git windows). Nothing in the folder is overwritten: the
rem  files are already the newest ones - only git's record of them is updated,
rem  and a change of your own that is not committed stays as it is.
rem ==========================================================================
setlocal
cd /d "%~dp0"
set "GIT="
where git >nul 2>nul && set "GIT=git"
if not defined GIT call :findvs "%ProgramFiles%\Microsoft Visual Studio"
if not defined GIT call :findvs "%ProgramFiles(x86)%\Microsoft Visual Studio"
if not defined GIT (
  echo Git was not found. Install Git for Windows, or Visual Studio 2019 or 2022 with its Git.
  goto end
)
if not exist "_history\HMI.bundle" (
  echo _history\HMI.bundle is missing.
  goto end
)
if not exist ".git" (
  "%GIT%" init -q || goto failed
  "%GIT%" symbolic-ref HEAD refs/heads/master
  "%GIT%" config core.autocrlf true
  "%GIT%" config core.filemode false
)
"%GIT%" fetch -q _history\HMI.bundle master || goto failed
"%GIT%" rev-parse -q --verify HEAD >nul 2>nul || goto take
"%GIT%" merge-base --is-ancestor HEAD FETCH_HEAD && goto take
echo Your master has commits that are not in Claude's history: nothing was changed.
echo Tell Claude, or keep your commits on a branch of your own.
goto end
:take
"%GIT%" reset -q FETCH_HEAD || goto failed
echo.
"%GIT%" log -1 --format="Up to date: %%h  %%s"
echo.
echo Changed and not committed (empty: none):
"%GIT%" status --short
goto end
:findvs
for /d %%V in ("%~1\*") do for /d %%E in ("%%~V\*") do if exist "%%~E\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe" set "GIT=%%~E\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe"
goto :eof
:failed
echo Something went wrong - see the lines above.
:end
pause
