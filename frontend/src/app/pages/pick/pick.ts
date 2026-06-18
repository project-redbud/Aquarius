import { Component, OnInit, signal } from '@angular/core';
import { ApiService, Bottle } from '../../services/api.service';
import { BottleViewComponent } from '../../components/bottle-view/bottle-view';

@Component({
  standalone: true,
  imports: [BottleViewComponent],
  templateUrl: './pick.html',
  styleUrls: ['./pick.scss']
})
export class PickPage implements OnInit {
  bottle = signal<Bottle | null>(null);
  loading = signal(false);
  refreshing = signal(false);

  constructor(private api: ApiService) {}

  ngOnInit() { this.pickBottle(); }

  pickBottle() {
    this.loading.set(true);
    this.api.pickRandom().subscribe(b => {
      this.bottle.set(b);
      this.loading.set(false);
      this.refreshing.set(false);
    });
  }

  touchStartY = 0;
  pullDistance = signal(0);
  onTouchStart(e: TouchEvent) { if (window.scrollY === 0) this.touchStartY = e.touches[0].clientY; }
  onTouchMove(e: TouchEvent) {
    if (window.scrollY === 0) { const d = e.touches[0].clientY - this.touchStartY; if (d > 0 && d < 120) this.pullDistance.set(d); }
  }
  onTouchEnd() {
    if (this.pullDistance() > 60) { this.refreshing.set(true); this.pickBottle(); }
    this.pullDistance.set(0); this.touchStartY = 0;
  }
}