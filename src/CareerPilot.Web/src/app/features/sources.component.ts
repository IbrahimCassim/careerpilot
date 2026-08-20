import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ApiService, CollectionSource, ScrapeRun } from '../core/api.service';

@Component({
  imports: [DatePipe, ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page"><header class="page-header"><div><h1>Collection sources</h1><p class="muted">Public search and career pages checked daily at 2:00 AM Brisbane time.</p></div><button mat-flat-button (click)="runNow()" [disabled]="running()"><mat-icon>sync</mat-icon>{{ running() ? 'Queued' : 'Run now' }}</button></header>
      <div class="source-grid"><form class="card form" [formGroup]="form" (ngSubmit)="save()"><h2>Add source</h2><mat-form-field><mat-label>Name</mat-label><input matInput formControlName="name" placeholder="SEEK — Brisbane .NET"></mat-form-field><mat-form-field><mat-label>Public search URL</mat-label><input matInput type="url" formControlName="searchUrl"></mat-form-field><div class="row"><mat-form-field><mat-label>Delay (ms)</mat-label><input matInput type="number" formControlName="requestDelayMs"></mat-form-field><mat-form-field><mat-label>Maximum pages</mat-label><input matInput type="number" formControlName="maximumPages"></mat-form-field></div><mat-checkbox formControlName="useBrowser">Requires JavaScript rendering</mat-checkbox><mat-checkbox formControlName="enabled">Enabled</mat-checkbox><p class="notice"><mat-icon>policy</mat-icon>CareerPilot stops at authentication, CAPTCHA, rate limits and access controls. It never attempts to bypass them.</p><button mat-flat-button type="submit" [disabled]="form.invalid">Save source</button></form>
        <section class="grid"><article class="card"><h2>Source health</h2>@for (source of sources(); track source.id) { <div class="source"><span class="health" [class.failed]="source.lastError"></span><div><strong>{{ source.name }}</strong><small>{{ source.kind }} · {{ source.lastSucceededAt ? ('Last success ' + (source.lastSucceededAt | date:'d MMM, h:mm a')) : 'Not run yet' }}</small>@if (source.lastError) { <p>{{ source.lastError }}</p> }</div><button mat-icon-button (click)="disable(source)" aria-label="Disable source"><mat-icon>pause_circle_outline</mat-icon></button></div> } @empty { <div class="empty">Add a public search or employer career page.</div> }</article>
          <article class="card"><h2>Recent runs</h2>@for (run of runs(); track run.id) { <div class="run"><span class="status">{{ run.status }}</span><div><strong>{{ run.collectionSource?.name || 'Collection' }}</strong><small>{{ run.startedAt | date:'d MMM, h:mm a' }} · {{ run.discoveredCount }} found, {{ run.addedCount }} new</small></div></div> } @empty { <div class="empty">No collection runs yet.</div> }</article>
        </section>
      </div>
    </section>
  `,
  styles: [`
    .source-grid { display: grid; grid-template-columns: 380px 1fr; gap: 18px; }.form { display: flex; flex-direction: column; align-self: start; }.form h2, article h2 { margin-top: 0; }.row { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }.notice { display: flex; gap: 8px; color: #7a5924; background: #fff7df; padding: 11px; border-radius: 8px; font-size: 12px; line-height: 1.4; }.notice mat-icon { flex: 0 0 auto; font-size: 18px; }.source, .run { display: grid; grid-template-columns: auto 1fr auto; gap: 12px; align-items: start; padding: 14px 0; border-top: 1px solid #edf0f5; }.source small, .run small { display: block; color: #68748a; margin-top: 4px; }.source p { color: #a33c26; font-size: 12px; margin: 6px 0 0; }.health { width: 9px; height: 9px; margin-top: 6px; background: #29a36a; border-radius: 99px; }.health.failed { background: #d05a3d; }.run { grid-template-columns: auto 1fr; } @media(max-width: 950px) { .source-grid { grid-template-columns: 1fr; } }
  `]
})
export class SourcesComponent {
  private readonly api = inject(ApiService); private readonly fb = inject(FormBuilder); readonly sources = signal<CollectionSource[]>([]); readonly runs = signal<ScrapeRun[]>([]); readonly running = signal(false);
  readonly form = this.fb.nonNullable.group({ name: ['', Validators.required], searchUrl: ['', [Validators.required, Validators.pattern(/^https?:\/\//)]], useBrowser: false, enabled: true, requestDelayMs: 1500, maximumPages: 2 });
  constructor() { this.load(); }
  load() { this.api.sources().subscribe(x => this.sources.set(x)); this.api.scrapeRuns().subscribe(x => this.runs.set(x)); }
  save() { if (this.form.invalid) return; this.api.saveSource(this.form.getRawValue()).subscribe(() => { this.form.reset({ name: '', searchUrl: '', useBrowser: false, enabled: true, requestDelayMs: 1500, maximumPages: 2 }); this.load(); }); }
  disable(source: CollectionSource) { this.api.disableSource(source.id).subscribe(() => this.load()); }
  runNow() { this.running.set(true); this.api.queueScrape().subscribe({ next: () => setTimeout(() => { this.running.set(false); this.load(); }, 2000), error: () => this.running.set(false) }); }
}
