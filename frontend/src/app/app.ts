import { Component, inject, OnInit, OnDestroy, computed, signal } from '@angular/core';
import { Location } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive, Router, NavigationEnd } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { filter, interval, startWith, switchMap, Subscription } from 'rxjs';
import { AuthService } from './services/auth.service';
import { ApiService } from './services/api.service';
import { SiteSettingsService } from './services/site-settings.service';
import { App as CapApp } from '@capacitor/app';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit, OnDestroy {
  auth = inject(AuthService);
  settings = inject(SiteSettingsService);
  private location = inject(Location);
  private api = inject(ApiService);
  private router = inject(Router);
  private title = inject(Title);
  private pollSub?: Subscription;

  unreadCount = this.api.unreadCount;
  unreadLabel = computed(() => {
    const c = this.unreadCount();
    return c > 9 ? '9+' : c > 0 ? String(c) : '';
  });

  toastMsg = signal('');
  private toastTimer: any;

  ngOnInit() {
    // Android 返回键处理
    let lastBack = 0;
    CapApp.addListener('backButton', ({ canGoBack }) => {
      if (canGoBack) {
        this.location.back();
      } else {
        const now = Date.now();
        if (now - lastBack < 2000) {
          CapApp.exitApp();
        } else {
          lastBack = now;
          this.showToast('再按一次退出');
        }
      }
    });

    this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => {
      const route = this.router.routerState.root.firstChild?.snapshot;
      const subtitle = route?.data['title'];
      const siteName = this.settings.siteName();
      this.title.setTitle(subtitle ? `${subtitle} - ${siteName}` : siteName);
    });

    this.pollSub = interval(30000).pipe(
      startWith(0),
      switchMap(() => this.auth.isLoggedIn() ? this.api.getUnreadCount() : [{ count: 0 }])
    ).subscribe(res => {
      this.api.unreadCount.set(res.count);
    });
  }

  showToast(msg: string) {
    this.toastMsg.set(msg);
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.toastMsg.set(''), 2000);
  }

  ngOnDestroy() {
    this.pollSub?.unsubscribe();
  }
}
