@echo off
chcp 65001 >nul
echo ========================================
echo   SiLA2 性能优化测试
echo ========================================
echo.

cd TestConsole

echo 正在运行性能测试...
echo.

dotnet run --configuration Debug -- --performance

echo.
echo ========================================
echo 测试完成
echo ========================================
pause

