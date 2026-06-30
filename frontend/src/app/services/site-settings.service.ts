import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class SiteSettingsService {
  private base = environment.apiBase + '/api';

  siteName = signal('Aquarius');
  copyright = signal('');
  siteBaseUrl = signal('');

  constructor(private http: HttpClient) {
    this.load();
  }

  load() {
    this.http.get<{ siteName: string; copyright: string; siteBaseUrl: string }>(`${this.base}/settings`)
      .subscribe(s => {
        this.siteName.set(s.siteName);
        this.copyright.set(s.copyright);
        this.siteBaseUrl.set(s.siteBaseUrl || '');
      });
  }
}
