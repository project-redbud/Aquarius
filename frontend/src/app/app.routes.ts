import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'pick', pathMatch: 'full' },
  { path: 'throw', loadComponent: () => import('./pages/throw/throw').then(m => m.ThrowPage) },
  { path: 'pick', loadComponent: () => import('./pages/pick/pick').then(m => m.PickPage) },
  { path: 'daily', loadComponent: () => import('./pages/daily/daily').then(m => m.DailyPage) },
  { path: 'admin', loadComponent: () => import('./pages/admin/admin').then(m => m.AdminPage) },
];
