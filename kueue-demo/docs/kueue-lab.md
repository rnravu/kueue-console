# Kueue Local Lab Guide

This guide documents a **complete local Kueue lab environment** for
learning and UI development.

Environment used:

-   Windows laptop
-   Docker Desktop
-   kubectl
-   kind
-   Kueue
-   Local Kubernetes cluster

------------------------------------------------------------------------

# 1. Verify Required Tools

## kubectl

``` powershell
kubectl version --client
```

## Docker

``` powershell
docker version
```

Ensure Docker shows both **Client** and **Server** sections.

## kind

``` powershell
kind version
```

Example:

    kind v0.31.0

------------------------------------------------------------------------

# 2. Create Kubernetes Cluster

``` powershell
kind create cluster --name kueue-dev
```

Verify:

``` powershell
kubectl get nodes
```

Expected:

    kueue-dev-control-plane   Ready

------------------------------------------------------------------------

# 3. Install Kueue

``` powershell
kubectl apply --server-side -f https://github.com/kubernetes-sigs/kueue/releases/download/v0.16.2/manifests.yaml
```

Wait for controller:

``` powershell
kubectl wait deploy/kueue-controller-manager -n kueue-system --for=condition=available --timeout=5m
```

------------------------------------------------------------------------

# 4. Verify Kueue

``` powershell
kubectl get pods -n kueue-system
```

Check CRDs:

``` powershell
kubectl get crd | findstr kueue
```

Important CRDs:

-   resourceflavors.kueue.x-k8s.io
-   clusterqueues.kueue.x-k8s.io
-   localqueues.kueue.x-k8s.io
-   workloads.kueue.x-k8s.io

------------------------------------------------------------------------

# 5. Create ResourceFlavor

File: `flavor.yaml`

``` yaml
apiVersion: kueue.x-k8s.io/v1beta1
kind: ResourceFlavor
metadata:
  name: default
```

Apply:

``` powershell
kubectl apply -f flavor.yaml
```

------------------------------------------------------------------------

# 6. Create ClusterQueue

File: `clusterqueue.yaml`

``` yaml
apiVersion: kueue.x-k8s.io/v1beta1
kind: ClusterQueue
metadata:
  name: cluster-queue
spec:
  namespaceSelector: {}
  resourceGroups:
    - coveredResources: ["cpu","memory"]
      flavors:
        - name: default
          resources:
            - name: cpu
              nominalQuota: 4
            - name: memory
              nominalQuota: 8Gi
```

Apply:

``` powershell
kubectl apply -f clusterqueue.yaml
```

Verify:

``` powershell
kubectl get clusterqueues
```

------------------------------------------------------------------------

# 7. Create LocalQueue

File: `localqueue.yaml`

``` yaml
apiVersion: kueue.x-k8s.io/v1beta1
kind: LocalQueue
metadata:
  name: user-queue
  namespace: default
spec:
  clusterQueue: cluster-queue
```

Apply:

``` powershell
kubectl apply -f localqueue.yaml
```

Verify:

``` powershell
kubectl get localqueues -A
```

------------------------------------------------------------------------

# 8. Create Test Job

File: `job.yaml`

``` yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: test-job
  labels:
    kueue.x-k8s.io/queue-name: user-queue
spec:
  suspend: true
  template:
    spec:
      restartPolicy: Never
      containers:
      - name: test
        image: busybox
        command: ["sh","-c","sleep 20"]
        resources:
          requests:
            cpu: "1"
            memory: "128Mi"
```

Apply:

``` powershell
kubectl apply -f job.yaml
```

------------------------------------------------------------------------

# 9. Verify Workload Admission

``` powershell
kubectl get workloads -A
kubectl get jobs
```

Expected lifecycle:

    Job created
     → Workload created
     → Queue evaluated
     → Workload admitted
     → Job runs
     → Job completes

Inspect workload:

``` powershell
kubectl describe workload <workload-name>
```

Full YAML:

``` powershell
kubectl get workloads -A -o yaml
```

------------------------------------------------------------------------

# 10. Raw API (Useful for UI Development)

Start proxy:

``` powershell
kubectl proxy
```

Open:

ClusterQueues

    http://localhost:8001/apis/kueue.x-k8s.io/v1beta1/clusterqueues

LocalQueues

    http://localhost:8001/apis/kueue.x-k8s.io/v1beta1/localqueues

Workloads

    http://localhost:8001/apis/kueue.x-k8s.io/v1beta1/workloads

------------------------------------------------------------------------

# 11. Useful Debug Commands

Cluster health:

``` powershell
kubectl get nodes
kubectl get pods -A
kubectl cluster-info
```

Queues:

``` powershell
kubectl get clusterqueues
kubectl get localqueues -A
```

Workloads:

``` powershell
kubectl get workloads -A
kubectl describe workload <name>
```

Jobs:

``` powershell
kubectl get jobs
kubectl describe job <name>
```

------------------------------------------------------------------------

# 12. Cleanup

Delete the cluster:

``` powershell
kind delete cluster --name kueue-dev
```

------------------------------------------------------------------------

# Result

You now have a full **local Kueue lab environment**:

-   Kubernetes cluster
-   Kueue controller
-   ResourceFlavor
-   ClusterQueue
-   LocalQueue
-   Job → Workload lifecycle

This environment is ready for:

-   Kueue experimentation
-   backend development
-   Kueue UI development
