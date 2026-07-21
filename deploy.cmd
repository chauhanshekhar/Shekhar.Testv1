trading-api-paper.tar@echo off
setlocal

set SERVER=ubuntu@13.206.229.15
set KEY=%USERPROFILE%\Downloads\trading-key.pem

echo Building API...
docker build -t trading-api-paper:latest -f src\Trading.API\Dockerfile src
if errorlevel 1 goto error

echo Building Worker...
docker build -t trading-worker-paper:latest -f src\Trading.Worker\Dockerfile src
if errorlevel 1 goto error

echo Saving Images...
docker save trading-api-paper:latest -o trading-api-paper.tar
docker save trading-worker-paper:latest -o trading-worker-paper.tar

echo Uploading...
scp -i "%KEY%" trading-api-paper.tar %SERVER%:/home/ubuntu/trading/
if errorlevel 1 goto error

scp -i "%KEY%" trading-worker-paper.tar %SERVER%:/home/ubuntu/trading/
if errorlevel 1 goto error

echo Deploying...

ssh -i "%KEY%" %SERVER% "sudo docker load -i /home/ubuntu/trading/trading-api-paper.tar"

ssh -i "%KEY%" %SERVER% "sudo docker load -i /home/ubuntu/trading/trading-worker-paper.tar"

ssh -i "%KEY%" %SERVER% "sudo docker stop trading-api-paper || true"
ssh -i "%KEY%" %SERVER% "sudo docker rm trading-api-paper || true"

ssh -i "%KEY%" %SERVER% "sudo docker stop trading-worker-paper || true"
ssh -i "%KEY%" %SERVER% "sudo docker rm trading-worker-paper || true"

ssh -i "%KEY%" %SERVER% "sudo docker run -d --name trading-api-paper --restart unless-stopped --network trading-network -p 8081:8080 -e ASPNETCORE_ENVIRONMENT=Production -e ASPNETCORE_URLS=http://+:8080 -e ConnectionStrings__DefaultConnection='Server=sqlserver;Database=TradingDB_Paper;User Id=sa;Password=Trade@123456;TrustServerCertificate=True;Encrypt=False;' trading-api-paper:latest"

ssh -i "%KEY%" %SERVER% "sudo docker run -d --name trading-worker-paper --restart unless-stopped --network trading-network -e DOTNET_ENVIRONMENT=Production -e ConnectionStrings__DefaultConnection='Server=sqlserver;Database=TradingDB_Paper;User Id=sa;Password=Trade@123456;TrustServerCertificate=True;Encrypt=False;' trading-worker-paper:latest"

ssh -i "%KEY%" %SERVER% "rm -f /home/ubuntu/trading/trading-api-paper.tar"
ssh -i "%KEY%" %SERVER% "rm -f /home/ubuntu/trading/trading-worker-paper.tar"

ssh -i "%KEY%" %SERVER% "sudo docker ps"

del trading-api-paper.tar
del trading-worker-paper.tar

echo.
echo ==========================
echo DEPLOY SUCCESSFUL
echo ==========================
pause
exit /b

:error
echo.
echo DEPLOY FAILED
pause