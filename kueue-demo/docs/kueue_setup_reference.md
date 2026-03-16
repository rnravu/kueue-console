# Kueue Local Setup & Test Guide (Windows Laptop)

This document records the **complete steps used to install and test
Kueue locally** on a Windows laptop.

Environment used:

-   Windows laptop (Snapdragon X Elite)
-   32 GB RAM
-   Docker Desktop
-   kubectl
-   kind
-   Local Kubernetes cluster

------------------------------------------------------------------------

# 1. Verify Required Tools

## Check kubectl

``` powershell
kubectl version --client
```

Example output:

    Client Version: v1.34.1

------------------------------------------------------------------------

## Check Docker

``` powershell
docker version
```

Docker must show both **Client** and **Server** sections.

------------------------------------------------------------------------

## Check kind

``` powershell
kind version
```

Example:

    kind v0.31.0

------------------------------------------------------------------------

# 2. Create Local Kubernetes Cluster

Create a cluster named `kueue-dev`.

``` powershell
kind create cluster --name kueue-dev
```

Verify cluster:

``` powershell
kubectl get nodes
```

Expected:

    kueue-dev-control-plane   Ready

------------------------------------------------------------------------

# 3. Install Kueue

Install Kueue controller and CRDs.

``` powershell
kubectl apply --server-side -f https://github.com/kubernetes-sigs/kueue/releases/download/v0.16.2/manifests.yaml
```

Wait for controller:

``` powershell
kubectl wait deploy/kueue-controller-manager -n kueue-system --for=condition=available --timeout=5m
```

------------------------------------------------------------------------

# 4. Verify Kueue Installation

Check controller pod:

``` powershell
kubectl get pods -n kueue-system
```

Example:

    kueue-controller-manager-xxxxx   Running

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

Create file `flavor.yaml`

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

Verify:

``` powershell
kubectl get resourceflavors
```

------------------------------------------------------------------------

# 6. Create ClusterQueue

Create `clusterqueue.yaml`

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

Create `localqueue.yaml`

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

Create `job.yaml`

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

Check workloads:

``` powershell
kubectl get workloads -A
```

Example:

    NAMESPACE   NAME                 QUEUE        RESERVED IN     ADMITTED   FINISHED
    default     job-test-job-xxxx    user-queue   cluster-queue   True       True

Check jobs:

``` powershell
kubectl get jobs
```

Example:

    NAME       STATUS     COMPLETIONS
    test-job   Complete   1/1

------------------------------------------------------------------------

# 10. Inspect Workload

``` powershell
kubectl describe workload job-test-job-xxxx
```

Full YAML:

``` powershell
kubectl get workloads -A -o yaml
```

------------------------------------------------------------------------

# 11. View Raw Kueue API (Useful for UI Development)

Start proxy:

``` powershell
kubectl proxy
```

Open in browser:

ClusterQueues

    http://localhost:8001/apis/kueue.x-k8s.io/v1beta1/clusterqueues

LocalQueues

    http://localhost:8001/apis/kueue.x-k8s.io/v1beta1/localqueues

Workloads

    http://localhost:8001/apis/kueue.x-k8s.io/v1beta1/workloads

These endpoints show the **exact data model a UI or backend would
read**.

------------------------------------------------------------------------

# 12. Cleanup (Optional)

Delete cluster:

``` powershell
kind delete cluster --name kueue-dev
```

------------------------------------------------------------------------

# Result

The following components were successfully tested:

-   Local Kubernetes cluster
-   Kueue controller
-   ResourceFlavor
-   ClusterQueue
-   LocalQueue
-   Job → Workload lifecycle
-   Workload admission
-   Job execution

This environment is now ready for **Kueue UI development**.
