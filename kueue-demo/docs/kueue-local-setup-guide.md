# Kueue Local Setup Guide on Windows Laptop (Step by Step)

This guide is for my local development setup on Windows, using:

- Windows laptop
- Docker Desktop
- kubectl
- kind
- Kueue
- Local Kubernetes cluster

This guide starts from scratch and goes slowly.

---

## 1. Goal

The goal is to set up:

- a local Kubernetes cluster on my laptop
- Kueue installed in that cluster
- a simple test queue and test job
- a base environment for developing a Kueue UI

---

## 2. What I already need

Before starting, make sure these are installed:

- Docker Desktop
- kubectl
- kind

I already verified:

### Check kubectl

```powershell
kubectl version --client
```

Example output:

```text
Client Version: v1.34.1
Kustomize Version: v5.7.1
```

### Check Docker

```powershell
docker version
```

Docker should show both **Client** and **Server** sections.

### Check kind

```powershell
kind version
```

Example output:

```text
kind v0.31.0 go1.25.5 windows/amd64
```

---

## 3. Create local Kubernetes cluster

Run:

```powershell
kind create cluster --name kueue-dev
```

What this does:

- creates a local Kubernetes cluster using Docker
- creates one control-plane node
- configures kubectl context automatically

### Verify cluster

```powershell
kubectl get nodes
```

At first, node may briefly show `NotReady`.

Wait a little and run again:

```powershell
kubectl get nodes
```

Expected final result:

```text
NAME                      STATUS   ROLES           AGE   VERSION
kueue-dev-control-plane   Ready    control-plane   ...   ...
```

---

## 4. Install Kueue

Run:

```powershell
kubectl apply --server-side -f https://github.com/kubernetes-sigs/kueue/releases/download/v0.16.2/manifests.yaml
```

Then wait for the controller:

```powershell
kubectl wait deploy/kueue-controller-manager -n kueue-system --for=condition=available --timeout=5m
```

Expected result:

```text
deployment.apps/kueue-controller-manager condition met
```

---

## 5. Verify Kueue installation

### Check Kueue pod

```powershell
kubectl get pods -n kueue-system
```

Expected output similar to:

```text
NAME                                       READY   STATUS    RESTARTS   AGE
kueue-controller-manager-bb7778444-bk7g5   1/1     Running   0          ...
```

### Check Kueue CRDs

```powershell
kubectl get crd | findstr kueue
```

Expected output includes entries such as:

- admissionchecks.kueue.x-k8s.io
- clusterqueues.kueue.x-k8s.io
- localqueues.kueue.x-k8s.io
- resourceflavors.kueue.x-k8s.io
- workloads.kueue.x-k8s.io

This confirms Kueue is installed correctly.

---

## 6. Create a demo folder

In my working folder, create a folder for YAML files:

```powershell
mkdir .\kueue-demo
cd .\kueue-demo
```

---

## 7. Create ResourceFlavor

Create file: `flavor.yaml`

```yaml
apiVersion: kueue.x-k8s.io/v1beta1
kind: ResourceFlavor
metadata:
  name: default
```

Apply it:

```powershell
kubectl apply -f .\flavor.yaml
```

Verify:

```powershell
kubectl get resourceflavors
```

---

## 8. Create ClusterQueue

Create file: `clusterqueue.yaml`

```yaml
apiVersion: kueue.x-k8s.io/v1beta1
kind: ClusterQueue
metadata:
  name: cluster-queue
spec:
  namespaceSelector: {}
  resourceGroups:
    - coveredResources: ["cpu", "memory"]
      flavors:
        - name: default
          resources:
            - name: cpu
              nominalQuota: 4
            - name: memory
              nominalQuota: 8Gi
```

Apply it:

```powershell
kubectl apply -f .\clusterqueue.yaml
```

Verify:

```powershell
kubectl get clusterqueues
```

---

## 9. Create LocalQueue

Create file: `localqueue.yaml`

```yaml
apiVersion: kueue.x-k8s.io/v1beta1
kind: LocalQueue
metadata:
  name: user-queue
  namespace: default
spec:
  clusterQueue: cluster-queue
```

Apply it:

```powershell
kubectl apply -f .\localqueue.yaml
```

Verify:

```powershell
kubectl get localqueues -A
```

---

## 10. Create test Job

Create file: `job.yaml`

```yaml
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
          command: ["sh", "-c", "sleep 20"]
          resources:
            requests:
              cpu: "1"
              memory: "128Mi"
```

Apply it:

```powershell
kubectl apply -f .\job.yaml
```

---

## 11. Verify workload creation

Check workload:

```powershell
kubectl get workloads -A
```

Check jobs:

```powershell
kubectl get jobs
```

Describe workload:

```powershell
kubectl describe workload test-job
```

What should happen:

- the Job creates a Workload
- Kueue evaluates the Workload
- if quota is available, Kueue admits it
- the Job becomes unsuspended and runs

---

## 12. Useful commands for learning Kueue

### View ClusterQueues

```powershell
kubectl get clusterqueues
kubectl describe clusterqueue cluster-queue
kubectl get clusterqueues -o yaml
```

### View LocalQueues

```powershell
kubectl get localqueues -A
kubectl describe localqueue -n default user-queue
kubectl get localqueues -A -o yaml
```

### View Workloads

```powershell
kubectl get workloads -A
kubectl describe workload test-job
kubectl get workloads -A -o yaml
```

### View Job

```powershell
kubectl get jobs
kubectl describe job test-job
```

---

## 13. View raw Kubernetes API

This is very useful for future UI development.

Start proxy:

```powershell
kubectl proxy
```

Then open in browser:

### ClusterQueues

```text
http://localhost:8001/apis/kueue.x-k8s.io/v1beta1/clusterqueues
```

### LocalQueues

```text
http://localhost:8001/apis/kueue.x-k8s.io/v1beta1/localqueues
```

### Workloads

```text
http://localhost:8001/apis/kueue.x-k8s.io/v1beta1/workloads
```

This raw JSON is what a backend or UI can read and display.

---

## 14. If something goes wrong

### Cluster issue

```powershell
kubectl get nodes
kubectl get pods -A
kubectl cluster-info
docker ps
```

### Kueue issue

```powershell
kubectl get pods -n kueue-system
kubectl describe pod -n kueue-system <pod-name>
```

### Queue / workload issue

```powershell
kubectl get clusterqueues
kubectl get localqueues -A
kubectl get workloads -A
kubectl describe workload test-job
```

---

## 15. Clean up everything

If I want to delete the cluster completely:

```powershell
kind delete cluster --name kueue-dev
```

This removes the entire local Kubernetes cluster.

---

## 16. What I have at the end

At the end of this setup, I will have:

- local Kubernetes running on laptop
- Kueue installed
- one ResourceFlavor
- one ClusterQueue
- one LocalQueue
- one test Job
- one test Workload

This is enough to start building a basic Kueue UI.

---

## 17. Recommended next step for UI work

Build a small read-only UI first that shows:

- ClusterQueues
- LocalQueues
- Workloads
- Workload details

Do not start with edit/create screens first.

---

## 18. My immediate next command sequence

Run these in order:

```powershell
kubectl apply -f .\flavor.yaml
kubectl apply -f .\clusterqueue.yaml
kubectl apply -f .\localqueue.yaml
kubectl apply -f .\job.yaml
kubectl get clusterqueues
kubectl get localqueues -A
kubectl get workloads -A
kubectl get jobs
kubectl describe workload test-job
```
