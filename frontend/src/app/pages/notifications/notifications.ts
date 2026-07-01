import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';

interface Notification {
  id: number; type: string; title: string; content: string;
  relatedBottleId?: number; isRead: boolean; createdAt: string;
}

@Component({
  standalone: true,
  imports: [DatePipe, RouterLink],
  templateUrl: './notifications.html',
  styleUrls: ['./notifications.scss']
})
export class NotificationsPage implements OnInit {
  private api = inject(ApiService);

  filter = signal('all');
  items = signal<Notification[]>([]);
  page = signal(1);
  total = signal(0);
  unreadTotal = signal(0);
  loading = signal(false);
  refreshing = signal(false);
  readonly pageSize = 20;
  totalPages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));

  // Pull-to-refresh
  touchStartY = 0;
  pullDistance = signal(0);

  ngOnInit() {
    this.load(true);
    if ('Notification' in window && Notification.permission === 'default') {
      Notification.requestPermission();
    }
  }

  load(initial = false) {
    if (!initial) this.loading.set(true);
    this.api.getNotifications(this.page(), this.pageSize, this.filter()).subscribe(res => {
      this.items.set(res.items);
      this.total.set(res.total);
      this.unreadTotal.set(res.unreadTotal);
      this.api.unreadCount.set(res.unreadTotal);
      this.loading.set(false);
      this.refreshing.set(false);
    });
  }

  refresh() {
    this.page.set(1);
    this.load();
  }

  setFilter(type: string) {
    this.filter.set(type);
    this.page.set(1);
    this.load();
  }

  goPage(p: number) {
    if (p < 1 || p > this.totalPages()) return;
    this.page.set(p);
    this.load();
  }

  markRead(id: number) {
    const item = this.items().find(n => n.id === id);
    if (!item || item.isRead) return; // 已读不重复处理
    this.api.markNotificationRead(id).subscribe(() => {
      this.items.update(list => list.map(n => n.id === id ? { ...n, isRead: true } : n));
      const newUnread = Math.max(0, this.unreadTotal() - 1);
      this.unreadTotal.set(newUnread);
      this.api.unreadCount.set(newUnread);
    });
  }

  markAllRead() {
    if (this.unreadTotal() === 0) return;
    this.api.markAllNotificationsRead().subscribe(() => {
      this.items.update(list => list.map(n => ({ ...n, isRead: true })));
      this.unreadTotal.set(0);
      this.api.unreadCount.set(0);
    });
  }

  typeLabel(t: string): string {
    const m: Record<string, string> = {
      system: '📢 系统通知',
      like: '❤️ 点赞',
      comment: '💬 评论',
      bottle_processed: '✅ 已处理',
      bottle_essence: '💎 精华'
    };
    return m[t] || t;
  }

  // ── Pull-to-refresh ─────────────────────────────────────

  onTouchStart(e: TouchEvent) {
    if (window.scrollY === 0) this.touchStartY = e.touches[0].clientY;
  }

  onTouchMove(e: TouchEvent) {
    if (window.scrollY === 0) {
      const d = e.touches[0].clientY - this.touchStartY;
      if (d > 0 && d < 120) this.pullDistance.set(d);
    }
  }

  onTouchEnd() {
    if (this.pullDistance() > 60) {
      this.refreshing.set(true);
      this.refresh();
    }
    this.pullDistance.set(0);
    this.touchStartY = 0;
  }
}
