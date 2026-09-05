import { Component, OnInit } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';
import { PermissionService } from '../../../core/services/permission.service';
import { HasPermissionDirective } from '../../../core/directives/has-permission.directive';
import { MENU_ITEMS, MenuItem } from '../../../core/config/menu.config';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, CommonModule, HasPermissionDirective],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent implements OnInit {
  currentUser: any = null;
  menuItems: MenuItem[] = [];

  constructor(
    private authService: AuthService,
    private permissionService: PermissionService
  ) {}

  ngOnInit(): void {
    // Get user profile
    if (this.authService.isAuthenticated()) {
      this.currentUser = this.authService.getUserProfile();
      this.filterMenuItems();
    }
  }

  isAdmin(): boolean {
    return this.authService.hasRole('ADMIN');
  }

  filterMenuItems(): void {
    this.menuItems = MENU_ITEMS.filter(item => {
      if (item.divider && (!item.roles || item.roles.length === 0)) {
        return true;
      }

      if (item.header) {
        if (item.roles && item.roles.length > 0) {
          return this.permissionService.hasAnyRole(item.roles);
        }
        return true;
      }

      if (item.roles && item.roles.length > 0 && !this.permissionService.hasAnyRole(item.roles)) {
        return false;
      }

      if ((!item.permissions || item.permissions.length === 0) &&
          (!item.roles || item.roles.length === 0)) {
        return true;
      }

      if (item.permissions && item.permissions.length > 0) {
        return this.permissionService.hasAnyPermission(item.permissions);
      }

      return true;
    });
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
      'ADMIN': 'Administrator'
    };

    return roleMap[role] || role;
  }
}