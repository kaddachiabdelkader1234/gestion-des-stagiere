import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, forkJoin } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { toApiError } from '../http/api-error';
import { AuditEntry, AuditPage, AuditQueryParams } from '../models/audit.model';

@Injectable({ providedIn: 'root' })
export class AuditService {
  /** One endpoint per microservice — the gateway routes each under its own prefix. */
  private readonly endpoints = [
    `${environment.apiUrl}/audit`,
    `${environment.apiUrl}/conventions/audit`,
    `${environment.apiUrl}/evaluations/audit`
  ];

  constructor(private http: HttpClient) {}

  /**
   * Fetches audit entries from all three services in parallel, merges them into a single
   * page sorted by timestamp (newest first), and applies client-side pagination.
   *
   * For the demo dataset (< 1000 total entries across all services) this is perfectly
   * adequate. A production system would use an event-sourced audit store or a materialised
   * view, but that is explicitly out of scope here.
   */
  getAuditLog(params: AuditQueryParams = {}): Observable<AuditPage> {
    const httpParams = this.buildParams(params);

    // Request a large page from each service so we can merge locally.
    // The combined total will rarely exceed a few hundred entries in a demo.
    const fetchParams = httpParams.set('pageSize', '500');

    const requests = this.endpoints.map(url =>
      this.http.get<AuditPage>(url, { params: fetchParams }).pipe(
        catchError(() => { return [{ items: [] as AuditEntry[], totalCount: 0, page: 1, pageSize: 500, totalPages: 0 } as AuditPage]; })
      )
    );

    return forkJoin(requests).pipe(
      map(pages => this.mergePages(pages, params))
    );
  }

  private buildParams(params: AuditQueryParams): HttpParams {
    let httpParams = new HttpParams();
    if (params.action) httpParams = httpParams.set('action', params.action);
    if (params.entityType) httpParams = httpParams.set('entityType', params.entityType);
    if (params.userId != null) httpParams = httpParams.set('userId', params.userId.toString());
    if (params.dateFrom) httpParams = httpParams.set('dateFrom', params.dateFrom);
    if (params.dateTo) httpParams = httpParams.set('dateTo', params.dateTo);
    if (params.search) httpParams = httpParams.set('search', params.search);
    return httpParams;
  }

  private mergePages(pages: AuditPage[], params: AuditQueryParams): AuditPage {
    // Flatten all items from all services.
    const allItems = pages.flatMap(p => p.items);

    // Sort by timestamp descending (newest first), break ties by id descending.
    allItems.sort((a, b) => {
      const timeDiff = new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime();
      return timeDiff !== 0 ? timeDiff : b.id - a.id;
    });

    const totalCount = allItems.length;
    const page = params.page ?? 1;
    const pageSize = params.pageSize ?? 25;
    const totalPages = Math.ceil(totalCount / pageSize);
    const start = (page - 1) * pageSize;
    const items = allItems.slice(start, start + pageSize);

    return { items, totalCount, page, pageSize, totalPages };
  }
}
