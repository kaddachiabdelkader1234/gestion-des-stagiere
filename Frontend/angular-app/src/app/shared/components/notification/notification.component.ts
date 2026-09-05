import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService, NotificationMessage } from '../../notification.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-notification',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!-- Notification Container -->
    <div class="fixed top-4 right-4 z-[9999] space-y-3 max-w-sm">
      <div *ngFor="let notification of notifications; trackBy: trackByFn" 
           class="notification-card animate-slide-in-right"
           [ngClass]="getNotificationClass(notification.type)">
        
        <!-- Notification Header -->
        <div class="flex items-start justify-between mb-2">
          <div class="flex items-center space-x-2">
            <!-- Icon -->
            <div class="flex-shrink-0">
              <svg *ngIf="notification.type === 'success'" class="w-5 h-5 text-green-600" fill="currentColor" viewBox="0 0 20 20">
                <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/>
              </svg>
              <svg *ngIf="notification.type === 'error'" class="w-5 h-5 text-red-600" fill="currentColor" viewBox="0 0 20 20">
                <path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/>
              </svg>
              <svg *ngIf="notification.type === 'warning'" class="w-5 h-5 text-yellow-600" fill="currentColor" viewBox="0 0 20 20">
                <path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/>
              </svg>
              <svg *ngIf="notification.type === 'info'" class="w-5 h-5 text-blue-600" fill="currentColor" viewBox="0 0 20 20">
                <path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/>
              </svg>
              <svg *ngIf="notification.type === 'reminder'" class="w-5 h-5 text-purple-600" fill="currentColor" viewBox="0 0 20 20">
                <path d="M10 2a6 6 0 00-6 6v3.586l-.707.707A1 1 0 004 14h12a1 1 0 00.707-1.707L16 11.586V8a6 6 0 00-6-6zM10 18a3 3 0 01-3-3h6a3 3 0 01-3 3z"/>
              </svg>
            </div>
            
            <!-- Title -->
            <h4 class="font-semibold text-sm" [ngClass]="getTitleClass(notification.type)">
              {{ notification.title }}
            </h4>
          </div>
          
          <!-- Close Button -->
          <button (click)="removeNotification(notification.id)" 
                  class="flex-shrink-0 ml-2 text-gray-400 hover:text-gray-600 transition-colors">
            <svg class="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
              <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd"/>
            </svg>
          </button>
        </div>
        
        <!-- Message -->
        <p class="text-sm text-gray-700 mb-3">{{ notification.message }}</p>
        
        <!-- Actions -->
        <div *ngIf="notification.actions && notification.actions.length > 0" 
             class="flex space-x-2">
          <button *ngFor="let action of notification.actions"
                  (click)="executeAction(action, notification.id)"
                  class="px-3 py-1.5 text-xs font-medium rounded-lg transition-all duration-200"
                  [ngClass]="getActionClass(action.style || 'secondary')">
            {{ action.label }}
          </button>
        </div>
        
        <!-- Progress Bar for Timed Notifications -->
        <div *ngIf="notification.duration && notification.duration > 0" 
             class="mt-3 w-full h-1 bg-gray-200 rounded-full overflow-hidden">
          <div class="h-full bg-current opacity-50 animate-progress" 
               [style.animation-duration]="notification.duration + 'ms'"></div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .notification-card {
      @apply bg-white rounded-xl shadow-lg border border-gray-200 p-4 backdrop-blur-sm;
      animation: slideInRight 0.3s ease-out;
    }

    .notification-success {
      @apply border-l-4 border-green-500 bg-green-50;
    }

    .notification-error {
      @apply border-l-4 border-red-500 bg-red-50;
    }

    .notification-warning {
      @apply border-l-4 border-yellow-500 bg-yellow-50;
    }

    .notification-info {
      @apply border-l-4 border-blue-500 bg-blue-50;
    }

    .notification-reminder {
      @apply border-l-4 border-purple-500 bg-purple-50 shadow-xl;
      animation: slideInRight 0.3s ease-out, pulse 2s infinite;
    }

    @keyframes slideInRight {
      from {
        transform: translateX(100%);
        opacity: 0;
      }
      to {
        transform: translateX(0);
        opacity: 1;
      }
    }

    @keyframes pulse {
      0%, 100% {
        box-shadow: 0 0 0 0 rgba(147, 51, 234, 0.4);
      }
      50% {
        box-shadow: 0 0 0 10px rgba(147, 51, 234, 0);
      }
    }

    @keyframes progress {
      from {
        width: 100%;
      }
      to {
        width: 0%;
      }
    }

    .animate-progress {
      animation: progress linear;
    }

    .animate-slide-in-right {
      animation: slideInRight 0.3s ease-out;
    }
  `]
})
export class NotificationComponent implements OnInit, OnDestroy {
  notifications: NotificationMessage[] = [];
  private subscription?: Subscription;

  constructor(private notificationService: NotificationService) {}

  ngOnInit(): void {
    this.subscription = this.notificationService.getNotifications().subscribe(
      notifications => this.notifications = notifications
    );
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }

  trackByFn(index: number, item: NotificationMessage): string {
    return item.id;
  }

  getNotificationClass(type: string): string {
    const baseClass = 'notification-card';
    switch (type) {
      case 'success': return `${baseClass} notification-success`;
      case 'error': return `${baseClass} notification-error`;
      case 'warning': return `${baseClass} notification-warning`;
      case 'info': return `${baseClass} notification-info`;
      case 'reminder': return `${baseClass} notification-reminder`;
      default: return baseClass;
    }
  }

  getTitleClass(type: string): string {
    switch (type) {
      case 'success': return 'text-green-800';
      case 'error': return 'text-red-800';
      case 'warning': return 'text-yellow-800';
      case 'info': return 'text-blue-800';
      case 'reminder': return 'text-purple-800';
      default: return 'text-gray-800';
    }
  }

  getActionClass(style: string): string {
    switch (style) {
      case 'primary': return 'bg-indigo-600 hover:bg-indigo-700 text-white shadow-md hover:shadow-lg transform hover:scale-105';
      case 'danger': return 'bg-red-600 hover:bg-red-700 text-white shadow-md hover:shadow-lg transform hover:scale-105';
      case 'secondary':
      default: return 'bg-gray-200 hover:bg-gray-300 text-gray-800 shadow-sm hover:shadow-md transform hover:scale-105';
    }
  }

  removeNotification(id: string): void {
    this.notificationService.removeNotification(id);
  }

  executeAction(action: any, notificationId: string): void {
    action.action();
    this.removeNotification(notificationId);
  }
}