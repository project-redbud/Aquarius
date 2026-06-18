import { Component, OnInit, signal, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService, Bottle } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';

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
  imports: [DatePipe, FormsModule, RouterLink],
  templateUrl: './my.html',
  styleUrls: ['./my.scss']
})
export class MyPage implements OnInit {
  auth = inject(AuthService);

  tab = signal<'bottles' | 'comments'>('bottles');
  myBottles = signal<Bottle[]>([]);
  myComments = signal<MyComment[]>([]);
  loading = signal(false);

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
  }

  switchTab(t: 'bottles' | 'comments') {
    this.tab.set(t);
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