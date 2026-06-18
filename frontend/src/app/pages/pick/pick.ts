import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService, Bottle, Comment } from '../../services/api.service';

@Component({
  standalone: true,
  imports: [FormsModule, DatePipe],
  templateUrl: './pick.html',
  styleUrls: ['./pick.scss']
})
export class PickPage implements OnInit {
  bottle = signal<Bottle | null>(null);
  comments = signal<Comment[]>([]);
  commentText = signal('');
  loading = signal(false);
  refreshing = signal(false);

  // reply state
  replyTo = signal<Comment | null>(null);
  replyText = signal('');

  // expanded replies: Map<commentId, Comment[]>
  expandedReplies = signal<Record<number, Comment[]>>({});

  // sort: true = oldest first (#1 = first comment), false = newest first
  sortAsc = signal(false);

  sortedComments(): Comment[] {
    const list = [...this.comments()];
    if (this.sortAsc()) {
      list.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
    }
    return list;
  }

  floorNum(i: number): number {
    // When newest-first (desc): first item should be highest floor number
    return this.sortAsc() ? i + 1 : this.comments().length - i;
  }

  toggleSort() { this.sortAsc.update(v => !v); }

  constructor(private api: ApiService) {}

  ngOnInit() { this.pickBottle(); }

  pickBottle() {
    this.loading.set(true);
    this.api.pickRandom().subscribe(b => {
      this.bottle.set(b);
      this.comments.set([]);
      this.commentText.set('');
      this.replyTo.set(null);
      this.expandedReplies.set({});
      this.loading.set(false);
      this.refreshing.set(false);
      if (b) this.loadComments();
    });
  }

  touchStartY = 0;
  pullDistance = signal(0);
  onTouchStart(e: TouchEvent) { if (window.scrollY === 0) this.touchStartY = e.touches[0].clientY; }
  onTouchMove(e: TouchEvent) {
    if (window.scrollY === 0) { const d = e.touches[0].clientY - this.touchStartY; if (d > 0 && d < 120) this.pullDistance.set(d); }
  }
  onTouchEnd() {
    if (this.pullDistance() > 60) { this.refreshing.set(true); this.pickBottle(); }
    this.pullDistance.set(0); this.touchStartY = 0;
  }

  toggleLike() {
    const b = this.bottle();
    if (!b) return;
    this.api.toggleLike(b.id).subscribe(res => {
      this.bottle.update(b => b ? { ...b, likedByMe: res.liked, likeCount: res.likeCount } : null);
    });
  }

  loadComments() {
    const b = this.bottle();
    if (!b) return;
    this.api.getComments(b.id).subscribe(c => this.comments.set(c));
  }

  // ── reply logic ──────────────────────────────────────

  /** Find the root comment id for a given comment/reply */
  private rootCommentId(c: Comment): number {
    return c.commentId ?? c.id;
  }

  startReply(target: Comment) {
    this.replyTo.set(target);
    this.replyText.set('');
  }

  cancelReply() {
    this.replyTo.set(null);
  }

  addComment() {
    const b = this.bottle();
    if (!b) return;
    const parent = this.replyTo();

    const text = parent ? this.replyText().trim() : this.commentText().trim();
    if (!text) return;

    // Determine commentId and parentReplyId
    const commentId = parent ? this.rootCommentId(parent) : undefined;
    // parentReplyId: set only if replying to a nested reply (itself has commentId != null)
    const parentReplyId = parent?.commentId != null ? parent.id : undefined;

    this.api.addComment(b.id, text, commentId, parentReplyId).subscribe(() => {
      if (parent) {
        // Reload replies for the root comment
        this.loadReplies(this.rootCommentId(parent));
        this.cancelReply();
      } else {
        this.commentText.set('');
      }
      this.loadComments();
    });
  }

  loadReplies(commentId: number) {
    const b = this.bottle();
    if (!b) return;
    this.api.getReplies(b.id, commentId).subscribe(replies => {
      this.expandedReplies.update(r => ({ ...r, [commentId]: replies }));
    });
  }

  toggleReplies(comment: Comment) {
    const id = comment.id;
    if (this.expandedReplies()[id]) {
      this.expandedReplies.update(r => { const n = { ...r }; delete n[id]; return n; });
    } else {
      this.loadReplies(id);
    }
  }
}
