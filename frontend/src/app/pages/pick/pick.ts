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

  constructor(private api: ApiService) {}

  ngOnInit() { this.pickBottle(); }

  pickBottle() {
    this.loading.set(true);
    this.api.pickRandom().subscribe(b => {
      this.bottle.set(b);
      this.comments.set([]);
      this.commentText.set('');
      this.loading.set(false);
      if (b) this.loadComments();
    });
  }

  toggleLike() {
    const b = this.bottle();
    if (!b) return;
    this.api.toggleLike(b.id).subscribe(res => {
      const updated = { ...b!, likedByMe: res.liked, likeCount: res.likeCount };
      this.bottle.set(updated);
    });
  }

  loadComments() {
    const b = this.bottle();
    if (!b) return;
    this.api.getComments(b.id).subscribe(c => this.comments.set(c));
  }

  addComment() {
    const b = this.bottle();
    const text = this.commentText().trim();
    if (!b || !text) return;
    this.api.addComment(b.id, text).subscribe(c => {
      this.comments.update(list => [c, ...list]);
      this.commentText.set('');
      this.bottle.update(b => b ? { ...b, commentCount: b.commentCount + 1 } : null);
    });
  }
}
