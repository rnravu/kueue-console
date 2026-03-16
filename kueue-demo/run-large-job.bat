@echo off
echo Running LARGE job...

kubectl apply -f jobs/job-large.yaml

echo.
echo Job submitted to large-queue
pause