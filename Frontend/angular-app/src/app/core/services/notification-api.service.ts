import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ApiNotification {
  id: string;
  destinataireId: number;
  destinataireRole: string;
  type: string;
  message: string;
  lu: boolean;
  dateCreation: string;
}

export interface PagedNotifications {
  items: ApiNotification[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

@Injectable({ providedIn: 'root' })
export class NotificationApiService {
  private readonly baseUrl = `${environment.apiUrl}/notifications`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 20): Observable<PagedNotifications> {
    return this.http.get<PagedNotifications>(
      `${this.baseUrl}?page=${page}&pageSize=${pageSize}`
    );
  }

  markAsRead(id: string): Observable<ApiNotification> {
    return this.http.put<ApiNotification>(`${this.baseUrl}/${id}`, { lu: true });
  }
}
