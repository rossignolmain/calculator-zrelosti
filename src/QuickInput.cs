using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MaturityApp
{
    public class AnswerChange
    {
        public int DirectionId;
        // Zero-based index into Data.Criteria and DirectionAssessment.Answers.
        public int Criterion;
        public double? Before, After;
    }

    public static class QuickInput
    {
        public static readonly string[] HeaderNames = {
            "Документы", "Выполнение", "Инструменты", "Квалификация",
            "Контроль", "Обучение", "Внешний аудит", "Актуализация"
        };
        static readonly string[][] Labels = {
            new[] { "Не разработаны", "Есть недостатки", "Соответствуют и обновляются" },
            new[] { "Не реализуется", "Частично", "По регламентам" },
            new[] { "Вручную", "Отдельные инструменты", "Полностью с инструментами" },
            new[] { "Нет компетенций", "Обучены, без опыта", "Есть компетенции" },
            new[] { "Не проводится", "После инцидентов", "Плановые проверки" },
            new[] { "Не проводится", "Эпизодически, по инициативе", "Без системы и измерения", "Программа и контроль знаний" },
            new[] { "Не проводится", "Эпизодически", "Регулярно, с улучшениями" },
            new[] { "Не проводится", "Эпизодически", "Улучшения по плану" }
        };

        public static string ShortLabel(int criterion, double value)
        {
            if (criterion < 0 || criterion >= Data.Criteria.Length) throw new ArgumentOutOfRangeException("criterion");
            AnswerOption[] options = Data.Criteria[criterion].Options;
            for (int index = 0; index < options.Length; index++)
                if (Math.Abs(options[index].Value - value) < 1e-10) return Labels[criterion][index];
            throw new ArgumentOutOfRangeException("value", "Недопустимая оценка для выбранного критерия.");
        }

        public static List<AnswerChange> CopyIntoEmpty(Assessment a, int sourceId)
        {
            if (sourceId < 1 || sourceId > 21) throw new ArgumentOutOfRangeException("sourceId");
            ValidateStructure(a);
            DirectionAssessment source = a.Directions.First(d => d.Id == sourceId);
            if (!source.Included) throw new InvalidOperationException("Источник ответов должен быть включён в область оценки.");
            List<AnswerChange> changes = new List<AnswerChange>();
            foreach (DirectionAssessment destination in a.Directions.Where(d => d.Included && d.Id != sourceId).OrderBy(d => d.Id))
                for (int criterion = 0; criterion < Data.Criteria.Length; criterion++)
                    if (!destination.Answers[criterion].HasValue && source.Answers[criterion].HasValue)
                        changes.Add(new AnswerChange {
                            DirectionId = destination.Id, Criterion = criterion,
                            Before = destination.Answers[criterion], After = source.Answers[criterion]
                        });
            // Plan every change before touching answers, so an invalid source or model never partially applies.
            Dictionary<int, DirectionAssessment> destinations = a.Directions.ToDictionary(d => d.Id);
            foreach (AnswerChange change in changes) destinations[change.DirectionId].Answers[change.Criterion] = change.After;
            return changes;
        }

        // criterion -1 means all eight criteria. A null scope means every included
        // direction; an explicit scope contains direction IDs, not array indices.
        // Duplicate IDs are harmless, and valid but excluded directions are skipped.
        // This method only plans changes, so updating an operation's parameters never
        // records answers. SetValue is the explicit command that applies this plan.
        public static List<AnswerChange> PreviewSetValue(Assessment a, int criterion, double value, bool overwrite, IEnumerable<int> directionIds = null)
        {
            if (criterion < -1 || criterion >= Data.Criteria.Length) throw new ArgumentOutOfRangeException("criterion");
            ValidateStructure(a);
            int[] criteria = criterion == -1 ? Enumerable.Range(0, Data.Criteria.Length).ToArray() : new[] { criterion };
            foreach (int selected in criteria)
                if (!Allowed(selected, value)) throw new ArgumentOutOfRangeException("value", "Оценка недопустима для одного из выбранных критериев.");

            // Enumerate and validate the entire scope before constructing/applying
            // any mutation, including a lazily supplied sequence that may fail.
            int[] requested = directionIds == null ? a.Directions.Select(d => d.Id).ToArray() : directionIds.ToArray();
            HashSet<int> ids = new HashSet<int>();
            foreach (int id in requested)
            {
                if (id < 1 || id > 21) throw new ArgumentOutOfRangeException("directionIds", "Номер направления должен находиться между 1 и 21.");
                ids.Add(id);
            }

            Dictionary<int, double> canonicalValues = criteria.ToDictionary(selected => selected,
                selected => Data.Criteria[selected].Options.First(o => Math.Abs(o.Value - value) < 1e-10).Value);
            List<AnswerChange> changes = new List<AnswerChange>();
            foreach (DirectionAssessment destination in a.Directions.Where(d => d.Included && ids.Contains(d.Id)).OrderBy(d => d.Id))
                foreach (int selected in criteria)
                {
                    double? before = destination.Answers[selected];
                    if (!overwrite && before.HasValue) continue; // An explicit zero is filled.
                    double after = canonicalValues[selected];
                    if (before.HasValue && Math.Abs(before.Value - after) < 1e-10) continue;
                    changes.Add(new AnswerChange { DirectionId = destination.Id, Criterion = selected, Before = before, After = after });
                }
            return changes;
        }

        public static List<AnswerChange> SetValue(Assessment a, int criterion, double value, bool overwrite, IEnumerable<int> directionIds = null)
        {
            List<AnswerChange> changes = PreviewSetValue(a, criterion, value, overwrite, directionIds);
            Dictionary<int, DirectionAssessment> destinations = a.Directions.ToDictionary(d => d.Id);
            foreach (AnswerChange change in changes) destinations[change.DirectionId].Answers[change.Criterion] = change.After;
            return changes;
        }

        public static int Revert(Assessment a, IEnumerable<AnswerChange> changes)
        {
            ValidateStructure(a);
            if (changes == null) throw new ArgumentNullException("changes");
            AnswerChange[] snapshot = changes.ToArray();
            Dictionary<int, DirectionAssessment> directions = a.Directions.ToDictionary(d => d.Id);
            int count = 0;
            foreach (AnswerChange change in snapshot)
            {
                if (change == null || change.Criterion < 0 || change.Criterion >= Data.Criteria.Length) continue;
                DirectionAssessment direction;
                if (!directions.TryGetValue(change.DirectionId, out direction)) continue;
                if (!Allowed(change.Criterion, change.Before) || !Allowed(change.Criterion, change.After) || change.Before == change.After) continue;
                if (direction.Answers[change.Criterion] != change.After) continue;
                direction.Answers[change.Criterion] = change.Before;
                count++;
            }
            return count;
        }

        static bool Allowed(int criterion, double? answer)
        {
            return !answer.HasValue || Data.Criteria[criterion].Options.Any(o => Math.Abs(o.Value - answer.Value) < 1e-10);
        }
        static void ValidateStructure(Assessment a)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (a.Directions == null || a.Directions.Length != 21) throw new InvalidDataException("В оценке должно быть 21 направление.");
            HashSet<int> ids = new HashSet<int>();
            foreach (DirectionAssessment direction in a.Directions)
            {
                if (direction == null || direction.Id < 1 || direction.Id > 21 || !ids.Add(direction.Id)) throw new InvalidDataException("Некорректные номера направлений.");
                if (direction.Answers == null || direction.Answers.Length != Data.Criteria.Length) throw new InvalidDataException("В направлении должно быть восемь критериев.");
                for (int criterion = 0; criterion < Data.Criteria.Length; criterion++)
                    if (!Allowed(criterion, direction.Answers[criterion])) throw new InvalidDataException("Недопустимая оценка критерия.");
            }
        }
    }
}
