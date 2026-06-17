import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Bottle {
  id: number;
  content: string;
  imagePath: string | null;
  authorName: string | null;
  type: string;
  likeCount: number;
  commentCount: number;
  likedByMe: boolean;
  createdAt: string;
}

export interface Comment {
  id: number;
  content: string;
  createdAt: string;
  userToken?: string | null; // only admin
}

export interface DailyPush {
  story: Bottle | null;
  qa: Bottle | null;
}

const STORAGE_KEY = 'aquarius_user_token';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private base = '/api';

  constructor(private http: HttpClient) {}

  // ── user identity ──────────────────────────────────────

  getUserToken(): string {
    let token = localStorage.getItem(STORAGE_KEY);
    if (!token) {
      token = crypto.randomUUID().replace(/-/g, '');
      localStorage.setItem(STORAGE_KEY, token);
    }
    return token;
  }

  private headers(extra?: Record<string, string>): HttpHeaders {
    let h = new HttpHeaders().set('X-User-Token', this.getUserToken());
    if (extra) {
      for (const [k, v] of Object.entries(extra)) {
        h = h.set(k, v);
      }
    }
    return h;
  }

  // ── bottles ────────────────────────────────────────────

  throwBottle(content: string, imageBase64?: string, authorName?: string): Observable<Bottle> {
    return this.http.post<Bottle>(`${this.base}/bottles`, {
      content,
      imageBase64,
      authorName: authorName || null
    }, { headers: this.headers() });
  }

  pickRandom(): Observable<Bottle | null> {
    return this.http.get<Bottle | null>(`${this.base}/bottles/random`, {
      headers: this.headers()
    });
  }

  getBottle(id: number): Observable<Bottle> {
    return this.http.get<Bottle>(`${this.base}/bottles/${id}`, {
      headers: this.headers()
    });
  }

  toggleLike(bottleId: number): Observable<{ liked: boolean; likeCount: number }> {
    return this.http.post<{ liked: boolean; likeCount: number }>(
      `${this.base}/bottles/${bottleId}/like`, null, { headers: this.headers() }
    );
  }

  // ── comments ───────────────────────────────────────────

  getComments(bottleId: number): Observable<Comment[]> {
    return this.http.get<Comment[]>(`${this.base}/bottles/${bottleId}/comments`, {
      headers: this.headers()
    });
  }

  addComment(bottleId: number, content: string): Observable<Comment> {
    return this.http.post<Comment>(`${this.base}/bottles/${bottleId}/comments`,
      { content }, { headers: this.headers() }
    );
  }

  // ── daily ──────────────────────────────────────────────

  getDaily(): Observable<DailyPush> {
    return this.http.get<DailyPush>(`${this.base}/daily`, {
      headers: this.headers()
    });
  }

  // ── admin ──────────────────────────────────────────────

  adminGetComments(bottleId: number, adminKey: string): Observable<Comment[]> {
    return this.http.get<Comment[]>(`${this.base}/admin/bottles/${bottleId}/comments`, {
      headers: new HttpHeaders().set('X-Admin-Key', adminKey)
    });
  }

  adminListBottles(adminKey: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/admin/bottles`, {
      headers: new HttpHeaders().set('X-Admin-Key', adminKey)
    });
  }

  adminDeleteBottle(id: number, adminKey: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/admin/bottles/${id}`, {
      headers: new HttpHeaders().set('X-Admin-Key', adminKey)
    });
  }
}
