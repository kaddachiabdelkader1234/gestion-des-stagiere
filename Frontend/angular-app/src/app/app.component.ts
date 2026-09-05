import { Component, OnDestroy } from '@angular/core';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { HeaderComponent } from './shared/header/header.component';
import { FooterComponent } from './shared/footer/footer.component';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs/operators';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, HeaderComponent, FooterComponent, CommonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnDestroy {
  title = 'SMARTEK';
  showHeaderFooter = true;
  private routerEventsSubscription: Subscription;

  constructor(private router: Router) {
    this.routerEventsSubscription = this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe((event: any) => {
      // Hide header and footer on dashboard and auth pages
      this.showHeaderFooter = !event.url.includes('/dashboard') && 
                              !event.url.includes('/auth/');
    });
  }

  ngOnDestroy(): void {
    this.routerEventsSubscription?.unsubscribe();
  }
}
