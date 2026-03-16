# AI Prompt Library — Kueue Console

This document contains recommended prompts for AI assistants when developing the Kueue Console project.

All prompts assume the AI has access to:

project-plan.md  
ai-dev-instructions.md

AI must follow those documents when generating code.

---

# 1. Create Basic Project Structure

Prompt:

Create the initial ASP.NET Core Razor Pages project structure for Kueue Console according to project-plan.md.

Include:

Models folder  
Services folder  
Pages folder

Add basic Program.cs configuration and dependency injection for services.

---

# 2. Create Kubernetes Client Service

Prompt:

Implement KueueService that creates a Kubernetes client using KubernetesClientConfiguration.BuildConfigFromConfigFile().

The service should be registered using dependency injection.

Do not implement workload logic yet.

Follow ai-dev-instructions.md.

---

# 3. Implement Workload Service

Prompt:

Create WorkloadService that reads Kueue workloads using the Kubernetes client.

Use CRD:

kueue.x-k8s.io  
v1beta1  
workloads

Convert the CRD response into WorkloadRow models.

Do not return raw JSON.

---

# 4. Create WorkloadRow Model

Prompt:

Create a model called WorkloadRow for UI display.

Fields should include:

Name  
Namespace  
Queue  
ReservedIn  
Admitted  
Finished  
Age

This model should be used by Razor pages.

---

# 5. Create Workloads Page

Prompt:

Create Razor Page:

Pages/Workloads.cshtml  
Pages/Workloads.cshtml.cs

The page should call WorkloadService and display workloads in a simple HTML table.

Columns:

Namespace  
Name  
Queue  
Admitted  
Finished  
Age

---

# 6. Add Navigation Layout

Prompt:

Add a shared layout page with a left sidebar navigation.

Links:

Dashboard  
Workloads  
Local Queues  
Cluster Queues

Use simple HTML and Bootstrap styling.

Do not add complex JavaScript frameworks.

---

# 7. Implement LocalQueue Service

Prompt:

Create LocalQueueService that reads Kueue LocalQueues from Kubernetes.

CRD:

kueue.x-k8s.io  
v1beta1  
localqueues

Convert the data to LocalQueueRow model.

---

# 8. Create LocalQueues Page

Prompt:

Create Razor page:

Pages/LocalQueues.cshtml

Display table with:

Namespace  
Queue Name  
ClusterQueue  
Pending Workloads  
Admitted Workloads

Use LocalQueueService.

---

# 9. Implement ClusterQueue Service

Prompt:

Create ClusterQueueService that reads ClusterQueue CRDs.

CRD:

kueue.x-k8s.io  
v1beta1  
clusterqueues

Map to ClusterQueueRow model.

---

# 10. Create ClusterQueues Page

Prompt:

Create Razor page that displays ClusterQueue information.

Columns:

Queue Name  
Cohort  
Pending Workloads

Use ClusterQueueService.

---

# 11. Implement Dashboard Page

Prompt:

Create Dashboard page showing summary metrics:

Total Workloads  
Admitted Workloads  
Pending Workloads  
Finished Workloads

These metrics should come from WorkloadService.

---

# 12. Implement Workload Detail Page

Prompt:

Create page:

Pages/WorkloadDetail.cshtml

URL format:

/workload/{namespace}/{name}

Display:

Metadata  
Queue  
Admission conditions  
Raw YAML

---

# 13. Add YAML Viewer

Prompt:

Add a section on WorkloadDetail page that displays raw workload YAML in a readable format.

Use preformatted HTML.

Do not add external libraries.

---

# 14. Add Auto Refresh

Prompt:

Add simple auto refresh to workload tables every 30 seconds.

Use minimal JavaScript.

Avoid large client frameworks.

---

# 15. Improve Table Styling

Prompt:

Improve table styling using Bootstrap classes.

Add:

striped rows  
hover effects  
compact layout

Do not introduce complex UI frameworks.

---

# 16. Improve Error Handling

Prompt:

Add proper error handling in services.

If Kubernetes calls fail:

log error  
return empty list  
display message in UI

---

# 17. Improve Performance

Prompt:

Add optional caching for workload data using memory cache.

Cache duration:

10 seconds.

---

# 18. Debug Kubernetes Data

Prompt:

Create debug page that shows raw JSON returned from Kubernetes workloads API.

This helps inspect CRD structure.

---

# 19. Add Logging

Prompt:

Add structured logging to all services using ILogger.

Log:

service start  
kubernetes request  
error conditions

---

# 20. Final UI Polish

Prompt:

Improve layout to look like a modern dashboard.

Add:

header  
sidebar navigation  
content container  
clean spacing

Do not introduce React or SPA frameworks.

---

# 21. Future Features

Prompt:

Propose future enhancements for Kueue Console such as:

quota visualization  
multi cluster support  
metrics integration  
job submission UI

But do not implement them yet.