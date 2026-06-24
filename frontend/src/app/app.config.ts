import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './services/auth.interceptor';

/** 防止浏览器缓存 API 响应 */
const noCacheInterceptor = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  if (req.url.includes('/api/')) {
    req = req.clone({ setHeaders: { 'Cache-Control': 'no-cache', 'Pragma': 'no-cache' } });
  }
  return next(req);
};

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor, noCacheInterceptor]))
  ]
};
