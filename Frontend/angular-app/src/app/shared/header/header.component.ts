import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { DataService } from '../../core/services/data.service';
import { MenuItem } from '../../core/models/menu.model';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent implements OnInit, OnDestroy {
  headerData: MenuItem[] = [];
  navbarOpen = false;
  sticky = false;
  userMenuOpen = false;
  currentUser: any = null;

  constructor(
    private dataService: DataService,
    private router: Router,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.dataService.getData().subscribe(data => {
      this.headerData = data.HeaderData;
    });

    window.addEventListener('scroll', this.handleScroll.bind(this));
    
    // Get user profile if authenticated
    if (this.isAuthenticated()) {
      this.currentUser = this.authService.getUserProfile();
    }
    
    // Close user menu when clicking outside
    document.addEventListener('click', this.handleClickOutside.bind(this));
  }

  ngOnDestroy(): void {
    document.removeEventListener('click', this.handleClickOutside.bind(this));
  }

  handleClickOutside(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (this.userMenuOpen && !target.closest('.user-menu-container')) {
      this.userMenuOpen = false;
    }
  }

  handleScroll(): void {
    this.sticky = window.scrollY >= 10;
  }

  toggleNavbar(): void {
    this.navbarOpen = !this.navbarOpen;
  }

  toggleUserMenu(): void {
    this.userMenuOpen = !this.userMenuOpen;
  }

  openSignIn(): void {
    this.navbarOpen = false;
    this.router.navigate(['/auth/sign-in']);
  }

  openSignUp(): void {
    this.navbarOpen = false;
    this.router.navigate(['/auth/sign-up']);
  }

  goToDashboard(): void {
    this.userMenuOpen = false;
    this.router.navigate(['/dashboard']);
  }

  logout(): void {
    this.userMenuOpen = false;
    this.authService.logout();
    this.router.navigate(['/']);
  }

  isAuthenticated(): boolean {
    return this.authService.isAuthenticated();
  }

  getUserInitial(): string {
    return this.currentUser?.firstName?.charAt(0).toUpperCase() || this.currentUser?.username?.charAt(0).toUpperCase() || 'U';
  }

  getUserImage(): string | null {
    return null;
  }
}