using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MaturityApp
{
    public class AssessmentAnalysis
    {
        public int Included, Answered, Total;
        public bool HasScope, Complete;
        public double CurrentUzi;
        public CriterionProfile[] Criteria;
        public DirectionRoadmap[] Directions;
        public DeficitRecord[] Deficits;
        public GroupedAction[] GroupedActions;
    }
    public class CriterionProfile
    {
        public int CriterionIndex, Included, Answered, Missing, ZeroCount, KnownDeficits, RequiredUnknown;
        public string Name;
        public double Weight, Coverage;
        // Both means use answered cells only. Neither is the maturity indicator Uzi.
        public double? Average, WeightedAverage;
    }
    public class DeficitRecord
    {
        public int DirectionId, TargetLevel;
        public int? CriterionIndex;
        public string DirectionName, CriterionName, Type, Action, ConditionText;
        public double? Current, Required;
        public bool BlockingTarget, Preliminary;
        // Stable identity of a plan row, used to keep its owner and deadline (PlanItem).
        public string Key { get { return DirectionId + ":" + (CriterionIndex.HasValue ? CriterionIndex.Value.ToString() : "P") + ":" + Type; } }
    }
    public class GroupedAction
    {
        public int Priority, CriterionIndex, DirectionCount;
        public string CriterionName, Type, Action, ConditionText;
        public double? Required;
        public bool BlockingTarget;
        public int[] DirectionIds;
        public string Owner = "", Deadline = "";
    }
    public class DirectionRoadmap
    {
        public int DirectionId, CurrentLevel, TargetLevel;
        public string DirectionName;
        public bool Included, Complete;
        public double CurrentScore;
        public LevelRoadmap Next, Target;
    }
    public class LevelRoadmap
    {
        public int Level, ProjectedLevel, ChangedCriteria, KnownChanges, UnknownChanges;
        public double RequiredScore, ProjectedScore;
        public bool Preliminary;
        public RoadmapChange[] Changes;
    }
    public class RoadmapChange
    {
        public int CriterionIndex;
        public string CriterionName, ConditionText;
        public double? Current;
        public double Required, Proposed;
        public bool NeedsAssessment;
    }

    public static class Analysis
    {
        const double Epsilon = 1e-9;
        public const string PriorityExplanation = "Сначала оценить неизвестные обязательные виды требований, затем устранить подтверждённые дефициты, затем заполнить остальные пропуски. Внутри групп — по охвату направлений и весу вида требования. Это порядок рассмотрения, а не оценка стоимости или сроков работ.";

        public static AssessmentAnalysis Build(Assessment assessment)
        {
            if (assessment == null) throw new ArgumentNullException("assessment");
            // Validation and normalization happen on a snapshot, never on the assessment being edited.
            Assessment a = Store.Parse(Store.Serialize(assessment));
            Summary summary = Calc.Summary(a);
            AssessmentAnalysis result = new AssessmentAnalysis {
                Included = summary.Count, Answered = summary.Answered, Total = summary.Total,
                HasScope = summary.Count > 0, Complete = summary.Complete, CurrentUzi = summary.Average
            };
            List<DeficitRecord> deficits = new List<DeficitRecord>();
            List<DirectionRoadmap> directions = new List<DirectionRoadmap>();
            foreach (DirectionAssessment d in a.Directions)
            {
                DirectionResult current = Calc.Direction(d);
                DirectionRoadmap direction = new DirectionRoadmap {
                    DirectionId = d.Id, DirectionName = Data.DirectionNames[d.Id - 1], Included = d.Included,
                    Complete = current.Complete, CurrentLevel = current.Level, TargetLevel = d.Target, CurrentScore = current.Score
                };
                if (d.Included)
                {
                    direction.Target = BuildRoadmapCore(d, d.Target);
                    direction.Next = current.Level < 4 ? BuildRoadmapCore(d, current.Level + 1) : null;
                    AddDeficits(d, current, deficits);
                }
                directions.Add(direction);
            }
            result.Directions = directions.ToArray();
            result.Deficits = deficits.ToArray();
            result.Criteria = Enumerable.Range(0, Data.Criteria.Length).Select(criterion => BuildProfile(a, criterion, deficits)).ToArray();
            result.GroupedActions = Group(deficits);
            return result;
        }

        public static LevelRoadmap BuildRoadmap(DirectionAssessment direction, int level)
        {
            if (direction == null) throw new ArgumentNullException("direction");
            if (level < 1 || level > 4) throw new ArgumentOutOfRangeException("level");
            Assessment check = Assessment.Create();
            check.Directions[0] = new DirectionAssessment {
                Id = 1, Included = true, Target = 1,
                Answers = direction.Answers == null ? null : direction.Answers.ToArray()
            };
            Store.Validate(check);
            return BuildRoadmapCore(check.Directions[0], level);
        }

        static CriterionProfile BuildProfile(Assessment a, int criterion, List<DeficitRecord> deficits)
        {
            DirectionAssessment[] included = a.Directions.Where(d => d.Included).ToArray();
            double[] answers = included.Where(d => d.Answers[criterion].HasValue).Select(d => d.Answers[criterion].Value).ToArray();
            double? mean = answers.Length == 0 ? (double?)null : answers.Average();
            return new CriterionProfile {
                CriterionIndex = criterion, Name = Data.Criteria[criterion].Name, Weight = Data.Criteria[criterion].Weight,
                Included = included.Length, Answered = answers.Length, Missing = included.Length - answers.Length,
                ZeroCount = answers.Count(value => value == 0),
                Average = mean, WeightedAverage = mean.HasValue ? mean.Value * Data.Criteria[criterion].Weight : (double?)null,
                Coverage = included.Length == 0 ? 0 : (double)answers.Length / included.Length,
                KnownDeficits = deficits.Count(d => d.CriterionIndex == criterion && d.Type == "improve"),
                RequiredUnknown = deficits.Count(d => d.CriterionIndex == criterion && d.Type == "assess" && d.BlockingTarget)
            };
        }

        static void AddDeficits(DirectionAssessment direction, DirectionResult current, List<DeficitRecord> output)
        {
            for (int criterion = 0; criterion < Data.Criteria.Length; criterion++)
            {
                double weightedMinimum = Calc.Minima[direction.Target - 1][criterion];
                AnswerOption minimum = RequiredOption(criterion, weightedMinimum);
                double? answer = direction.Answers[criterion];
                bool required = weightedMinimum > Epsilon;
                if (!answer.HasValue)
                {
                    output.Add(new DeficitRecord {
                        DirectionId = direction.Id, DirectionName = Data.DirectionNames[direction.Id - 1], TargetLevel = direction.Target,
                        CriterionIndex = criterion, CriterionName = Data.Criteria[criterion].Name,
                        Type = "assess", Action = "Оценить", Current = null, Required = required ? (double?)minimum.Value : null,
                        ConditionText = required ? "Определите подтверждённую оценку. Условие для целевого уровня: " + minimum.Text : "Выберите оценку по подтверждающим данным. Обязательный минимум для этого вида требования на целевом уровне не установлен.",
                        BlockingTarget = required, Preliminary = true
                    });
                }
                else if (answer.Value * Data.Criteria[criterion].Weight < weightedMinimum - Epsilon)
                {
                    output.Add(new DeficitRecord {
                        DirectionId = direction.Id, DirectionName = Data.DirectionNames[direction.Id - 1], TargetLevel = direction.Target,
                        CriterionIndex = criterion, CriterionName = Data.Criteria[criterion].Name,
                        Type = "improve", Action = "Улучшить", Current = answer, Required = minimum.Value,
                        ConditionText = minimum.Text, BlockingTarget = true, Preliminary = !current.Complete
                    });
                }
            }
            if (current.Score < Calc.Thresholds[direction.Target - 1] - Epsilon)
                output.Add(new DeficitRecord {
                    DirectionId = direction.Id, DirectionName = Data.DirectionNames[direction.Id - 1], TargetLevel = direction.Target,
                    CriterionIndex = null, CriterionName = "Сумма баллов направления", Type = "score", Action = "Набрать сумму баллов",
                    Current = current.Score, Required = Calc.Thresholds[direction.Target - 1],
                    ConditionText = "Повысить значения видов требований так, чтобы сумма баллов направления была не ниже " + Calc.F(Calc.Thresholds[direction.Target - 1], "0.###") + " (порог уровня " + direction.Target + " по таблице 3 Методики) при выполнении обязательных значений.",
                    BlockingTarget = true, Preliminary = !current.Complete
                });
        }

        static LevelRoadmap BuildRoadmapCore(DirectionAssessment direction, int level)
        {
            if (level < 1 || level > 4) throw new ArgumentOutOfRangeException("level");
            double?[] proposed = direction.Answers.ToArray();
            List<RoadmapChange> changes = new List<RoadmapChange>();
            for (int criterion = 0; criterion < Data.Criteria.Length; criterion++)
            {
                double weightedMinimum = Calc.Minima[level - 1][criterion];
                if (weightedMinimum <= Epsilon) continue;
                AnswerOption minimum = RequiredOption(criterion, weightedMinimum);
                double? answer = direction.Answers[criterion];
                if (answer.HasValue && answer.Value * Data.Criteria[criterion].Weight >= weightedMinimum - Epsilon) continue;
                proposed[criterion] = minimum.Value;
                changes.Add(new RoadmapChange {
                    CriterionIndex = criterion, CriterionName = Data.Criteria[criterion].Name, Current = answer,
                    Required = minimum.Value, Proposed = minimum.Value, ConditionText = minimum.Text, NeedsAssessment = !answer.HasValue
                });
            }
            DirectionResult projected = Calc.Direction(new DirectionAssessment { Id = direction.Id, Answers = proposed, Target = level });
            // For the current table 3 each threshold equals the sum of its mandatory minima.
            // Keep this explicit check so future table changes cannot silently produce an insufficient roadmap.
            if (projected.Level < level) throw new InvalidOperationException("Не удалось построить достаточный маршрут по обязательным минимумам таблицы 3.");
            return new LevelRoadmap {
                Level = level, ProjectedLevel = projected.Level, RequiredScore = Calc.Thresholds[level - 1], ProjectedScore = projected.Score,
                ChangedCriteria = changes.Count, KnownChanges = changes.Count(c => !c.NeedsAssessment), UnknownChanges = changes.Count(c => c.NeedsAssessment),
                Preliminary = direction.Answers.Any(answer => !answer.HasValue), Changes = changes.ToArray()
            };
        }

        static AnswerOption RequiredOption(int criterion, double weightedMinimum)
        {
            return Data.Criteria[criterion].Options.First(option => option.Value * Data.Criteria[criterion].Weight >= weightedMinimum - Epsilon);
        }

        static GroupedAction[] Group(List<DeficitRecord> deficits)
        {
            IEnumerable<GroupedAction> groups = deficits.Where(d => d.CriterionIndex.HasValue)
                .GroupBy(d => d.Type + ":" + d.CriterionIndex.Value + ":" + (d.Required.HasValue ? d.Required.Value.ToString("R", CultureInfo.InvariantCulture) : "unknown") + ":" + d.BlockingTarget)
                .Select(group => {
                    DeficitRecord first = group.First();
                    int[] ids = group.Select(d => d.DirectionId).Distinct().OrderBy(id => id).ToArray();
                    return new GroupedAction {
                        CriterionIndex = first.CriterionIndex.Value, CriterionName = first.CriterionName,
                        Type = first.Type, Action = first.Action, Required = first.Required, ConditionText = first.ConditionText,
                        BlockingTarget = first.BlockingTarget, DirectionIds = ids, DirectionCount = ids.Length,
                        Owner = "", Deadline = ""
                    };
                });
            GroupedAction[] result = groups.OrderBy(g => g.Type == "assess" && g.BlockingTarget ? 0 : g.Type == "improve" ? 1 : 2)
                .ThenByDescending(g => g.DirectionCount).ThenByDescending(g => Data.Criteria[g.CriterionIndex].Weight)
                .ThenBy(g => g.CriterionIndex).ThenByDescending(g => g.Required.GetValueOrDefault()).ToArray();
            for (int index = 0; index < result.Length; index++) result[index].Priority = index + 1;
            return result;
        }
    }
}
