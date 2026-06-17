import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { Router } from '@angular/router';

@Component({
  standalone: true,
  imports: [FormsModule],
  templateUrl: './throw.html',
  styleUrls: ['./throw.scss']
})
export class ThrowPage {
  content = signal('');
  authorName = signal('');
  isAnonymous = signal(false);
  imageBase64 = signal<string | null>(null);
  sending = signal(false);

  constructor(private api: ApiService, private router: Router) {}

  onFileChange(e: Event) {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => this.imageBase64.set(reader.result as string);
    reader.readAsDataURL(file);
  }

  removeImage() {
    this.imageBase64.set(null);
  }

  async throwBottle() {
    if (!this.content().trim()) return;
    this.sending.set(true);
    try {
      await this.api.throwBottle(
        this.content().trim(),
        this.imageBase64() ?? undefined,
        this.isAnonymous() ? undefined : (this.authorName().trim() || undefined)
      ).toPromise();
      this.router.navigate(['/pick']);
    } finally {
      this.sending.set(false);
    }
  }
}
