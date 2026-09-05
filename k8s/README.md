# Kubernetes - Phase 7

## C'est quoi Kubernetes ?

Kubernetes (k8s) = Un chef d'orchestre qui gère tes conteneurs Docker.
- Si un service crash, il le redémarre automatiquement
- Si tu as besoin de plus de puissance, il ajoute des instances automatiquement
- Il gère les mises à jour sans downtime

## Comment déployer sur Kubernetes

### Prérequis
- Kubernetes installé (minikube, kind, ou Docker Desktop)
- kubectl configuré
- Images Docker buildées et pushées sur ghcr.io

### Commandes de déploiement

```bash
# 1. Créer le namespace
kubectl apply -f k8s/namespace.yaml

# 2. Créer les secrets
kubectl apply -f k8s/secret.yaml

# 3. Créer les deployments et services
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml

# 4. Créer l'ingress (optionnel)
kubectl apply -f k8s/ingress.yaml

# Vérifier que tout tourne
kubectl get pods -n gestion-stagiaires-stb
kubectl get services -n gestion-stagiaires-stb
```

## Accéder aux services

```bash
# Port-forward pour accéder depuis localhost
kubectl port-forward svc/api-gateway 8080:8080 -n gestion-stagiaires-stb
kubectl port-forward svc/eureka-server 8761:8761 -n gestion-stagiaires-stb
```

## Supprimer tout

```bash
kubectl delete namespace gestion-stagiaires-stb
```

## Structure des fichiers

- `namespace.yaml` : Isolé les ressources dans un namespace dédié
- `secret.yaml` : Stocke les mots de passe (base64)
- `configmap.yaml` : Stocke la configuration non sensible
- `deployment.yaml` : Définit combien de replicas de chaque service
- `service.yaml` : Expose les services dans le cluster
- `ingress.yaml` : Point d'entrée HTTP externe