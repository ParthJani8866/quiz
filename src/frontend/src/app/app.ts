import { Component, effect, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { AuthService } from './core/auth.service';
import { QuizLibraryComponent } from './features/quiz-library/quiz-library.component';

@Component({
  selector: 'app-root',
  imports: [ReactiveFormsModule, QuizLibraryComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly formBuilder = inject(FormBuilder);
  private readonly platformId = inject(PLATFORM_ID);
  readonly auth = inject(AuthService);
  readonly title = 'QuizFlow';
  readonly workspace = signal(false);
  readonly isRegistering = signal(false);
  readonly isResetting = signal(false);
  readonly isVerifying = signal(false);
  readonly busy = signal(false);
  readonly notice = signal('');
  readonly error = signal('');
  readonly form = this.formBuilder.nonNullable.group({
    firstName: [''],
    lastName: [''],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.pattern('^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[^A-Za-z\\d]).{12,}$')]],
    confirmPassword: ['']
  });

  constructor() {
    if (!isPlatformBrowser(this.platformId)) return;
    this.workspace.set(window.location.pathname.startsWith('/app'));
    effect(() => {
      if (this.auth.accessToken()) {
        this.workspace.set(true);
        if (!window.location.pathname.startsWith('/app')) window.history.replaceState({}, '', '/app/quizzes');
      }
    });
    this.auth.listenForGoogleLogin();
    const url = new URL(window.location.href);
    if (url.pathname.endsWith('/verify-email')) {
      this.isVerifying.set(true);
      this.auth.verifyEmail(url.searchParams.get('email') ?? '', url.searchParams.get('token') ?? '')
        .subscribe({ next: () => this.notice.set('Your email is verified. You can now sign in.'), error: () => this.error.set('This verification link is invalid or has expired.') });
    } else if (url.pathname.endsWith('/confirm-email-change')) {
      this.isVerifying.set(true);
      this.auth.confirmEmailChange(url.searchParams.get('email') ?? '', url.searchParams.get('newEmail') ?? '', url.searchParams.get('token') ?? '')
        .subscribe({ next: () => this.notice.set('Your email address has been updated. Sign in with the new address.'), error: () => this.error.set('This email change link is invalid or has expired.') });
    } else if (url.pathname.endsWith('/reset-password')) {
      this.isResetting.set(true);
      this.form.patchValue({ email: url.searchParams.get('email') ?? '' });
    } else if (!this.workspace()) {
      this.auth.refresh().subscribe({ error: () => undefined });
    }
  }

  setMode(register: boolean): void {
    this.isRegistering.set(register);
    const firstName = this.form.controls.firstName;
    const lastName = this.form.controls.lastName;
    const confirmation = this.form.controls.confirmPassword;
    if (register) {
      firstName.setValidators([Validators.required, Validators.maxLength(100)]);
      lastName.setValidators([Validators.required, Validators.maxLength(100)]);
      confirmation.setValidators([Validators.required]);
    } else {
      firstName.clearValidators();
      lastName.clearValidators();
      confirmation.clearValidators();
    }
    firstName.updateValueAndValidity();
    lastName.updateValueAndValidity();
    confirmation.updateValueAndValidity();
    this.notice.set('');
    this.error.set('');
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.busy()) return;
    const { email, password, firstName, lastName, confirmPassword } = this.form.getRawValue();
    if (this.isRegistering() && password !== confirmPassword) {
      this.error.set('Password and confirmation must match.');
      return;
    }
    this.busy.set(true);
    this.error.set('');
    const request = this.isResetting()
      ? this.auth.resetPassword(email, new URLSearchParams(window.location.search).get('token') ?? '', password)
      : this.isRegistering()
        ? this.auth.register({ email, password, firstName, lastName, confirmPassword })
        : this.auth.login(email, password);
    request.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => {
        this.notice.set(this.isResetting() ? 'Password updated. You can sign in now.' : this.isRegistering() ? 'Account created. Check your inbox to verify your email.' : 'Signed in successfully.');
        if (!this.isResetting() && !this.isRegistering()) {
          this.workspace.set(true);
          window.history.replaceState({}, '', '/app/quizzes');
        }
      },
      error: (response: HttpErrorResponse) => this.error.set(response.error?.message ?? response.error?.errors?.join(' ') ?? 'We could not complete that request. Check your details and try again.')
    });
  }

  forgotPassword(): void {
    const email = this.form.controls.email.value;
    if (!this.form.controls.email.valid) {
      this.form.controls.email.markAsTouched();
      this.error.set('Enter your email address first.');
      return;
    }
    this.auth.forgotPassword(email).subscribe({ next: () => this.notice.set('If that account exists, password reset instructions are on the way.'), error: () => this.error.set('We could not send reset instructions. Try again shortly.') });
  }
}
