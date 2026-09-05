import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Auth guard that checks if user is authenticated.
 * Redirects to the sign-in page if not, preserving the attempted URL.
 */
export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    // '/auth/sign-in', not '/login': the latter matches no route in app.routes.ts, so the
    // wildcard sent the user to the public home page instead of the login form.
    router.navigate(['/auth/sign-in'], { queryParams: { redirectTo: state.url } });
    return false;
  }

  return true;
};