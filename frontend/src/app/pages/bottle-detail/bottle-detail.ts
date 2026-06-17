import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService, Bottle, Comment } from '../../services/api.service';

@Component({
  standalone: true,
  imports: [FormsModule, DatePipe, RouterLink],
  templateUrl: './bottle-detail.html',
  styleUrls: ['./bottle-detail.scss']
})
export class BottleDetailPage implements OnInit {
  bottle = signal<Bottle | null>(null);
  comments = signal<Comment[]>([]);
  commentText = signal('');
  loading = signal(true);
  replyTo = signal<Comment | null>(null);
  replyText = signal('');
  expandedReplies = signal<Record<number, Comment[]>>({});

  constructor(private route: ActivatedRoute, private api: ApiService) {}

  ngOnInit() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (id) this.api.getBottle(id).subscribe(b => { this.bottle.set(b); this.loading.set(false); if (b) this.loadComments(); });
  }

  toggleLike() {
    const b = this.bottle();
    if (!b) return;
    this.api.toggleLike(b.id).subscribe(res => this.bottle.update(b => b ? { ...b, likedByMe: res.liked, likeCount: res.likeCount } : null));
  }

  loadComments() { const b = this.bottle(); if (b) this.api.getComments(b.id).subscribe(c => this.comments.set(c)); }

  private rootCommentId(c: Comment): number { return c.commentId ?? c.id; }
  startReply(target: Comment) { this.replyTo.set(target); this.replyText.set(''); }
  cancelReply() { this.replyTo.set(null); }

  addComment() {
    const b = this.bottle();
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
    const b = this.bottle();
    if (!b) return;
    this.api.getReplies(b.id, commentId).subscribe(replies => this.expandedReplies.update(r => ({ ...r, [commentId]: replies })));
  }

  toggleReplies(comment: Comment) {
    if (this.expandedReplies()[comment.id]) this.expandedReplies.update(r => { const n = { ...r }; delete n[comment.id]; return n; });
    else this.loadReplies(comment.id);
  }
}
