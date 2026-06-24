import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../services/api.service';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrls: ['./login.scss']
})
export class LoginPage implements OnInit {
  private returnUrl: string | null = null;

  loginField = signal('');
  password = signal('');
  error = signal('');
  loading = signal(false);

  // Forgot password
  showForgot = signal(false);
  forgotEmail = signal('');
  forgotMessage = signal('');
  forgotLoading = signal(false);
  forgotCooldown = signal(0);

  // Resend verification
  verifyCooldown = signal(0);
  verifyMsg = signal('');

  private auth = inject(AuthService);
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  ngOnInit() {
    if (this.auth.isLoggedIn()) {
      this.router.navigate(['/my']);
      return;
    }
    this.returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
  }

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
        this.router.navigateByUrl(this.returnUrl || '/pick');
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.error || '登录失败，请重试');
      }
    });
  }

  toggleForgot() {
    this.showForgot.update(v => !v);
    this.forgotEmail.set('');
    this.forgotMessage.set('');
  }

  submitForgot() {
    const email = this.forgotEmail().trim();
    if (!email || this.forgotCooldown() > 0) return;

    this.startForgotCooldown();
    this.forgotLoading.set(true);
    this.forgotMessage.set('');

    this.api.forgotPassword(email).subscribe({
      next: (res) => {
        this.forgotLoading.set(false);
        this.forgotMessage.set(res.message);
      },
      error: (err) => {
        this.forgotLoading.set(false);
        this.forgotMessage.set(err.error?.error || '发送失败');
      }
    });
  }

  resendVerify() {
    const email = this.loginField().trim();
    if (!email || this.verifyCooldown() > 0) return;
    this.startVerifyCooldown();
    this.verifyMsg.set('');
    this.api.resendVerification(email).subscribe({
      next: r => { this.verifyMsg.set(r.message); },
      error: e => { this.verifyMsg.set(e.error?.error || '发送失败'); }
    });
  }

  private forgotTimer: any;
  private startForgotCooldown() {
    this.forgotCooldown.set(60);
    this.forgotTimer = setInterval(() => {
      const v = this.forgotCooldown() - 1;
      this.forgotCooldown.set(v);
      if (v <= 0) clearInterval(this.forgotTimer);
    }, 1000);
  }

  private verifyTimer: any;
  private startVerifyCooldown() {
    this.verifyCooldown.set(60);
    this.verifyTimer = setInterval(() => {
      const v = this.verifyCooldown() - 1;
      this.verifyCooldown.set(v);
      if (v <= 0) clearInterval(this.verifyTimer);
    }, 1000);
  }
}
