@echo off
echo ========================================
echo  Testing Shin-chan Notification API
echo ========================================
echo.

echo [1] Health check...
curl -s http://127.0.0.1:8000/health
echo.
echo.

echo [2] Sending test text message...
curl -s -X POST http://127.0.0.1:8000/api/send -H "Content-Type: application/json" -d "{\"type\":\"text\",\"content\":\"Hello from test script!\"}"
echo.
echo.

echo [3] Test complete!
echo.
echo Open http://127.0.0.1:8000/ in your browser to use the Web UI.
echo.
pause
