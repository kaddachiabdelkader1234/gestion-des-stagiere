import { Component, OnInit, OnDestroy } from '@angular/core';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationComponent } from '../../../shared/components/notification/notification.component';
import { filter } from 'rxjs/operators';

@Component({
  selector: 'app-dashboard-layout',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, CommonModule, NotificationComponent],
  templateUrl: './dashboard-layout.component.html',
  styleUrl: './dashboard-layout.component.scss'
})
export class DashboardLayoutComponent implements OnInit, OnDestroy {
  currentUser: any = null;
  currentRoute: string = '';

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Get user profile
    if (this.authService.isAuthenticated()) {
      this.currentUser = this.authService.getUserProfile();
    }
    
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe((event: any) => {
      this.currentRoute = event.url;
    });
  }

  ngOnDestroy(): void {
    // No subscriptions to clean up
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/']);
  }

  getUserInitial(): string {
    return this.currentUser?.firstName?.charAt(0).toUpperCase() || this.currentUser?.username?.charAt(0).toUpperCase() || 'U';
  }

  getUserImage(): string | null {
    return null;
  }

  formatRole(role: string | undefined): string {
    if (!role) return 'User';
    const roleMap: { [key: string]: string } = {
      'USER': 'User',
      'LEARNER': 'Learner',
      'TRAINER': 'Encadrant',
      'RH_COMPANY': 'HR Company',
      'RH_SMARTEK': 'HR Smartek',
      'ADMIN': 'Administrator'
    };
    return roleMap[role] || role;
  }

  getPageTitle(): string {
    return 'Dashboard';
  }
}