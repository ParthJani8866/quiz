import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { QuizApiService, QuizDetails, QuizFolder, QuizQuestion, QuizQuestionType, QuizSettings, QuizSummary, SaveQuiz } from './quiz-api.service';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-quiz-library',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule],
  templateUrl: './quiz-library.component.html',
  styleUrl: './quiz-library.component.scss'
})
export class QuizLibraryComponent implements OnInit {
  private readonly api = inject(QuizApiService);
  private readonly auth = inject(AuthService);
  readonly quizzes = signal<QuizSummary[]>([]);
  readonly folders = signal<QuizFolder[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly search = signal('');
  readonly statusFilter = signal('All');
  readonly editing = signal(false);
  readonly currentId = signal<string | null>(null);
  readonly showSettings = signal(false);
  readonly notice = signal('');
  readonly questionTypes: { value: QuizQuestionType; label: string }[] = [
    { value: 'SingleChoice', label: 'Multiple choice' }, { value: 'MultipleSelect', label: 'Select all that apply' },
    { value: 'TrueFalse', label: 'True or false' }, { value: 'TypeAnswer', label: 'Type the answer' },
    { value: 'FillInTheBlanks', label: 'Fill in the blanks' }, { value: 'Numeric', label: 'Numeric answer' },
    { value: 'ImageBased', label: 'Image question' }, { value: 'AudioBased', label: 'Audio question' },
    { value: 'VideoBased', label: 'Video question' }, { value: 'Matching', label: 'Matching' },
    { value: 'Ordering', label: 'Order the items' }, { value: 'OpenEnded', label: 'Open ended' }, { value: 'Poll', label: 'Opinion poll' }
  ];
  draft: SaveQuiz = this.emptyQuiz();

  ngOnInit(): void { this.load(); }

  get filtered(): QuizSummary[] {
    const term = this.search().trim().toLowerCase();
    return this.quizzes().filter(quiz => (this.statusFilter() === 'All' || quiz.status === this.statusFilter()) &&
      (!term || `${quiz.title} ${quiz.description ?? ''} ${quiz.category ?? ''}`.toLowerCase().includes(term)));
  }

  load(): void {
    this.loading.set(true); this.error.set('');
    this.api.refreshAndList().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: quizzes => {
        this.quizzes.set(quizzes);
        this.api.folders().subscribe({ next: folders => this.folders.set(folders) });
      },
      error: response => this.error.set(response.status === 401 ? 'Sign in to open your workspace.' : 'Your quiz library could not be loaded. Check the API and database connection, then try again.')
    });
  }

  create(): void { this.currentId.set(null); this.draft = this.emptyQuiz(); this.editing.set(true); this.showSettings.set(false); this.notice.set(''); }

  edit(id: string): void {
    this.loading.set(true);
    this.api.get(id).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: quiz => { this.currentId.set(quiz.id); this.draft = this.clone(quiz); this.editing.set(true); this.showSettings.set(false); },
      error: () => this.error.set('This quiz could not be opened. Refresh the library and try again.')
    });
  }

  addQuestion(): void {
    this.draft.questions.push({ type: 'SingleChoice', title: '', description: '', explanation: '', points: 100,
      negativePoints: 0, timeLimitSeconds: 30, difficulty: 'Medium', randomizeOptions: false, isRequired: true,
      options: [{ text: '', isCorrect: true }, { text: '', isCorrect: false }] });
    this.draft.questions = [...this.draft.questions];
  }

  removeQuestion(index: number): void { this.draft.questions = this.draft.questions.filter((_, i) => i !== index); }
  addOption(question: QuizQuestion): void { question.options = [...question.options, { text: '', isCorrect: false }]; }
  removeOption(question: QuizQuestion, index: number): void { question.options = question.options.filter((_, i) => i !== index); }
  updateTags(raw: string): void { this.draft.tags = raw.split(',').map(tag => tag.trim()).filter(tag => !!tag); }
  setCorrect(question: QuizQuestion, index: number): void {
    question.options = question.type === 'MultipleSelect'
      ? question.options.map((option, i) => ({ ...option, isCorrect: i === index ? !option.isCorrect : option.isCorrect }))
      : question.options.map((option, i) => ({ ...option, isCorrect: i === index }));
  }

  setTextAnswer(question: QuizQuestion, answer: string): void {
    question.answerConfigurationJson = JSON.stringify({ acceptedAnswers: answer.split('|').map(value => value.trim()).filter(Boolean) });
  }

  getTextAnswer(question: QuizQuestion): string {
    try { return (JSON.parse(question.answerConfigurationJson || '{}').acceptedAnswers as string[] | undefined)?.join(' | ') ?? ''; }
    catch { return ''; }
  }

  logout(): void {
    this.auth.logout().subscribe({ next: () => { window.history.replaceState({}, '', '/'); window.location.reload(); }, error: () => { window.history.replaceState({}, '', '/'); window.location.reload(); } });
  }

  save(publish = false): void {
    if (!this.draft.title.trim()) { this.error.set('Add a title before saving your quiz.'); return; }
    if (publish && this.draft.questions.length === 0) { this.error.set('Add at least one question before publishing.'); return; }
    if (this.draft.questions.some(question => !question.title.trim())) { this.error.set('Give each question a title before saving.'); return; }
    if (this.draft.questions.some(question => ['SingleChoice', 'MultipleSelect', 'TrueFalse'].includes(question.type) && question.options.filter(option => option.text.trim()).length < 2)) {
      this.error.set('Choice questions need at least two answer options.'); return;
    }
    this.error.set(''); this.saving.set(true);
    const payload = this.clone(this.draft);
    if (publish) payload.status = 'Published';
    const request = this.currentId() ? this.api.update(this.currentId()!, payload) : this.api.create(payload);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: result => { this.currentId.set(result.id); this.draft = this.clone(result); this.notice.set(publish ? 'Quiz published and ready to share.' : 'Your changes are saved.'); this.load(); },
      error: response => this.error.set(response.error?.errors?.[0] ?? response.error?.title ?? 'Your quiz could not be saved. Please try again.')
    });
  }

  archive(quiz: QuizSummary): void {
    if (!confirm(`Archive “${quiz.title}”? You can no longer edit or publish it.`)) return;
    this.api.archive(quiz.id).subscribe({ next: () => { this.quizzes.update(items => items.filter(item => item.id !== quiz.id)); this.notice.set('Quiz archived.'); }, error: () => this.error.set('This quiz could not be archived.') });
  }

  createFolder(): void {
    const name = window.prompt('Choose a name for this folder')?.trim();
    if (!name) return;
    this.api.createFolder(name).subscribe({
      next: folder => { this.folders.update(items => [...items, folder].sort((a, b) => a.name.localeCompare(b.name))); this.draft.folderId = folder.id; },
      error: () => this.error.set('The folder could not be created.')
    });
  }

  closeEditor(): void { this.editing.set(false); this.error.set(''); this.notice.set(''); }

  private emptyQuiz(): SaveQuiz {
    const settings: QuizSettings = { randomizeQuestionOrder: false, randomizeAnswerOptions: false,
      showCorrectAnswersImmediately: false, showCorrectAnswersAtEnd: true, showExplanations: true,
      allowRetries: false, maximumAttempts: 1, passingPercentage: 60, timeLimitSeconds: null,
      perQuestionTimeLimitSeconds: null, enableLeaderboard: true, requireParticipantName: true,
      allowAnonymousParticipation: true, displayParticipantResults: true, allowAnswerReview: true, preventMultipleSubmissions: true };
    return { title: '', description: '', coverImageUrl: null, thumbnailUrl: null, category: '', language: 'en',
      visibility: 'Private', status: 'Draft', instructions: '', startsAtUtc: null, endsAtUtc: null,
      estimatedDurationMinutes: 5, tags: [], folderId: null, settings, questions: [] };
  }

  private clone<T>(value: T): T { return JSON.parse(JSON.stringify(value)) as T; }
}
