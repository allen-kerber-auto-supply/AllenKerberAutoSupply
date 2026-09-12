import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent {
  private readonly auth = inject(AuthService);

  loginMode: 'google' | 'password' = 'password';
  email = '';
  password = '';
  error = '';

  passwordLogin() {
    this.error = '';
    this.auth.passwordLogin(this.email, this.password).subscribe({
      next: () => location.reload(),
      error: error => {
        this.error = error.status === 403
          ? ''
          : (error.error?.message || error.error || 'Unable to sign in.');
      }
    });
  }
}