import { Routes } from '@angular/router';
import { HomePageComponent } from './features/home/home-page/home-page.component';
import { DashboardLayoutComponent } from './features/dashboard/dashboard-layout/dashboard-layout.component';
import { DashboardPageComponent } from './features/dashboard/dashboard-page/dashboard-page.component';
import { SignUpComponent } from './features/auth/sign-up/sign-up.component';
import { SignInComponent } from './features/auth/sign-in/sign-in.component';
import { authGuard } from './core/guards/auth.guard';
import { permissionGuard } from './core/guards/permission.guard';
import { Role } from './core/enums/role.enum';

export const routes: Routes = [
  { path: '', component: HomePageComponent },
  // Auth routes
  { path: 'auth/sign-in', component: SignInComponent },
  { path: 'auth/sign-up', component: SignUpComponent },

  // Dashboard (with navbar and sidebar)
  {
    path: 'dashboard',
    component: DashboardLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        component: DashboardPageComponent
      },

      // ---------- Notifications (all roles) ----------
      {
        path: 'notifications',
        loadComponent: () =>
          import('./features/notification/notifications-list/notifications-list.component')
            .then(m => m.NotificationsListComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.LEARNER, Role.TRAINER, Role.ADMIN] }
      },

      // ---------- Stagiaire ----------
      {
        path: 'ma-candidature',
        // Lazy-loaded: a learner never needs the admin bundle, and vice versa.
        loadComponent: () =>
          import('./features/candidature/ma-candidature/ma-candidature.component')
            .then(m => m.MaCandidatureComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.LEARNER, Role.ADMIN] }
      },
      {
        path: 'ma-convention',
        loadComponent: () =>
          import('./features/convention/ma-convention/ma-convention.component')
            .then(m => m.MaConventionComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.LEARNER, Role.ADMIN] }
      },
      {
        path: 'mon-journal',
        loadComponent: () =>
          import('./features/journal/mon-journal/mon-journal.component')
            .then(m => m.MonJournalComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.LEARNER, Role.ADMIN] }
      },

      // ---------- Admin ----------
      {
        path: 'creer-encadrant',
        loadComponent: () =>
          import('./features/admin/create-encadrant/create-encadrant.component')
            .then(m => m.CreateEncadrantComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.ADMIN] }
      },
      {
        path: 'candidatures',
        loadComponent: () =>
          import('./features/candidature/candidatures-admin/candidatures-admin.component')
            .then(m => m.CandidaturesAdminComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.ADMIN] }
      },
      {
        path: 'conventions',
        loadComponent: () =>
          import('./features/convention/conventions-admin/conventions-admin.component')
            .then(m => m.ConventionsAdminComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.ADMIN] }
      },
      {
        path: 'audit',
        loadComponent: () =>
          import('./features/admin/audit-log/audit-log.component')
            .then(m => m.AuditLogComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.ADMIN] }
      },
      {
        path: 'stagiaires',
        loadComponent: () =>
          import('./features/stagiaire/stagiaires-list/stagiaires-list.component')
            .then(m => m.StagiairesListComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.ADMIN] }
      },

      // ---------- Evaluation ----------
      {
        path: 'mes-evaluations',
        loadComponent: () =>
          import('./features/evaluation/mon-evaluation/mon-evaluation.component')
            .then(m => m.MonEvaluationComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.LEARNER, Role.ADMIN] }
      },
      {
        path: 'evaluations',
        loadComponent: () =>
          import('./features/evaluation/evaluation-encadrant/evaluation-encadrant.component')
            .then(m => m.EvaluationEncadrantComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.TRAINER, Role.ADMIN] }
      },

      // ---------- Encadrant ----------
      {
        // Same component as /stagiaires: the API scopes rows by role, so a trainer automatically
        // sees only their own assigned stagiaires.
        path: 'mes-stagiaires',
        loadComponent: () =>
          import('./features/stagiaire/stagiaires-list/stagiaires-list.component')
            .then(m => m.StagiairesListComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.TRAINER, Role.ADMIN] }
      },
      {
        path: 'journaux',
        loadComponent: () =>
          import('./features/journal/journal-encadrant/journal-encadrant.component')
            .then(m => m.JournalEncadrantComponent),
        canActivate: [permissionGuard],
        data: { roles: [Role.TRAINER, Role.ADMIN] }
      }
    ]
  },

  { path: '**', redirectTo: '' }
];
