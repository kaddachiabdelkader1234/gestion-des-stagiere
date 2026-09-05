import { Injectable } from '@angular/core';
import { Role } from '../enums/role.enum';
import { Permission } from '../enums/permission.enum';
import { ROLE_PERMISSIONS } from '../config/role-permission.config';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class PermissionService {
  constructor(private authService: AuthService) {}

  /**
   * Vérifie si l'utilisateur actuel a une permission spécifique
   */
  hasPermission(permission: Permission): boolean {
    if (!this.authService.isAuthenticated()) {
      return false;
    }

    const roles = this.authService.getRoles();
    
    // Check each role's permissions
    for (const roleStr of roles) {
      const role = roleStr as Role;
      const permissions = ROLE_PERMISSIONS[role];
      if (permissions && permissions.includes(permission)) {
        return true;
      }
    }
    
    return false;
  }

  /**
   * Vérifie si l'utilisateur a au moins une des permissions fournies
   */
  hasAnyPermission(permissions: Permission[]): boolean {
    return permissions.some(permission => this.hasPermission(permission));
  }

  /**
   * Vérifie si l'utilisateur a toutes les permissions fournies
   */
  hasAllPermissions(permissions: Permission[]): boolean {
    return permissions.every(permission => this.hasPermission(permission));
  }

  /**
   * Récupère toutes les permissions de l'utilisateur actuel
   */
  getUserPermissions(): Permission[] {
    if (!this.authService.isAuthenticated()) {
      return [];
    }

    const roles = this.authService.getRoles();
    const allPermissions: Permission[] = [];
    
    for (const roleStr of roles) {
      const role = roleStr as Role;
      const permissions = ROLE_PERMISSIONS[role] || [];
      allPermissions.push(...permissions);
    }
    
    // Remove duplicates
    return [...new Set(allPermissions)];
  }

  /**
   * Vérifie si l'utilisateur a un rôle spécifique
   */
  hasRole(role: Role): boolean {
    return this.authService.hasRole(role);
  }

  /**
   * Vérifie si l'utilisateur a au moins un des rôles fournis
   */
  hasAnyRole(roles: Role[]): boolean {
    return this.authService.hasAnyRole(roles);
  }

  /**
   * Vérifie si l'utilisateur est un administrateur
   */
  isAdmin(): boolean {
    return this.authService.isAdmin();
  }

  /**
   * Vérifie si l'utilisateur est RH Smartek
   */
  isRHSmartek(): boolean {
    return this.hasRole(Role.RH_SMARTEK);
  }

  /**
   * Vérifie si l'utilisateur a des droits d'administration (Admin ou RH Smartek)
   */
  hasAdminRights(): boolean {
    return this.hasAnyRole([Role.ADMIN, Role.RH_SMARTEK]);
  }
}