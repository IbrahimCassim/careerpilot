import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ApiService, CareerEvidence, EvidenceKind, Preferences } from '../core/api.service';

@Component({
  imports: [ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page"><header class="page-header"><div><h1>Career profile</h1><p class="muted">The approved evidence CareerPilot may use in applications.</p></div></header>
      <div class="profile-grid">
        <div class="grid">
          <form class="card form" [formGroup]="evidenceForm" (ngSubmit)="saveEvidence()"><div class="section-title"><h2>Add career evidence</h2><span class="status">Evidence only</span></div>
            <mat-form-field><mat-label>Type</mat-label><mat-select formControlName="kind">@for (kind of evidenceKinds; track kind) { <mat-option [value]="kind">{{ kind }}</mat-option> }</mat-select></mat-form-field>
            <mat-form-field><mat-label>Title</mat-label><input matInput formControlName="title" placeholder="Reduced deployment time by 40%"></mat-form-field>
            <mat-form-field><mat-label>Organisation</mat-label><input matInput formControlName="organisation"></mat-form-field>
            <mat-form-field><mat-label>Description</mat-label><textarea matInput rows="4" formControlName="description" placeholder="What you did, how you did it, and the measurable result"></textarea></mat-form-field>
            <mat-form-field><mat-label>Skills (comma separated)</mat-label><input matInput formControlName="skillsCsv" placeholder="C#, Azure, SQL"></mat-form-field>
            <mat-checkbox formControlName="approvedForApplications">Approved for resumes and cover letters</mat-checkbox><button mat-flat-button type="submit" [disabled]="evidenceForm.invalid">Save evidence</button>
          </form>
          <form class="card form" [formGroup]="preferencesForm" (ngSubmit)="savePreferences()"><h2>Matching preferences</h2>
            <mat-form-field><mat-label>Target titles</mat-label><input matInput formControlName="targetTitlesCsv"></mat-form-field>
            <mat-form-field><mat-label>Locations</mat-label><input matInput formControlName="locationsCsv"></mat-form-field>
            <mat-form-field><mat-label>Positive keywords</mat-label><input matInput formControlName="positiveKeywordsCsv"></mat-form-field>
            <mat-form-field><mat-label>Negative keywords</mat-label><input matInput formControlName="negativeKeywordsCsv"></mat-form-field>
            <mat-form-field><mat-label>Immediate knockout terms</mat-label><input matInput formControlName="knockoutKeywordsCsv"></mat-form-field>
            <div class="row"><mat-form-field><mat-label>Maximum age (days)</mat-label><input matInput type="number" formControlName="maxAgeDays"></mat-form-field><mat-form-field><mat-label>Minimum score</mat-label><input matInput type="number" formControlName="minimumScore"></mat-form-field></div>
            <button mat-flat-button type="submit">Save and rescore jobs</button>
          </form>
        </div>
        <section class="card evidence-list"><h2>Approved inventory</h2><p class="muted">{{ evidence().length }} evidence items</p>
          @for (item of evidence(); track item.id) { <article><div><span class="status">{{ item.kind }}</span><h3>{{ item.title }}</h3><p>{{ item.description }}</p><small>{{ item.organisation }} · {{ item.skillsCsv }}</small></div><button mat-icon-button aria-label="Delete evidence" (click)="remove(item)"><mat-icon>delete_outline</mat-icon></button></article> }
        </section>
      </div>
    </section>
  `,
  styles: [`
    .profile-grid { display: grid; grid-template-columns: minmax(360px, 1fr) minmax(360px, 1.3fr); gap: 18px; }.form { display: grid; gap: 4px; }.form h2 { margin-top: 0; }.form button[type=submit] { justify-self: start; margin-top: 12px; }.section-title { display: flex; justify-content: space-between; align-items: center; }.row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }.evidence-list h2 { margin: 0; }.evidence-list article { display: grid; grid-template-columns: 1fr auto; gap: 12px; border-top: 1px solid #edf0f5; padding: 18px 0; }.evidence-list h3 { margin: 9px 0 5px; font-size: 16px; }.evidence-list p { margin: 0 0 8px; line-height: 1.5; }.evidence-list small { color: #68748a; } @media(max-width: 950px) { .profile-grid { grid-template-columns: 1fr; } }
  `]
})
export class ProfileComponent {
  private readonly api = inject(ApiService); private readonly fb = inject(FormBuilder); readonly evidence = signal<CareerEvidence[]>([]);
  readonly evidenceKinds: EvidenceKind[] = ['Role', 'Achievement', 'Project', 'Skill', 'Education', 'Certification', 'Portfolio'];
  readonly evidenceForm = this.fb.nonNullable.group({ kind: 'Achievement' as EvidenceKind, title: ['', Validators.required], organisation: '', description: ['', Validators.required], skillsCsv: '', approvedForApplications: true });
  readonly preferencesForm = this.fb.nonNullable.group({ targetTitlesCsv: 'Software Engineer,Developer', titleSynonymsJson: '{}', locationsCsv: 'Australia', workMode: 'Any', positiveKeywordsCsv: '', negativeKeywordsCsv: '', knockoutKeywordsCsv: '', maxAgeDays: 30, minimumScore: 45 });
  constructor() { this.loadEvidence(); this.api.preferences().subscribe(value => this.preferencesForm.patchValue(value)); }
  loadEvidence() { this.api.evidence().subscribe(values => this.evidence.set(values)); }
  saveEvidence() { if (this.evidenceForm.invalid) return; this.api.saveEvidence(this.evidenceForm.getRawValue()).subscribe(() => { this.evidenceForm.reset({ kind: 'Achievement', title: '', organisation: '', description: '', skillsCsv: '', approvedForApplications: true }); this.loadEvidence(); }); }
  remove(item: CareerEvidence) { this.api.deleteEvidence(item.id).subscribe(() => this.loadEvidence()); }
  savePreferences() { this.api.savePreferences(this.preferencesForm.getRawValue() as Preferences).subscribe(); }
}
