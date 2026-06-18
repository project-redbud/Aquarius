import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { adminGuard } from './guards/admin.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'pick', pathMatch: 'full' },
  { path: 'throw', loadComponent: () => import('./pages/throw/throw').then(m => m.ThrowPage) },
  { path: 'pick', loadComponent: () => import('./pages/pick/pick').then(m => m.PickPage) },
  { path: 'daily', loadComponent: () => import('./pages/daily/daily').then(m => m.DailyPage) },
  { path: 'admin', loadComponent: () => import('./pages/admin/admin').then(m => m.AdminPage), canActivate: [adminGuard] },
  { path: 'my', loadComponent: () => import('./pages/my/my').then(m => m.MyPage), canActivate: [authGuard] },
  { path: 'bottle/:id', loadComponent: () => import('./pages/bottle-detail/bottle-detail').then(m => m.BottleDetailPage) },
  { path: 'login', loadComponent: () => import('./pages/login/login').then(m => m.LoginPage) },
  { path: 'register', loadComponent: () => import('./pages/register/register').then(m => m.RegisterPage) },
];