import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AuthUser {
  userId: number;
  username: string;
  isAdmin: boolean;
}

export interface LoginResponse {
  token: string;
  username: string;
  isAdmin: boolean;
}

const TOKEN_KEY = 'aquarius_jwt';
const USER_KEY = 'aquarius_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private base = environment.apiBase + '/api/auth';

  // ── Reactive state ─────────────────────────────────────
  private _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  private _user = signal<AuthUser | null>(this.loadUser());

  readonly token = this._token.asReadonly();
  readonly user = this._user.asReadonly();
  readonly isLoggedIn = computed(() => !!this._token());
  readonly isAdmin = computed(() => this._user()?.isAdmin ?? false);

  constructor(private http: HttpClient) {}

  // ── Public API ──────────────────────────────────────────

  login(usernameOrEmail: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.base}/login`, {
      login: usernameOrEmail,
      password
    }).pipe(tap(res => this.persist(res)));
  }

  register(username: string, email: string, password: string, confirmPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/register`, {
      username,
      email,
      password,
      confirmPassword
    });
  }

  /** 邮箱验证后保存登录状态 */
  persistLogin(res: LoginResponse): void {
    this.persist(res);
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._token.set(null);
    this._user.set(null);
  }

  getToken(): string | null {
    return this._token();
  }

  // ── Internals ───────────────────────────────────────────

  private persist(res: LoginResponse): void {
    const userId = this.parseUserId(res.token);
    localStorage.setItem(TOKEN_KEY, res.token);
    localStorage.setItem(USER_KEY, JSON.stringify({ userId, username: res.username, isAdmin: res.isAdmin }));
    this._token.set(res.token);
    this._user.set({ userId, username: res.username, isAdmin: res.isAdmin });
  }

  private loadUser(): AuthUser | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try { return JSON.parse(raw); }
    catch { return null; }
  }

  private parseUserId(token: string): number {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      // ClaimTypes.NameIdentifier can be in various formats
      const id = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
        || payload.nameid
        || payload.sub;
      return parseInt(id, 10) || 0;
    } catch { return 0; }
  }
}
