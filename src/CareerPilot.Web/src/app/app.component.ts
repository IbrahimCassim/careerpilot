import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'cp-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatIconModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="shell">
      <aside>
        <a class="brand" routerLink="/"><span class="mark">CP</span><span>CareerPilot</span></a>
        <nav aria-label="Primary navigation">
          @for (item of navigation; track item.path) {
            <a [routerLink]="item.path" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: item.path === '/' }">
              <mat-icon>{{ item.icon }}</mat-icon><span>{{ item.label }}</span>
            </a>
          }
        </nav>
        <div class="privacy"><mat-icon>verified_user</mat-icon><span>Private, single-user workspace</span></div>
      </aside>
      <main><router-outlet /></main>
    </div>
  `,
  styles: [`
    .shell { min-height: 100%; display: grid; grid-template-columns: 232px 1fr; }
    aside { position: sticky; top: 0; height: 100vh; padding: 24px 16px; background: #0b1220; color: #d7deea; display: flex; flex-direction: column; }
    .brand { display: flex; align-items: center; gap: 10px; color: #fff; text-decoration: none; font-size: 18px; font-weight: 800; padding: 0 10px 24px; }
    .mark { display: grid; place-items: center; width: 34px; height: 34px; border-radius: 10px; color: #0b1220; background: #8be0bd; font-size: 12px; }
    nav { display: grid; gap: 5px; }
    nav a { display: flex; align-items: center; gap: 12px; padding: 11px 12px; color: #aeb9ca; border-radius: 9px; text-decoration: none; }
    nav a:hover, nav a.active { color: #fff; background: #1b2639; }
    nav mat-icon { font-size: 20px; width: 20px; height: 20px; }
    .privacy { margin-top: auto; display: flex; gap: 9px; align-items: center; padding: 12px; color: #8795aa; font-size: 12px; }
    .privacy mat-icon { font-size: 18px; width: 18px; height: 18px; }
    main { min-width: 0; }
    @media (max-width: 800px) { .shell { grid-template-columns: 1fr; padding-bottom: 66px; } aside { z-index: 10; position: fixed; top: auto; bottom: 0; width: 100%; height: 66px; padding: 7px 10px; } .brand, .privacy { display: none; } nav { display: flex; justify-content: space-around; } nav a { flex-direction: column; gap: 1px; padding: 5px 9px; font-size: 10px; } }
  `]
})
export class AppComponent {
  readonly navigation = [
    { path: '/', label: 'Overview', icon: 'space_dashboard' },
    { path: '/jobs', label: 'Job matches', icon: 'work_outline' },
    { path: '/applications', label: 'Applications', icon: 'view_kanban' },
    { path: '/profile', label: 'Career profile', icon: 'person_outline' },
    { path: '/sources', label: 'Sources', icon: 'travel_explore' }
  ];
}
