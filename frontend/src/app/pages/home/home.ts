import { Component, OnInit, signal, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ApiService } from '../../services/api.service';
@Component({
  standalone: true,
  imports: [DatePipe, RouterLink],
  templateUrl: './home.html',
  styleUrls: ['./home.scss']
})
export class HomePage implements OnInit {
  private api = inject(ApiService);
  private http = inject(HttpClient);

  news = signal<any>(null);
  story = signal<any>(null);
  qa = signal<any>(null);
  latest = signal<any[]>([]);
  hot = signal<any[]>([]);
  loading = signal(true);

  ngOnInit() {
    this.http.get<any>('/api/home', { headers: { 'X-User-Token': this.api.getUserToken() } })
      .subscribe(data => {
        this.news.set(data.pushes.news);
        this.story.set(data.pushes.story);
        this.qa.set(data.pushes.qa);
        this.latest.set(data.latest);
        this.hot.set(data.hot);
        this.loading.set(false);
      });
  }

  truncate(text: string, max: number): string {
    return text.length > max ? text.slice(0, max) + '...' : text;
  }

  hotHeat(h: any): number {
    return Math.max(0, h.likeCount * 2 + h.commentCount - this.daysSince(h.createdAt));
  }

  daysSince(date: string): number {
    return Math.floor((Date.now() - new Date(date).getTime()) / 86400000);
  }
}
