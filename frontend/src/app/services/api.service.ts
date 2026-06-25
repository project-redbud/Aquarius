import { Injectable, signal } from '@angular/core';
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
  reportedBottleId?: number | null;
  isClosed: boolean;
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
  adminUsername?: string | null;
  isBottleOwnerBadge: boolean;
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

  /** 共享未读通知数（App 角标 + 通知中心同步读取） */
  readonly unreadCount = signal(0);

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

  addComment(bottleId: number, content: string, commentId?: number, parentReplyId?: number, isAdminBadge?: boolean, isBottleOwnerBadge?: boolean): Observable<Comment> {
    return this.http.post<Comment>(`${this.base}/bottles/${bottleId}/comments`,
      { content, commentId: commentId || null, parentReplyId: parentReplyId || null, isAdminBadge: isAdminBadge ?? false, isBottleOwnerBadge: isBottleOwnerBadge ?? false },
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

  adminListBottles(page = 1, pageSize = 10): Observable<{ items: any[]; total: number; page: number; pageSize: number }> {
    return this.http.get<any>(`${this.base}/admin/bottles?page=${page}&pageSize=${pageSize}`);
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

  adminListDaily(page = 1, pageSize = 10): Observable<{ items: any[]; total: number; page: number; pageSize: number }> {
    return this.http.get<any>(`${this.base}/admin/daily?page=${page}&pageSize=${pageSize}`);
  }

  adminEditDaily(id: number, content: string, imagePath?: string | null): Observable<any> {
    return this.http.put<any>(`${this.base}/admin/daily/${id}`, { content, imagePath });
  }

  adminDeleteDaily(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/admin/daily/${id}`);
  }

  adminRepublishDaily(id: number, date: string, force = false): Observable<any> {
    return this.http.post<any>(`${this.base}/admin/daily/${id}/republish`, { date, force });
  }

  adminGetSettings(): Observable<any> {
    return this.http.get<any>(`${this.base}/admin/settings`);
  }

  adminUpdateSettings(
    siteName?: string, copyright?: string,
    smtpHost?: string, smtpPort?: number, smtpUser?: string,
    smtpPassword?: string, smtpFrom?: string,
    smtpEnableSsl?: boolean, siteBaseUrl?: string
  ): Observable<any> {
    return this.http.put<any>(`${this.base}/admin/settings`, {
      siteName, copyright,
      smtpHost, smtpPort, smtpUser,
      smtpPassword, smtpFrom,
      smtpEnableSsl, siteBaseUrl
    });
  }

  // ── admin users ────────────────────────────────────────

  adminListUsers(page = 1, pageSize = 10, q?: string): Observable<any> {
    let url = `${this.base}/admin/users?page=${page}&pageSize=${pageSize}`;
    if (q) url += `&q=${encodeURIComponent(q)}`;
    return this.http.get<any>(url);
  }

  adminGetUser(id: number): Observable<any> {
    return this.http.get<any>(`${this.base}/admin/users/${id}`);
  }

  adminDeleteUser(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/admin/users/${id}`);
  }

  adminBanUser(id: number, reason: string, days: number): Observable<any> {
    return this.http.post<any>(`${this.base}/admin/users/${id}/ban`, { reason, days });
  }

  adminUnbanUser(id: number): Observable<any> {
    return this.http.post<any>(`${this.base}/admin/users/${id}/unban`, {});
  }

  adminSetUserRole(id: number, role: string): Observable<any> {
    return this.http.post<any>(`${this.base}/admin/users/${id}/role`, { role });
  }

  adminSearch(q: string): Observable<{ users: any[]; bottles: any[] }> {
    return this.http.get<any>(`${this.base}/admin/users/search?q=${encodeURIComponent(q)}`);
  }

  adminListSuggestions(page = 1, pageSize = 10): Observable<any> {
    return this.http.get<any>(`${this.base}/admin/suggestions?page=${page}&pageSize=${pageSize}`);
  }

  // ── report ─────────────────────────────────────────────

  reportBottle(bottleId: number, content: string, imageBase64?: string): Observable<Bottle> {
    return this.http.post<Bottle>(`${this.base}/bottles/${bottleId}/report`, { content, imageBase64 });
  }

  // ── my ─────────────────────────────────────────────────

  getMyBottles(page = 1, pageSize = 15): Observable<PaginatedResult<Bottle>> {
    return this.http.get<PaginatedResult<Bottle>>(`${this.base}/bottles/mine?page=${page}&pageSize=${pageSize}`, {
      headers: this.headers()
    });
  }

  getMyLikedBottles(page = 1, pageSize = 15): Observable<PaginatedResult<Bottle>> {
    return this.http.get<PaginatedResult<Bottle>>(`${this.base}/bottles/liked?page=${page}&pageSize=${pageSize}`, {
      headers: this.headers()
    });
  }

  getMyComments(page = 1, pageSize = 15): Observable<PaginatedResult<{ id: number; content: string; createdAt: string; editedAt?: string | null; bottleId: number; bottleContent: string }>> {
    return this.http.get<any>(`${this.base}/comments/mine?page=${page}&pageSize=${pageSize}`, {
      headers: this.headers()
    });
  }

  // ── logs ───────────────────────────────────────────────

  getBottleLogs(bottleId: number): Observable<{ id: number; operatorUsername: string; action: string; detail?: string | null; createdAt: string }[]> {
    return this.http.get<any[]>(`${this.base}/bottles/${bottleId}/logs`, {
      headers: this.headers()
    });
  }

  // ── admin bottle actions ───────────────────────────────

  adminCloseBottle(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/admin/bottles/${id}/close`, {});
  }

  adminOpenBottle(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/admin/bottles/${id}/open`, {});
  }

  adminDeleteComment(commentId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/admin/comments/${commentId}`);
  }

  // ── notifications ─────────────────────────────────────

  getNotifications(page = 1, pageSize = 20, type?: string): Observable<{ items: any[]; total: number; unreadTotal: number; page: number; pageSize: number }> {
    let url = `${this.base}/notifications?page=${page}&pageSize=${pageSize}`;
    if (type && type !== 'all') url += `&type=${type}`;
    return this.http.get<any>(url);
  }

  getUnreadCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.base}/notifications/unread-count`);
  }

  markNotificationRead(id: number): Observable<void> {
    return this.http.post<void>(`${this.base}/notifications/${id}/read`, {});
  }

  markAllNotificationsRead(): Observable<void> {
    return this.http.post<void>(`${this.base}/notifications/read-all`, {});
  }

  // ── admin notifications ────────────────────────────────

  adminSendNotification(title: string, content: string, targetUsers?: string, expireDays?: number): Observable<{ bottleId: number; targetCount: number }> {
    return this.http.post<{ bottleId: number; targetCount: number }>(`${this.base}/admin/notifications/send`, { title, content, targetUsers: targetUsers || null, expireDays: expireDays ?? 7 });
  }

  // ── user settings ──────────────────────────────────────

  changePassword(oldPassword: string, newPassword: string): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.base}/users/password`, { oldPassword, newPassword });
  }

  changeEmail(newEmail: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/users/change-email`, { newEmail });
  }

  verifyNewEmail(token: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/users/verify-new-email`, { token });
  }

  resendUserVerification(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/users/resend-verification`, {});
  }

  getUserPreferences(): Observable<{ notifyPreference: string; viewPrivateComments: boolean; throwAnonymous: boolean; defaultAuthorName?: string | null; showAdminUsername: boolean; email: string; emailVerified: boolean; newEmail?: string | null; isAdmin: boolean }> {
    return this.http.get<any>(`${this.base}/users/preferences`);
  }

  updateUserPreferences(notifyPreference?: string, viewPrivateComments?: boolean, throwAnonymous?: boolean, defaultAuthorName?: string, showAdminUsername?: boolean): Observable<any> {
    return this.http.put<any>(`${this.base}/users/preferences`, { notifyPreference, viewPrivateComments, throwAnonymous, defaultAuthorName, showAdminUsername });
  }

  // ── auth ───────────────────────────────────────────────

  verifyEmail(token: string): Observable<{ token: string; username: string; isAdmin: boolean }> {
    return this.http.post<{ token: string; username: string; isAdmin: boolean }>(`${this.base}/auth/verify-email`, { token });
  }

  resendVerification(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/auth/resend-verification`, { email });
  }

  forgotPassword(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/auth/forgot-password`, { email });
  }

  resetPassword(token: string, newPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/auth/reset-password`, { token, newPassword });
  }
}