# Observabilité - Phase 8

## C'est quoi ?

Prometheus + Grafana = tableau de bord pour superviser ton système.

- **Prometheus** : collecte les métriques (CPU, RAM, requêtes HTTP)
- **Grafana** : affiche les métriques dans des graphiques

## Installation locale

```bash
# Lancer Prometheus
docker run -d -p 9090:9090 \
  -v ${PWD}/prometheus.yml:/etc/prometheus/prometheus.yml \
  prom/prometheus

# Lancer Grafana
docker run -d -p 3000:3000 \
  grafana/grafana
```

## Accéder aux interfaces

- **Prometheus** : http://localhost:9090
- **Grafana** : http://localhost:3000 (admin/admin)

## Configuration

1. Ouvrir Grafana : http://localhost:3000
2. Ajouter Prometheus comme datasource :
   - URL : http://prometheus:9090
3. Importer le dashboard : `grafana-dashboard.json`

## Métriques disponibles

- CPU Usage par service
- Memory Usage par service
- Nombre de requêtes HTTP par seconde
- Statut des services (up/down)