import { Component, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrls: ['./login.scss']
})
export class LoginPage {
  loginField = signal('');
  password = signal('');
  error = signal('');
  loading = signal(false);

  private auth = inject(AuthService);
  private router = inject(Router);

  onSubmit() {
    const login = this.loginField().trim();
    const pwd = this.password();
    if (!login || !pwd) {
      this.error.set('请填写登录名和密码');
      return;
    }

    this.error.set('');
    this.loading.set(true);

    this.auth.login(login, pwd).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/pick']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.error || '登录失败，请重试');
      }
    });
  }
}