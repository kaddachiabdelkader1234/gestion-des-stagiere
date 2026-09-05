# Gestion des Stagiaires STB - Guide Simple

## C'est quoi ce projet ?

Une plateforme pour gérer les stagiaires de la STB (Société Tunisienne de Banque).
 architecture microservices = plusieurs petits services qui travaillent ensemble.

## Architecture simple

```
Frontend Angular (4200)
    ↓
API Gateway (18080) → Point d'entrée unique
    ↓
┌──────────┼──────────┐
Java Services    .NET Services
(Eureka, Auth)    (Stagiaire, Convention, Evaluation, Notification)
    ↓                 ↓
  MySQL           PostgreSQL
                    ↓
              RabbitMQ (messages)
```

## Les étapes qu'on a faites

### Phase 1 - Eureka + Gateway + Auth
**Ce qu'on a fait :** 3 services Java
- **Eureka** : annuaire des services (comme un répertoire téléphonique)
- **Auth** : gère les logins/mots de passe (JWT)
- **Gateway** : porte d'entrée unique pour toutes les requêtes

**Bénéfice :** Plus sécurisé. Toutes les requêtes passent par une seule porte.

### Phase 2 - Services .NET
**Ce qu'on a fait :** 4 services C#
- **Stagiaire** : CRUD stagiaires
- **Convention** : génération de conventions PDF
- **Evaluation** : notes des stagiaires
- **Notification** : envoi d'emails

**Bénéfice :** Code propre. Chaque service a son propre rôle.

### Phase 3 - JWT Trust
**Ce qu'on a fait :** Java et .NET partagent les tokens JWT
- Auth Service (Java) génère un token
- Les services .NET vérifient que c'est valide

**Bénéfice :** Pas besoin de se reconnecter sur chaque service.

### Phase 4 - RabbitMQ
**Ce qu'on a fait :** Messages asynchrones
- Créer un stagiaire → publié dans RabbitMQ
- Notification Service reçoit le message → envoie un email

**Bénéfice :** Services découplés. Si Notification est en panne, les autres services continuent de marcher.

**Sans RabbitMQ :**
```
Convention → appelle Notification → si NotificationDown → Convention crash ❌
```

**Avec RabbitMQ :**
```
Convention → message dans RabbitMQ → continue de marcher ✅
Notification → lit le message quand elle est prête
```

### Phase 5 - Docker
**Ce qu'on a fait :** Dockerfiles + docker-compose.yml
- Chaque service a son Dockerfile
- Un seul fichier `docker-compose.yml` pour tout lancer

**Comment lancer :**
```bash
docker-compose up -d
```

**Bénéfice :** Même environnement sur tous les PC. "Ça marche sur ma machine" → "Ça marche sur toutes les machines".

### Phase 6 - GitHub Actions CI
**Ce qu'on a fait :** Robot de test automatique
- À chaque `git push`, GitHub compile et teste tout
- 3 jobs : Java, .NET, Angular

**Comment ça marche :**
```
git push → GitHub Actions → Build + Test → Vert ✅ ou Rouge ❌
```

**Bénéfice :** Détecte les bugs immédiatement, sans tester manuellement.

### Phase 7 - Kubernetes
**Ce qu'on a fait :** Fichiers de déploiement Kubernetes
- **namespace** : isole les ressources
- **secret** : stocke les mots de passe
- **configmap** : stocke la config
- **deployment** : lance les services
- **service** : expose les ports
- **ingress** : point d'entrée HTTP

**Bénéfice :** Gestion automatique des conteneurs. Si un service crash, il redémarre tout seul.

### Phase 8 - Observabilité
**Ce qu'on a fait :** Monitoring avec Prometheus + Grafana
- **Prometheus** : collecte les métriques (CPU, RAM, requêtes)
- **Grafana** : affiche les métriques dans des graphiques
- **Dashboard** : vue d'ensemble de la santé du système

**Comment lancer :**
```bash
docker run -d -p 9090:9090 prom/prometheus
docker run -d -p 3000:3000 grafana/grafana
```

**Bénéfice :** Tu vois en temps réel ce qui se passe dans ton système.

## Lancer le projet

### Option 1 : Docker (recommandé)
```bash
docker-compose up -d
```

### Option 2 : Manuel (développement)
```bash
# 1. Infrastructure
docker run -d --name rabbitmq -p 5672:5672 rabbitmq:3-management
docker run -d --name postgres -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgres:15
docker run -d --name mysql -p 3306:3306 -e MYSQL_ROOT_PASSWORD=root mysql:8.0

# 2. Java services
cd Backend/eureka-server && mvn spring-boot:run
cd Backend/auth-service && mvn spring-boot:run
cd Backend/api-gateway && mvn spring-boot:run

# 3. .NET services
cd Stagiaire.Service && dotnet run
cd Convention.Service && dotnet run
cd Evaluation.Service && dotnet run
cd Notification.Service && dotnet run

# 4. Frontend
cd Frontend/angular-app && npm install && ng serve
```

## URLs
- Frontend : http://localhost:4200
- API Gateway : http://localhost:18080
- Eureka : http://localhost:8761
- RabbitMQ UI : http://localhost:15672 (guest/guest)
- Prometheus : http://localhost:9090
- Grafana : http://localhost:3000 (admin/admin)

## Technologies utilisées
- **Backend Java** : Spring Boot, Spring Cloud, Eureka, JWT
- **Backend .NET** : ASP.NET Core 8, EF Core, PostgreSQL
- **Frontend** : Angular 18
- **Messagerie** : RabbitMQ + MassTransit
- **CI/CD** : GitHub Actions
- **Docker** : docker-compose
- **Monitoring** : Prometheus + Grafana

## Avantages de cette architecture

1. **Scalable** : Ajouter un service = facile
2. **Résilient** : Si un service crash, les autres continuent
3. **Testable** : CI automatique à chaque push
4. **Déployable** : Docker = même partout
5. **Découplé** : RabbitMQ = services indépendants
6. **Observable** : Monitoring en temps réel avec Prometheus + Grafana