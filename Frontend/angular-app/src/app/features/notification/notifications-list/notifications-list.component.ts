import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationApiService, ApiNotification } from '../../../core/services/notification-api.service';

@Component({
  selector: 'app-notifications-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './notifications-list.component.html'
})
export class NotificationsListComponent implements OnInit {
  notifications: ApiNotification[] = [];
  isLoading = true;
  errorMessage = '';

  constructor(private notificationApi: NotificationApiService) {}

  ngOnInit(): void {
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.isLoading = true;
    this.notificationApi.getAll().subscribe({
      next: (result) => {
        this.notifications = result.items;
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = 'Erreur lors du chargement des notifications.';
        this.isLoading = false;
      }
    });
  }

  markAsRead(notification: ApiNotification): void {
    if (notification.lu) return;
    this.notificationApi.markAsRead(notification.id).subscribe({
      next: () => { notification.lu = true; },
      error: () => {}
    });
  }

  getTypeLabel(type: string): string {
    switch (type) {
      case 'CandidatureAcceptee': return 'Candidature acceptée';
      case 'CandidatureRejetee': return 'Candidature rejetée';
      case 'ConventionGeneree': return 'Convention générée';
      case 'EvaluationSoumise': return 'Évaluation enregistrée';
      case 'RappelDelai': return 'Rappel';
      default: return type;
    }
  }

  getTypeIcon(type: string): string {
    switch (type) {
      case 'CandidatureAcceptee': return 'check_circle';
      case 'CandidatureRejetee': return 'cancel';
      case 'ConventionGeneree': return 'description';
      case 'EvaluationSoumise': return 'grading';
      case 'RappelDelai': return 'schedule';
      default: return 'notifications';
    }
  }

  getTypeColor(type: string): string {
    switch (type) {
      case 'CandidatureAcceptee': return 'text-green-600 bg-green-50';
      case 'CandidatureRejetee': return 'text-red-600 bg-red-50';
      case 'ConventionGeneree': return 'text-blue-600 bg-blue-50';
      case 'EvaluationSoumise': return 'text-purple-600 bg-purple-50';
      case 'RappelDelai': return 'text-orange-600 bg-orange-50';
      default: return 'text-gray-600 bg-gray-50';
    }
  }
}
