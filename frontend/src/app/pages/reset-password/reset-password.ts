import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrls: ['./reset-password.scss']
})
export class ResetPasswordPage implements OnInit {
  token = signal('');
  newPassword = signal('');
  confirmPassword = signal('');
  status = signal<'form' | 'loading' | 'success' | 'error'>('form');
  message = signal('');

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private api: ApiService
  ) {}

  ngOnInit() {
    const t = this.route.snapshot.queryParamMap.get('token');
    if (!t) {
      this.status.set('error');
      this.message.set('重置链接无效');
    } else {
      this.token.set(t);
    }
  }

  onSubmit() {
    const p = this.newPassword();
    const cp = this.confirmPassword();

    if (!p || p.length < 6) {
      this.message.set('新密码至少 6 个字符');
      return;
    }
    if (p !== cp) {
      this.message.set('两次输入的密码不一致');
      return;
    }

    this.status.set('loading');
    this.api.resetPassword(this.token(), p).subscribe({
      next: (res) => {
        this.status.set('success');
        this.message.set(res.message);
        setTimeout(() => this.router.navigate(['/login']), 2000);
      },
      error: (err) => {
        this.status.set('form');
        this.message.set(err.error?.error || '重置失败');
      }
    });
  }
}
