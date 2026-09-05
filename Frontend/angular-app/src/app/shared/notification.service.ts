import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, interval } from 'rxjs';

export interface NotificationMessage {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info' | 'reminder';
  title: string;
  message: string;
  duration?: number; // in milliseconds, 0 for persistent
  actions?: NotificationAction[];
  timestamp: Date;
  isPopup?: boolean; // For reminder popups
}

export interface NotificationAction {
  label: string;
  action: () => void;
  style?: 'primary' | 'secondary' | 'danger';
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private notifications$ = new BehaviorSubject<NotificationMessage[]>([]);
  private reminderQueue: NotificationMessage[] = [];
  private lastReminderTime: { [key: string]: number } = {};

  constructor() {
    // Start reminder system - check every 5 minutes
    this.startReminderSystem();
  }

  getNotifications(): Observable<NotificationMessage[]> {
    return this.notifications$.asObservable();
  }

  showNotification(notification: Omit<NotificationMessage, 'id' | 'timestamp'>): void {
    const newNotification: NotificationMessage = {
      ...notification,
      id: this.generateId(),
      timestamp: new Date()
    };

    const currentNotifications = this.notifications$.value;
    this.notifications$.next([...currentNotifications, newNotification]);

    // Auto-remove after duration (default 5 seconds)
    if (notification.duration !== 0) {
      const duration = notification.duration || 5000;
      setTimeout(() => {
        this.removeNotification(newNotification.id);
      }, duration);
    }
  }

  showSuccess(title: string, message: string, duration?: number): void {
    this.showNotification({
      type: 'success',
      title,
      message,
      duration
    });
  }

  showError(title: string, message: string, duration?: number): void {
    this.showNotification({
      type: 'error',
      title,
      message,
      duration: duration || 8000 // Errors stay longer
    });
  }

  showWarning(title: string, message: string, duration?: number): void {
    this.showNotification({
      type: 'warning',
      title,
      message,
      duration
    });
  }

  showInfo(title: string, message: string, duration?: number): void {
    this.showNotification({
      type: 'info',
      title,
      message,
      duration
    });
  }

  showReminder(title: string, message: string, actions?: NotificationAction[]): void {
    this.showNotification({
      type: 'reminder',
      title,
      message,
      duration: 0, // Reminders are persistent
      actions,
      isPopup: true
    });
  }

  removeNotification(id: string): void {
    const currentNotifications = this.notifications$.value;
    const filteredNotifications = currentNotifications.filter(n => n.id !== id);
    this.notifications$.next(filteredNotifications);
  }

  clearAll(): void {
    this.notifications$.next([]);
  }

  private generateId(): string {
    return Math.random().toString(36).substr(2, 9) + Date.now().toString(36);
  }

  private scheduleReminder(key: string, minutes: number): void {
    setTimeout(() => {
      // Reset the reminder time to allow showing again
      delete this.lastReminderTime[key];
    }, minutes * 60 * 1000);
  }

  private startReminderSystem(): void {
    // Check for reminders every 5 minutes
    interval(5 * 60 * 1000).subscribe(() => {
      // This will be triggered by the components that have access to real data
    });
  }
}