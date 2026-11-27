namespace QuizFlow.Domain.Activities;

public enum ActivityStatus { Draft, Published, Archived }
public enum ActivityVisibility { Private, Public, Organization }
public enum QuestionDifficulty { Easy, Medium, Hard }
public enum QuizQuestionType { SingleChoice, MultipleSelect, TrueFalse, TypeAnswer, FillInTheBlanks, Numeric, ImageBased, AudioBased, VideoBased, Matching, Ordering, OpenEnded, Poll }
public enum PollQuestionType { SingleChoice, MultipleChoice, YesNo, RatingScale, StarRating, NumericScale, Emoji, WordCloud, OpenText, Ranking, ImageChoice, NetPromoterScore }
public enum SurveyQuestionType { ShortText, LongText, SingleChoice, MultipleChoice, Dropdown, YesNo, RatingScale, StarRating, NumericScale, Date, Email, Phone, FileUpload, Ranking, NetPromoterScore, Matrix, ImageChoice, Signature }
