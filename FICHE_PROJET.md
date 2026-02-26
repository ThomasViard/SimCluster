# SimCluster - Fiche Recapitulative du Projet

## Vue d'ensemble

SimCluster est un **simulateur de cluster de calcul distribue** deploye sur **Kubernetes**.
Il demontre les concepts de : orchestration de conteneurs, autoscaling horizontal, repartition de charge, resilience, et monitoring temps reel.

**Stack technique** : .NET 9 (C#) / Blazor Server / Kubernetes (Minikube) / Prometheus / Grafana

---

## Architecture

```
┌───────────────────────────────────────────────────────────────────┐
│                        Kubernetes (Minikube)                      │
│                        Namespace: simcluster                      │
│                                                                   │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────────────┐ │
│  │   Master     │────>│  Worker 1   │     │     Dashboard       │ │
│  │  (1 pod)     │────>│  Worker 2   │     │   (Blazor Server)   │ │
│  │             │────>│  Worker N   │     │                     │ │
│  │  :8080       │     │  :5001      │     │  :8080 -> :3000     │ │
│  └──────┬───────┘     └──────┬──────┘     └────────┬────────────┘ │
│         │                    │                     │              │
│         │    /metrics        │    /metrics          │  /grafana/* │
│         v                    v                     v              │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────────────┐ │
│  │ Prometheus   │<────│  scrape     │     │     Grafana         │ │
│  │  :9090       │     │  auto-      │     │  :3001              │ │
│  │             │     │  discovery   │     │  (dashboards)       │ │
│  └──────┬───────┘     └─────────────┘     └─────────────────────┘ │
│         │                                         ^               │
│         └─────────────────────────────────────────┘               │
│                      datasource                                   │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  HPA (Horizontal Pod Autoscaler)                            │  │
│  │  Observe CPU/Memory des Workers -> Scale 2 a 10 pods        │  │
│  └─────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────┘
```

### Flux de donnees

1. L'utilisateur soumet une **tache** via le Dashboard ou l'API (`POST /api/task`)
2. Le **Master** met la tache en file d'attente (priority queue)
3. Le **TaskDispatcher** du Master assigne la tache a un Worker disponible (round-robin ou least-loaded)
4. Le **Worker** execute la tache (boucle de calcul CPU-intensive) et renvoie le resultat au Master
5. **Prometheus** scrape les metriques des pods toutes les 10s
6. **Grafana** affiche les dashboards temps reel, egalement integre dans le Dashboard Blazor
7. Le **HPA** observe via le Metrics Server la consommation CPU/memoire et scale les Workers

---

## Structure du projet

### Projets .NET (Solution SimCluster.sln)

| Projet | Role | Port | Fichier principal |
|--------|------|------|-------------------|
| **Common** | Librairie partagee (modeles, utils) | - | Models/, Utils/ |
| **Master** | Orchestrateur central, API REST, file de taches | 8080 | Program.cs |
| **Worker** | Executeur de taches, calcul CPU | 5001 | Program.cs |
| **Dashboard** | Interface web Blazor + proxy Grafana | 8080 (K8s) / 3000 (local) | Program.cs |

### Arborescence cle

```
SimCluster/
├── Common/
│   ├── Models/
│   │   ├── WorkerRegistrationRequest.cs   # Requete d'inscription d'un worker
│   │   ├── WorkerHeartbeatRequest.cs      # Heartbeat periodique
│   │   └── WorkerDisconnectRequest.cs     # Notification de deconnexion
│   └── Utils/
│       └── PrefixedConsoleWriter.cs       # Prefixage des logs [Master] / [Worker-X]
│
├── Master/
│   ├── Program.cs                         # Point d'entree + endpoint /metrics
│   ├── Controllers/
│   │   ├── MasterController.cs            # API workers (register, heartbeat, disconnect)
│   │   └── TaskController.cs              # API taches (submit, complete, fail, stats)
│   ├── Services/
│   │   ├── TaskQueue.cs                   # File de taches avec priorite
│   │   ├── TaskDispatcherService.cs       # Dispatche les taches aux workers (BackgroundService)
│   │   ├── WorkerRegistry.cs              # Registre des workers connectes
│   │   ├── WorkerMonitoringService.cs     # Detection des workers morts (timeout heartbeat)
│   │   ├── RoundRobinScheduler.cs         # Strategie de repartition round-robin
│   │   ├── LeastLoadedScheduler.cs        # Strategie least-loaded (moins charge)
│   │   └── DockerScalingService.cs        # Scaling Docker Compose (non utilise en K8s)
│   └── Models/
│       ├── TaskModel.cs                   # Modele d'une tache (nom, duree, priorite, statut)
│       └── WorkerInfo.cs                  # Info d'un worker (id, url, threads, heartbeat)
│
├── Worker/
│   ├── Program.cs                         # Point d'entree + endpoint /metrics + /health
│   ├── Controllers/
│   │   └── WorkerController.cs            # Recoit les taches du Master
│   ├── Services/
│   │   ├── WorkerService.cs               # Execute les taches (SimulateCpuWork)
│   │   ├── WorkerRegistrationService.cs   # S'inscrit aupres du Master au demarrage
│   │   ├── HeartbeatService.cs            # Envoie un heartbeat toutes les 5s
│   │   └── WorkerDisconnectNotificationService.cs  # Notifie le Master a l'arret
│   └── Models/
│       ├── WorkerState.cs                 # Etat des threads (busy/free/max) + compteurs
│       └── WorkerConfiguration.cs         # Config (workerId, workerUrl, masterUrl)
│
├── Dashboard/
│   ├── Program.cs                         # Blazor Server + reverse proxy /grafana/*
│   ├── Pages/
│   │   ├── Index.razor                    # Page principale (stats, workers, taches)
│   │   └── Monitoring.razor               # Page monitoring (iframes Grafana)
│   ├── Services/
│   │   ├── ClusterApiService.cs           # Client HTTP vers Master API
│   │   └── GrafanaSettings.cs             # Config URL Grafana
│   └── Shared/
│       ├── MainLayout.razor               # Layout principal
│       └── NavMenu.razor                  # Navigation (Dashboard, Monitoring)
│
├── k8s/                                   # Manifests Kubernetes (detail ci-dessous)
│   ├── deploy.ps1                         # Script de deploiement tout-en-un
│   └── *.yaml                             # 17 fichiers de config K8s
│
├── docker-compose.yml                     # Deploiement local (sans K8s)
├── SimCluster.sln                         # Solution .NET
└── DEMO_GUIDE.md                          # Guide de demo rapide
```

---

## Composants en detail

### 1. Master (Orchestrateur)

**Fichier** : `Master/Program.cs` + `Master/Controllers/` + `Master/Services/`

Le Master est le cerveau du cluster :
- **TaskQueue** : file d'attente avec priorite (High > Normal > Low). Les taches soumises y sont stockees.
- **TaskDispatcherService** : service en arriere-plan qui depile les taches et les envoie aux workers via HTTP POST.
- **WorkerRegistry** : maintient la liste des workers connectes avec leurs capacites (threads libres).
- **WorkerMonitoringService** : verifie les heartbeats ; si un worker n'envoie rien pendant 30s, il est marque offline.
- **Scheduler** : 2 strategies disponibles (configurable via env `SCHEDULER`) :
  - `round-robin` : distribue equitablement en alternance
  - `least-loaded` : choisit le worker avec le plus de threads libres

**Endpoint /metrics** : expose les metriques Prometheus du Master :
```
simcluster_tasks_pending          # Taches en attente
simcluster_tasks_running          # Taches en cours
simcluster_tasks_completed_total  # Total taches completees
simcluster_tasks_failed_total     # Total taches echouees
simcluster_workers_total          # Nombre de workers inscrits
simcluster_workers_available      # Workers disponibles
```

### 2. Worker (Executeur)

**Fichier** : `Worker/Program.cs` + `Worker/Services/WorkerService.cs`

Le Worker execute les taches de calcul :
- Au demarrage, il s'inscrit aupres du Master (`WorkerRegistrationService`)
- Envoie un **heartbeat** toutes les 5 secondes (`HeartbeatService`)
- Execute les taches dans `SimulateCpuWork()` : une **boucle de calcul CPU-intensive** (`Math.Sqrt` + `Math.Sin`) qui consomme du vrai CPU. C'est crucial pour que le HPA Kubernetes detecte la charge.
- Chaque worker a **4 threads** max (configurable via `MAX_THREADS`). Quand tous les threads sont occupes, les nouvelles taches sont rejetees.

**Pourquoi pas un simple `Task.Delay` ?** : `Task.Delay` ne consomme aucun CPU. Le HPA observe la consommation CPU reelle pour decider de scaler. Avec `SimulateCpuWork`, quand les workers sont charges, leur CPU monte a 100%, et le HPA ajoute des pods.

**Endpoint /metrics** : expose les metriques Prometheus du Worker :
```
simcluster_worker_busy_threads          # Threads actuellement occupes
simcluster_worker_free_threads          # Threads disponibles
simcluster_worker_max_threads           # Nombre max de threads
simcluster_worker_tasks_executed_total  # Total taches executees
simcluster_worker_tasks_failed_total    # Total taches echouees
process_cpu_seconds_total               # CPU consomme par le process
process_working_set_bytes               # Memoire utilisee
```

### 3. Dashboard (Interface web)

**Fichier** : `Dashboard/Program.cs` + `Dashboard/Pages/`

Application Blazor Server avec 2 pages :
- **Index** (`/`) : vue principale avec stats temps reel (polling 2s), liste des workers, liste des taches, formulaire de soumission, controles de scaling
- **Monitoring** (`/monitoring`) : 6 panels Grafana embarques via iframes

Le Dashboard inclut un **reverse proxy** (`/grafana/*`) qui relaie les requetes du navigateur vers le service Grafana interne au cluster. Cela permet d'afficher les graphiques Grafana directement dans le Dashboard sans exposer Grafana publiquement.

---

## Configuration Kubernetes (k8s/)

### Vue d'ensemble des fichiers

| Fichier | Type K8s | Role |
|---------|----------|------|
| `namespace.yaml` | Namespace | Cree le namespace `simcluster` qui isole toutes les ressources |
| `configmap.yaml` | ConfigMap | Variables d'environnement partagees entre les pods |
| `master-deployment.yaml` | Deployment | Deploie le pod Master (1 replique) |
| `master-service.yaml` | Service (ClusterIP) | Expose le Master en interne (`master-svc:8080`) |
| `worker-deployment.yaml` | Deployment | Deploie les pods Worker (2 repliques initiales) |
| `worker-service.yaml` | Service (ClusterIP) | Expose les Workers en interne (`worker-svc:5001`) |
| `dashboard-deployment.yaml` | Deployment | Deploie le Dashboard Blazor |
| `dashboard-service.yaml` | Service (ClusterIP) | Expose le Dashboard (`dashboard-svc:8080`) |
| `hpa-worker.yaml` | HorizontalPodAutoscaler | Autoscaling des Workers (2-10 pods) |
| `ingress.yaml` | Ingress | Routage HTTP externe (optionnel) |
| `prometheus-rbac.yaml` | ServiceAccount + RBAC | Permissions pour Prometheus de decouvrir les pods |
| `prometheus-config.yaml` | ConfigMap | Configuration de scraping Prometheus |
| `prometheus-deployment.yaml` | Deployment | Deploie Prometheus |
| `prometheus-service.yaml` | Service (ClusterIP) | Expose Prometheus (`prometheus-svc:9090`) |
| `grafana-config.yaml` | ConfigMap (x3) | Datasource Prometheus + dashboard JSON pre-configure |
| `grafana-deployment.yaml` | Deployment | Deploie Grafana |
| `grafana-service.yaml` | Service (ClusterIP) | Expose Grafana (`grafana-svc:3001`) |
| `kustomization.yaml` | Kustomization | Orchestre le deploiement de tous les fichiers ci-dessus |
| `deploy.ps1` | Script PowerShell | Automatise build, deploy, test, scale, etc. |

### Explication des fichiers cles

#### kustomization.yaml
```yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
namespace: simcluster          # Force toutes les ressources dans ce namespace
commonLabels:
  project: simcluster          # Label commun pour filtrer les ressources
resources:                     # Liste ordonnee de tous les manifests a appliquer
  - namespace.yaml
  - configmap.yaml
  - master-deployment.yaml
  # ... (17 fichiers au total)
```
**Kustomize** est un outil integre a `kubectl` qui permet de regrouper et personnaliser des manifests K8s. Au lieu de faire `kubectl apply -f` sur chaque fichier, on fait `kubectl apply -k k8s/` qui applique tout d'un coup dans le bon ordre.

#### configmap.yaml
```yaml
data:
  MASTER_URL: "http://master-svc:8080"   # URL interne du Master (DNS K8s)
  GRAFANA_URL: "http://grafana-svc:3001" # URL interne de Grafana
  HEARTBEAT_INTERVAL: "5"                # Workers envoient un heartbeat toutes les 5s
  WORKER_TIMEOUT: "30"                   # Master considere un worker mort apres 30s
  MAX_THREADS: "4"                       # Chaque worker a 4 threads (saturation rapide)
```
Un **ConfigMap** stocke des paires cle-valeur injectees comme variables d'environnement dans les pods. Cela permet de configurer l'application sans modifier les images Docker.

#### worker-deployment.yaml (points cles)
```yaml
spec:
  replicas: 2              # 2 workers au demarrage (le HPA ajuste ensuite)
  template:
    metadata:
      annotations:
        prometheus.io/scrape: "true"   # Dit a Prometheus de scraper ce pod
        prometheus.io/port: "5001"     # Sur ce port
        prometheus.io/path: "/metrics" # A cette URL
    spec:
      containers:
      - env:
        - name: WORKER_ID
          valueFrom:
            fieldRef:
              fieldPath: metadata.name  # Le nom du pod = ID du worker (auto-genere)
        - name: WORKER_URL
          value: "http://$(POD_IP):5001" # L'IP du pod (Downward API K8s)
        resources:
          requests:
            cpu: "100m"     # 0.1 CPU garanti par pod
          limits:
            cpu: "500m"     # 0.5 CPU max par pod
```
Les **resources requests/limits** sont essentielles pour le HPA : il calcule le pourcentage CPU par rapport aux `requests`.

#### hpa-worker.yaml
```yaml
spec:
  scaleTargetRef:
    kind: Deployment
    name: worker
  minReplicas: 2             # Jamais moins de 2 workers
  maxReplicas: 10            # Jamais plus de 10 workers
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        averageUtilization: 60    # Scale-up si CPU moyen > 60%
  - type: Resource
    resource:
      name: memory
      target:
        averageUtilization: 70    # Scale-up si memoire moyenne > 70%
  behavior:
    scaleUp:
      stabilizationWindowSeconds: 30    # Attendre 30s avant de scaler up
      policies:
      - type: Pods
        value: 2                         # Ajouter max 2 pods a la fois
        periodSeconds: 60                # Toutes les 60s
    scaleDown:
      stabilizationWindowSeconds: 120   # Attendre 2 min avant de scaler down
      policies:
      - type: Pods
        value: 1                         # Retirer 1 pod a la fois
```
Le **HPA** (Horizontal Pod Autoscaler) observe les metriques CPU/memoire via le **Metrics Server** et ajuste automatiquement le nombre de repliques du deployment `worker`. Le `behavior` controle la vitesse de scaling pour eviter les oscillations.

---

## Prometheus

### Qu'est-ce que Prometheus ?

Prometheus est un systeme open-source de **monitoring et alerting** concu pour les environnements Cloud (cree par SoundCloud, maintenant sous la CNCF avec Kubernetes).

**Principe** : Prometheus **pull** (scrape) les metriques depuis les applications, contrairement a d'autres systemes ou les applications push. Chaque application expose un endpoint HTTP `/metrics` au format texte.

### Comment ca marche dans SimCluster

1. **Les pods exposent `/metrics`** : Master sur `:8080/metrics`, Workers sur `:5001/metrics`
2. **Prometheus decouvre les pods automatiquement** via `kubernetes_sd_configs` (Service Discovery K8s)
3. **Toutes les 10 secondes**, Prometheus scrape chaque pod et stocke les metriques en base TSDB
4. **Les metriques sont interrogeables** via PromQL (langage de requete Prometheus)

### Configuration (prometheus-config.yaml)

```yaml
scrape_configs:
  - job_name: 'simcluster-master'
    kubernetes_sd_configs:          # Decouverte automatique des pods K8s
      - role: pod
        namespaces: ['simcluster']  # Uniquement dans ce namespace
    relabel_configs:
      - source_labels: [__meta_kubernetes_pod_label_app]
        regex: master               # Ne garder que les pods avec label app=master
        action: keep
      - source_labels: [__meta_kubernetes_pod_ip]
        target_label: __address__
        replacement: $1:8080        # Scraper sur l'IP du pod, port 8080
```

**kubernetes_sd_configs** : Prometheus interroge l'API Kubernetes pour lister les pods. `relabel_configs` filtre ensuite pour ne garder que ceux qui nous interessent. Quand un nouveau worker est cree par le HPA, Prometheus le detecte automatiquement.

### RBAC (prometheus-rbac.yaml)

Pour que Prometheus puisse interroger l'API K8s, il a besoin de permissions :
- **ServiceAccount** `prometheus` : identite du pod Prometheus
- **ClusterRole** : autorise `get`, `list`, `watch` sur les `pods`, `services`, `endpoints`
- **ClusterRoleBinding** : lie le ServiceAccount au ClusterRole

Sans ces permissions, Prometheus ne pourrait pas decouvrir les pods a scraper.

---

## Grafana

### Qu'est-ce que Grafana ?

Grafana est une plateforme open-source de **visualisation et analyse** de metriques. Elle se connecte a des datasources (Prometheus, InfluxDB, etc.) et permet de creer des dashboards avec des graphiques temps reel.

### Comment ca marche dans SimCluster

1. **Datasource Prometheus** : Grafana est configuree pour interroger `http://prometheus-svc:9090`
2. **Dashboard pre-provisionne** : Un dashboard JSON avec 12 panels est charge automatiquement au demarrage
3. **Acces anonyme** : Pas besoin de login pour consulter les dashboards (role Viewer)
4. **Embedding** : `GF_SECURITY_ALLOW_EMBEDDING=true` permet d'integrer les panels dans le Dashboard Blazor

### Configuration (grafana-config.yaml)

Ce fichier contient **3 ConfigMaps** :

1. **grafana-datasources** : Definit Prometheus comme source de donnees
```yaml
datasources:
  - name: Prometheus
    type: prometheus
    url: http://prometheus-svc:9090    # URL interne du service Prometheus
    uid: PBFA97CFB590B2093             # UID fixe (reference dans le dashboard JSON)
    isDefault: true
```

2. **grafana-dashboard-providers** : Indique a Grafana ou charger les dashboards
```yaml
providers:
  - name: SimCluster
    type: file
    options:
      path: /var/lib/grafana/dashboards   # Monte via volume ConfigMap
```

3. **grafana-dashboards** : Le dashboard JSON complet avec 12 panels :
   - 6 **Stat panels** (valeurs instantanees) : Pending, Running, Completed, Failed, Workers, Available
   - 6 **Timeseries panels** (graphiques) : Task Queue, Throughput, Busy Threads, CPU, Workers Count, Memory

### Grafana dans le Dashboard Blazor

La page `Monitoring.razor` embarque 6 panels Grafana via des `<iframe>` :
```
/grafana/d-solo/simcluster-main/simcluster?panelId=7&from=now-15m&to=now
```

Le chemin `/grafana/*` est intercepte par un **reverse proxy** dans `Dashboard/Program.cs` qui relaie les requetes vers le service Grafana interne. Cela resout le probleme d'acces : le navigateur de l'utilisateur n'a pas acces directement a `grafana-svc:3001` (ClusterIP interne), mais il peut passer par le Dashboard qui, lui, est dans le cluster.

---

## Script de deploiement (deploy.ps1)

Le script `k8s/deploy.ps1` centralise toutes les operations :

| Commande | Description |
|----------|-------------|
| `.\deploy.ps1` ou `.\deploy.ps1 -Action all` | Build + Load + Deploy complet |
| `.\deploy.ps1 -Action build` | Build des 3 images Docker (master, worker, dashboard) |
| `.\deploy.ps1 -Action load` | Charge les images dans le cache Minikube |
| `.\deploy.ps1 -Action deploy` | Applique les manifests K8s via Kustomize |
| `.\deploy.ps1 -Action status` | Affiche pods, services, deployments, HPA |
| `.\deploy.ps1 -Action dashboard` | Ouvre les port-forwards (Dashboard:3000, Master:9090, Prometheus:9091, Grafana:3001) |
| `.\deploy.ps1 -Action test` | Lance 100 taches de test et affiche les resultats |
| `.\deploy.ps1 -Action stress` | Lance 500 taches CPU-intensives pour declencher le HPA |
| `.\deploy.ps1 -Action hpa` | Observe le HPA en temps reel |
| `.\deploy.ps1 -Action scale -Replicas N` | Scale manuellement a N workers |
| `.\deploy.ps1 -Action kill` | Supprime un pod worker aleatoire (test de resilience) |
| `.\deploy.ps1 -Action logs` | Affiche les logs d'un composant (Master, Workers, Dashboard) |
| `.\deploy.ps1 -Action delete` | Supprime tout le namespace simcluster |

---

## Deroulement de la demo

### Etape 1 - Deploiement
```powershell
cd k8s
.\deploy.ps1 -Action all
```
Explique : build les images Docker, les charge dans Minikube, deploie les 21 ressources K8s.

### Etape 2 - Acces aux services
```powershell
.\deploy.ps1 -Action dashboard
```
Ouvre 4 port-forwards. Montrer le Dashboard dans le navigateur (http://localhost:3000).

### Etape 3 - Statut du cluster
```powershell
.\deploy.ps1 -Action status
```
Montre les 2 workers initiaux, le HPA, les services.

### Etape 4 - Test de charge
```powershell
.\deploy.ps1 -Action test
```
Soumet 100 taches. Montrer la distribution entre les workers. 0 failed.

### Etape 5 - Autoscaling HPA
```powershell
# Terminal 1 : observer le HPA
.\deploy.ps1 -Action hpa

# Terminal 2 : envoyer 500 taches CPU-intensives
.\deploy.ps1 -Action stress
```
Le HPA detecte la charge CPU > 60% et scale de 2 a 10 workers. Montrer les pods qui apparaissent.

### Etape 6 - Resilience
```powershell
.\deploy.ps1 -Action kill
```
Un pod worker est supprime. K8s le relance automatiquement en quelques secondes.

### Etape 7 - Monitoring
Ouvrir http://localhost:3000/monitoring dans le Dashboard Blazor.
Ou http://localhost:3001 pour Grafana directement.
Montrer les graphiques de CPU, throughput, workers count.

### Etape 8 - Nettoyage
```powershell
.\deploy.ps1 -Action delete
```

---

## Concepts K8s demontres

| Concept | Ou dans le projet |
|---------|-------------------|
| **Namespace** | `namespace.yaml` - isolation des ressources |
| **Deployment** | `*-deployment.yaml` - replicas, rolling updates, self-healing |
| **Service (ClusterIP)** | `*-service.yaml` - DNS interne, load balancing |
| **ConfigMap** | `configmap.yaml` - configuration externalisee |
| **HPA** | `hpa-worker.yaml` - autoscaling horizontal base sur CPU/memoire |
| **RBAC** | `prometheus-rbac.yaml` - permissions granulaires |
| **Health probes** | `liveness` et `readiness` dans les deployments |
| **Downward API** | `worker-deployment.yaml` - injection du nom/IP du pod |
| **Kustomize** | `kustomization.yaml` - deploiement declaratif unifie |
| **Port-forward** | `deploy.ps1 -Action dashboard` - acces aux services clusterIP |
| **Service Discovery** | `prometheus-config.yaml` - decouverte automatique des pods |
