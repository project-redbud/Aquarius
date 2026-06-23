import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { adminGuard } from './guards/admin.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'pick', pathMatch: 'full' },
  { path: 'throw', loadComponent: () => import('./pages/throw/throw').then(m => m.ThrowPage), data: { title: '投瓶' } },
  { path: 'pick', loadComponent: () => import('./pages/pick/pick').then(m => m.PickPage), data: { title: '捞瓶' } },
  { path: 'daily', loadComponent: () => import('./pages/daily/daily').then(m => m.DailyPage), data: { title: '每日推送' } },
  { path: 'admin', loadComponent: () => import('./pages/admin/admin').then(m => m.AdminPage), canActivate: [adminGuard], data: { title: '管理面板' } },
  { path: 'my', loadComponent: () => import('./pages/my/my').then(m => m.MyPage), canActivate: [authGuard], data: { title: '我的' } },
  { path: 'bottle/:id', loadComponent: () => import('./pages/bottle-detail/bottle-detail').then(m => m.BottleDetailPage) },
  { path: 'login', loadComponent: () => import('./pages/login/login').then(m => m.LoginPage), data: { title: '登录' } },
  { path: 'register', loadComponent: () => import('./pages/register/register').then(m => m.RegisterPage), data: { title: '注册' } },
  { path: 'report', loadComponent: () => import('./pages/report/report').then(m => m.ReportPage), data: { title: '举报' } },
];