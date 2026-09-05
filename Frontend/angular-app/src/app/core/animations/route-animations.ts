import { trigger, transition, style, animate, query, group } from '@angular/animations';

/**
 * Subtle route transition: a quick fade + slight upward slide.
 * Applied to the router outlet so dashboard views transition smoothly.
 */
export const routeFadeAnimation = trigger('routeAnimation', [
  transition('* <=> *', [
    // Animate both the entering and leaving pages simultaneously.
    query(':enter', [
      style({ opacity: 0, transform: 'translateY(8px)' })
    ], { optional: true }),
    group([
      query(':leave', [
        animate('150ms ease-out', style({ opacity: 0 }))
      ], { optional: true }),
      query(':enter', [
        animate('250ms 100ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ], { optional: true })
    ])
  ])
]);
