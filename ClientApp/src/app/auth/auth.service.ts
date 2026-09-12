import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AuthSession {
  authenticated: boolean;
  roles: string[];
  name?: string;
  email?: string;
  mustChangePassword: boolean;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  getCurrentSession(): Observable<AuthSession> {
    return this.http.get<AuthSession>('/api/auth/me');
  }

  passwordLogin(email: string, password: string): Observable<void> {
    return this.http.post<void>('/auth/password-login', { email, password });
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>('/auth/change-password', { currentPassword, newPassword });
  }

  logout(): Observable<void> {
    return this.http.post<void>('/auth/logout', {});
  }
}