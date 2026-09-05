import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService, AuthResponse } from '../../../core/services/auth.service';
import { StatsService, DashboardStats } from '../../../core/services/stats.service';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardPageComponent implements OnInit {
  currentUser: AuthResponse | null = null;
  stats: DashboardStats | null = null;
  statsLoading = false;

  constructor(
    private authService: AuthService,
    private statsService: StatsService,
  ) {}

  ngOnInit(): void {
    this.currentUser = this.authService.getUserInfo();

    this.authService.fetchUserData().subscribe({
      next: (userData) => {
        this.currentUser = userData;
      },
      error: () => {
        // Keep locally cached user data if the refresh fails.
      }
    });

    // Load real stats for admins
    if (this.currentUser?.role === 'ADMIN') {
      this.statsLoading = true;
      this.statsService.getDashboardStats().subscribe({
        next: (data) => {
          this.stats = data;
          this.statsLoading = false;
        },
        error: () => {
          this.statsLoading = false;
        }
      });
    }
  }

  getUserImage(): string | null {
    if (this.currentUser?.imageBase64) {
      return `data:image/jpeg;base64,${this.currentUser.imageBase64}`;
    }
    return null;
  }

  getUserInitial(): string {
    return this.currentUser?.firstName?.charAt(0).toUpperCase() || 'U';
  }

  formatRole(role: string | undefined): string {
    if (!role) return 'User';

    const roleMap: { [key: string]: string } = {
      'ADMIN': 'Admin',
      'LEARNER': 'Learner',
      'TRAINER': 'Encadrant'
    };

    return roleMap[role] || role;
  }

  isAdmin(): boolean {
    return this.currentUser?.role === 'ADMIN';
  }
}