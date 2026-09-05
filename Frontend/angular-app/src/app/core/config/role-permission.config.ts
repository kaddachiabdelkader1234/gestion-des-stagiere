import { Role } from '../enums/role.enum';
import { Permission } from '../enums/permission.enum';

export const ROLE_PERMISSIONS: Record<Role, Permission[]> = {
  [Role.LEARNER]: [
    // Courses & Learning
    Permission.COURSES_VIEW,
    Permission.COURSES_ENROLL,
    Permission.LEARNING_PATH_VIEW,

    // Exams
    Permission.EXAMS_TAKE,
    Permission.EXAMS_VIEW_RESULTS,

    // Certifications & Badges
    Permission.CERTIFICATIONS_VIEW,
    Permission.BADGES_VIEW,
    Permission.BADGES_ASSIGN,

    // Training
    Permission.TRAINING_VIEW,
    Permission.TRAINING_ENROLL,

    // Skill Evidence
    Permission.SKILL_EVIDENCE_VIEW,
    Permission.SKILL_EVIDENCE_UPLOAD,

    // Profile
    Permission.PROFILE_VIEW,
    Permission.PROFILE_EDIT
  ],

  [Role.TRAINER]: [
    // Training Management
    Permission.TRAINING_VIEW,
    Permission.TRAINING_CREATE,
    Permission.TRAINING_EDIT,
    Permission.TRAINING_DELETE,
    Permission.TRAINING_MANAGE_PARTICIPANTS,

    // Planning/Schedule
    Permission.PLANNING_VIEW,
    Permission.PLANNING_CREATE,
    Permission.PLANNING_EDIT,
    Permission.PLANNING_DELETE,

    // Courses (limited)
    Permission.COURSES_VIEW,
    Permission.COURSES_TEACH,

    // Events
    Permission.EVENTS_VIEW,
    Permission.EVENTS_CREATE,
    Permission.EVENTS_EDIT,
    Permission.EVENTS_MANAGE,

    // Offers & Opportunities
    Permission.OFFERS_VIEW,
    Permission.OFFERS_CREATE,
    Permission.OFFERS_EDIT,

    // Users (view only)
    Permission.USERS_VIEW,
    Permission.USERS_VIEW_PROGRESS,

    // Skill Evidence
    Permission.SKILL_EVIDENCE_VIEW,
    Permission.SKILL_EVIDENCE_VALIDATE,

    // Profile
    Permission.PROFILE_VIEW,
    Permission.PROFILE_EDIT
  ],

  [Role.RH_COMPANY]: [
    // Interview Management
    Permission.INTERVIEWS_VIEW,
    Permission.INTERVIEWS_CREATE,
    Permission.INTERVIEWS_EDIT,
    Permission.INTERVIEWS_DELETE,
    Permission.INTERVIEWS_SCHEDULE,
    Permission.INTERVIEWS_UPDATE_STATUS,

    // Planning/Schedule
    Permission.PLANNING_VIEW,
    Permission.PLANNING_CREATE,
    Permission.PLANNING_EDIT,

    // Job Offers
    Permission.JOB_OFFERS_VIEW,
    Permission.JOB_OFFERS_CREATE,
    Permission.JOB_OFFERS_EDIT,
    Permission.JOB_OFFERS_DELETE,
    Permission.JOB_OFFERS_PUBLISH,

    // Events
    Permission.EVENTS_VIEW,
    Permission.EVENTS_CREATE,
    Permission.EVENTS_EDIT,
    Permission.EVENTS_MANAGE,
    Permission.EVENTS_MANAGE_PARTICIPANTS,

    // Company (own company only)
    Permission.COMPANY_VIEW,
    Permission.COMPANY_EDIT,

    // Users (limited to candidates)
    Permission.USERS_VIEW,
    Permission.USERS_VIEW_PROFILES,

    // Profile
    Permission.PROFILE_VIEW,
    Permission.PROFILE_EDIT
  ],

  [Role.RH_SMARTEK]: [
    // Course Management (FULL)
    Permission.COURSES_VIEW,
    Permission.COURSES_CREATE,
    Permission.COURSES_EDIT,
    Permission.COURSES_DELETE,
    Permission.COURSES_PUBLISH,
    Permission.COURSES_MANAGE_CONTENT,

    // Exam Management (FULL)
    Permission.EXAMS_VIEW,
    Permission.EXAMS_CREATE,
    Permission.EXAMS_EDIT,
    Permission.EXAMS_DELETE,
    Permission.EXAMS_VIEW_ALL_RESULTS,
    Permission.EXAMS_GRADE,

    // Certification Management
    Permission.CERTIFICATIONS_VIEW,
    Permission.CERTIFICATIONS_CREATE,
    Permission.CERTIFICATIONS_EDIT,
    Permission.CERTIFICATIONS_DELETE,
    Permission.CERTIFICATIONS_ASSIGN,

    // Badge Management
    Permission.BADGES_VIEW,
    Permission.BADGES_CREATE,
    Permission.BADGES_EDIT,
    Permission.BADGES_DELETE,
    Permission.BADGES_ASSIGN,

    // Training Management (FULL)
    Permission.TRAINING_VIEW,
    Permission.TRAINING_CREATE,
    Permission.TRAINING_EDIT,
    Permission.TRAINING_DELETE,
    Permission.TRAINING_MANAGE_ALL,

    // Skill Evidence Management
    Permission.SKILL_EVIDENCE_VIEW_ALL,
    Permission.SKILL_EVIDENCE_VALIDATE,
    Permission.SKILL_EVIDENCE_MANAGE,

    // Interview Management
    Permission.INTERVIEWS_VIEW_ALL,
    Permission.INTERVIEWS_MANAGE,

    // User Management
    Permission.USERS_VIEW,
    Permission.USERS_CREATE,
    Permission.USERS_EDIT,
    Permission.USERS_DELETE,
    Permission.USERS_MANAGE_ROLES,
    Permission.USERS_VIEW_ALL_DATA,

    // Company Management
    Permission.COMPANIES_VIEW,
    Permission.COMPANIES_CREATE,
    Permission.COMPANIES_EDIT,
    Permission.COMPANIES_DELETE,

    // Contact Management
    Permission.CONTACTS_VIEW,
    Permission.CONTACTS_CREATE,
    Permission.CONTACTS_EDIT,
    Permission.CONTACTS_DELETE,

    // Event Management
    Permission.EVENTS_VIEW_ALL,
    Permission.EVENTS_CREATE,
    Permission.EVENTS_EDIT,
    Permission.EVENTS_DELETE,
    Permission.EVENTS_MANAGE_ALL,

    // Participation Management
    Permission.PARTICIPATION_VIEW_ALL,
    Permission.PARTICIPATION_MANAGE,

    // Learning Path
    Permission.LEARNING_PATH_VIEW,
    Permission.LEARNING_PATH_CREATE,
    Permission.LEARNING_PATH_EDIT,
    Permission.LEARNING_PATH_DELETE,

    // Planning
    Permission.PLANNING_VIEW_ALL,
    Permission.PLANNING_MANAGE_ALL,

    // Profile
    Permission.PROFILE_VIEW,
    Permission.PROFILE_EDIT
  ],

  [Role.ADMIN]: [
    // User Management (FULL)
    Permission.USERS_VIEW,
    Permission.USERS_CREATE,
    Permission.USERS_EDIT,
    Permission.USERS_DELETE,
    Permission.USERS_MANAGE_ROLES,
    Permission.USERS_ACTIVATE_DEACTIVATE,
    Permission.USERS_VIEW_ALL_DATA,
    Permission.USERS_EXPORT,

    // Company Management (FULL)
    Permission.COMPANIES_VIEW,
    Permission.COMPANIES_CREATE,
    Permission.COMPANIES_EDIT,
    Permission.COMPANIES_DELETE,
    Permission.COMPANIES_MANAGE_ALL,

    // Contact Management (FULL)
    Permission.CONTACTS_VIEW,
    Permission.CONTACTS_CREATE,
    Permission.CONTACTS_EDIT,
    Permission.CONTACTS_DELETE,
    Permission.CONTACTS_EXPORT,

    // Participation Management
    Permission.PARTICIPATION_VIEW_ALL,
    Permission.PARTICIPATION_MANAGE,
    Permission.PARTICIPATION_EXPORT,

    // System Settings
    Permission.SYSTEM_SETTINGS,
    Permission.SYSTEM_LOGS,
    Permission.SYSTEM_BACKUP,

    // Full Access
    Permission.ADMIN_FULL_ACCESS,

    // Profile
    Permission.PROFILE_VIEW,
    Permission.PROFILE_EDIT
  ],

};
