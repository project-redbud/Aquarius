import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService, Bottle } from '../../services/api.service';

interface MyComment {
  id: number;
  content: string;
  createdAt: string;
  bottleId: number;
  bottleContent: string;
}

@Component({
  standalone: true,
  imports: [DatePipe, RouterLink],
  templateUrl: './my.html',
  styleUrls: ['./my.scss']
})
export class MyPage implements OnInit {
  tab = signal<'bottles' | 'comments'>('bottles');
  myBottles = signal<Bottle[]>([]);
  myComments = signal<MyComment[]>([]);
  loading = signal(false);

  constructor(private api: ApiService) {}

  private loadedBottles = false;
  private loadedComments = false;

  ngOnInit() {
    this.loading.set(true);
    // 并发加载两个计数
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
    // 只在未加载过时才请求
    if (t === 'bottles' && !this.loadedBottles) {
      this.api.getMyBottles().subscribe(data => { this.myBottles.set(data); this.loadedBottles = true; });
    } else if (t === 'comments' && !this.loadedComments) {
      this.api.getMyComments().subscribe(data => { this.myComments.set(data); this.loadedComments = true; });
    }
  }
}
