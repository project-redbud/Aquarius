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
  editedAt?: string | null;
  userId?: number | null;
  requireLogin: boolean;
  commentsPrivate: boolean;
  expiresAt: string;
  reThrowCount: number;
  lastReThrowAt?: string | null;
  isAdminBadge: boolean;
}

export interface Comment {
  id: number;
  content: string;
  createdAt: string;
  editedAt?: string | null;
  userId?: number | null;
  userToken?: string | null; // only admin
  commentId?: number | null;
  parentReplyId?: number | null;
  parentContent?: string | null;
  replyCount?: number;
  replies?: Comment[];
  isAdminBadge: boolean;
}

export interface DailyPush {
  story: Bottle | null;
  qa: Bottle | null;
}

export interface PaginatedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
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
    if (typeof crypto !== 'undefined' && crypto.randomUUID) {
      return crypto.randomUUID().replace(/-/g, '');
    }
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

  throwBottle(content: string, imageBase64?: string, authorName?: string, requireLogin?: boolean, commentsPrivate?: boolean, isAdminBadge?: boolean): Observable<Bottle> {
    return this.http.post<Bottle>(`${this.base}/bottles`, {
      content,
      imageBase64,
      authorName: authorName || null,
      requireLogin: requireLogin ?? false,
      commentsPrivate: commentsPrivate ?? false,
      isAdminBadge: isAdminBadge ?? false
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

  editBottle(id: number, content: string, imageBase64?: string, authorName?: string): Observable<Bottle> {
    return this.http.put<Bottle>(`${this.base}/bottles/${id}`, {
      content,
      imageBase64,
      authorName: authorName || null
    });
  }

  deleteBottle(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/bottles/${id}`);
  }

  toggleLike(bottleId: number): Observable<{ liked: boolean; likeCount: number }> {
    return this.http.post<{ liked: boolean; likeCount: number }>(
      `${this.base}/bottles/${bottleId}/like`, null, { headers: this.headers() }
    );
  }

  rethrowBottle(id: number): Observable<Bottle> {
    return this.http.post<Bottle>(`${this.base}/bottles/${id}/rethrow`, null, { headers: this.headers() });
  }

  // ── comments ───────────────────────────────────────────

  getComments(bottleId: number): Observable<Comment[]> {
    return this.http.get<Comment[]>(`${this.base}/bottles/${bottleId}/comments`, {
      headers: this.headers()
    });
  }

  addComment(bottleId: number, content: string, commentId?: number, parentReplyId?: number, isAdminBadge?: boolean): Observable<Comment> {
    return this.http.post<Comment>(`${this.base}/bottles/${bottleId}/comments`,
      { content, commentId: commentId || null, parentReplyId: parentReplyId || null, isAdminBadge: isAdminBadge ?? false },
      { headers: this.headers() }
    );
  }

  editComment(bottleId: number, commentId: number, content: string): Observable<Comment> {
    return this.http.put<Comment>(`${this.base}/bottles/${bottleId}/comments/${commentId}`, { content });
  }

  deleteComment(bottleId: number, commentId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/bottles/${bottleId}/comments/${commentId}`, {
      headers: this.headers()
    });
  }

  getReplies(bottleId: number, commentId: number): Observable<Comment[]> {
    return this.http.get<Comment[]>(`${this.base}/bottles/${bottleId}/comments/${commentId}/replies`, {
      headers: this.headers()
    });
  }

  // ── daily ──────────────────────────────────────────────

  getDaily(date?: string): Observable<DailyPush> {
    let url = `${this.base}/daily`;
    if (date) url += `?date=${date}`;
    return this.http.get<DailyPush>(url, {
      headers: this.headers()
    });
  }

  // ── admin (JWT via interceptor) ────────────────────────

  adminGetComments(bottleId: number): Observable<Comment[]> {
    return this.http.get<Comment[]>(`${this.base}/admin/bottles/${bottleId}/comments`);
  }

  adminListBottles(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/admin/bottles`);
  }

  adminDeleteBottle(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/admin/bottles/${id}`);
  }

  adminCreateDaily(type: string, content: string, date: string, imagePath: string | null): Observable<any> {
    return this.http.post<any>(`${this.base}/admin/daily`,
      { type, content, date, imagePath }
    );
  }

  adminCheckDaily(type: string, date: string): Observable<{ id: number; content: string; date: string; bottleId: number }> {
    return this.http.get<{ id: number; content: string; date: string; bottleId: number }>(
      `${this.base}/admin/daily/check?type=${type}&date=${date}`
    );
  }

  // ── my ─────────────────────────────────────────────────

  getMyBottles(): Observable<Bottle[]> {
    return this.http.get<Bottle[]>(`${this.base}/bottles/mine`, {
      headers: this.headers()
    });
  }

  getMyLikedBottles(page = 1, pageSize = 15): Observable<PaginatedResult<Bottle>> {
    return this.http.get<PaginatedResult<Bottle>>(`${this.base}/bottles/liked?page=${page}&pageSize=${pageSize}`, {
      headers: this.headers()
    });
  }

  getMyComments(): Observable<{ id: number; content: string; createdAt: string; editedAt?: string | null; bottleId: number; bottleContent: string }[]> {
    return this.http.get<any[]>(`${this.base}/comments/mine`, {
      headers: this.headers()
    });
  }
}