import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Injectable({ providedIn: 'root' })
export class SiteSettingsService {
  private base = '/api';

  siteName = signal('Aquarius');
  copyright = signal('');

  constructor(private http: HttpClient) {
    this.load();
  }

  load() {
    this.http.get<{ siteName: string; copyright: string }>(`${this.base}/settings`)
      .subscribe(s => {
        this.siteName.set(s.siteName);
        this.copyright.set(s.copyright);
      });
  }
}
