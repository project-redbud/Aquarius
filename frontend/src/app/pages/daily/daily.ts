import { Component, OnInit, signal, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService, DailyDayItem, DailyListResponse } from '../../services/api.service';
import { LinkifyPipe } from '../../pipes/linkify.pipe';

@Component({
  standalone: true,
  imports: [DatePipe, RouterLink, LinkifyPipe],
  templateUrl: './daily.html',
  styleUrls: ['./daily.scss']
})
export class DailyPage implements OnInit {
  days = signal<DailyDayItem[]>([]);
  currentDate = signal('');
  loading = signal(false);
  minDate = '';
  maxDate = '';

  api = inject(ApiService);

  constructor(private route: ActivatedRoute) {}

  ngOnInit() {
    this.loadAll();
  }

  /** 一次性加载 7 天窗口全部推送 + 边界，后续翻页纯客户端过滤。 */
  loadAll() {
    this.loading.set(true);
    this.api.getDaily().subscribe({
      next: (res: DailyListResponse) => {
        this.days.set(res.days);
        this.minDate = res.minDate;
        this.maxDate = res.maxDate;

        // queryParam ?date=XX 优先，否则默认最新日期
        const dateParam = this.route.snapshot.queryParamMap.get('date');
        if (dateParam && this.days().some(d => d.date === dateParam)) {
          this.currentDate.set(dateParam);
        } else {
          this.currentDate.set(res.maxDate);
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        // API 失败时给一个空数组避免卡死
        this.days.set([]);
      }
    });
  }

  /** 当前选中日期对应的推送数据。 */
  currentDay(): DailyDayItem | undefined {
    return this.days().find(d => d.date === this.currentDate());
  }

  goToPrevDay() {
    const idx = this.days().findIndex(d => d.date === this.currentDate());
    if (idx > 0) this.currentDate.set(this.days()[idx - 1].date);
  }

  goToNextDay() {
    const idx = this.days().findIndex(d => d.date === this.currentDate());
    if (idx >= 0 && idx < this.days().length - 1) this.currentDate.set(this.days()[idx + 1].date);
  }

  canGoPrev(): boolean {
    return this.days().findIndex(d => d.date === this.currentDate()) > 0;
  }

  canGoNext(): boolean {
    const idx = this.days().findIndex(d => d.date === this.currentDate());
    return idx >= 0 && idx < this.days().length - 1;
  }
}
