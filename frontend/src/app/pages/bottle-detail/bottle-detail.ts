import { Component, OnInit, OnDestroy, signal, inject } from '@angular/core';
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
  private title = inject(Title);
  private settings = inject(SiteSettingsService);
  private sub?: Subscription;

  constructor(private route: ActivatedRoute, private api: ApiService) {}

  ngOnInit() {
    this.sub = this.route.paramMap.subscribe(params => {
      const id = Number(params.get('id'));
      this.loading.set(true);
      this.title.setTitle(`瓶子 #${id} - ${this.settings.siteName()}`);
      if (id) this.api.getBottle(id).subscribe({
        next: b => { this.bottle.set(b); this.loading.set(false); },
        error: () => { this.bottle.set(null); this.loading.set(false); }
      });
    });
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
  }
}