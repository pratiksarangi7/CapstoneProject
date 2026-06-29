import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { isAdmin, isLoggedIn } from "../helpers";

export const adminGuard: CanActivateFn = () => {
    const router = inject(Router);
    if (!isLoggedIn()) {
        router.navigate(['/login']);
        return false;
    }
    const status = isAdmin();
    if (status) return true;
    router.navigate(['/user-dashboard']);
    return false;
}