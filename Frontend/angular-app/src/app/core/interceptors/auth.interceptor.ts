import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * HTTP interceptor that adds JWT token to outgoing requests
 * and handles authentication errors with refresh token support.
 *
 * On 401: attempts to refresh the access token silently.
 * If refresh succeeds, retries the original request.
 * If refresh fails, logs out and redirects to sign-in.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const token = authService.getToken();

  // Only attach token to API Gateway requests
  const isApiRequest = req.url.includes('/api/');

  // Skip refresh for login/register/refresh endpoints
  const isAuthEndpoint = req.url.includes('/auth/login') ||
                         req.url.includes('/auth/register') ||
                         req.url.includes('/auth/refresh');
  
  if (token && isApiRequest) {
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // On 401: try to refresh the token silently (unless this IS an auth endpoint)
      if (error.status === 401 && !isAuthEndpoint && authService.getRefreshToken()) {
        return authService.refreshAccessToken().pipe(
          switchMap(success => {
            if (success) {
              // Retry the original request with the new token
              const newToken = authService.getToken();
              const retryReq = req.clone({
                setHeaders: { Authorization: `Bearer ${newToken}` }
              });
              return next(retryReq);
            }
            // Refresh failed — log out and redirect
            authService.logout();
            router.navigate(['/auth/sign-in']);
            return throwError(() => error);
          })
        );
      }

      // No refresh token or refresh failed: clear the stale session
      if (error.status === 401) {
        authService.logout();
        router.navigate(['/auth/sign-in']);
      }

      // Authenticated but not allowed: keep the session, send them somewhere they can be.
      if (error.status === 403) {
        router.navigate(['/dashboard']);
      }

      return throwError(() => error);
    })
  );
};