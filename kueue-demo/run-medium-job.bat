@echo off
echo Running MEDIUM job...

kubectl apply -f jobs/job-medium.yaml

echo.
echo Job submitted to medium-queue
pause