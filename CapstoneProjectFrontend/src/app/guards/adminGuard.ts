import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { isAdmin } from "../helpers";

export const adminGuard: CanActivateFn = () => {
    const router = inject(Router);
    const status = isAdmin();
    if (status) return true;
    router.navigate(['/user-dashboard']);
    return false;
}