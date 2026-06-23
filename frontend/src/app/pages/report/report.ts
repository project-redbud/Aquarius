import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ImageService } from '../../services/image.service';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './report.html',
  styleUrls: ['./report.scss']
})
export class ReportPage implements OnInit {
  auth = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private api = inject(ApiService);
  private image = inject(ImageService);

  bottleId = signal<number>(0);
  content = signal('');
  imageBase64 = signal<string | null>(null);
  submitting = signal(false);

  ngOnInit() {
    const id = Number(this.route.snapshot.queryParamMap.get('bottleId'));
    if (id) this.bottleId.set(id);
  }

  async onFileSelected(e: Event) {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (file) {
      this.imageBase64.set(await this.image.compress(file));
    }
  }

  submit() {
    const text = this.content().trim();
    if (!text || !this.bottleId()) return;
    this.submitting.set(true);
    this.api.reportBottle(this.bottleId(), text, this.imageBase64() ?? undefined).subscribe({
      next: () => {
        alert('举报已提交');
        this.router.navigate(['/pick']);
      },
      error: () => {
        alert('提交失败，请登录后再试');
        this.submitting.set(false);
      }
    });
  }
}
