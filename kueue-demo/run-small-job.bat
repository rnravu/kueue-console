@echo off
echo Running SMALL job...

kubectl apply -f jobs/job-small.yaml

echo.
echo Job submitted to small-queue
pause