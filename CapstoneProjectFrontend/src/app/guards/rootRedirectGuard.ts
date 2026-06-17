import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { isLoggedIn, isAdmin } from '../helpers'; 
export const rootRedirectGuard: CanActivateFn = () => {
  const router = inject(Router);

  if (!isLoggedIn()) {
    router.navigate(['/login']);
    return false;
  }

  if (isAdmin()) {
    router.navigate(['/admin-dashboard']);
    return false;
  }

  router.navigate(['/user-dashboard']);
  return false;
};