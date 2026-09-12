import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-password-change',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './password-change.component.html',
  styleUrls: ['./password-change.component.css']
})
export class PasswordChangeComponent {
  private readonly auth = inject(AuthService);

  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  error = '';

  changePassword() {
    if (this.newPassword !== this.confirmPassword) {
      this.error = 'The new passwords do not match.';
      return;
    }

    this.error = '';
    this.auth.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: () => location.reload(),
      error: error => this.error = error.error?.message || 'Unable to update your password.'
    });
  }
}