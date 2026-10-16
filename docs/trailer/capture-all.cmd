@echo off
rem One Unity process per trailer shot (keeps GPU memory low). Output: Builds\trailer\v2
set U=D:\Unity\6000.5.1f1\Editor\Unity.exe
set P=E:\ball-battle\BallBattleUnity
set S=E:\ball-battle\docs\trailer\trailer-spec.txt
set O=E:\ball-battle\Builds\trailer\v2
for %%N in (01_ember_mantis 02_mantis_wrecker 03_sparkler_mirror 04_spore_hive 05_bgl_unity 06_boss_shellking 07_boss_warden 08_boss_mimic 09_ko_slowmo) do (
  "%U%" -batchmode -force-d3d11 -projectPath %P% -executeMethod BallBattle.EditorTools.TrailerCapture.CaptureAndExit -bbTrailerSpec %S% -bbTrailerOut %O% -bbTrailerOnly %%N -logFile E:\trailer-v2-%%N.log
)
echo done > %O%\ALL_DONE.txt
