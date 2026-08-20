import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { ApiService, Job, JobReviewStatus, matchExplanation } from '../core/api.service';

@Component({
  imports: [DatePipe, ReactiveFormsModule, MatButtonModule, MatIconModule, MatInputModule, MatFormFieldModule, MatSelectModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page"><header class="page-header"><div><h1>Job matches</h1><p class="muted">Transparent scores, newest opportunities first.</p></div></header>
      <div class="toolbar card">
        <mat-form-field subscriptSizing="dynamic"><mat-label>Search</mat-label><input matInput [formControl]="search" placeholder="Title, company or skill"><mat-icon matSuffix>search</mat-icon></mat-form-field>
        <mat-form-field subscriptSizing="dynamic"><mat-label>Status</mat-label><mat-select [formControl]="status"><mat-option value="">All</mat-option>@for (item of statuses; track item) { <mat-option [value]="item">{{ item }}</mat-option> }</mat-select></mat-form-field>
        <mat-form-field subscriptSizing="dynamic"><mat-label>Minimum score</mat-label><mat-select [formControl]="minimumScore"><mat-option [value]="0">Any</mat-option><mat-option [value]="45">45+</mat-option><mat-option [value]="65">65+</mat-option><mat-option [value]="80">80+</mat-option></mat-select></mat-form-field>
      </div>
      <div class="jobs">
        @for (job of jobs(); track job.id) {
          <article class="card job" [class.selected]="selected()?.id === job.id">
            <button class="job-main" (click)="selected.set(job)">
              <span class="score" [class.low]="job.matchScore < 65">{{ job.matchScore | number:'1.0-0' }}</span>
              <span class="summary"><strong>{{ job.title }}</strong><span>{{ job.company }} · {{ job.location || 'Location not listed' }}</span><small>{{ job.postedAt ? (job.postedAt | date:'d MMM y') : 'Posting date unavailable' }}</small></span>
              <span class="status">{{ job.reviewStatus }}</span>
            </button>
            <div class="actions"><button mat-button (click)="setStatus(job, 'Saved')"><mat-icon>bookmark_border</mat-icon> Save</button><button mat-button (click)="startApplication(job)"><mat-icon>description</mat-icon> Prepare</button><a mat-button [href]="job.canonicalUrl" target="_blank" rel="noopener"><mat-icon>open_in_new</mat-icon> Open</a><button mat-icon-button aria-label="Dismiss" (click)="setStatus(job, 'Dismissed')"><mat-icon>close</mat-icon></button></div>
          </article>
        } @empty { <div class="card empty">No jobs match these filters. Add a source or adjust your preferences.</div> }
      </div>
      @if (selected(); as job) {
        <aside class="detail card"><button mat-icon-button class="close" aria-label="Close detail" (click)="selected.set(null)"><mat-icon>close</mat-icon></button><span class="score">{{ job.matchScore | number:'1.0-0' }}</span><h2>{{ job.title }}</h2><p class="muted">{{ job.company }} · {{ job.location }}</p>
          <h3>Why it matched</h3>@for (factor of explanation(job).factors; track factor.name) { <div class="factor"><strong>{{ factor.name }}</strong><span [class.negative]="factor.points < 0">{{ factor.points > 0 ? '+' : '' }}{{ factor.points }}</span><p>{{ factor.explanation }}</p></div> }
          <h3>Description</h3><p class="description">{{ job.description }}</p>
        </aside>
      }
    </section>
  `,
  styles: [`
    .toolbar { margin-bottom: 16px; padding: 14px; } .toolbar mat-form-field:first-child { flex: 1; min-width: 260px; } .jobs { display: grid; gap: 10px; max-width: 980px; }
    .job { padding: 0; overflow: hidden; }.job.selected { border-color: #3a7d65; }.job-main { width: 100%; border: 0; background: transparent; padding: 17px 18px; display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 16px; text-align: left; cursor: pointer; }
    .summary { display: grid; gap: 4px; }.summary strong { font-size: 16px; }.summary span, .summary small { color: #68748a; }.actions { display: flex; align-items: center; border-top: 1px solid #edf0f5; padding: 4px 8px; justify-content: flex-end; }
    .detail { position: fixed; right: 22px; top: 22px; bottom: 22px; width: min(480px, calc(100vw - 44px)); z-index: 20; overflow: auto; box-shadow: 0 20px 70px rgb(0 0 0 / 20%); }.close { float: right; }.detail h2 { margin-bottom: 4px; }.detail h3 { margin: 26px 0 12px; }.factor { display: grid; grid-template-columns: 1fr auto; border-top: 1px solid #edf0f5; padding: 11px 0; }.factor span { color: #16724a; font-weight: 800; }.factor span.negative { color: #b04422; }.factor p { grid-column: 1 / -1; margin: 4px 0 0; color: #68748a; }.description { white-space: pre-line; line-height: 1.55; }
  `]
})
export class JobsComponent {
  private readonly api = inject(ApiService); readonly jobs = signal<Job[]>([]); readonly selected = signal<Job | null>(null);
  readonly search = new FormControl('', { nonNullable: true }); readonly status = new FormControl('', { nonNullable: true }); readonly minimumScore = new FormControl(45, { nonNullable: true });
  readonly statuses: JobReviewStatus[] = ['New', 'Saved', 'Dismissed', 'Applied', 'Closed']; readonly explanation = matchExplanation;
  constructor() { this.load(); this.search.valueChanges.pipe(debounceTime(250), distinctUntilChanged()).subscribe(() => this.load()); this.status.valueChanges.subscribe(() => this.load()); this.minimumScore.valueChanges.subscribe(() => this.load()); }
  load() { this.api.jobs({ search: this.search.value, status: this.status.value, minimumScore: this.minimumScore.value }).subscribe(values => this.jobs.set(values)); }
  setStatus(job: Job, status: JobReviewStatus) { this.api.setJobStatus(job.id, status).subscribe(() => this.load()); }
  startApplication(job: Job) { this.api.createApplication(job.id).subscribe(() => this.setStatus(job, 'Saved')); }
}
