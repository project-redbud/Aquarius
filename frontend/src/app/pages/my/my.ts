import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService, Bottle, PaginatedResult } from '../../services/api.service';
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
  unreadCount = inject(ApiService).unreadCount;

  tab = signal<'bottles' | 'comments' | 'likes'>('bottles');
  myBottles = signal<Bottle[]>([]);
  myComments = signal<MyComment[]>([]);
  likedBottles = signal<Bottle[]>([]);
  loading = signal(false);

  // Pagination state
  readonly pageSize = 15;

  // Bottles pagination
  bottlesPage = signal(1);
  bottlesTotal = signal(0);
  bottlesTotalPages = computed(() => Math.max(1, Math.ceil(this.bottlesTotal() / this.pageSize)));

  // Comments pagination
  commentsPage = signal(1);
  commentsTotal = signal(0);
  commentsTotalPages = computed(() => Math.max(1, Math.ceil(this.commentsTotal() / this.pageSize)));

  // Likes pagination
  likesPage = signal(1);
  likesTotal = signal(0);
  likesTotalPages = computed(() => Math.max(1, Math.ceil(this.likesTotal() / this.pageSize)));

  // Edit state
  editingBottleId = signal<number | null>(null);
  editBottleContent = signal('');
  editBottleAuthor = signal('');

  editingCommentId = signal<number | null>(null);
  editCommentContent = signal('');
  editCommentBottleId = signal<number | null>(null);

  deletingId = signal<number | null>(null);

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.loadBottlesPage(1);
    this.loadCommentsPage(1);
    this.loadLikedPage(1);
  }

  switchTab(t: 'bottles' | 'comments' | 'likes') {
    this.tab.set(t);
    if (t === 'likes' && this.likedBottles().length === 0) this.loadLikedPage(1);
  }

  // ── Bottles pagination ──────────────────────────────────

  loadBottlesPage(page: number) {
    this.loading.set(true);
    this.api.getMyBottles(page, this.pageSize).subscribe(res => {
      this.myBottles.set(res.items);
      this.bottlesTotal.set(res.total);
      this.bottlesPage.set(res.page);
      this.loading.set(false);
    });
  }

  goToBottlesPage(page: number) {
    if (page < 1 || page > this.bottlesTotalPages()) return;
    this.loadBottlesPage(page);
  }

  // ── Comments pagination ─────────────────────────────────

  loadCommentsPage(page: number) {
    this.loading.set(true);
    this.api.getMyComments(page, this.pageSize).subscribe(res => {
      this.myComments.set(res.items);
      this.commentsTotal.set(res.total);
      this.commentsPage.set(res.page);
      this.loading.set(false);
    });
  }

  goToCommentsPage(page: number) {
    if (page < 1 || page > this.commentsTotalPages()) return;
    this.loadCommentsPage(page);
  }

  // ── Likes pagination ────────────────────────────────────

  loadLikedPage(page: number) {
    this.loading.set(true);
    this.api.getMyLikedBottles(page, this.pageSize).subscribe(res => {
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
