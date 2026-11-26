import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

export interface AuthTokens {
  accessToken: string;
  expiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly endpoint = '/api/v1/auth';
  readonly accessToken = signal<string | null>(null);

  constructor(private readonly http: HttpClient) {}

  login(email: string, password: string): Observable<AuthTokens> {
    return this.http.post<AuthTokens>(`${this.endpoint}/login`, { email, password }, { withCredentials: true })
      .pipe(tap(tokens => this.accessToken.set(tokens.accessToken)));
  }

  register(data: { firstName: string; lastName: string; email: string; password: string; confirmPassword: string }): Observable<unknown> {
    return this.http.post(`${this.endpoint}/register`, data, { withCredentials: true });
  }

  signInWithGoogle(): void {
    window.open(`${this.endpoint}/google`, 'quizflow-google', 'popup,width=520,height=650');
  }

  listenForGoogleLogin(): void {
    window.addEventListener('message', event => {
      if (event.origin !== window.location.origin || event.data?.type !== 'quizflow:auth') return;
      this.accessToken.set(event.data.accessToken as string);
    });
  }

  refresh(): Observable<AuthTokens> {
    return this.http.post<AuthTokens>(`${this.endpoint}/refresh`, {}, { withCredentials: true })
      .pipe(tap(tokens => this.accessToken.set(tokens.accessToken)));
  }

  verifyEmail(email: string, token: string): Observable<void> {
    return this.http.post<void>(`${this.endpoint}/verify-email`, { email, token });
  }

  confirmEmailChange(email: string, newEmail: string, token: string): Observable<void> {
    return this.http.post<void>(`${this.endpoint}/confirm-email-change`, { email, newEmail, token });
  }

  forgotPassword(email: string): Observable<unknown> {
    return this.http.post(`${this.endpoint}/forgot-password`, { email });
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.endpoint}/reset-password`, { email, token, newPassword });
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.endpoint}/logout`, {}, { withCredentials: true })
      .pipe(tap(() => this.accessToken.set(null)));
  }
}
