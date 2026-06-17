import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService, Comment } from '../../services/api.service';

@Component({
  standalone: true,
  imports: [FormsModule, DatePipe],
  templateUrl: './admin.html',
  styleUrls: ['./admin.scss']
})
export class AdminPage {
  adminKey = signal('');
  loggedIn = signal(false);
  loginError = signal('');
  loggingIn = signal(false);
  bottles = signal<any[]>([]);
  // per-bottle comment map
  commentsCache = signal<Record<number, Comment[]>>({});
  loadingComments = signal<Record<number, boolean>>({});

  // daily push form
  pushType = signal('story');
  pushContent = signal('');
  pushDate = signal(new Date().toISOString().slice(0, 10));

  constructor(private api: ApiService) {}

  login() {
    const key = this.adminKey().trim();
    if (!key) return;

    this.loginError.set('');
    this.loggingIn.set(true);

    // 验证密钥：尝试拉取瓶子列表，成功才算登录
    this.api.adminListBottles(key).subscribe({
      next: (b) => {
        this.loggedIn.set(true);
        this.loggingIn.set(false);
        this.bottles.set(b);
      },
      error: () => {
        this.loginError.set('密钥错误，请重试');
        this.loggingIn.set(false);
      }
    });
  }

  loadBottles() {
    this.api.adminListBottles(this.adminKey()).subscribe(b => this.bottles.set(b));
  }

  toggleComments(bottleId: number) {
    if (this.commentsCache()[bottleId]) {
      // hide
      const next = { ...this.commentsCache() };
      delete next[bottleId];
      this.commentsCache.set(next);
      return;
    }

    // track loading
    this.loadingComments.update(l => ({ ...l, [bottleId]: true }));

    this.api.adminGetComments(bottleId, this.adminKey()).subscribe(c => {
      this.commentsCache.update(cache => ({ ...cache, [bottleId]: c }));
      this.loadingComments.update(l => { const n = { ...l }; delete n[bottleId]; return n; });
    });
  }

  deleteBottle(id: number) {
    if (!confirm('确定删除？')) return;
    this.api.adminDeleteBottle(id, this.adminKey()).subscribe(() =>
      this.bottles.update(list => list.filter(b => b.id !== id))
    );
  }

  createDaily() {
    const content = this.pushContent().trim();
    if (!content) return;

    const type = this.pushType();
    const date = this.pushDate();
    const key = this.adminKey();

    // 先检查是否已存在同类推送
    this.api.adminCheckDaily(type, date, key).subscribe({
      next: (existing) => {
        const label = type === 'story' ? '故事' : '问答';
        const ok = confirm(
          `📅 ${date} 的「${label}」推送已存在：\n\n` +
          `"${existing.content.slice(0, 80)}${existing.content.length > 80 ? '...' : ''}"\n\n` +
          `是否更新为新内容？`
        );
        if (ok) this.doCreateDaily(type, content, date, key);
      },
      error: () => {
        // 不存在，直接创建
        this.doCreateDaily(type, content, date, key);
      }
    });
  }

  private doCreateDaily(type: string, content: string, date: string, key: string) {
    this.api.adminCreateDaily(type, content, date, null, key).subscribe((res: any) => {
      this.pushContent.set('');
      alert(res.updated ? '推送已更新！' : '推送创建成功！');
    });
  }
}
