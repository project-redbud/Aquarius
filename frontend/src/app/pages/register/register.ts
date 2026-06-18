import { Component, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrls: ['./register.scss']
})
export class RegisterPage {
  username = signal('');
  email = signal('');
  password = signal('');
  confirmPassword = signal('');
  error = signal('');
  loading = signal(false);

  private auth = inject(AuthService);
  private router = inject(Router);

  onSubmit() {
    const u = this.username().trim();
    const e = this.email().trim();
    const p = this.password();
    const cp = this.confirmPassword();

    if (!u || !e || !p || !cp) {
      this.error.set('请填写所有字段');
      return;
    }
    if (p !== cp) {
      this.error.set('两次输入的密码不一致');
      return;
    }
    if (p.length < 6) {
      this.error.set('密码至少 6 个字符');
      return;
    }

    this.error.set('');
    this.loading.set(true);

    this.auth.register(u, e, p, cp).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/pick']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.error || '注册失败，请重试');
      }
    });
  }
}