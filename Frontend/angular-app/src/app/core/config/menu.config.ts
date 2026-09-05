import { Permission } from '../enums/permission.enum';
import { Role } from '../enums/role.enum';

export interface MenuItem {
  /** Not required for `divider`/`header` rows, which render no link text. */
  label?: string;
  icon?: string;
  route?: string;
  permissions?: Permission[];
  roles?: Role[];
  children?: MenuItem[];
  divider?: boolean;
  header?: string;
}

/**
 * Sidebar navigation, filtered per role by SidebarComponent.filterMenuItems().
 *
 * Items are gated with `roles` rather than `permissions`: ROLE_PERMISSIONS still describes the
 * inherited LMS model (courses, exams, badges) and has no entries for internship management, so
 * matching on it would hide everything. Roles map 1:1 to the three the app actually uses.
 *
 * Only ADMIN, TRAINER (Encadrant) and LEARNER (Stagiaire) are in scope — RH_COMPANY and RH_SMARTEK
 * exist in the enum but have no screens.
 */
export const MENU_ITEMS: MenuItem[] = [
  // Visible to everyone who can log in.
  {
    label: 'Tableau de bord',
    icon: 'dashboard',
    route: '/dashboard'
  },
  {
    label: 'Notifications',
    icon: 'notifications',
    route: '/dashboard/notifications',
    roles: [Role.LEARNER, Role.TRAINER, Role.ADMIN]
  },

  // ---------- Stagiaire ----------
  {
    header: 'Mon stage',
    icon: '',
    roles: [Role.LEARNER]
  },
  {
    label: 'Ma candidature',
    icon: 'assignment',
    route: '/dashboard/ma-candidature',
    roles: [Role.LEARNER]
  },
  {
    label: 'Ma convention',
    icon: 'description',
    route: '/dashboard/ma-convention',
    roles: [Role.LEARNER]
  },
  {
    label: 'Mon journal de bord',
    icon: 'menu_book',
    route: '/dashboard/mon-journal',
    roles: [Role.LEARNER]
  },
  {
    label: 'Mon évaluation',
    icon: 'grading',
    route: '/dashboard/mes-evaluations',
    roles: [Role.LEARNER]
  },

  // ---------- Admin ----------
  {
    header: 'Administration',
    icon: '',
    roles: [Role.ADMIN]
  },
  {
    label: 'Créer un encadrant',
    icon: 'person_add',
    route: '/dashboard/creer-encadrant',
    roles: [Role.ADMIN]
  },
  {
    label: 'Candidatures',
    icon: 'how_to_reg',
    route: '/dashboard/candidatures',
    roles: [Role.ADMIN]
  },
  {
    label: 'Conventions',
    icon: 'description',
    route: '/dashboard/conventions',
    roles: [Role.ADMIN]
  },
  {
    label: 'Stagiaires',
    icon: 'people',
    route: '/dashboard/stagiaires',
    roles: [Role.ADMIN]
  },
  {
    label: 'Journal d\'audit',
    icon: 'history',
    route: '/dashboard/audit',
    roles: [Role.ADMIN]
  },

  // ---------- Encadrant ----------
  {
    header: 'Encadrement',
    icon: '',
    roles: [Role.TRAINER]
  },
  {
    label: 'Mes stagiaires',
    icon: 'supervisor_account',
    route: '/dashboard/mes-stagiaires',
    roles: [Role.TRAINER]
  },
  {
    label: 'Journaux de bord',
    icon: 'rate_review',
    route: '/dashboard/journaux',
    roles: [Role.TRAINER]
  },
  {
    label: 'Évaluations',
    icon: 'grading',
    route: '/dashboard/evaluations',
    roles: [Role.TRAINER]
  }
];
