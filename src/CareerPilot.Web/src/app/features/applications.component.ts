import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { ApiService, Application, ApplicationStatus } from '../core/api.service';

@Component({
  imports: [DatePipe, FormsModule, MatButtonModule, MatIconModule, MatSelectModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page"><header class="page-header"><div><h1>Applications</h1><p class="muted">A focused record of every application and outcome.</p></div></header>
      <div class="board">
        @for (stage of stages; track stage) {
          <section class="column"><header><span>{{ stage }}</span><strong>{{ inStage(stage).length }}</strong></header>
            @for (application of inStage(stage); track application.id) {
              <article class="card application"><small>{{ application.job.company }}</small><h2>{{ application.job.title }}</h2><p>{{ application.job.location }}</p><time>{{ application.updatedAt | date:'d MMM' }}</time>
                <mat-select aria-label="Application status" [value]="application.status" (selectionChange)="move(application, $event.value)">@for (target of allowedTargets(application.status); track target) { <mat-option [value]="target">Move to {{ target }}</mat-option> }</mat-select>
                <div class="documents">@for (document of application.documents; track document.id) { <a mat-button [href]="'/api/documents/' + document.id + '/download'"><mat-icon>download</mat-icon>{{ document.kind }} v{{ document.version }}</a> }</div>
              </article>
            } @empty { <div class="dropzone">No applications</div> }
          </section>
        }
      </div>
    </section>
  `,
  styles: [`
    .board { display: grid; grid-template-columns: repeat(6, minmax(230px, 1fr)); gap: 14px; overflow-x: auto; padding-bottom: 18px; }.column { min-width: 230px; }.column>header { display: flex; justify-content: space-between; padding: 0 5px 12px; text-transform: uppercase; font-size: 11px; font-weight: 800; letter-spacing: .08em; color: #68748a; }.column>header strong { background: #e6eaf0; border-radius: 99px; padding: 2px 7px; }
    .application { padding: 15px; margin-bottom: 10px; }.application small { color: #68748a; }.application h2 { font-size: 15px; margin: 6px 0; }.application p { color: #68748a; margin: 0 0 12px; }.application time { font-size: 11px; color: #8a94a5; }.application mat-select { margin-top: 14px; padding: 8px; border-radius: 7px; background: #f4f6f9; font-size: 12px; }.documents { display: grid; margin-top: 8px; }.dropzone { border: 1px dashed #cbd2de; border-radius: 12px; padding: 24px 10px; text-align: center; color: #929bad; font-size: 12px; }
  `]
})
export class ApplicationsComponent {
  private readonly api = inject(ApiService); readonly applications = signal<Application[]>([]);
  readonly stages: ApplicationStatus[] = ['Draft', 'Ready', 'Applied', 'Screening', 'Interview', 'Offer'];
  private readonly transitions: Record<ApplicationStatus, ApplicationStatus[]> = {
    Draft: ['Draft', 'Ready', 'Withdrawn'], Ready: ['Ready', 'Draft', 'Applied', 'Withdrawn'], Applied: ['Applied', 'Screening', 'Interview', 'Rejected', 'Withdrawn'],
    Screening: ['Screening', 'Interview', 'Offer', 'Rejected', 'Withdrawn'], Interview: ['Interview', 'Offer', 'Rejected', 'Withdrawn'], Offer: ['Offer', 'Withdrawn'], Rejected: ['Rejected'], Withdrawn: ['Withdrawn']
  };
  constructor() { this.load(); }
  load() { this.api.applications().subscribe(values => this.applications.set(values)); }
  inStage(stage: ApplicationStatus) { return this.applications().filter(x => x.status === stage); }
  allowedTargets(status: ApplicationStatus) { return this.transitions[status]; }
  move(application: Application, status: ApplicationStatus) { this.api.updateApplication(application.id, status).subscribe(() => this.load()); }
}
