import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import { AuthService } from '../../core/auth.service';

export type QuizStatus = 'Draft' | 'Published' | 'Archived';
export type QuizQuestionType = 'SingleChoice' | 'MultipleSelect' | 'TrueFalse' | 'TypeAnswer' | 'FillInTheBlanks' | 'Numeric' | 'ImageBased' | 'AudioBased' | 'VideoBased' | 'Matching' | 'Ordering' | 'OpenEnded' | 'Poll';

export interface QuizOption { id?: string; text: string; isCorrect: boolean; mediaUrl?: string | null; }
export interface QuizQuestion {
  id?: string;
  type: QuizQuestionType;
  title: string;
  description?: string | null;
  explanation?: string | null;
  points: number;
  negativePoints: number;
  answerConfigurationJson?: string | null;
  timeLimitSeconds?: number | null;
  difficulty: 'Easy' | 'Medium' | 'Hard';
  randomizeOptions: boolean;
  isRequired: boolean;
  options: QuizOption[];
}
export interface QuizSettings {
  randomizeQuestionOrder: boolean; randomizeAnswerOptions: boolean;
  showCorrectAnswersImmediately: boolean; showCorrectAnswersAtEnd: boolean;
  showExplanations: boolean; allowRetries: boolean; maximumAttempts?: number | null;
  passingPercentage?: number | null; timeLimitSeconds?: number | null;
  perQuestionTimeLimitSeconds?: number | null; enableLeaderboard: boolean;
  requireParticipantName: boolean; allowAnonymousParticipation: boolean;
  displayParticipantResults: boolean; allowAnswerReview: boolean; preventMultipleSubmissions: boolean;
}
export interface QuizSummary { id: string; title: string; description: string | null; status: QuizStatus; visibility: string; questionCount: number; updatedAtUtc: string; category: string | null; estimatedDurationMinutes: number | null; }
export interface QuizFolder { id: string; name: string; parentFolderId: string | null; }
export interface QuizDetails extends Omit<QuizSummary, 'questionCount' | 'updatedAtUtc'> {
  coverImageUrl: string | null; thumbnailUrl: string | null; language: string; instructions: string | null;
  startsAtUtc: string | null; endsAtUtc: string | null; tags: string[]; folderId: string | null; settings: QuizSettings; questions: QuizQuestion[];
}

@Injectable({ providedIn: 'root' })
export class QuizApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly endpoint = '/api/v1/quizzes';
  list(): Observable<QuizSummary[]> { return this.http.get<QuizSummary[]>(this.endpoint); }
  get(id: string): Observable<QuizDetails> { return this.http.get<QuizDetails>(`${this.endpoint}/${id}`); }
  create(quiz: SaveQuiz): Observable<QuizDetails> { return this.http.post<QuizDetails>(this.endpoint, quiz); }
  update(id: string, quiz: SaveQuiz): Observable<QuizDetails> { return this.http.put<QuizDetails>(`${this.endpoint}/${id}`, quiz); }
  archive(id: string): Observable<void> { return this.http.delete<void>(`${this.endpoint}/${id}`); }
  folders(): Observable<QuizFolder[]> { return this.http.get<QuizFolder[]>(`${this.endpoint}/folders`); }
  createFolder(name: string): Observable<QuizFolder> { return this.http.post<QuizFolder>(`${this.endpoint}/folders`, { name }); }
  refreshAndList(): Observable<QuizSummary[]> {
    return this.auth.refresh().pipe(switchMap(() => this.list()));
  }
}

export type SaveQuiz = Omit<QuizDetails, 'id' | 'questionCount'>;
