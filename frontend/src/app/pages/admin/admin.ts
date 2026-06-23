import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService, Comment } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
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
  commentsCache = signal<Record<number, Comment[]>>({});
  loadingComments = signal<Record<number, boolean>>({});

  pushType = signal('story');
  pushContent = signal('');
  pushDate = signal(new Date().toISOString().slice(0, 10));

  constructor(private api: ApiService) {}

  ngOnInit() {
    if (this.auth.isAdmin()) {
      this.loadBottles();
    }
  }

  loadBottles() {
    this.api.adminListBottles().subscribe(b => this.bottles.set(b));
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
    });
  }
}