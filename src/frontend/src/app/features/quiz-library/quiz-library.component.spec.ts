import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { QuizApiService } from './quiz-api.service';
import { QuizLibraryComponent } from './quiz-library.component';

describe('QuizLibraryComponent', () => {
  const api = {
    refreshAndList: vi.fn(() => of([])),
    folders: vi.fn(() => of([])),
    get: vi.fn(), create: vi.fn(), update: vi.fn(), archive: vi.fn()
  };
  const auth = { logout: vi.fn(() => of(undefined)) };

  beforeEach(async () => {
    api.refreshAndList.mockClear().mockReturnValue(of([]));
    api.folders.mockClear().mockReturnValue(of([]));
    await TestBed.configureTestingModule({
      imports: [QuizLibraryComponent],
      providers: [
        { provide: QuizApiService, useValue: api },
        { provide: AuthService, useValue: auth }
      ]
    }).compileComponents();
  });

  it('loads the library and presents an actionable empty state', async () => {
    const fixture = TestBed.createComponent(QuizLibraryComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.refreshAndList).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.textContent).toContain('Your next great quiz starts here');
    expect(fixture.nativeElement.querySelector('.empty-state .primary-button')).toBeTruthy();
  });

  it('starts a draft and adds editable choice questions', async () => {
    const fixture = TestBed.createComponent(QuizLibraryComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.create();
    fixture.componentInstance.addQuestion();
    fixture.detectChanges();

    expect(fixture.componentInstance.draft.questions).toHaveLength(1);
    expect(fixture.componentInstance.draft.questions[0].options).toHaveLength(2);
    expect(fixture.nativeElement.textContent).toContain('Create a new quiz');
  });
});
