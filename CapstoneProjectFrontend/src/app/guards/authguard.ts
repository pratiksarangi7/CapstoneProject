import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { isLoggedIn } from "../helpers";

export const authGuard: CanActivateFn = () => {
    const router = inject(Router);
    const status = isLoggedIn();
    if (status) return true;
    router.navigate(['/login']);
    return false;
}