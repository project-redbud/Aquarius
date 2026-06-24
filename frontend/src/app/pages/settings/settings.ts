import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './settings.html',
  styleUrls: ['./settings.scss']
})
export class SettingsPage implements OnInit {
  private api = inject(ApiService);

  // Password
  oldPassword = signal('');
  newPassword = signal('');
  pwdMsg = signal('');
  pwdLoading = signal(false);

  // Email
  email = signal('');
  emailVerified = signal(false);
  pendingEmail = signal<string | null>(null);
  newEmail = signal('');
  emailMsg = signal('');
  emailLoading = signal(false);
  resendCooldown = signal(0);

  // Preferences
  notifyPreference = signal('default');
  viewPrivateComments = signal(false);
  throwAnonymous = signal(true);
  defaultAuthorName = signal('');
  isAdmin = signal(false);
  prefMsg = signal('');
  prefLoading = signal(false);

  ngOnInit() {
    this.api.getUserPreferences().subscribe(p => {
      this.email.set(p.email);
      this.emailVerified.set(p.emailVerified);
      this.pendingEmail.set(p.newEmail || null);
      this.notifyPreference.set(p.notifyPreference || 'default');
      this.viewPrivateComments.set(p.viewPrivateComments);
      this.throwAnonymous.set(p.throwAnonymous !== false);
      this.defaultAuthorName.set(p.defaultAuthorName || '');
      this.isAdmin.set(p.isAdmin);
    });
  }

  changePassword() {
    const old = this.oldPassword().trim();
    const pwd = this.newPassword().trim();
    if (!old || !pwd) { this.pwdMsg.set('请填写完整'); return; }
    if (pwd.length < 6) { this.pwdMsg.set('新密码至少6个字符'); return; }
    this.pwdLoading.set(true);
    this.api.changePassword(old, pwd).subscribe({
      next: r => { this.pwdMsg.set(r.message); this.pwdLoading.set(false); this.oldPassword.set(''); this.newPassword.set(''); },
      error: e => { this.pwdMsg.set(e.error?.error || '修改失败'); this.pwdLoading.set(false); }
    });
  }

  changeEmail() {
    const email = this.newEmail().trim();
    if (!email) { this.emailMsg.set('请输入新邮箱'); return; }
    this.emailLoading.set(true);
    this.api.changeEmail(email).subscribe({
      next: r => { this.emailMsg.set(r.message); this.emailLoading.set(false); },
      error: e => { this.emailMsg.set(e.error?.error || '请求失败'); this.emailLoading.set(false); }
    });
  }

  resendVerification() {
    this.api.resendUserVerification().subscribe(r => alert(r.message));
  }

  resendPendingVerification() {
    if (this.resendCooldown() > 0) return;
    this.startCooldown();
    this.emailLoading.set(true);
    this.api.resendUserVerification().subscribe({
      next: r => { alert(r.message); this.emailLoading.set(false); },
      error: e => { alert(e.error?.error || '发送失败'); this.emailLoading.set(false); }
    });
  }

  private cooldownTimer: any;
  private startCooldown() {
    this.resendCooldown.set(60);
    this.cooldownTimer = setInterval(() => {
      const v = this.resendCooldown() - 1;
      this.resendCooldown.set(v);
      if (v <= 0) clearInterval(this.cooldownTimer);
    }, 1000);
  }

  savePreferences() {
    this.prefLoading.set(true);
    this.api.updateUserPreferences(
      this.notifyPreference(),
      this.viewPrivateComments(),
      this.throwAnonymous(),
      this.defaultAuthorName().trim() || undefined
    ).subscribe({
      next: () => { this.prefMsg.set('已保存'); this.prefLoading.set(false); },
      error: e => { this.prefMsg.set(e.error?.error || '保存失败'); this.prefLoading.set(false); }
    });
  }

  toggleViewPrivateComments() {
    this.viewPrivateComments.update(v => !v);
    this.api.updateUserPreferences(undefined, this.viewPrivateComments()).subscribe();
  }
}
