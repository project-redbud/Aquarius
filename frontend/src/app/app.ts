import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive, Router, NavigationEnd } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { filter } from 'rxjs';
import { AuthService } from './services/auth.service';
import { SiteSettingsService } from './services/site-settings.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  auth = inject(AuthService);
  settings = inject(SiteSettingsService);
  private router = inject(Router);
  private title = inject(Title);

  ngOnInit() {
    this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => {
      const route = this.router.routerState.root.firstChild?.snapshot;
      const subtitle = route?.data['title'];
      const siteName = this.settings.siteName();
      this.title.setTitle(subtitle ? `${subtitle} - ${siteName}` : siteName);
    });
  }
}