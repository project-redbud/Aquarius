import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ImageService } from '../../services/image.service';
import { Router } from '@angular/router';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './throw.html',
  styleUrls: ['./throw.scss']
})
export class ThrowPage implements OnInit {
  content = signal('');
  authorName = signal('');
  isAnonymous = signal(true);
  requireLogin = signal(false);
  commentsPrivate = signal(false);
  showAdminBadge = signal(false);
  imageBase64 = signal<string | null>(null);
  sending = signal(false);
  compressing = signal(false);

  auth = inject(AuthService);
  isLoggedIn = this.auth.isLoggedIn;

  constructor(private api: ApiService, private imageService: ImageService, private router: Router) {}

  ngOnInit() {
    this.api.getUserPreferences().subscribe(p => {
      if (!p.throwAnonymous) {
        this.isAnonymous.set(false);
        this.authorName.set(this.auth.user()?.username || '');
      } else if (p.defaultAuthorName) {
        this.authorName.set(p.defaultAuthorName);
      }
    });
  }

  async onFileChange(e: Event) {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.compressing.set(true);
    try {
      const compressed = await this.imageService.compress(file);
      this.imageBase64.set(compressed);
    } finally {
      this.compressing.set(false);
    }
  }

  removeImage() {
    this.imageBase64.set(null);
  }

  toggleAnonymous() {
    this.isAnonymous.update(v => !v);
    if (!this.isAnonymous()) {
      this.authorName.set(this.auth.user()?.username || '');
    } else {
      this.authorName.set('');
    }
  }

  async throwBottle() {
    if (!this.content().trim()) return;
    this.sending.set(true);
    try {
      const bottle = await this.api.throwBottle(
        this.content().trim(),
        this.imageBase64() ?? undefined,
        this.authorName().trim() || (this.isAnonymous() ? undefined : (this.auth.user()?.username || undefined)),
        this.requireLogin(),
        this.commentsPrivate(),
        this.showAdminBadge()
      ).toPromise();
      this.router.navigate(['/bottle', bottle!.id]);
    } finally {
      this.sending.set(false);
    }
  }
}
