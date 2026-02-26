# ==============================================================================
# SimCluster - Script de deploiement Kubernetes
# ==============================================================================
#
# POURQUOI CE SCRIPT ?
# --------------------
# Minikube utilise son propre daemon Docker, isole de celui de la machine hote.
# Les images buildees localement ne sont pas visibles par Minikube.
# Ce script automatise : build > chargement dans Minikube > deploiement K8s.
#
# USAGE :
#   ./deploy.ps1                     # Build + Load + Deploy complet
#   ./deploy.ps1 -Action build       # Build des images Docker uniquement
#   ./deploy.ps1 -Action load        # Charger les images dans Minikube
#   ./deploy.ps1 -Action deploy      # Deployer les manifests K8s
#   ./deploy.ps1 -Action status      # Statut du cluster
#   ./deploy.ps1 -Action test        # Lancer un test de charge (100 tasks)
#   ./deploy.ps1 -Action stress      # Stress test (500 tasks) pour HPA
#   ./deploy.ps1 -Action hpa         # Observer le HPA en temps reel
#   ./deploy.ps1 -Action dashboard   # Ouvrir le Dashboard (port-forward)
#   ./deploy.ps1 -Action scale -Replicas 5  # Scaler les workers
#   ./deploy.ps1 -Action kill        # Supprimer un pod worker (resilience)
#   ./deploy.ps1 -Action logs        # Voir les logs d'un composant
#   ./deploy.ps1 -Action delete      # Supprimer le namespace simcluster
#
# ==============================================================================

param(
    [ValidateSet("all", "build", "load", "deploy", "status", "test", "stress", "dashboard", "delete", "scale", "logs", "hpa", "kill")]
    [string]$Action = "all",
    [int]$Replicas = 0
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$ImageTag = "dev"
$Namespace = "simcluster"

function Write-Step  { param($msg) Write-Host "`n[$((Get-Date).ToString('HH:mm:ss'))] $msg" -ForegroundColor Cyan }
function Write-Ok    { param($msg) Write-Host "  OK: $msg" -ForegroundColor Green }
function Write-Warn  { param($msg) Write-Host "  WARN: $msg" -ForegroundColor Yellow }

# ==============================================================================
# ETAPE 1 : BUILD
# Construit les 3 images Docker (Master, Worker, Dashboard) depuis le code source.
# Les Dockerfiles utilisent un multi-stage build : SDK pour compiler, runtime pour executer.
# ==============================================================================
function Build-Images {
    Write-Step "Build des images Docker..."

    $services = @(
        @{ Name = "master";    Dockerfile = "Master/Dockerfile" },
        @{ Name = "worker";    Dockerfile = "Worker/Dockerfile" },
        @{ Name = "dashboard"; Dockerfile = "Dashboard/Dockerfile" }
    )

    foreach ($svc in $services) {
        Write-Host "  Building simcluster-$($svc.Name):$ImageTag ..." -NoNewline
        docker build -t "simcluster-$($svc.Name):$ImageTag" -f "$ProjectRoot/$($svc.Dockerfile)" "$ProjectRoot" 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Host " FAILED" -ForegroundColor Red; exit 1 }
        Write-Ok "done"
    }
}

# ==============================================================================
# ETAPE 2 : LOAD
# Charge les images dans Minikube. Minikube a son propre cache d'images Docker.
# Sans cette etape, les pods K8s ne trouvent pas les images (ImagePullBackOff).
# `minikube image load` copie l'image du Docker local vers le Docker de Minikube.
# ==============================================================================
function Load-Images {
    Write-Step "Chargement des images dans Minikube..."

    $minikubeStatus = minikube status --format '{{.Host}}' 2>$null
    if ($minikubeStatus -ne "Running") {
        Write-Step "Demarrage de Minikube..."
        minikube start --driver=docker
    }

    foreach ($name in @("master", "worker", "dashboard")) {
        Write-Host "  Loading simcluster-${name}:$ImageTag ..." -NoNewline
        minikube image load "simcluster-${name}:$ImageTag" 2>&1 | Out-Null
        Write-Ok "loaded"
    }
}

# ==============================================================================
# ETAPE 3 : DEPLOY
# Applique tous les manifests K8s via Kustomize.
# Kustomize permet de regrouper namespace, deployments, services, HPA, etc.
# kubectl apply -k : applique tous les fichiers listes dans kustomization.yaml.
# ==============================================================================
function Deploy-Cluster {
    Write-Step "Deploiement sur Kubernetes..."

    kubectl apply -k "$ProjectRoot/k8s" 2>&1

    Write-Step "Attente du rollout des deployments..."
    kubectl rollout status deployment/master     -n $Namespace --timeout=120s
    kubectl rollout status deployment/worker     -n $Namespace --timeout=120s
    kubectl rollout status deployment/dashboard  -n $Namespace --timeout=120s
    kubectl rollout status deployment/prometheus -n $Namespace --timeout=120s
    kubectl rollout status deployment/grafana    -n $Namespace --timeout=120s

    Write-Ok "Tous les pods sont Ready"
    Get-Status
}

# ==============================================================================
# STATUS
# Affiche l'etat complet du cluster : pods, services, deployments, HPA.
# ==============================================================================
function Get-Status {
    Write-Step "Statut du cluster $Namespace"

    Write-Host "`n  --- Pods ---" -ForegroundColor Yellow
    kubectl get pods -n $Namespace -o wide

    Write-Host "`n  --- Services ---" -ForegroundColor Yellow
    kubectl get svc -n $Namespace

    Write-Host "`n  --- Deployments ---" -ForegroundColor Yellow
    kubectl get deployments -n $Namespace

    Write-Host "`n  --- HPA (Autoscaling) ---" -ForegroundColor Yellow
    kubectl get hpa -n $Namespace
}

# ==============================================================================
# DASHBOARD
# Acceder au Dashboard Blazor depuis le navigateur.
# En K8s, les services sont internes (ClusterIP). Le port-forward cree un tunnel
# depuis localhost vers le pod, permettant d'y acceder sans Ingress ni NodePort.
#
#   port-forward svc/dashboard-svc 3000:8080  >  http://localhost:3000
#   port-forward svc/master-svc    9090:8080  >  http://localhost:9090/api/...
# ==============================================================================
function Start-Dashboard {
    Write-Step "Ouverture des acces au cluster..."

    Write-Host "  Dashboard    : http://localhost:3000"
    Write-Host "  Master API   : http://localhost:9090"
    Write-Host "  Prometheus   : http://localhost:9091"
    Write-Host "  Grafana      : http://localhost:3001  (admin / simcluster)"
    Write-Host "  (Ctrl+C pour arreter)`n"

    Start-Job -ScriptBlock {
        kubectl port-forward svc/master-svc 9090:8080 -n simcluster 2>&1 | Out-Null
    } | Out-Null

    Start-Job -ScriptBlock {
        kubectl port-forward svc/prometheus-svc 9091:9090 -n simcluster 2>&1 | Out-Null
    } | Out-Null

    Start-Job -ScriptBlock {
        kubectl port-forward svc/grafana-svc 3001:3001 -n simcluster 2>&1 | Out-Null
    } | Out-Null

    kubectl port-forward svc/dashboard-svc 3000:8080 -n $Namespace
}

# ==============================================================================
# TEST
# Soumet 100 taches et verifie que tout est traite sans erreur.
# Montre la repartition round-robin entre les workers.
# ==============================================================================
function Run-Test {
    Write-Step "Test de charge : 100 taches..."

    $job = Start-Job -ScriptBlock {
        kubectl port-forward svc/master-svc 9090:8080 -n simcluster 2>&1 | Out-Null
    }
    Start-Sleep -Seconds 3

    $rng = [System.Random]::new()
    for ($i = 1; $i -le 100; $i++) {
        $body = @{
            name       = "LoadTest-$i"
            durationMs = $rng.Next(500, 3000)
            priority   = $rng.Next(0, 3)
        } | ConvertTo-Json -Compress

        Invoke-RestMethod -Method Post -Uri "http://localhost:9090/api/task" `
            -ContentType "application/json" -Body $body | Out-Null

        if ($i % 25 -eq 0) { Write-Host "  $i/100 submitted..." }
    }

    Write-Step "100 taches soumises. Attente du traitement..."
    Start-Sleep -Seconds 15

    $stats = Invoke-RestMethod "http://localhost:9090/api/task/stats"
    Write-Host "`n  Results:" -ForegroundColor Yellow
    Write-Host "    Pending:   $($stats.pending)"
    Write-Host "    Running:   $($stats.running)"
    Write-Host "    Completed: $($stats.completed)" -ForegroundColor Green
    Write-Host "    Failed:    $($stats.failed)" -ForegroundColor $(if ($stats.failed -gt 0) { "Red" } else { "Green" })

    $tasks = (Invoke-RestMethod "http://localhost:9090/api/task").tasks
    Write-Host "`n  Distribution:" -ForegroundColor Yellow
    $tasks | Where-Object { $_.name -like "LoadTest-*" } |
        Group-Object assignedWorkerId |
        Select-Object Name, Count |
        Format-Table

    Stop-Job $job -ErrorAction SilentlyContinue
    Remove-Job $job -ErrorAction SilentlyContinue
}

# ==============================================================================
# SCALE
# Modifie le nombre de replicas du deployment worker.
# K8s cree/supprime automatiquement les pods pour atteindre le nombre voulu.
# Les nouveaux workers s'enregistrent automatiquement aupres du Master.
# ==============================================================================
function Scale-Workers {
    if ($Replicas -le 0) {
        Write-Host "  Usage: ./deploy.ps1 -Action scale -Replicas <nombre>" -ForegroundColor Red
        return
    }
    Write-Step "Scaling workers to $Replicas replicas..."
    kubectl scale deployment worker -n $Namespace --replicas=$Replicas
    kubectl rollout status deployment/worker -n $Namespace --timeout=60s
    Write-Ok "$Replicas worker pods running"
    kubectl get pods -n $Namespace -l app=worker -o wide
}

# ==============================================================================
# STRESS
# Soumet 500 taches CPU-intensives pour declencher le HPA.
# Les taches sont longues (1-5s) pour saturer les threads des workers.
# ==============================================================================
function Run-Stress {
    Write-Step "Stress test : 500 taches CPU-intensives pour declencher le HPA..."

    $job = Start-Job -ScriptBlock {
        kubectl port-forward svc/master-svc 9090:8080 -n simcluster 2>&1 | Out-Null
    }
    Start-Sleep -Seconds 3

    $rng = [System.Random]::new()
    for ($i = 1; $i -le 500; $i++) {
        $body = @{
            name       = "HPA-Stress-$i"
            durationMs = $rng.Next(1000, 5000)
            priority   = 2
        } | ConvertTo-Json -Compress

        Invoke-RestMethod -Method Post -Uri "http://localhost:9090/api/task" `
            -ContentType "application/json" -Body $body -ErrorAction SilentlyContinue | Out-Null

        if ($i % 100 -eq 0) { Write-Host "  $i/500 submitted..." }
    }

    Write-Ok "500 taches soumises. Le HPA devrait reagir dans 30-60s."
    Write-Host "  Lancer '.\deploy.ps1 -Action hpa' pour observer le scaling." -ForegroundColor Yellow

    Stop-Job $job -ErrorAction SilentlyContinue
    Remove-Job $job -ErrorAction SilentlyContinue
}

# ==============================================================================
# HPA
# Affiche le HPA en temps reel (mode watch) pour observer le scaling automatique.
# ==============================================================================
function Watch-HPA {
    Write-Step "Observation du HPA (Ctrl+C pour arreter)..."
    Write-Host ""
    kubectl get hpa -n $Namespace -w
}

# ==============================================================================
# KILL
# Supprime un pod worker aleatoire pour demontrer la resilience.
# K8s le relance automatiquement grace au ReplicaSet.
# ==============================================================================
function Kill-Worker {
    Write-Step "Resilience : suppression d'un pod worker..."

    $pods = kubectl get pods -n $Namespace -l app=worker --no-headers -o custom-columns=":metadata.name" 2>$null
    if (-not $pods) {
        Write-Warn "Aucun worker pod trouve."
        return
    }
    $podList = $pods -split "`n" | Where-Object { $_ -ne "" }
    $target = $podList | Get-Random
    Write-Host "  Suppression du pod: $target" -ForegroundColor Yellow
    kubectl delete pod $target -n $Namespace

    Write-Step "Observation du redemarrage automatique..."
    Start-Sleep -Seconds 2
    kubectl get pods -n $Namespace -l app=worker -o wide
    Write-Ok "K8s relance automatiquement le pod supprime."
}

# ==============================================================================
# LOGS
# Affiche les logs en temps reel de tous les pods d'un composant.
# ==============================================================================
function Get-Logs {
    Write-Step "Composants disponibles :"
    Write-Host "  1. Master"
    Write-Host "  2. Workers"
    Write-Host "  3. Dashboard"
    $choice = Read-Host "Choix"
    switch ($choice) {
        "1" { kubectl logs -l app=master    -n $Namespace --tail=100 -f }
        "2" { kubectl logs -l app=worker    -n $Namespace --tail=100 -f }
        "3" { kubectl logs -l app=dashboard -n $Namespace --tail=100 -f }
        default { Write-Warn "Choix invalide" }
    }
}

# ==============================================================================
# DELETE
# Supprime tout le namespace simcluster et toutes ses ressources.
# ==============================================================================
function Delete-Cluster {
    Write-Warn "Suppression du namespace '$Namespace'..."
    $confirm = Read-Host "Confirmer ? (y/N)"
    if ($confirm -ne "y") { Write-Host "  Annule."; return }
    kubectl delete namespace $Namespace
    Write-Ok "Namespace supprime"
}

# ==============================================================================
# MAIN
# ==============================================================================
switch ($Action) {
    "all"       { Build-Images; Load-Images; Deploy-Cluster }
    "build"     { Build-Images }
    "load"      { Load-Images }
    "deploy"    { Deploy-Cluster }
    "status"    { Get-Status }
    "test"      { Run-Test }
    "stress"    { Run-Stress }
    "dashboard" { Start-Dashboard }
    "scale"     { Scale-Workers }
    "hpa"       { Watch-HPA }
    "kill"      { Kill-Worker }
    "logs"      { Get-Logs }
    "delete"    { Delete-Cluster }
}
