@echo off
echo Creating job in default namespace using LocalQueue user-queue...
kubectl create -f job-user-queue.yaml
echo.
echo Current jobs:
kubectl get jobs -n default
echo.
echo Current workloads:
kubectl get workloads -n default
pause