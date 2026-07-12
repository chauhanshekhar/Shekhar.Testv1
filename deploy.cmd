@echo off
setlocal

set SERVER=ubuntu@13.206.229.15
set KEY=%USERPROFILE%\Downloads\trading-key.pem

echo Building API...
docker build -t trading-api:latest -f src\Trading.API\Dockerfile src
if errorlevel 1 goto error

echo Building Worker...
docker build -t trading-worker:latest -f src\Trading.Worker\Dockerfile src
if errorlevel 1 goto error

echo Saving Images...
docker save trading-api:latest -o trading-api.tar
docker save trading-worker:latest -o trading-worker.tar

echo Uploading...
scp -i "%KEY%" trading-api.tar %SERVER%:/home/ubuntu/trading/
if errorlevel 1 goto error

scp -i "%KEY%" trading-worker.tar %SERVER%:/home/ubuntu/trading/
if errorlevel 1 goto error

echo Deploying...

ssh -i "%KEY%" %SERVER% "sudo docker load -i /home/ubuntu/trading/trading-api.tar"

ssh -i "%KEY%" %SERVER% "sudo docker load -i /home/ubuntu/trading/trading-worker.tar"

ssh -i "%KEY%" %SERVER% "sudo docker stop trading-api || true"
ssh -i "%KEY%" %SERVER% "sudo docker rm trading-api || true"

ssh -i "%KEY%" %SERVER% "sudo docker stop trading-worker || true"
ssh -i "%KEY%" %SERVER% "sudo docker rm trading-worker || true"

ssh -i "%KEY%" %SERVER% "sudo docker run -d --name trading-api --restart unless-stopped --network trading-network -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Production -e ASPNETCORE_URLS=http://+:8080 -e ConnectionStrings__DefaultConnection='Server=sqlserver;Database=TradingDB;User Id=sa;Password=Trade@123456;TrustServerCertificate=True;Encrypt=False;' trading-api:latest"

ssh -i "%KEY%" %SERVER% "sudo docker run -d --name trading-worker --restart unless-stopped --network trading-network -e DOTNET_ENVIRONMENT=Production -e ConnectionStrings__DefaultConnection='Server=sqlserver;Database=TradingDB;User Id=sa;Password=Trade@123456;TrustServerCertificate=True;Encrypt=False;' trading-worker:latest"

ssh -i "%KEY%" %SERVER% "rm -f /home/ubuntu/trading/trading-api.tar"
ssh -i "%KEY%" %SERVER% "rm -f /home/ubuntu/trading/trading-worker.tar"

ssh -i "%KEY%" %SERVER% "sudo docker ps"

del trading-api.tar
del trading-worker.tar

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