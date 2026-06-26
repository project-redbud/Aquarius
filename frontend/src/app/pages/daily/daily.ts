import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService, DailyPush } from '../../services/api.service';
import { LinkifyPipe } from '../../pipes/linkify.pipe';

@Component({
  standalone: true,
  imports: [DatePipe, RouterLink, LinkifyPipe],
  templateUrl: './daily.html',
  styleUrls: ['./daily.scss']
})
export class DailyPage implements OnInit {
  daily = signal<DailyPush | null>(null);
  currentDate = signal(new Date());
  loading = signal(false);

  readonly minDate: Date;
  readonly maxDate: Date;

  constructor(private api: ApiService, private route: ActivatedRoute) {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    this.maxDate = today;
    this.minDate = new Date(today);
    this.minDate.setDate(this.minDate.getDate() - 6);

    // 支持通过 queryParam date 跳转到指定日期
    const dateParam = this.route.snapshot.queryParamMap.get('date');
    if (dateParam) {
      const d = new Date(dateParam + 'T00:00:00');
      if (!isNaN(d.getTime()) && d >= this.minDate && d <= this.maxDate) {
        this.currentDate.set(d);
        return;
      }
    }
    this.currentDate.set(new Date(today));
  }

  ngOnInit() {
    this.loadDaily();
  }

  private fmt(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  loadDaily() {
    const dateStr = this.fmt(this.currentDate());
    this.loading.set(true);
    this.api.getDaily(dateStr).subscribe(d => {
      this.daily.set(d);
      this.loading.set(false);
    });
  }

  goToPrevDay() {
    const d = new Date(this.currentDate());
    d.setDate(d.getDate() - 1);
    if (d >= this.minDate) {
      this.currentDate.set(d);
      this.loadDaily();
    }
  }

  goToNextDay() {
    const d = new Date(this.currentDate());
    d.setDate(d.getDate() + 1);
    if (d <= this.maxDate) {
      this.currentDate.set(d);
      this.loadDaily();
    }
  }

  canGoPrev(): boolean {
    const d = new Date(this.currentDate());
    d.setDate(d.getDate() - 1);
    return d >= this.minDate;
  }

  canGoNext(): boolean {
    const d = new Date(this.currentDate());
    d.setDate(d.getDate() + 1);
    return d <= this.maxDate;
  }
}
