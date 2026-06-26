import { Component, OnInit, OnDestroy, signal, computed, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { Subscription } from 'rxjs';
import { ApiService, Bottle } from '../../services/api.service';
import { SiteSettingsService } from '../../services/site-settings.service';
import { BottleViewComponent } from '../../components/bottle-view/bottle-view';

@Component({
  standalone: true,
  imports: [RouterLink, BottleViewComponent],
  templateUrl: './bottle-detail.html',
  styleUrls: ['./bottle-detail.scss']
})
export class BottleDetailPage implements OnInit, OnDestroy {
  bottle = signal<Bottle | null>(null);
  loading = signal(true);
  refreshing = signal(false);

  backPath = computed(() => {
    const t = this.bottle()?.type;
    if (t === 'story' || t === 'qa' || t === 'news') return '/daily';
    if (t === 'suggestion' || t === 'notification') return '/my';
    return '/pick';
  });

  backParams = computed(() => {
    const t = this.bottle()?.type;
    const b = this.bottle();
    if ((t === 'story' || t === 'qa' || t === 'news') && b?.createdAt) {
      return { date: new Date(b.createdAt).toISOString().slice(0, 10) };
    }
    return {};
  });

  backLabel = computed(() => {
    const t = this.bottle()?.type;
    if (t === 'story' || t === 'qa' || t === 'news') return '返回推送';
    if (t === 'suggestion' || t === 'notification') return '返回我的';
    return '返回捞瓶';
  });
  private title = inject(Title);
  private settings = inject(SiteSettingsService);
  private sub?: Subscription;
  private currentId = 0;

  constructor(private route: ActivatedRoute, private api: ApiService) {}

  ngOnInit() {
    this.sub = this.route.paramMap.subscribe(params => {
      const id = Number(params.get('id'));
      this.currentId = id;
      this.loadBottle();
    });
  }

  loadBottle() {
    const id = this.currentId;
    if (!id) return;
    this.loading.set(true);
    this.title.setTitle(`瓶子 #${id} - ${this.settings.siteName()}`);
    this.api.getBottle(id).subscribe({
      next: b => { this.bottle.set(b); this.loading.set(false); this.refreshing.set(false); },
      error: () => { this.bottle.set(null); this.loading.set(false); this.refreshing.set(false); }
    });
  }

  // Pull-to-refresh
  touchStartY = 0;
  pullDistance = signal(0);

  onTouchStart(e: TouchEvent) {
    if (window.scrollY === 0) this.touchStartY = e.touches[0].clientY;
  }
  onTouchMove(e: TouchEvent) {
    if (window.scrollY === 0) {
      const d = e.touches[0].clientY - this.touchStartY;
      if (d > 0 && d < 120) this.pullDistance.set(d);
    }
  }
  onTouchEnd() {
    if (this.pullDistance() > 60) {
      this.refreshing.set(true);
      this.loadBottle();
    }
    this.pullDistance.set(0);
    this.touchStartY = 0;
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
  }
}
