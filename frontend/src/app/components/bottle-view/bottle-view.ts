import { Component, Input, signal, inject, OnChanges, SimpleChanges, ChangeDetectorRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService, Bottle, Comment } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';

@Component({
  standalone: true,
  selector: 'app-bottle-view',
  imports: [FormsModule, DatePipe, RouterLink],
  templateUrl: './bottle-view.html',
  styleUrls: ['./bottle-view.scss']
})
export class BottleViewComponent implements OnChanges {
  auth = inject(AuthService);
  isMine = (userId?: number | null) => userId != null && userId === this.auth.user()?.userId;

  @Input() bottle: Bottle | null = null;
  @Input() loading = false;

  comments = signal<Comment[]>([]);
  commentText = signal('');
  replyTo = signal<Comment | null>(null);
  replyText = signal('');
  expandedReplies = signal<Record<number, Comment[]>>({});
  sortAsc = signal(false);

  editingBottle = signal(false);
  editBottleContent = signal('');
  editingCommentId = signal<number | null>(null);
  editCommentContent = signal('');
  deletingCommentId = signal<number | null>(null);

  menuOpen = signal(false);

  /** 评论私密提示：null=正常, 'hidden'=隐藏, 'admin'=管理员可见 */
  commentsPrivateNote = signal<string | null>(null);

  sortedComments(): Comment[] {
    const list = [...this.comments()];
    if (this.sortAsc()) list.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
    return list;
  }
  floorNum(i: number): number { return this.sortAsc() ? i + 1 : this.comments().length - i; }
  toggleSort() { this.sortAsc.update(v => !v); }

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnChanges(changes: SimpleChanges) {
    if (changes['bottle'] && this.bottle) {
      this.comments.set([]);
      this.commentText.set('');
      this.replyTo.set(null);
      this.expandedReplies.set({});
      this.editingBottle.set(false);
      this.editingCommentId.set(null);
      this.deletingCommentId.set(null);
      this.commentsPrivateNote.set(null);

      // 评论仅作者可见检查
      if (this.bottle.commentsPrivate) {
        const isAuthor = this.isMine(this.bottle.userId);
        const isAdmin = this.auth.isAdmin();
        if (isAdmin) {
          this.commentsPrivateNote.set('admin');
        } else if (isAuthor) {
          this.commentsPrivateNote.set(null);
        } else {
          this.commentsPrivateNote.set('hidden');
        }
        this.loadComments(); // 后端会按权限过滤，评论者能看到自己的
      } else {
        this.commentsPrivateNote.set(null);
        this.loadComments();
      }
    }
  }

  // ── Likes ────────────────────────────────────────────────

  toggleLike() {
    const b = this.bottle;
    if (!b) return;
    this.api.toggleLike(b.id).subscribe(res => {
      b.likedByMe = res.liked;
      b.likeCount = res.likeCount;
      this.cdr.detectChanges();
    });
  }

  // ── Menu ─────────────────────────────────────────────────

  toggleMenu() { this.menuOpen.update(v => !v); }
  shareBottle() {
    const id = this.bottle?.id;
    if (id) {
      navigator.clipboard.writeText(`${location.origin}/bottle/${id}`)
        .then(() => { this.menuOpen.set(false); alert('链接已复制到剪贴板'); })
        .catch(() => alert('复制失败'));
    }
  }

  // ── Bottle edit ─────────────────────────────────────────

  startEditBottle() {
    const b = this.bottle;
    if (b) { this.editBottleContent.set(b.content); this.editingBottle.set(true); }
  }
  cancelEditBottle() { this.editingBottle.set(false); }
  saveEditBottle() {
    const b = this.bottle;
    const content = this.editBottleContent().trim();
    if (!b || !content) return;
    this.api.editBottle(b.id, content, b.imagePath ?? undefined, b.authorName ?? undefined).subscribe(updated => {
      Object.assign(b, updated);
      this.editingBottle.set(false);
      this.cdr.detectChanges();
    });
  }

  // ── Comments ─────────────────────────────────────────────

  loadComments() { const b = this.bottle; if (b) this.api.getComments(b.id).subscribe(c => this.comments.set(c)); }

  startEditComment(c: Comment) {
    this.editingCommentId.set(c.id);
    this.editCommentContent.set(c.content);
  }
  cancelEditComment() { this.editingCommentId.set(null); }
  saveEditComment(c: Comment) {
    const b = this.bottle;
    const content = this.editCommentContent().trim();
    if (!b || !content) return;
    this.api.editComment(b.id, c.id, content).subscribe(() => {
      this.loadComments();
      this.editingCommentId.set(null);
    });
  }
  confirmDeleteComment(c: Comment) { this.deletingCommentId.set(c.id); }
  cancelDeleteComment() { this.deletingCommentId.set(null); }
  deleteComment(c: Comment) {
    const b = this.bottle;
    if (!b) return;
    this.api.deleteComment(b.id, c.id).subscribe(() => {
      this.loadComments();
      this.deletingCommentId.set(null);
    });
  }

  // ── Reply ───────────────────────────────────────────────

  private rootCommentId(c: Comment): number { return c.commentId ?? c.id; }
  startReply(target: Comment) { this.replyTo.set(target); this.replyText.set(''); }
  cancelReply() { this.replyTo.set(null); }
  addComment() {
    const b = this.bottle;
    if (!b) return;
    const parent = this.replyTo();
    const text = parent ? this.replyText().trim() : this.commentText().trim();
    if (!text) return;
    const commentId = parent ? this.rootCommentId(parent) : undefined;
    const parentReplyId = parent?.commentId != null ? parent.id : undefined;
    this.api.addComment(b.id, text, commentId, parentReplyId).subscribe(() => {
      if (parent) { this.loadReplies(this.rootCommentId(parent)); this.cancelReply(); }
      else { this.commentText.set(''); }
      this.loadComments();
    });
  }
  loadReplies(commentId: number) {
    const b = this.bottle;
    if (!b) return;
    this.api.getReplies(b.id, commentId).subscribe(replies => {
      this.expandedReplies.update(r => ({ ...r, [commentId]: replies }));
    });
  }
  toggleReplies(comment: Comment) {
    if (this.expandedReplies()[comment.id]) {
      this.expandedReplies.update(r => { const n = { ...r }; delete n[comment.id]; return n; });
    } else {
      this.loadReplies(comment.id);
    }
  }
}