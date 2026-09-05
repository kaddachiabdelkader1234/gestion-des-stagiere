export enum Permission {
  // Courses
  COURSES_VIEW = 'courses.view',
  COURSES_CREATE = 'courses.create',
  COURSES_EDIT = 'courses.edit',
  COURSES_DELETE = 'courses.delete',
  COURSES_ENROLL = 'courses.enroll',
  COURSES_PUBLISH = 'courses.publish',
  COURSES_MANAGE_CONTENT = 'courses.manage-content',
  COURSES_TEACH = 'courses.teach',

  // Exams
  EXAMS_VIEW = 'exams.view',
  EXAMS_CREATE = 'exams.create',
  EXAMS_EDIT = 'exams.edit',
  EXAMS_DELETE = 'exams.delete',
  EXAMS_TAKE = 'exams.take',
  EXAMS_VIEW_RESULTS = 'exams.view-results',
  EXAMS_VIEW_ALL_RESULTS = 'exams.view-all-results',
  EXAMS_GRADE = 'exams.grade',

  // Certifications
  CERTIFICATIONS_VIEW = 'certifications.view',
  CERTIFICATIONS_CREATE = 'certifications.create',
  CERTIFICATIONS_EDIT = 'certifications.edit',
  CERTIFICATIONS_DELETE = 'certifications.delete',
  CERTIFICATIONS_ASSIGN = 'certifications.assign',

  // Badges
  BADGES_VIEW = 'badges.view',
  BADGES_CREATE = 'badges.create',
  BADGES_EDIT = 'badges.edit',
  BADGES_DELETE = 'badges.delete',
  BADGES_ASSIGN = 'badges.assign',

  // Training
  TRAINING_VIEW = 'training.view',
  TRAINING_CREATE = 'training.create',
  TRAINING_EDIT = 'training.edit',
  TRAINING_DELETE = 'training.delete',
  TRAINING_ENROLL = 'training.enroll',
  TRAINING_MANAGE_PARTICIPANTS = 'training.manage-participants',
  TRAINING_MANAGE_ALL = 'training.manage-all',

  // Skill Evidence
  SKILL_EVIDENCE_VIEW = 'skill-evidence.view',
  SKILL_EVIDENCE_UPLOAD = 'skill-evidence.upload',
  SKILL_EVIDENCE_VIEW_ALL = 'skill-evidence.view-all',
  SKILL_EVIDENCE_VALIDATE = 'skill-evidence.validate',
  SKILL_EVIDENCE_MANAGE = 'skill-evidence.manage',

  // Users
  USERS_VIEW = 'users.view',
  USERS_CREATE = 'users.create',
  USERS_EDIT = 'users.edit',
  USERS_DELETE = 'users.delete',
  USERS_MANAGE_ROLES = 'users.manage-roles',
  USERS_VIEW_ALL_DATA = 'users.view-all-data',
  USERS_VIEW_PROGRESS = 'users.view-progress',
  USERS_VIEW_PROFILES = 'users.view-profiles',
  USERS_ACTIVATE_DEACTIVATE = 'users.activate-deactivate',
  USERS_EXPORT = 'users.export',

  // Companies
  COMPANIES_VIEW = 'companies.view',
  COMPANIES_CREATE = 'companies.create',
  COMPANIES_EDIT = 'companies.edit',
  COMPANIES_DELETE = 'companies.delete',
  COMPANIES_MANAGE_ALL = 'companies.manage-all',

  // Company (own)
  COMPANY_VIEW = 'company.view',
  COMPANY_EDIT = 'company.edit',

  // Interviews
  INTERVIEWS_VIEW = 'interviews.view',
  INTERVIEWS_CREATE = 'interviews.create',
  INTERVIEWS_EDIT = 'interviews.edit',
  INTERVIEWS_DELETE = 'interviews.delete',
  INTERVIEWS_SCHEDULE = 'interviews.schedule',
  INTERVIEWS_UPDATE_STATUS = 'interviews.update-status',
  INTERVIEWS_VIEW_ALL = 'interviews.view-all',
  INTERVIEWS_MANAGE = 'interviews.manage',

  // Job Offers
  JOB_OFFERS_VIEW = 'job-offers.view',
  JOB_OFFERS_CREATE = 'job-offers.create',
  JOB_OFFERS_EDIT = 'job-offers.edit',
  JOB_OFFERS_DELETE = 'job-offers.delete',
  JOB_OFFERS_PUBLISH = 'job-offers.publish',

  // Planning
  PLANNING_VIEW = 'planning.view',
  PLANNING_CREATE = 'planning.create',
  PLANNING_EDIT = 'planning.edit',
  PLANNING_DELETE = 'planning.delete',
  PLANNING_VIEW_ALL = 'planning.view-all',
  PLANNING_MANAGE_ALL = 'planning.manage-all',

  // Events
  EVENTS_VIEW = 'events.view',
  EVENTS_CREATE = 'events.create',
  EVENTS_EDIT = 'events.edit',
  EVENTS_DELETE = 'events.delete',
  EVENTS_MANAGE = 'events.manage',
  EVENTS_MANAGE_PARTICIPANTS = 'events.manage-participants',
  EVENTS_VIEW_ALL = 'events.view-all',
  EVENTS_MANAGE_ALL = 'events.manage-all',

  // Contacts
  CONTACTS_VIEW = 'contacts.view',
  CONTACTS_CREATE = 'contacts.create',
  CONTACTS_EDIT = 'contacts.edit',
  CONTACTS_DELETE = 'contacts.delete',
  CONTACTS_EXPORT = 'contacts.export',

  // Participation
  PARTICIPATION_VIEW = 'participation.view',
  PARTICIPATION_VIEW_ALL = 'participation.view-all',
  PARTICIPATION_MANAGE = 'participation.manage',
  PARTICIPATION_EXPORT = 'participation.export',

  // Learning Path
  LEARNING_PATH_VIEW = 'learning-path.view',
  LEARNING_PATH_CREATE = 'learning-path.create',
  LEARNING_PATH_EDIT = 'learning-path.edit',
  LEARNING_PATH_DELETE = 'learning-path.delete',

  // Offers (general)
  OFFERS_VIEW = 'offers.view',
  OFFERS_CREATE = 'offers.create',
  OFFERS_EDIT = 'offers.edit',

  // Profile
  PROFILE_VIEW = 'profile.view',
  PROFILE_EDIT = 'profile.edit',

  // System
  SYSTEM_SETTINGS = 'system.settings',
  SYSTEM_LOGS = 'system.logs',
  SYSTEM_BACKUP = 'system.backup',
  ADMIN_FULL_ACCESS = 'admin.full-access'
}
