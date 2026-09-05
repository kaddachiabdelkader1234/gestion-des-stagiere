# Gestion des Stagiaires STB

## 📋 Présentation

Plateforme de gestion des stagiaires pour **STB** (Société Tunisienne de Banque).
Architecture microservices avec backend Java (Spring Boot) et .NET, frontend Angular, et messagerie asynchrone RabbitMQ.

## 🏗️ Architecture

```
Frontend Angular (4200)
       │
API Gateway (18080) - Spring Cloud Gateway
       │
┌──────┼──────────────┐
│      │              │
Eureka  Auth Service  Services .NET
(8761)  (8081)        (5070-5073)
│      │              │
MySQL  MySQL          PostgreSQL
                      │
                 RabbitMQ (5672)
```

## 🚀 Services

| Service | Technologie | Port |
|---------|-------------|------|
| Eureka Server | Java Spring Boot | 8761 |
| Auth Service | Java Spring Boot | 8081 |
| Config Server | Java Spring Cloud | 8888 |
| API Gateway | Java Spring Cloud | 18080 |
| Stagiaire Service | .NET 8 | 5070 |
| Convention Service | .NET 8 | 5071 |
| Evaluation Service | .NET 8 | 5072 |
| Notification Service | .NET 8 | 5073 |
| Angular Frontend | Angular 18 | 4200 |

Toutes les requêtes du frontend passent par la passerelle sur `18080`, sur les routes versionnées
`/api/v1/**` — jamais directement sur le port d'un service.

## 📦 Infrastructure

- **RabbitMQ** : Messagerie asynchrone (port 5672, UI: 15672)
- **MySQL** : Base de données des services Java (port 3306)
- **PostgreSQL** : Base de données des services .NET (port 5432)

## 🔧 Démarrage rapide

Le secret JWT partagé est obligatoire — aucun service ne démarre sans lui :

```bash
cp .env.example .env
# puis générer une vraie valeur pour JWT_SECRET (voir les instructions dans .env.example)
```

Tout le backend et l'infrastructure démarrent avec Docker Compose :

```bash
docker compose up -d --build
```

Le frontend Angular n'est pas conteneurisé — il se lance séparément :

```bash
cd Frontend/angular-app && npm install && npm start   # http://localhost:4200
```

Vérifications rapides :

| Quoi | URL |
|------|-----|
| Dashboard Eureka | <http://localhost:8761> |
| Santé de la passerelle | <http://localhost:18080/actuator/health> |
| UI RabbitMQ | <http://localhost:15672> (guest / guest) |

## 📖 Documentation

- **[HANDOFF.md](./HANDOFF.md)** — start here if you are picking this project up: current state,
  known traps, and what is still outstanding.
- [IMPLEMENTATION_BRIEF.md](./IMPLEMENTATION_BRIEF.md) — the full scope and the order of work.
- [PROJECT_WORK_LOG.md](./PROJECT_WORK_LOG.md) — what was changed in each phase and how it was tested.
- [PROJECT_GUIDE.md](./PROJECT_GUIDE.md) — architecture overview.