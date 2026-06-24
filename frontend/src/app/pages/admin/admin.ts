import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService, Comment } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { SiteSettingsService } from '../../services/site-settings.service';
import { LinkifyPipe } from '../../pipes/linkify.pipe';

@Component({
  standalone: true,
  imports: [FormsModule, DatePipe, RouterLink, LinkifyPipe],
  templateUrl: './admin.html',
  styleUrls: ['./admin.scss']
})
export class AdminPage implements OnInit {
  auth = inject(AuthService);
  isAdmin = this.auth.isAdmin;

  bottles = signal<any[]>([]);
  bottlePage = signal(1);
  bottleTotal = signal(0);
  readonly bottlePageSize = 10;
  bottleTotalPages = computed(() => Math.max(1, Math.ceil(this.bottleTotal() / this.bottlePageSize)));
  commentsCache = signal<Record<number, Comment[]>>({});
  loadingComments = signal<Record<number, boolean>>({});

  pushType = signal('story');
  pushContent = signal('');
  pushDate = signal(new Date().toISOString().slice(0, 10));

  // ── Push management ─────────────────────────────────
  dailyPushes = signal<any[]>([]);
  pushPage = signal(1);
  pushTotal = signal(0);
  readonly pushPageSize = 10;
  pushTotalPages = computed(() => Math.max(1, Math.ceil(this.pushTotal() / this.pushPageSize)));
  editingPushId = signal<number | null>(null);
  editPushContent = signal('');
  republishPushId = signal<number | null>(null);
  republishDate = signal(new Date().toISOString().slice(0, 10));

  constructor(private api: ApiService, private siteSettings: SiteSettingsService) {}

  // ── Site settings ────────────────────────────────────
  settingSiteName = signal('');
  settingCopyright = signal('');
  settingSmtpHost = signal('');
  settingSmtpPort = signal(587);
  settingSmtpUser = signal('');
  settingSmtpPassword = signal('');
  settingSmtpFrom = signal('');
  settingSmtpEnableSsl = signal(true);
  settingSiteBaseUrl = signal('');

  ngOnInit() {
    if (this.auth.isAdmin()) {
      this.refreshBottles();
      this.loadDailyPushes();
      this.loadSettings();
      this.loadUsers();
      this.loadSuggestions();
    }
  }

  loadSettings() {
    this.api.adminGetSettings().subscribe(s => {
      this.settingSiteName.set(s.siteName);
      this.settingCopyright.set(s.copyright);
      this.settingSmtpHost.set((s as any).smtpHost || '');
      this.settingSmtpPort.set((s as any).smtpPort || 587);
      this.settingSmtpUser.set((s as any).smtpUser || '');
      this.settingSmtpFrom.set((s as any).smtpFrom || '');
      this.settingSmtpEnableSsl.set((s as any).smtpEnableSsl !== false);
      this.settingSiteBaseUrl.set((s as any).siteBaseUrl || '');
      // password is masked by backend, only set if user typed something
    });
  }

  saveSettings() {
    const smtpPwd = this.settingSmtpPassword().trim();
    this.api.adminUpdateSettings(
      this.settingSiteName().trim() || undefined,
      this.settingCopyright().trim() || undefined,
      this.settingSmtpHost().trim() || undefined,
      this.settingSmtpPort(),
      this.settingSmtpUser().trim() || undefined,
      smtpPwd || undefined,
      this.settingSmtpFrom().trim() || undefined,
      this.settingSmtpEnableSsl(),
      this.settingSiteBaseUrl().trim() || undefined
    ).subscribe(s => {
      this.siteSettings.siteName.set(s.siteName);
      this.siteSettings.copyright.set(s.copyright);
      alert('设置已保存');
    });
  }

  loadDailyPushes() {
    this.api.adminListDaily(this.pushPage(), this.pushPageSize).subscribe(res => {
      this.dailyPushes.set(res.items);
      this.pushTotal.set(res.total);
    });
  }

  goToPushPage(page: number) {
    if (page < 1 || page > this.pushTotalPages()) return;
    this.pushPage.set(page);
    this.loadDailyPushes();
  }

  startEditPush(p: any) {
    this.editingPushId.set(p.id);
    this.editPushContent.set(p.content);
  }

  cancelEditPush() {
    this.editingPushId.set(null);
    this.editPushContent.set('');
  }

  saveEditPush(id: number) {
    const content = this.editPushContent().trim();
    if (!content) return;
    this.api.adminEditDaily(id, content).subscribe(() => {
      this.dailyPushes.update(list => list.map(p => p.id === id ? { ...p, content } : p));
      this.cancelEditPush();
    });
  }

  deletePush(id: number) {
    if (!confirm('确定删除此推送？关联的瓶子也会被删除。')) return;
    this.api.adminDeleteDaily(id).subscribe(() => {
      this.dailyPushes.update(list => list.filter(p => p.id !== id));
    });
  }

  startRepublish(pushId: number) {
    this.republishPushId.set(pushId);
    this.republishDate.set(new Date().toISOString().slice(0, 10));
  }

  cancelRepublish() {
    this.republishPushId.set(null);
  }

  confirmRepublish(force = false) {
    const id = this.republishPushId();
    const date = this.republishDate();
    if (!id || !date) return;
    this.api.adminRepublishDaily(id, date, force).subscribe({
      next: () => {
        alert('重新推送成功！');
        this.cancelRepublish();
        this.loadDailyPushes();
      },
      error: (err) => {
        if (err.status === 409 && !force) {
          if (confirm(`${err.error}\n\n是否覆盖？`)) {
            this.confirmRepublish(true);
          }
        } else {
          alert(err.error || '重新推送失败');
        }
      }
    });
  }

  refreshBottles() {
    this.bottlePage.set(1);
    this.loadBottles();
  }

  loadBottles() {
    this.api.adminListBottles(this.bottlePage(), this.bottlePageSize).subscribe(res => {
      this.bottles.set(res.items);
      this.bottleTotal.set(res.total);
    });
  }

  goToBottlePage(page: number) {
    if (page < 1 || page > this.bottleTotalPages()) return;
    this.bottlePage.set(page);
    this.loadBottles();
  }

  toggleComments(bottleId: number) {
    if (this.commentsCache()[bottleId]) {
      const next = { ...this.commentsCache() };
      delete next[bottleId];
      this.commentsCache.set(next);
      return;
    }

    this.loadingComments.update(l => ({ ...l, [bottleId]: true }));

    this.api.adminGetComments(bottleId).subscribe(c => {
      this.commentsCache.update(cache => ({ ...cache, [bottleId]: c }));
      this.loadingComments.update(l => { const n = { ...l }; delete n[bottleId]; return n; });
    });
  }

  deleteBottle(id: number) {
    if (!confirm('确定删除？')) return;
    this.api.adminDeleteBottle(id).subscribe(() =>
      this.bottles.update(list => list.filter(b => b.id !== id))
    );
  }

  createDaily() {
    const content = this.pushContent().trim();
    if (!content) return;

    const type = this.pushType();
    const date = this.pushDate();

    this.api.adminCheckDaily(type, date).subscribe({
      next: (existing) => {
        const label = type === 'story' ? '故事' : '问答';
        const ok = confirm(
          `📅 ${date} 的「${label}」推送已存在：\n\n` +
          `"${existing.content.slice(0, 80)}${existing.content.length > 80 ? '...' : ''}"\n\n` +
          `是否更新为新内容？`
        );
        if (ok) this.doCreateDaily(type, content, date);
      },
      error: () => {
        this.doCreateDaily(type, content, date);
      }
    });
  }

  private doCreateDaily(type: string, content: string, date: string) {
    this.api.adminCreateDaily(type, content, date, null).subscribe((res: any) => {
      this.pushContent.set('');
      alert(res.updated ? '推送已更新！' : '推送创建成功！');
      this.loadDailyPushes();
    });
  }

  // ── User management ───────────────────────────────────
  users = signal<any[]>([]);
  userPage = signal(1);
  userTotal = signal(0);
  readonly userPageSize = 10;
  userTotalPages = computed(() => Math.max(1, Math.ceil(this.userTotal() / this.userPageSize)));
  userSearch = signal('');
  userDetail = signal<any | null>(null);
  banUserId = signal<number | null>(null);
  banReason = signal('');
  banDays = signal(7);

  loadUsers() {
    this.api.adminListUsers(this.userPage(), this.userPageSize, this.userSearch() || undefined).subscribe(res => {
      this.users.set(res.items);
      this.userTotal.set(res.total);
    });
  }

  goToUserPage(p: number) {
    if (p < 1 || p > this.userTotalPages()) return;
    this.userPage.set(p);
    this.loadUsers();
  }

  searchUsers() { this.userPage.set(1); this.loadUsers(); }

  toggleUserDetail(id: number) {
    if (this.userDetail()?.id === id) { this.userDetail.set(null); return; }
    this.api.adminGetUser(id).subscribe(u => this.userDetail.set(u));
  }

  startBanUser(id: number) { this.banUserId.set(id); this.banReason.set(''); this.banDays.set(7); }
  cancelBan() { this.banUserId.set(null); }
  confirmBan() {
    const id = this.banUserId(); if (!id) return;
    this.api.adminBanUser(id, this.banReason(), this.banDays()).subscribe(() => {
      this.cancelBan();
      this.loadUsers();
      alert('已封禁');
    });
  }

  unbanUser(id: number) {
    this.api.adminUnbanUser(id).subscribe(() => this.loadUsers());
  }

  setUserRole(id: number, role: string) {
    this.api.adminSetUserRole(id, role).subscribe(() => {
      this.loadUsers();
      this.userDetail.set(null);
    });
  }

  deleteUser(id: number) {
    if (!confirm('确定删除该用户？其瓶子和评论将保留（转为匿名）。')) return;
    this.api.adminDeleteUser(id).subscribe(() => {
      this.loadUsers();
      this.userDetail.set(null);
    });
  }

  // ── Suggestions ───────────────────────────────────────
  suggestions = signal<any[]>([]);
  sugPage = signal(1);
  sugTotal = signal(0);
  sugPendingTotal = signal(0);
  readonly sugPageSize = 10;
  sugTotalPages = computed(() => Math.max(1, Math.ceil(this.sugTotal() / this.sugPageSize)));

  loadSuggestions() {
    this.api.adminListSuggestions(this.sugPage(), this.sugPageSize).subscribe(res => {
      this.suggestions.set(res.items);
      this.sugTotal.set(res.total);
      this.sugPendingTotal.set(res.pendingTotal ?? 0);
    });
  }

  goToSugPage(p: number) {
    if (p < 1 || p > this.sugTotalPages()) return;
    this.sugPage.set(p);
    this.loadSuggestions();
  }

  // ── Notification push ─────────────────────────────────
  notifTitle = signal('');
  notifContent = signal('');
  notifTargetUsers = signal('');
  notifSending = signal(false);
  notifResult = signal<number | null>(null);

  sendNotification() {
    this.notifSending.set(true);
    this.notifResult.set(null);
    this.api.adminSendNotification(
      this.notifTitle().trim(),
      this.notifContent().trim(),
      this.notifTargetUsers().trim() || undefined
    ).subscribe({
      next: res => {
        this.notifResult.set(res.bottleId);
        this.notifTitle.set('');
        this.notifContent.set('');
        this.notifTargetUsers.set('');
        this.notifSending.set(false);
      },
      error: () => { this.notifSending.set(false); }
    });
  }

  closeBottle(id: number) {
    if (!confirm('确定关闭此瓶子？关闭后不再被捞取，也无法评论。')) return;
    this.api.adminCloseBottle(id).subscribe(() => {
      this.loadBottles();
      this.loadSuggestions();
    });
  }

  openBottle(id: number) {
    if (!confirm('确定重新打开此瓶子？')) return;
    this.api.adminOpenBottle(id).subscribe(() => {
      this.loadBottles();
      this.loadSuggestions();
    });
  }
}