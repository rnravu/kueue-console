Write-Host "Creating demo namespace..."
kubectl create namespace demo --dry-run=client -o yaml | kubectl apply -f -

Write-Host "Creating ClusterQueues..."
kubectl apply -f queues/clusterqueues.yaml

Write-Host "Creating LocalQueues..."
kubectl apply -f queues/localqueues.yaml

Write-Host "Submitting jobs..."

for ($i=1; $i -le 5; $i++) {
    kubectl apply -f jobs/job-small.yaml
}

for ($i=1; $i -le 3; $i++) {
    kubectl apply -f jobs/job-medium.yaml
}

for ($i=1; $i -le 2; $i++) {
    kubectl apply -f jobs/job-large.yaml
}

Write-Host "Demo jobs submitted!"