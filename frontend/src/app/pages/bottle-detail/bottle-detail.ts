import { Component, OnInit, signal, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { ApiService, Bottle } from '../../services/api.service';
import { SiteSettingsService } from '../../services/site-settings.service';
import { BottleViewComponent } from '../../components/bottle-view/bottle-view';

@Component({
  standalone: true,
  imports: [RouterLink, BottleViewComponent],
  templateUrl: './bottle-detail.html',
  styleUrls: ['./bottle-detail.scss']
})
export class BottleDetailPage implements OnInit {
  bottle = signal<Bottle | null>(null);
  loading = signal(true);
  private title = inject(Title);
  private settings = inject(SiteSettingsService);

  constructor(private route: ActivatedRoute, private api: ApiService) {}

  ngOnInit() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.title.setTitle(`瓶子 #${id} - ${this.settings.siteName()}`);
    if (id) this.api.getBottle(id).subscribe({
      next: b => { this.bottle.set(b); this.loading.set(false); },
      error: () => { this.bottle.set(null); this.loading.set(false); }
    });
  }
}