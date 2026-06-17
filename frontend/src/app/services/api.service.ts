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
  commentId?: number | null;
  parentReplyId?: number | null;
  parentContent?: string | null;
  replyCount?: number;
  replies?: Comment[];
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
      token = this.generateUUID();
      localStorage.setItem(STORAGE_KEY, token);
    }
    return token;
  }

  private generateUUID(): string {
    // crypto.randomUUID not available in older mobile browsers
    if (typeof crypto !== 'undefined' && crypto.randomUUID) {
      return crypto.randomUUID().replace(/-/g, '');
    }
    // fallback
    return 'xxxxxxxxxxxx4xxxyxxxxxxxxxxxxxxx'.replace(/[xy]/g, c => {
      const r = Math.random() * 16 | 0;
      return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    });
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

  addComment(bottleId: number, content: string, commentId?: number, parentReplyId?: number): Observable<Comment> {
    return this.http.post<Comment>(`${this.base}/bottles/${bottleId}/comments`,
      { content, commentId: commentId || null, parentReplyId: parentReplyId || null },
      { headers: this.headers() }
    );
  }

  getReplies(bottleId: number, commentId: number): Observable<Comment[]> {
    return this.http.get<Comment[]>(`${this.base}/bottles/${bottleId}/comments/${commentId}/replies`, {
      headers: this.headers()
    });
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

  adminCreateDaily(type: string, content: string, date: string, imagePath: string | null, adminKey: string): Observable<any> {
    return this.http.post<any>(`${this.base}/admin/daily`,
      { type, content, date, imagePath },
      { headers: new HttpHeaders().set('X-Admin-Key', adminKey) }
    );
  }

  adminCheckDaily(type: string, date: string, adminKey: string): Observable<{ id: number; content: string; date: string; bottleId: number }> {
    return this.http.get<{ id: number; content: string; date: string; bottleId: number }>(
      `${this.base}/admin/daily/check?type=${type}&date=${date}`,
      { headers: new HttpHeaders().set('X-Admin-Key', adminKey) }
    );
  }

  // ── my ─────────────────────────────────────────────────

  getMyBottles(): Observable<Bottle[]> {
    return this.http.get<Bottle[]>(`${this.base}/bottles/mine`, {
      headers: this.headers()
    });
  }

  getMyComments(): Observable<{ id: number; content: string; createdAt: string; bottleId: number; bottleContent: string }[]> {
    return this.http.get<any[]>(`${this.base}/comments/mine`, {
      headers: this.headers()
    });
  }
}
