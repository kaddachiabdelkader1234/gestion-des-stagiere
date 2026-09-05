import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuditService } from '../../../core/services/audit.service';
import {
  AuditEntry,
  AuditQueryParams,
  AuditPage,
  AUDIT_ACTION_LABELS,
  AUDIT_ENTITY_LABELS,
  auditActionBadgeClass
} from '../../../core/models/audit.model';

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AuditLogComponent implements OnInit {
  auditPage: AuditPage | null = null;
  loading = false;
  error: string | null = null;

  // Filters
  search = '';
  selectedAction = '';
  selectedEntityType = '';
  dateFrom = '';
  dateTo = '';
  currentPage = 1;
  pageSize = 25;

  availableActions = Object.entries(AUDIT_ACTION_LABELS);
  availableEntityTypes = Object.entries(AUDIT_ENTITY_LABELS);

  constructor(private auditService: AuditService) {}

  ngOnInit(): void {
    this.loadAuditLog();
  }

  loadAuditLog(): void {
    this.loading = true;
    this.error = null;

    const params: AuditQueryParams = {
      page: this.currentPage,
      pageSize: this.pageSize
    };

    if (this.search.trim()) params.search = this.search.trim();
    if (this.selectedAction) params.action = this.selectedAction;
    if (this.selectedEntityType) params.entityType = this.selectedEntityType;
    if (this.dateFrom) params.dateFrom = this.dateFrom;
    if (this.dateTo) params.dateTo = this.dateTo;

    this.auditService.getAuditLog(params).subscribe({
      next: (data) => {
        this.auditPage = data;
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message || 'Erreur lors du chargement du journal d\'audit.';
        this.loading = false;
      }
    });
  }

  onFilterChange(): void {
    this.currentPage = 1;
    this.loadAuditLog();
  }

  onPageChange(page: number): void {
    this.currentPage = page;
    this.loadAuditLog();
  }

  clearFilters(): void {
    this.search = '';
    this.selectedAction = '';
    this.selectedEntityType = '';
    this.dateFrom = '';
    this.dateTo = '';
    this.currentPage = 1;
    this.loadAuditLog();
  }

  getActionLabel(action: string): string {
    return AUDIT_ACTION_LABELS[action] || action;
  }

  getEntityLabel(entityType: string): string {
    return AUDIT_ENTITY_LABELS[entityType] || entityType;
  }

  getActionBadgeClass(action: string): string {
    return auditActionBadgeClass(action);
  }

  formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    return d.toLocaleDateString('fr-FR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  get pages(): number[] {
    if (!this.auditPage) return [];
    const total = this.auditPage.totalPages;
    const current = this.currentPage;
    const pages: number[] = [];
    const start = Math.max(1, current - 2);
    const end = Math.min(total, current + 2);
    for (let i = start; i <= end; i++) pages.push(i);
    return pages;
  }
}
