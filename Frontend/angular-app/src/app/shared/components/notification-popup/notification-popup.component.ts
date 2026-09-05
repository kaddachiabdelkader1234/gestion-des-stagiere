import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService, NotificationMessage } from '../../notification.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-notification-popup',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!-- Notification Container -->
    <div class="fixed top-4 right-4 z-50 space-y-4 max-w-md">
      <div *ngFor="let notification of notifications; trackBy: trackByNotificationId" 
           class="transform transition-all duration-500 ease-in-out"
           [ngClass]="{
             'translate-x-0 opacity-100': true,
             'translate-x-full opacity-0': false
           }">
        
        <!-- Notification Card -->
        <div class="bg-white rounded-2xl shadow-2xl border border-gray-100 overflow-hidden max-w-md"
             [ngClass]="{
               'border-l-4 border-l-blue-500': notification.type === 'info',
               'border-l-4 border-l-green-500': notification.type === 'success',
               'border-l-4 border-l-yellow-500': notification.type === 'warning',
               'border-l-4 border-l-red-500': notification.type === 'error',
               'border-l-4 border-l-purple-500': notification.type === 'reminder'
             }">
          
          <!-- Header -->
          <div class="p-4 pb-2">
            <div class="flex items-start justify-between">
              <div class="flex items-center space-x-3">
                <!-- Icon -->
                <div class="flex-shrink-0">
                  <div class="w-10 h-10 rounded-xl flex items-center justify-center"
                       [ngClass]="{
                         'bg-blue-100': notification.type === 'info',
                         'bg-green-100': notification.type === 'success',
                         'bg-yellow-100': notification.type === 'warning',
                         'bg-red-100': notification.type === 'error',
                         'bg-purple-100': notification.type === 'reminder'
                       }">
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"
                         [ngClass]="{
                           'text-blue-600': notification.type === 'info',
                           'text-green-600': notification.type === 'success',
                           'text-yellow-600': notification.type === 'warning',
                           'text-red-600': notification.type === 'error',
                           'text-purple-600': notification.type === 'reminder'
                         }">
                      <!-- Info Icon -->
                      <path *ngIf="notification.type === 'info'" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
                      <!-- Success Icon -->
                      <path *ngIf="notification.type === 'success'" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
                      <!-- Warning Icon -->
                      <path *ngIf="notification.type === 'warning'" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
                      <!-- Error Icon -->
                      <path *ngIf="notification.type === 'error'" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
                      <!-- Reminder Icon -->
                      <path *ngIf="notification.type === 'reminder'" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"/>
                    </svg>
                  </div>
                </div>
                
                <!-- Title and Message -->
                <div class="flex-1 min-w-0">
                  <h4 class="text-sm font-bold text-gray-900 mb-1">{{ notification.title }}</h4>
                  <p class="text-sm text-gray-600 leading-relaxed">{{ notification.message }}</p>
                </div>
              </div>
              
              <!-- Close Button -->
              <button (click)="removeNotification(notification.id)"
                      class="flex-shrink-0 ml-2 p-1 rounded-lg hover:bg-gray-100 transition-colors duration-200">
                <svg class="w-4 h-4 text-gray-400 hover:text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                </svg>
              </button>
            </div>
          </div>
          
          <!-- Actions -->
          <div *ngIf="notification.actions && notification.actions.length > 0" class="px-4 pb-4">
            <div class="flex items-center space-x-2 pt-2">
              <button *ngFor="let action of notification.actions"
                      (click)="executeAction(action, notification.id)"
                      class="px-4 py-2 text-xs font-semibold rounded-lg transition-all duration-200 transform hover:scale-105"
                      [ngClass]="{
                        'bg-indigo-500 hover:bg-indigo-600 text-white shadow-md': action.style === 'primary',
                        'bg-gray-100 hover:bg-gray-200 text-gray-700': action.style === 'secondary',
                        'bg-red-500 hover:bg-red-600 text-white shadow-md': action.style === 'danger'
                      }">
                {{ action.label }}
              </button>
            </div>
          </div>
          
          <!-- Progress Bar for Timed Notifications -->
          <div *ngIf="notification.duration && notification.duration > 0" 
               class="h-1 bg-gray-100">
            <div class="h-full bg-gradient-to-r from-indigo-500 to-purple-500 transition-all duration-100 ease-linear"
                 [style.width.%]="getProgressPercentage(notification)">
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host {
      pointer-events: none;
    }
    
    .notification-card {
      pointer-events: auto;
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
    
    @keyframes slideOutRight {
      from {
        transform: translateX(0);
        opacity: 1;
      }
      to {
        transform: translateX(100%);
        opacity: 0;
      }
    }
    
    .animate-slide-in {
      animation: slideInRight 0.5s ease-out;
    }
    
    .animate-slide-out {
      animation: slideOutRight 0.3s ease-in;
    }
  `]
})
export class NotificationPopupComponent implements OnInit, OnDestroy {
  notifications: NotificationMessage[] = [];
  private subscription?: Subscription;
  private progressIntervals: { [key: string]: any } = {};

  constructor(private notificationService: NotificationService) {}

  ngOnInit(): void {
    this.subscription = this.notificationService.getNotifications().subscribe(
      notifications => {
        // Only show popup notifications (reminders and important alerts)
        this.notifications = notifications.filter(n => 
          n.type === 'reminder' || 
          n.type === 'error' || 
          (n.type === 'warning' && n.isPopup)
        );
        
        // Start progress tracking for timed notifications
        this.notifications.forEach(notification => {
          if (notification.duration && notification.duration > 0 && !this.progressIntervals[notification.id]) {
            this.startProgressTracking(notification);
          }
        });
      }
    );
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
    
    // Clear all progress intervals
    Object.values(this.progressIntervals).forEach(interval => {
      if (interval) clearInterval(interval);
    });
  }

  trackByNotificationId(index: number, notification: NotificationMessage): string {
    return notification.id;
  }

  removeNotification(id: string): void {
    // Clear progress interval
    if (this.progressIntervals[id]) {
      clearInterval(this.progressIntervals[id]);
      delete this.progressIntervals[id];
    }
    
    this.notificationService.removeNotification(id);
  }

  executeAction(action: any, notificationId: string): void {
    action.action();
    this.removeNotification(notificationId);
  }

  private startProgressTracking(notification: NotificationMessage): void {
    if (!notification.duration) return;
    
    const startTime = notification.timestamp.getTime();
    const duration = notification.duration;
    
    this.progressIntervals[notification.id] = setInterval(() => {
      const elapsed = Date.now() - startTime;
      const progress = Math.min((elapsed / duration) * 100, 100);
      
      if (progress >= 100) {
        clearInterval(this.progressIntervals[notification.id]);
        delete this.progressIntervals[notification.id];
      }
    }, 100);
  }

  getProgressPercentage(notification: NotificationMessage): number {
    if (!notification.duration) return 0;
    
    const elapsed = Date.now() - notification.timestamp.getTime();
    const progress = Math.min((elapsed / notification.duration) * 100, 100);
    return 100 - progress; // Reverse for countdown effect
  }
}