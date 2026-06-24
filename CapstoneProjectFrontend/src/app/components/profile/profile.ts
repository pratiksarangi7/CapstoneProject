import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UserService } from '../../services/user.service';
import { UserProfileResponseDto } from '../../dtos/user-profile.response.dto';
import { ChangePasswordRequestDto } from '../../dtos/change-password.request.dto';

@Component({
  selector: 'app-profile',
  imports: [FormsModule],
  templateUrl: './profile.html',
  styleUrl: './profile.css',
})
export class Profile implements OnInit {
  // ── Profile data ──────────────────────────────────────────────────────────
  profile = signal<UserProfileResponseDto | null>(null);
  isLoading = signal(true);
  loadError = signal<string | null>(null);

  // ── Change password form ──────────────────────────────────────────────────
  oldPassword = '';
  newPassword = '';
  confirmPassword = '';

  isChangingPassword = signal(false);
  passwordSuccess = signal<string | null>(null);
  passwordError = signal<string | null>(null);
  showOldPassword = signal(false);
  showNewPassword = signal(false);
  showConfirmPassword = signal(false);

  constructor(private userService: UserService) { }

  ngOnInit(): void {
    this.loadProfile();
  }

  // ── Load profile ──────────────────────────────────────────────────────────
  loadProfile(): void {
    this.isLoading.set(true);
    this.loadError.set(null);
    this.userService.getProfileDetails().subscribe({
      next: (data) => {
        this.profile.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load profile', err);
        this.loadError.set('Could not load your profile. Please try again.');
        this.isLoading.set(false);
      },
    });
  }

  // ── Change password ───────────────────────────────────────────────────────
  submitChangePassword(): void {
    this.passwordSuccess.set(null);
    this.passwordError.set(null);

    if (!this.oldPassword || !this.newPassword || !this.confirmPassword) {
      this.passwordError.set('All fields are required.');
      return;
    }
    if (this.newPassword.length < 8) {
      this.passwordError.set('New password must be at least 8 characters.');
      return;
    }
    if (this.newPassword !== this.confirmPassword) {
      this.passwordError.set('New password and confirm password do not match.');
      return;
    }
    if (this.oldPassword === this.newPassword) {
      this.passwordError.set('New password must differ from the current password.');
      return;
    }

    const body: ChangePasswordRequestDto = {
      oldPassword: this.oldPassword,
      newPassword: this.newPassword,
    };

    this.isChangingPassword.set(true);
    this.userService.changePassword(body).subscribe({
      next: () => {
        this.passwordSuccess.set('Password changed successfully.');
        this.isChangingPassword.set(false);
        this.oldPassword = '';
        this.newPassword = '';
        this.confirmPassword = '';
      },
      error: (err) => {
        console.error('Failed to change password', err);
        const msg =
          err?.error?.message ||
          err?.error ||
          'Failed to change password. Please verify your current password.';
        this.passwordError.set(typeof msg === 'string' ? msg : JSON.stringify(msg));
        this.isChangingPassword.set(false);
      },
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────
  getInitials(name: string): string {
    return name
      .split(' ')
      .map((n) => n.charAt(0))
      .join('')
      .toUpperCase()
      .slice(0, 2);
  }

  levelLabel(level: number): string {
    return `Level ${level}`;
  }
}
