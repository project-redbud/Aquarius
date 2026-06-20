import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService, DailyPush } from '../../services/api.service';

@Component({
  standalone: true,
  imports: [DatePipe, RouterLink],
  templateUrl: './daily.html',
  styleUrls: ['./daily.scss']
})
export class DailyPage implements OnInit {
  daily = signal<DailyPush | null>(null);
  currentDate = signal(new Date());
  loading = signal(false);

  readonly minDate: Date;
  readonly maxDate: Date;

  constructor(private api: ApiService) {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    this.maxDate = today;
    this.minDate = new Date(today);
    this.minDate.setDate(this.minDate.getDate() - 6);
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
