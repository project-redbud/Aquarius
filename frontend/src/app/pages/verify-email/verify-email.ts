import { Component, OnInit, signal, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';

@Component({
  standalone: true,
  imports: [RouterLink],
  templateUrl: './verify-email.html',
  styleUrls: ['./verify-email.scss']
})
export class VerifyEmailPage implements OnInit {
  status = signal<'loading' | 'success' | 'error'>('loading');
  message = signal('');

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private api: ApiService,
    private auth: AuthService
  ) {}

  ngOnInit() {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!token) {
      this.status.set('error');
      this.message.set('验证链接无效');
      return;
    }

    this.api.verifyEmail(token).subscribe({
      next: (res) => {
        this.auth.persistLogin(res);
        this.status.set('success');
        this.message.set('邮箱验证成功！');
        setTimeout(() => this.router.navigate(['/pick']), 2000);
      },
      error: (err) => {
        this.status.set('error');
        this.message.set(err.error?.error || '验证失败，链接可能已过期');
      }
    });
  }
}
