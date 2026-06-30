import { Component, Input, signal, computed, inject, OnChanges, SimpleChanges, ChangeDetectorRef, ElementRef, HostListener } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink, Router } from '@angular/router';
import { ApiService, Bottle, Comment } from '../../services/api.service';
import { LinkifyPipe } from '../../pipes/linkify.pipe';
import { AuthService } from '../../services/auth.service';
import { ImageService } from '../../services/image.service';
import { SiteSettingsService } from '../../services/site-settings.service';
import { environment } from '../../../environments/environment';

@Component({
  standalone: true,
  selector: 'app-bottle-view',
  imports: [FormsModule, DatePipe, RouterLink, LinkifyPipe],
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
  sortAsc = signal(true);

  editingBottle = signal(false);
  editBottleContent = signal('');
  editingCommentId = signal<number | null>(null);
  editCommentContent = signal('');
  deletingCommentId = signal<number | null>(null);

  menuOpen = signal(false);

  /** 举报弹窗 */
  reportOpen = signal(false);
  reportContent = signal('');
  reportImage = signal<string | null>(null);
  reportSubmitting = signal(false);

  /** 操作日志弹窗 */
  logOpen = signal(false);
  logs = signal<{ id: number; operatorUsername: string; action: string; detail?: string | null; createdAt: string }[]>([]);
  logsLoading = signal(false);

  zoomImage = signal<string | null>(null);
  zoomScale = signal(1);
  zoomTranslateX = signal(0);
  zoomTranslateY = signal(0);
  private pinchStartDist = 0;
  private pinchStartScale = 1;
  private lastZoomTapTime = 0;

  openZoom(url: string) {
    this.zoomScale.set(1);
    this.zoomTranslateX.set(0);
    this.zoomTranslateY.set(0);
    this.zoomImage.set(url);
  }

  closeZoom() { this.zoomImage.set(null); }

  onZoomTouchStart(e: TouchEvent) {
    if (e.touches.length === 2) {
      this.pinchStartDist = this._touchDist(e.touches);
      this.pinchStartScale = this.zoomScale();
      e.preventDefault();
    }
  }

  onZoomTouchMove(e: TouchEvent) {
    if (e.touches.length === 2) {
      const dist = this._touchDist(e.touches);
      const s = Math.max(0.5, Math.min(5, this.pinchStartScale * (dist / this.pinchStartDist)));
      this.zoomScale.set(Math.round(s * 100) / 100);
      e.preventDefault();
    }
  }

  onZoomClick(e: MouseEvent) {
    const now = Date.now();
    if (now - this.lastZoomTapTime < 300) {
      // double-tap → toggle 1x / 2x
      if (this.zoomScale() > 1.1) {
        this.zoomScale.set(1);
        this.zoomTranslateX.set(0);
        this.zoomTranslateY.set(0);
      } else {
        this.zoomScale.set(2);
      }
    }
    this.lastZoomTapTime = now;
    e.stopPropagation();
  }

  onZoomWheel(e: WheelEvent) {
    e.preventDefault();
    const delta = e.deltaY > 0 ? -0.15 : 0.15;
    const s = Math.max(0.5, Math.min(5, this.zoomScale() + delta));
    this.zoomScale.set(Math.round(s * 100) / 100);
  }

  private _touchDist(touches: TouchList): number {
    const dx = touches[0].clientX - touches[1].clientX;
    const dy = touches[0].clientY - touches[1].clientY;
    return Math.sqrt(dx * dx + dy * dy);
  }

  imageUrl(path: string | null | undefined): string {
    if (!path) return '';
    if (path.startsWith('http')) return path;
    return environment.apiBase + '/' + path;
  }

  /** 评论私密提示：null=正常, 'hidden'=隐藏, 'admin'=管理员可见 */
  commentsPrivateNote = signal<string | null>(null);

  /** 评论时是否带管理员标识 */
  commentAdminBadge = signal(false);

  /** 评论时是否带瓶主标识 */
  commentBottleOwnerBadge = signal(false);

  /** 评论分页 */
  readonly commentPageSize = 12;
  commentPage = signal(1);
  commentTotalPages = computed(() => Math.max(1, Math.ceil(this.comments().length / this.commentPageSize)));

  pagedComments(): Comment[] {
    const list = [...this.comments()];
    if (this.sortAsc()) list.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
    const start = (this.commentPage() - 1) * this.commentPageSize;
    return list.slice(start, start + this.commentPageSize);
  }
  goCommentPage(p: number) {
    if (p < 1 || p > this.commentTotalPages()) return;
    this.commentPage.set(p);
  }

  floorNum(i: number): number {
    const offset = (this.commentPage() - 1) * this.commentPageSize;
    return this.sortAsc() ? offset + i + 1 : this.comments().length - offset - i;
  }
  floorLabel(i: number): string {
    const n = this.floorNum(i) + 1;
    return n <= 4 ? `${n}🧴` : `${n}🌊`;
  }
  toggleSort() { this.sortAsc.update(v => !v); }

  constructor(private api: ApiService, private cdr: ChangeDetectorRef, private el: ElementRef, private router: Router, private image: ImageService, private siteSettings: SiteSettingsService) {}

  ngOnChanges(changes: SimpleChanges) {
    if (changes['bottle'] && this.bottle) {
      this.comments.set([]);
      this.commentText.set('');
      this.replyTo.set(null);
      this.expandedReplies.set({});
      this.commentPage.set(1);
      this.editingBottle.set(false);
      this.editingCommentId.set(null);
      this.deletingCommentId.set(null);
      this.commentsPrivateNote.set(null);
      this.commentAdminBadge.set(false);
      this.commentBottleOwnerBadge.set(false);

      // 意见瓶/通知瓶：管理员默认带标识
      if ((this.bottle.type === 'suggestion' || this.bottle.type === 'notification') && this.auth.isAdmin()) {
        this.commentAdminBadge.set(true);
      }

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
  openReport() {
    this.menuOpen.set(false);
    this.reportContent.set('');
    this.reportImage.set(null);
    this.reportOpen.set(true);
  }

  closeReport() { this.reportOpen.set(false); }

  openLog() {
    this.menuOpen.set(false);
    this.logs.set([]);
    this.logsLoading.set(true);
    this.logOpen.set(true);
    this.api.getBottleLogs(this.bottle!.id).subscribe({
      next: data => { this.logs.set(data); this.logsLoading.set(false); },
      error: () => { this.logsLoading.set(false); }
    });
  }

  closeLog() { this.logOpen.set(false); }

  adminCloseBottle() {
    if (!this.bottle || !confirm('确定关闭此瓶子？')) return;
    this.menuOpen.set(false);
    this.api.adminCloseBottle(this.bottle.id).subscribe(() => {
      this.bottle!.isClosed = true;
      this.cdr.detectChanges();
    });
  }

  adminOpenBottle() {
    if (!this.bottle || !confirm('确定重新打开此瓶子？')) return;
    this.menuOpen.set(false);
    this.api.adminOpenBottle(this.bottle.id).subscribe(() => {
      this.bottle!.isClosed = false;
      this.cdr.detectChanges();
    });
  }

  actionLabel(action: string): string {
    const map: Record<string, string> = {
      close: '🔒 关闭瓶子',
      open: '🔓 打开瓶子',
      delete_reply: '🗑️ 删除回复',
      republish_daily: '🔄 重新推送'
    };
    return map[action] || action;
  }

  async onReportFileSelected(e: Event) {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (file) this.reportImage.set(await this.image.compress(file));
  }

  submitReport() {
    const text = this.reportContent().trim();
    if (!text || !this.bottle) return;
    this.reportSubmitting.set(true);
    this.api.reportBottle(this.bottle.id, text, this.reportImage() ?? undefined).subscribe({
      next: () => {
        alert('举报已提交');
        this.closeReport();
        this.reportSubmitting.set(false);
      },
      error: () => {
        alert('提交失败，请登录后再试');
        this.reportSubmitting.set(false);
      }
    });
  }
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    if (!this.menuOpen()) return;
    const target = event.target as HTMLElement;
    const host = this.el.nativeElement as HTMLElement;
    const moreBtn = host.querySelector('.more-btn');
    const dropdown = host.querySelector('.dropdown');
    if (!moreBtn?.contains(target) && !dropdown?.contains(target)) {
      this.menuOpen.set(false);
      this.cdr.detectChanges();
    }
  }
  shareBottle() {
    const id = this.bottle?.id;
    if (!id) return;
    const base = this.siteSettings.siteBaseUrl() || location.origin;
    const url = `${base.replace(/\/+$/, '')}/bottle/${id}`;
    if (navigator.clipboard) {
      navigator.clipboard.writeText(url)
        .then(() => { this.menuOpen.set(false); alert('链接已复制到剪贴板'); })
        .catch(() => alert('复制失败'));
    } else {
      const ta = document.createElement('textarea');
      ta.value = url;
      ta.style.position = 'fixed';
      ta.style.opacity = '0';
      document.body.appendChild(ta);
      ta.select();
      try {
        document.execCommand('copy');
        this.menuOpen.set(false);
        alert('链接已复制到剪贴板');
      } catch {
        alert('复制失败');
      }
      document.body.removeChild(ta);
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
      // Reload parent comment's replies if editing a reply
      const parentId = c.commentId ?? c.id;
      if (c.commentId != null && this.expandedReplies()[parentId]) {
        this.loadReplies(parentId);
      }
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
  startReply(target: Comment) {
    this.replyTo.set(target);
    this.replyText.set('');
    this.cdr.detectChanges();
    setTimeout(() => {
      (document.querySelector('.reply-bar input[type="text"]') as HTMLInputElement)?.focus();
    });
  }
  cancelReply() { this.replyTo.set(null); }
  addComment() {
    const b = this.bottle;
    if (!b) return;
    const parent = this.replyTo();
    const text = parent ? this.replyText().trim() : this.commentText().trim();
    if (!text) return;
    const commentId = parent ? this.rootCommentId(parent) : undefined;
    const parentReplyId = parent?.commentId != null ? parent.id : undefined;
    this.api.addComment(b.id, text, commentId, parentReplyId, this.commentAdminBadge(), this.commentBottleOwnerBadge()).subscribe(() => {
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