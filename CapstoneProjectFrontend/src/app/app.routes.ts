import { Routes } from '@angular/router';
import { Login } from './components/login/login';
import { Register } from './components/register/register';
import { UserDashboard } from './components/user-dashboard/user-dashboard';
import { AdminDashboard } from './components/admin-dashboard/admin-dashboard';
import { adminGuard } from './guards/adminGuard';
import { authGuard } from './guards/authguard';
import { rootRedirectGuard } from './guards/rootRedirectGuard';

export const routes: Routes = [
    {
        path: '', pathMatch: 'full',
        canActivate: [rootRedirectGuard],
        children: []
    },
    { path: 'login', component: Login },
    { path: 'register', component: Register },
    {
        path: "user-dashboard", component: UserDashboard, canActivate: [authGuard]
    },
    {
        path: "admin-dashboard", component: AdminDashboard, canActivate: [adminGuard]
    }
];
