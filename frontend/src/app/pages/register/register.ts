import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../services/api.service';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrls: ['./register.scss']
})
export class RegisterPage implements OnInit {
  username = signal('');
  email = signal('');
  password = signal('');
  confirmPassword = signal('');
  error = signal('');
  loading = signal(false);
  success = signal(false);
  registeredEmail = signal('');

  private auth = inject(AuthService);
  private api = inject(ApiService);
  private router = inject(Router);

  ngOnInit() {
    if (this.auth.isLoggedIn()) {
      this.router.navigate(['/my']);
    }
  }

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
      next: (res) => {
        this.loading.set(false);
        this.success.set(true);
        this.registeredEmail.set(e);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.error || '注册失败，请重试');
      }
    });
  }

  resendVerification() {
    const e = this.registeredEmail();
    if (!e) return;
    this.loading.set(true);
    this.api.resendVerification(e).subscribe({
      next: (res) => {
        this.loading.set(false);
        alert(res.message);
      },
      error: (err) => {
        this.loading.set(false);
        alert(err.error?.error || '发送失败');
      }
    });
  }
}
