import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export type JobReviewStatus = 'New' | 'Saved' | 'Dismissed' | 'Applied' | 'Closed';
export type ApplicationStatus = 'Draft' | 'Ready' | 'Applied' | 'Screening' | 'Interview' | 'Offer' | 'Rejected' | 'Withdrawn';
export type EvidenceKind = 'Role' | 'Achievement' | 'Project' | 'Skill' | 'Education' | 'Certification' | 'Portfolio';

export interface SourceListing { id: string; sourceUrl: string; externalId: string; }
export interface Job {
  id: string; title: string; company: string; location: string; description: string; canonicalUrl: string;
  postedAt?: string; reviewStatus: JobReviewStatus; matchScore: number; matchExplanationJson: string; sourceListings: SourceListing[];
}
export interface MatchFactor { name: string; points: number; explanation: string; }
export interface MatchExplanation { score: number; isKnockedOut: boolean; factors: MatchFactor[]; missingRequirements: string[]; }
export interface CareerEvidence {
  id: string; kind: EvidenceKind; title: string; organisation: string; description: string; skillsCsv: string; approvedForApplications: boolean;
}
export interface Preferences {
  targetTitlesCsv: string; titleSynonymsJson: string; locationsCsv: string; workMode: string; positiveKeywordsCsv: string;
  negativeKeywordsCsv: string; knockoutKeywordsCsv: string; maxAgeDays: number; minimumScore: number;
}
export interface DocumentVersion { id: string; kind: string; version: number; fileName: string; createdAt: string; }
export interface Application { id: string; jobId: string; job: Job; status: ApplicationStatus; notes: string; updatedAt: string; documents: DocumentVersion[]; }
export interface CollectionSource {
  id: string; name: string; kind: string; searchUrl: string; useBrowser: boolean; enabled: boolean; requestDelayMs: number;
  maximumPages: number; lastSucceededAt?: string; lastError: string;
}
export interface ScrapeRun { id: string; status: string; startedAt: string; completedAt?: string; discoveredCount: number; addedCount: number; error: string; collectionSource?: CollectionSource; }
export interface Dashboard { newJobs: number; strongMatches: number; activeApplications: number; lastRun?: ScrapeRun; applicationStatuses: { status: ApplicationStatus; count: number }[]; }

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  dashboard() { return this.http.get<Dashboard>('/api/dashboard'); }
  jobs(filters: { search?: string; status?: string; minimumScore?: number } = {}) {
    let params = new HttpParams(); Object.entries(filters).forEach(([key, value]) => { if (value !== undefined && value !== '') params = params.set(key, String(value)); });
    return this.http.get<Job[]>('/api/jobs', { params });
  }
  setJobStatus(id: string, status: JobReviewStatus) { return this.http.patch<Job>(`/api/jobs/${id}/status`, { status }); }
  importJob(value: object) { return this.http.post('/api/jobs/import', value); }
  evidence() { return this.http.get<CareerEvidence[]>('/api/evidence'); }
  saveEvidence(value: Partial<CareerEvidence>) { return this.http.post<CareerEvidence>('/api/evidence', value); }
  deleteEvidence(id: string) { return this.http.delete(`/api/evidence/${id}`); }
  preferences() { return this.http.get<Preferences>('/api/preferences'); }
  savePreferences(value: Preferences) { return this.http.put<Preferences>('/api/preferences', value); }
  applications() { return this.http.get<Application[]>('/api/applications'); }
  createApplication(jobId: string) { return this.http.post<Application>('/api/applications', { jobId }); }
  updateApplication(id: string, status: ApplicationStatus, notes?: string) { return this.http.patch<Application>(`/api/applications/${id}`, { status, notes }); }
  createDocuments(id: string, value: object) { return this.http.post<DocumentVersion[]>(`/api/applications/${id}/documents`, value); }
  sources() { return this.http.get<CollectionSource[]>('/api/sources'); }
  saveSource(value: object) { return this.http.post<CollectionSource>('/api/sources', value); }
  disableSource(id: string) { return this.http.delete(`/api/sources/${id}`); }
  scrapeRuns() { return this.http.get<ScrapeRun[]>('/api/scrapes'); }
  queueScrape() { return this.http.post('/api/scrapes/run', {}); }
}

export function matchExplanation(job: Job): MatchExplanation {
  try { return JSON.parse(job.matchExplanationJson) as MatchExplanation; }
  catch { return { score: job.matchScore, isKnockedOut: false, factors: [], missingRequirements: [] }; }
}
