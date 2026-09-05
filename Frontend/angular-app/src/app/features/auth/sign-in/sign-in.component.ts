import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ApiError } from '../../../core/http/api-error';

@Component({
  selector: 'app-sign-in',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterModule],
  templateUrl: './sign-in.component.html',
  styleUrls: ['./sign-in.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SignInComponent {
  signInForm: FormGroup;
  showPassword = false;
  rememberMe = false;
  isLoading = false;
  errorMessage = '';

  constructor(
    private fb: FormBuilder,
    private router: Router,
    private authService: AuthService
  ) {
    this.signInForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

  togglePasswordVisibility() {
    this.showPassword = !this.showPassword;
  }

  onSubmit() {
    if (this.signInForm.valid && !this.isLoading) {
      this.isLoading = true;
      this.errorMessage = '';

      const loginData = {
        username: this.signInForm.value.email,
        password: this.signInForm.value.password
      };

      this.authService.login(loginData).subscribe({
        next: () => {
          // Only reached on a real 2xx now — AuthService rethrows failures instead of
          // converting them into a normal emission, which used to land here with no token.
          this.isLoading = false;
          this.router.navigate(['/dashboard']);
        },
        error: (error: ApiError) => {
          this.isLoading = false;
          this.errorMessage = error.message;
        }
      });
    }
  }
}
