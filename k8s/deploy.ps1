# ==============================================================================
# Script de déploiement SimCluster sur Kubernetes
# ==============================================================================
# Usage:
#   ./deploy.ps1                    # Déploiement complet
#   ./deploy.ps1 -Action build      # Build des images Docker uniquement
#   ./deploy.ps1 -Action deploy     # Déploiement K8s uniquement
#   ./deploy.ps1 -Action status     # Afficher le statut du cluster
#   ./deploy.ps1 -Action delete     # Supprimer le déploiement
#   ./deploy.ps1 -Action logs       # Voir les logs
# ==============================================================================

param(
    [ValidateSet("all", "build", "deploy", "status", "delete", "logs", "port-forward")]
    [string]$Action = "all",
    
    [string]$ImageTag = "latest",
    
    [switch]$Minikube
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

# Couleurs pour les messages
function Write-Info { Write-Host "[INFO] $args" -ForegroundColor Cyan }
function Write-Success { Write-Host "[OK] $args" -ForegroundColor Green }
function Write-Warning { Write-Host "[WARN] $args" -ForegroundColor Yellow }
function Write-Error { Write-Host "[ERROR] $args" -ForegroundColor Red }

# ==============================================================================
# BUILD - Construction des images Docker
# ==============================================================================
function Build-Images {
    Write-Info "Construction des images Docker..."
    
    # Si Minikube, utiliser son Docker daemon
    if ($Minikube) {
        Write-Info "Configuration de l'environnement Docker Minikube..."
        & minikube -p minikube docker-env --shell powershell | Invoke-Expression
    }
    
    # Build Master
    Write-Info "Build simcluster-master:$ImageTag"
    docker build -t "simcluster-master:$ImageTag" -f "$ProjectRoot/Master/Dockerfile" "$ProjectRoot"
    if ($LASTEXITCODE -ne 0) { throw "Échec du build Master" }
    
    # Build Worker
    Write-Info "Build simcluster-worker:$ImageTag"
    docker build -t "simcluster-worker:$ImageTag" -f "$ProjectRoot/Worker/Dockerfile" "$ProjectRoot"
    if ($LASTEXITCODE -ne 0) { throw "Échec du build Worker" }
    
    # Build Dashboard
    Write-Info "Build simcluster-dashboard:$ImageTag"
    docker build -t "simcluster-dashboard:$ImageTag" -f "$ProjectRoot/Dashboard/Dockerfile" "$ProjectRoot"
    if ($LASTEXITCODE -ne 0) { throw "Échec du build Dashboard" }
    
    Write-Success "Toutes les images ont été construites avec succès!"
}

# ==============================================================================
# DEPLOY - Déploiement sur Kubernetes
# ==============================================================================
function Deploy-Cluster {
    Write-Info "Déploiement sur Kubernetes..."
    
    # Vérifier la connexion au cluster
    kubectl cluster-info | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Impossible de se connecter au cluster Kubernetes" }
    
    # Déployer avec Kustomize
    Write-Info "Application des manifests Kubernetes..."
    kubectl apply -k "$ProjectRoot/k8s"
    
    if ($LASTEXITCODE -ne 0) { throw "Échec du déploiement" }
    
    Write-Success "Déploiement lancé!"
    Write-Info "Attente du démarrage des Pods..."
    
    # Attendre que les déploiements soient prêts
    kubectl rollout status deployment/master -n simcluster --timeout=120s
    kubectl rollout status deployment/worker -n simcluster --timeout=120s
    kubectl rollout status deployment/dashboard -n simcluster --timeout=120s
    
    Write-Success "Tous les composants sont déployés!"
    Get-Status
}

# ==============================================================================
# STATUS - Afficher le statut du cluster
# ==============================================================================
function Get-Status {
    Write-Info "=== Statut du cluster SimCluster ==="
    
    Write-Host "`n--- Pods ---" -ForegroundColor Yellow
    kubectl get pods -n simcluster -o wide
    
    Write-Host "`n--- Services ---" -ForegroundColor Yellow
    kubectl get svc -n simcluster
    
    Write-Host "`n--- Deployments ---" -ForegroundColor Yellow
    kubectl get deployments -n simcluster
    
    Write-Host "`n--- HPA ---" -ForegroundColor Yellow
    kubectl get hpa -n simcluster
    
    Write-Host "`n--- Ingress ---" -ForegroundColor Yellow
    kubectl get ingress -n simcluster
}

# ==============================================================================
# DELETE - Supprimer le déploiement
# ==============================================================================
function Delete-Cluster {
    Write-Warning "Suppression du déploiement SimCluster..."
    
    $confirm = Read-Host "Êtes-vous sûr de vouloir supprimer le namespace 'simcluster'? (y/N)"
    if ($confirm -ne "y") {
        Write-Info "Annulé."
        return
    }
    
    kubectl delete namespace simcluster
    Write-Success "Namespace 'simcluster' supprimé."
}

# ==============================================================================
# LOGS - Afficher les logs
# ==============================================================================
function Get-Logs {
    Write-Info "Logs disponibles:"
    Write-Host "  1. Master"
    Write-Host "  2. Workers"
    Write-Host "  3. Dashboard"
    Write-Host "  4. Tous"
    
    $choice = Read-Host "Choix"
    
    switch ($choice) {
        "1" { kubectl logs -l app=master -n simcluster --tail=100 -f }
        "2" { kubectl logs -l app=worker -n simcluster --tail=100 -f }
        "3" { kubectl logs -l app=dashboard -n simcluster --tail=100 -f }
        "4" { kubectl logs -l project=simcluster -n simcluster --tail=50 -f }
        default { Write-Warning "Choix invalide" }
    }
}

# ==============================================================================
# PORT-FORWARD - Accès local au Dashboard
# ==============================================================================
function Start-PortForward {
    Write-Info "Démarrage du port-forward pour le Dashboard..."
    Write-Host "Dashboard accessible sur: http://localhost:8080" -ForegroundColor Green
    Write-Host "API Master accessible sur: http://localhost:8081" -ForegroundColor Green
    Write-Host "Appuyez sur Ctrl+C pour arrêter.`n"
    
    # Lancer en parallèle
    Start-Job -ScriptBlock { kubectl port-forward svc/dashboard-svc 8080:80 -n simcluster }
    kubectl port-forward svc/master-svc 8081:8080 -n simcluster
}

# ==============================================================================
# MAIN - Point d'entrée
# ==============================================================================
Write-Host @"

 _____ _           _____ _           _            
/  ___(_)         /  __ \ |         | |           
\ `--._ _ __ ___  | /  \/ |_   _ ___| |_ ___ _ __ 
 `--. \ | '_ ` _ \| |   | | | | / __| __/ _ \ '__|
/\__/ / | | | | | | \__/\ | |_| \__ \ ||  __/ |   
\____/|_|_| |_| |_|\____/_|\__,_|___/\__\___|_|   
                                                  
           Kubernetes Deployment Script

"@ -ForegroundColor Cyan

switch ($Action) {
    "all" {
        Build-Images
        Deploy-Cluster
    }
    "build" { Build-Images }
    "deploy" { Deploy-Cluster }
    "status" { Get-Status }
    "delete" { Delete-Cluster }
    "logs" { Get-Logs }
    "port-forward" { Start-PortForward }
}
