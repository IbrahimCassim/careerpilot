import { Routes } from '@angular/router';
import { DashboardComponent } from './features/dashboard.component';
import { JobsComponent } from './features/jobs.component';
import { ApplicationsComponent } from './features/applications.component';
import { ProfileComponent } from './features/profile.component';
import { SourcesComponent } from './features/sources.component';

export const routes: Routes = [
  { path: '', component: DashboardComponent, title: 'Dashboard · CareerPilot' },
  { path: 'jobs', component: JobsComponent, title: 'Job matches · CareerPilot' },
  { path: 'applications', component: ApplicationsComponent, title: 'Applications · CareerPilot' },
  { path: 'profile', component: ProfileComponent, title: 'Career profile · CareerPilot' },
  { path: 'sources', component: SourcesComponent, title: 'Sources · CareerPilot' },
  { path: '**', redirectTo: '' }
];
