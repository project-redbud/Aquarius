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
  bottles = signal<any[]>([]);
  selectedComments = signal<Comment[]>([]);

  constructor(private api: ApiService) {}

  login() {
    if (!this.adminKey().trim()) return;
    this.loggedIn.set(true);
    this.loadBottles();
  }

  loadBottles() {
    this.api.adminListBottles(this.adminKey()).subscribe(b => this.bottles.set(b));
  }

  viewComments(bottleId: number) {
    this.api.adminGetComments(bottleId, this.adminKey()).subscribe(c =>
      this.selectedComments.set(c)
    );
  }

  deleteBottle(id: number) {
    if (!confirm('确定删除？')) return;
    this.api.adminDeleteBottle(id, this.adminKey()).subscribe(() =>
      this.bottles.update(list => list.filter(b => b.id !== id))
    );
  }
}
