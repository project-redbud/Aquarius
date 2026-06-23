import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService, Bottle } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { LinkifyPipe } from '../../pipes/linkify.pipe';

interface MyComment {
  id: number;
  content: string;
  createdAt: string;
  editedAt?: string | null;
  bottleId: number;
  bottleContent: string;
}

@Component({
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, LinkifyPipe],
  templateUrl: './my.html',
  styleUrls: ['./my.scss']
})
export class MyPage implements OnInit {
  auth = inject(AuthService);

  tab = signal<'bottles' | 'comments' | 'likes'>('bottles');
  myBottles = signal<Bottle[]>([]);
  myComments = signal<MyComment[]>([]);
  likedBottles = signal<Bottle[]>([]);
  loading = signal(false);

  // Likes pagination
  likesPage = signal(1);
  likesTotal = signal(0);
  readonly likesPageSize = 15;
  likesTotalPages = computed(() => Math.max(1, Math.ceil(this.likesTotal() / this.likesPageSize)));

  // Edit state
  editingBottleId = signal<number | null>(null);
  editBottleContent = signal('');
  editBottleAuthor = signal('');

  editingCommentId = signal<number | null>(null);
  editCommentContent = signal('');
  editCommentBottleId = signal<number | null>(null);

  deletingId = signal<number | null>(null);

  constructor(private api: ApiService) {}

  private loadedBottles = false;
  private loadedComments = false;

  ngOnInit() {
    this.loading.set(true);
    this.api.getMyBottles().subscribe(data => {
      this.myBottles.set(data);
      this.loadedBottles = true;
      if (this.loadedComments) this.loading.set(false);
    });
    this.api.getMyComments().subscribe(data => {
      this.myComments.set(data);
      this.loadedComments = true;
      if (this.loadedBottles) this.loading.set(false);
    });
    this.loadLikedPage(1);
  }

  switchTab(t: 'bottles' | 'comments' | 'likes') {
    this.tab.set(t);
    if (t === 'likes' && this.likedBottles().length === 0) {
      this.loadLikedPage(1);
    }
  }

  loadLikedPage(page: number) {
    this.loading.set(true);
    this.api.getMyLikedBottles(page, this.likesPageSize).subscribe(res => {
      this.likedBottles.set(res.items);
      this.likesTotal.set(res.total);
      this.likesPage.set(res.page);
      this.loading.set(false);
    });
  }

  goToLikesPage(page: number) {
    if (page < 1 || page > this.likesTotalPages()) return;
    this.loadLikedPage(page);
  }

  // ── Bottle edit ─────────────────────────────────────────

  startEditBottle(b: Bottle) {
    this.editingBottleId.set(b.id);
    this.editBottleContent.set(b.content);
    this.editBottleAuthor.set(b.authorName || '');
  }

  cancelEditBottle() {
    this.editingBottleId.set(null);
    this.editBottleContent.set('');
    this.editBottleAuthor.set('');
  }

  saveEditBottle(id: number) {
    const content = this.editBottleContent().trim();
    if (!content) return;
    this.api.editBottle(id, content, undefined, this.editBottleAuthor().trim() || undefined).subscribe(b => {
      this.myBottles.update(list => list.map(x => x.id === id ? b : x));
      this.cancelEditBottle();
    });
  }

  confirmDeleteBottle(id: number) {
    this.deletingId.set(id);
  }

  cancelDelete() {
    this.deletingId.set(null);
  }

  deleteBottle(id: number) {
    this.api.deleteBottle(id).subscribe(() => {
      this.myBottles.update(list => list.filter(b => b.id !== id));
      this.deletingId.set(null);
    });
  }

  isExpired(b: Bottle): boolean {
    return new Date(b.expiresAt) < new Date();
  }

  rethrowBottle(b: Bottle) {
    this.api.rethrowBottle(b.id).subscribe(updated => {
      Object.assign(b, updated);
      alert('重新投出成功！');
    });
  }

  // ── Comment edit ────────────────────────────────────────

  startEditComment(c: MyComment) {
    this.editingCommentId.set(c.id);
    this.editCommentBottleId.set(c.bottleId);
    this.editCommentContent.set(c.content);
  }

  cancelEditComment() {
    this.editingCommentId.set(null);
    this.editCommentBottleId.set(null);
    this.editCommentContent.set('');
  }

  saveEditComment(id: number) {
    const content = this.editCommentContent().trim();
    const bottleId = this.editCommentBottleId();
    if (!content || !bottleId) return;
    this.api.editComment(bottleId, id, content).subscribe(c => {
      this.myComments.update(list => list.map(x => x.id === id ? { ...x, content: c.content, editedAt: c.editedAt } : x));
      this.cancelEditComment();
    });
  }

  confirmDeleteComment(id: number) {
    this.deletingId.set(id);
  }

  deleteComment(id: number) {
    const c = this.myComments().find(x => x.id === id);
    if (!c) return;
    this.api.deleteComment(c.bottleId, id).subscribe(() => {
      this.myComments.update(list => list.filter(x => x.id !== id));
      this.deletingId.set(null);
    });
  }
}