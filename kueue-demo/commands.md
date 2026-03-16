# kueue-demo: Quick commands

Short list of kubectl commands to check Kueue CRDs, apply the demo resources, and verify status.

## Check Kueue API / CRDs

PowerShell:

```powershell
kubectl api-resources | Select-String -Pattern kueue
kubectl get crd | Select-String -Pattern kueue
```

Bash:

```bash
kubectl api-resources | grep -i kueue
kubectl get crd | grep -i kueue
```

## Apply demo YAMLs (recommended order)

```bash
kubectl apply -f kueue-demo/flavor.yaml
kubectl apply -f kueue-demo/clusterqueue.yaml
kubectl apply -f kueue-demo/localqueue.yaml
kubectl apply -f kueue-demo/job.yaml
```

## Verify created objects and Job

```bash
kubectl get resourceflavors
kubectl get clusterqueues
kubectl get localqueues -n default
kubectl get jobs test-job -n default -o yaml
kubectl get workloads -n default
kubectl describe job test-job -n default
kubectl get events -n default --sort-by='.metadata.creationTimestamp'
```

## Notes

- `job.yaml` sets `spec.suspend: true`. If Kueue is installed, the Kueue controller will admit and unsuspend the Job when resources are available. Do not manually unsuspend unless you're testing.
- To manually unsuspend (not recommended with Kueue):

```bash
kubectl patch job test-job -n default -p '{"spec":{"suspend":false}}'
```
