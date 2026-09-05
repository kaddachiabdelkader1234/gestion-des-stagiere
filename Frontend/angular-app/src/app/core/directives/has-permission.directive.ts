import { Directive, Input, TemplateRef, ViewContainerRef, OnInit, OnDestroy } from '@angular/core';
import { PermissionService } from '../services/permission.service';
import { Permission } from '../enums/permission.enum';
import { Role } from '../enums/role.enum';

@Directive({
  selector: '[appHasPermission]',
  standalone: true
})
export class HasPermissionDirective implements OnInit, OnDestroy {
  private permissions: Permission[] = [];
  private roles: Role[] = [];
  private requireAll = false;

  constructor(
    private templateRef: TemplateRef<any>,
    private viewContainer: ViewContainerRef,
    private permissionService: PermissionService
  ) {}

  @Input()
  set appHasPermission(permissions: Permission | Permission[]) {
    this.permissions = Array.isArray(permissions) ? permissions : [permissions];
    this.updateView();
  }

  @Input()
  set appHasPermissionRoles(roles: Role | Role[]) {
    this.roles = Array.isArray(roles) ? roles : [roles];
    this.updateView();
  }

  @Input()
  set appHasPermissionRequireAll(requireAll: boolean) {
    this.requireAll = requireAll;
    this.updateView();
  }

  ngOnInit() {
    this.updateView();
  }

  ngOnDestroy() {
    this.viewContainer.clear();
  }

  private updateView() {
    this.viewContainer.clear();

    let hasAccess = false;

    // Vérifier les rôles d'abord si spécifiés
    if (this.roles.length > 0) {
      hasAccess = this.permissionService.hasAnyRole(this.roles);
      if (!hasAccess) {
        return; // Pas besoin de vérifier les permissions si le rôle ne correspond pas
      }
    }

    // Vérifier les permissions
    if (this.permissions.length > 0) {
      hasAccess = this.requireAll
        ? this.permissionService.hasAllPermissions(this.permissions)
        : this.permissionService.hasAnyPermission(this.permissions);
    } else if (this.roles.length === 0) {
      // Si ni permissions ni rôles ne sont spécifiés, afficher par défaut
      hasAccess = true;
    }

    if (hasAccess) {
      this.viewContainer.createEmbeddedView(this.templateRef);
    }
  }
}
