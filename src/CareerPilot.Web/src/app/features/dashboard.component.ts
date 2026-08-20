import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService, Dashboard } from '../core/api.service';

@Component({
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <header class="page-header"><div><p class="eyebrow">YOUR SEARCH, AT A GLANCE</p><h1>Good morning</h1><p class="muted">Review the roles most likely to deserve your time.</p></div><a mat-flat-button routerLink="/jobs"><mat-icon>work_outline</mat-icon> Review matches</a></header>
      @if (data(); as dashboard) {
        <div class="metrics">
          <article class="card metric"><span>New roles</span><strong>{{ dashboard.newJobs }}</strong><small>Awaiting review</small></article>
          <article class="card metric"><span>Strong matches</span><strong>{{ dashboard.strongMatches }}</strong><small>Score 65 or higher</small></article>
          <article class="card metric"><span>Active applications</span><strong>{{ dashboard.activeApplications }}</strong><small>Across your pipeline</small></article>
          <article class="card metric"><span>Last collection</span><strong class="date">{{ dashboard.lastRun?.startedAt ? (dashboard.lastRun?.startedAt | date:'d MMM, h:mm a') : 'Not run' }}</strong><small>{{ dashboard.lastRun?.status || 'Add a source to begin' }}</small></article>
        </div>
        <div class="content">
          <article class="card">
            <div class="section-title"><div><h2>Application pipeline</h2><p>Current outcomes by stage</p></div><a mat-button routerLink="/applications">Open board</a></div>
            <div class="pipeline">
              @for (stage of stages; track stage) {
                <div><span>{{ stage }}</span><strong>{{ count(dashboard, stage) }}</strong></div>
              }
            </div>
          </article>
          <article class="card next"><div class="icon"><mat-icon>auto_awesome</mat-icon></div><h2>Make matching smarter</h2><p>Add measurable achievements and the technologies you used. CareerPilot only drafts claims backed by approved evidence.</p><a mat-stroked-button routerLink="/profile">Improve profile</a></article>
        </div>
      } @else { <div class="card empty">Loading your workspace…</div> }
    </section>
  `,
  styles: [`
    .eyebrow { margin: 0 0 6px; color: #526176; font-size: 11px; font-weight: 800; letter-spacing: .12em; }
    .metrics { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; }
    .metric { display: grid; gap: 8px; }
    .metric span, .metric small { color: #68748a; } .metric strong { font-size: 34px; letter-spacing: -.04em; } .metric strong.date { font-size: 20px; }
    .content { margin-top: 18px; display: grid; grid-template-columns: 2fr 1fr; gap: 18px; }
    .section-title { display: flex; justify-content: space-between; align-items: flex-start; } h2 { margin: 0 0 5px; font-size: 18px; } p { margin: 0; }
    .pipeline { display: grid; grid-template-columns: repeat(6, 1fr); gap: 8px; margin-top: 26px; }
    .pipeline div { background: #f6f8fb; border-radius: 10px; padding: 14px 10px; display: grid; gap: 10px; text-transform: capitalize; color: #68748a; font-size: 12px; }
    .pipeline strong { color: #172033; font-size: 22px; }.next { background: #102034; color: #fff; }.next p { color: #aebbd0; line-height: 1.6; margin-bottom: 20px; }.next .icon { display: grid; place-items: center; width: 42px; height: 42px; background: #8be0bd; color: #102034; border-radius: 12px; margin-bottom: 18px; }
    @media(max-width: 1100px) { .metrics { grid-template-columns: repeat(2, 1fr); } .content { grid-template-columns: 1fr; } } @media(max-width: 600px) { .metrics { grid-template-columns: 1fr; } .pipeline { grid-template-columns: repeat(3, 1fr); } }
  `]
})
export class DashboardComponent {
  private readonly api = inject(ApiService); readonly data = signal<Dashboard | null>(null);
  readonly stages = ['Draft', 'Ready', 'Applied', 'Screening', 'Interview', 'Offer'];
  constructor() { this.api.dashboard().subscribe(value => this.data.set(value)); }
  count(data: Dashboard, stage: string) { return data.applicationStatuses.find(x => x.status === stage)?.count ?? 0; }
}
