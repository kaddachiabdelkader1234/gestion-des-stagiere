import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { ApiError } from '../../../core/http/api-error';
import { toApiError } from '../../../core/http/api-error';

@Component({
  selector: 'app-create-encadrant',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './create-encadrant.component.html'
})
export class CreateEncadrantComponent {
  form: FormGroup;
  isLoading = false;
  errorMessage = '';
  successMessage = '';
  tempPassword = '';

  constructor(
    private fb: FormBuilder,
    private router: Router,
    private http: HttpClient
  ) {
    this.form = this.fb.group({
      firstName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
      email: ['', [Validators.required, Validators.email]],
      departement: ['', Validators.required]
    });
  }

  onSubmit() {
    if (this.form.valid && !this.isLoading) {
      this.isLoading = true;
      this.errorMessage = '';
      this.successMessage = '';
      this.tempPassword = '';

      this.http.post<any>(`${environment.apiUrl}/auth/encadrants`, this.form.value)
        .subscribe({
          next: (response) => {
            this.isLoading = false;
            this.tempPassword = this.extractTempPassword(response.message);
            this.successMessage = `Encadrant "${response.firstName}" créé avec succès !`;
            this.form.reset();
          },
          error: (err) => {
            this.isLoading = false;
            const apiError = toApiError(err);
            this.errorMessage = apiError.message;
          }
        });
    }
  }

  private extractTempPassword(message: string): string {
    const prefix = 'Mot de passe temporaire: ';
    const idx = message.indexOf(prefix);
    return idx >= 0 ? message.substring(idx + prefix.length) : '';
  }

  copyPassword() {
    navigator.clipboard.writeText(this.tempPassword);
  }
}
