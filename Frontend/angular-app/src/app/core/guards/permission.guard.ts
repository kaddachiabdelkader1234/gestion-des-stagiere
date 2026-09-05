import { inject } from '@angular/core';
import { Router, CanActivateFn, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Permission guard that checks if user has required roles.
 * Supports checking for specific roles and requiring all or any.
 */
export const permissionGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // Get required roles from route data
  const requiredRoles = route.data['roles'] as string[];
  const requireAll = route.data['requireAll'] as boolean; // true = all roles, false = any role

  if (!requiredRoles || requiredRoles.length === 0) {
    // No roles required, allow access
    return true;
  }

  // Check if user has required roles
  const hasAccess = requireAll
    ? authService.hasAllRoles(requiredRoles)
    : authService.hasAnyRole(requiredRoles);

  if (!hasAccess) {
    console.warn('Access denied: User does not have required roles', requiredRoles);
    router.navigate(['/dashboard']);
    return false;
  }

  return true;
};