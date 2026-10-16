using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace MaturityApp
{
    // Model-only regression checks. No WPF, files, user documents or GUI calls.
    public static class BulkVerification
    {
        public static int TestCount { get; private set; }
        public static int AssertionCount { get; private set; }
        static List<string> errors;

        public static void Run()
        {
            TestCount = AssertionCount = 0; errors = new List<string>();
            Case("preview is nonmutating and matches the applied plan", Preview);
            Case("implicit scope means included directions", IncludedScope);
            Case("explicit subset skips exclusions and ignores duplicate IDs", ExplicitScope);
            Case("only-empty preserves zero and other filled grades", PreserveFilled);
            Case("education accepts 0.3 without changing other criteria", Education);
            foreach (double grade in new[] { 0.0, .5, 1.0 })
            {
                double selected = grade;
                Case("common grade " + grade + " applies to all eight criteria", () => CommonGrade(selected));
            }
            Case("all-criteria only-empty preserves existing education 0.3", PreserveEducation);
            Case("overwrite records real previous values and undo restores them", OverwriteUndo);
            Case("undo preserves subsequent manual edits", PreserveManualEdits);
            Case("identical grades and an empty scope are no-ops", NoOp);
            Case("scope uses IDs even when the model array is reversed", UnorderedIds);
            Case("metadata, evidence, category, scope and goals stay unchanged", PreserveMetadata);
            Case("accepted near-equal grades are canonicalized", CanonicalGrade);
            foreach (int criterion in new[] { -2, 8, Int32.MaxValue })
            {
                int invalid = criterion;
                Case("reject invalid criterion " + criterion + " atomically", () =>
                {
                    Assessment a = Assessment.Create();
                    Reject(a, () => QuickInput.SetValue(a, invalid, 1, true), typeof(ArgumentOutOfRangeException));
                });
            }
            foreach (double value in new[] { Double.NaN, Double.PositiveInfinity, Double.NegativeInfinity, -1.0, .2, .3, .75, 2.0 })
            {
                double invalid = value;
                Case("reject invalid single-criterion value " + value + " atomically", () =>
                {
                    Assessment a = Assessment.Create();
                    Reject(a, () => QuickInput.SetValue(a, 0, invalid, true), typeof(ArgumentOutOfRangeException));
                });
            }
            Case("0.3 for all criteria fails before any mutation", () =>
            {
                Assessment a = Assessment.Create();
                Reject(a, () => QuickInput.SetValue(a, -1, .3, true), typeof(ArgumentOutOfRangeException));
                Reject(a, () => QuickInput.PreviewSetValue(a, -1, .3, true, new int[0]), typeof(ArgumentOutOfRangeException));
            });
            foreach (int invalidId in new[] { -1, 0, 22, Int32.MaxValue })
            {
                int invalid = invalidId;
                Case("reject invalid trailing direction ID " + invalidId + " atomically", () =>
                {
                    Assessment a = Assessment.Create();
                    Reject(a, () => QuickInput.SetValue(a, 0, 1, true, new[] { 1, 2, invalid }), typeof(ArgumentOutOfRangeException));
                });
            }
            Case("failing lazy scope cannot partially apply changes", () =>
            {
                Assessment a = Assessment.Create();
                Reject(a, () => QuickInput.SetValue(a, 0, 1, true, FailingScope()), typeof(InvalidOperationException));
            });
            Case("null model is rejected", () => Reject(null, () => QuickInput.SetValue(null, 0, 1, true), typeof(ArgumentNullException)));
            InvalidModel("null directions", a => a.Directions = null);
            InvalidModel("wrong direction count", a => a.Directions = a.Directions.Take(20).ToArray());
            InvalidModel("null direction", a => a.Directions[1] = null);
            InvalidModel("duplicate direction ID", a => a.Directions[1].Id = 1);
            InvalidModel("invalid direction ID", a => a.Directions[1].Id = 22);
            InvalidModel("null answer array", a => a.Directions[1].Answers = null);
            InvalidModel("wrong answer count", a => a.Directions[1].Answers = new double?[7]);
            InvalidModel("invalid existing answer", a => a.Directions[1].Answers[0] = .2);
            if (errors.Count > 0) throw new InvalidOperationException("Bulk verification failed: " + String.Join("; ", errors));
        }
        static void Case(string name, Action action)
        {
            TestCount++;
            try { action(); }
            catch (Exception ex) { errors.Add(name + ": " + ex.GetType().Name + ": " + ex.Message); }
        }
        static void Check(bool condition, string message)
        {
            AssertionCount++; if (!condition) throw new InvalidOperationException(message);
        }
        static void Reject(Assessment a, Action action, Type expected)
        {
            string before = Store.Serialize(a); Exception rejection = null;
            try { action(); } catch (Exception ex) { rejection = ex; }
            Check(rejection != null && expected.IsInstanceOfType(rejection), "The invalid input did not produce the expected rejection.");
            Check(Store.Serialize(a) == before, "Rejected input partially changed the assessment.");
        }
        static void InvalidModel(string name, Action<Assessment> invalidate)
        {
            Case("reject " + name + " without touching other answers", () =>
            {
                Assessment a = Assessment.Create(); invalidate(a);
                Reject(a, () => QuickInput.SetValue(a, 0, 1, true), typeof(InvalidDataException));
            });
        }
        static IEnumerable<int> FailingScope()
        {
            yield return 1; yield return 2; throw new InvalidOperationException("Test scope enumeration failure.");
        }
        static string Metadata(Assessment a)
        {
            return new JavaScriptSerializer().Serialize(new {
                a.Version, a.Organization, a.OrganizationShort, a.OrganizationAddress, a.City,
                a.SystemName, a.Assessor, a.AssessorPosition, a.ApproverName, a.ApproverPosition,
                a.DocumentNumber, a.Date, a.Category, a.Notes,
                Directions = a.Directions.Select(d => new { d.Id, d.Included, d.Target, d.Evidence }).ToArray()
            });
        }
        static void Preview()
        {
            Assessment a = Assessment.Create(); string before = Store.Serialize(a);
            List<AnswerChange> plan = QuickInput.PreviewSetValue(a, 3, .5, false);
            Check(plan.Count == 21 && plan.All(c => c.Criterion == 3 && !c.Before.HasValue && c.After == .5), "Preview returned an incorrect plan.");
            Check(Store.Serialize(a) == before, "Preview wrote answers.");
            List<AnswerChange> applied = QuickInput.SetValue(a, 3, .5, false);
            Check(new JavaScriptSerializer().Serialize(applied) == new JavaScriptSerializer().Serialize(plan), "Applied changes differ from the preview.");
            Check(a.Directions.All(d => d.Answers[3] == .5 && d.Answers.Where((v, j) => j != 3).All(v => !v.HasValue)), "Single-criterion bulk edit changed unrelated cells.");
        }
        static void IncludedScope()
        {
            Assessment a = Assessment.Create(); a.Directions[1].Included = false;
            List<AnswerChange> changes = QuickInput.SetValue(a, 2, 1, false);
            Check(changes.Count == 20 && changes.All(c => c.DirectionId != 2), "Default scope modified an excluded direction.");
            Check(a.Directions[1].Answers.All(v => !v.HasValue), "Excluded answers were populated.");
        }
        static void ExplicitScope()
        {
            Assessment a = Assessment.Create(); a.Directions[11].Included = false;
            List<AnswerChange> changes = QuickInput.SetValue(a, 4, .5, false, new[] { 2, 5, 12, 2 });
            Check(changes.Count == 2 && changes.Select(c => c.DirectionId).SequenceEqual(new[] { 2, 5 }), "Explicit scope was ignored or duplicate IDs caused duplicate changes.");
            Check(a.Directions.Where(d => d.Id != 2 && d.Id != 5).All(d => d.Answers.All(v => !v.HasValue)), "Explicit subset changed a direction outside its scope.");
            string before = Store.Serialize(a);
            Check(QuickInput.SetValue(a, -1, 1, true, new int[0]).Count == 0 && Store.Serialize(a) == before, "An empty explicit subset changed answers.");
        }
        static void PreserveFilled()
        {
            Assessment a = Assessment.Create(); a.Directions[0].Answers[1] = 0; a.Directions[1].Answers[1] = .5; a.Directions[2].Answers[1] = 1;
            List<AnswerChange> changes = QuickInput.SetValue(a, 1, 1, false);
            Check(changes.Count == 18, "Only-empty edit did not restrict itself to missing answers.");
            Check(a.Directions[0].Answers[1] == 0 && a.Directions[1].Answers[1] == .5 && a.Directions[2].Answers[1] == 1, "Only-empty overwrote an existing grade, including zero.");
            Check(Calc.Summary(a).Answered == 21, "Explicit zero was confused with a missing answer.");
        }
        static void Education()
        {
            Assessment a = Assessment.Create();
            Check(QuickInput.SetValue(a, 5, .3, false).Count == 21, "Education grade 0.3 was not applied.");
            Check(a.Directions.All(d => d.Answers[5] == .3 && d.Answers.Where((v, j) => j != 5).All(v => !v.HasValue)), "Education operation changed other criteria.");
        }
        static void CommonGrade(double value)
        {
            Assessment a = Assessment.Create();
            Check(QuickInput.SetValue(a, -1, value, false).Count == 168, "Common grade did not cover all 168 included cells.");
            Check(a.Directions.All(d => d.Answers.All(v => v == value)), "Common grade was only partially applied.");
            Check(Calc.Summary(a).Answered == 168, "The filled common-grade assessment is not complete.");
        }
        static void PreserveEducation()
        {
            Assessment a = Assessment.Create(); a.Directions[0].Answers[5] = .3;
            Check(QuickInput.SetValue(a, -1, .5, false).Count == 167, "Only-empty all-criteria edit counted a filled education answer as empty.");
            Check(a.Directions[0].Answers[5] == .3, "Only-empty replaced the existing education 0.3.");
        }
        static void OverwriteUndo()
        {
            Assessment a = Assessment.Create(); a.Directions[0].Answers[3] = 0; a.Directions[1].Answers[3] = .5; a.Directions[2].Answers[3] = 1;
            string before = Store.Serialize(a); List<AnswerChange> changes = QuickInput.SetValue(a, 3, .5, true);
            Check(changes.Count == 20 && changes.Any(c => c.Before == 0) && changes.Any(c => c.Before == 1) && changes.Any(c => !c.Before.HasValue), "Overwrite did not capture actual prior grades.");
            Check(a.Directions.All(d => d.Answers[3] == .5), "Overwrite did not apply the selected grade.");
            Check(QuickInput.Revert(a, changes) == 20 && Store.Serialize(a) == before, "Undo did not exactly restore overwritten values and missing answers.");
        }
        static void PreserveManualEdits()
        {
            Assessment a = Assessment.Create(); List<AnswerChange> changes = QuickInput.SetValue(a, 0, .5, false);
            a.Directions[3].Answers[0] = 1;
            Check(QuickInput.Revert(a, changes) == 20, "Undo reverted a subsequently changed cell.");
            Check(a.Directions[3].Answers[0] == 1 && a.Directions.Where(d => d.Id != 4).All(d => !d.Answers[0].HasValue), "Undo did not preserve the manual answer.");
        }
        static void NoOp()
        {
            Assessment a = Assessment.Create(); foreach (DirectionAssessment d in a.Directions) d.Answers[2] = 1;
            string before = Store.Serialize(a);
            Check(QuickInput.PreviewSetValue(a, 2, 1, true).Count == 0, "Preview reported already identical grades as changes.");
            Check(QuickInput.SetValue(a, 2, 1, true).Count == 0 && QuickInput.SetValue(a, 2, .5, false).Count == 0, "An identical/protected answer was unnecessarily recorded as a change.");
            Check(Store.Serialize(a) == before, "A no-op changed the assessment.");
        }
        static void UnorderedIds()
        {
            Assessment a = Assessment.Create(); Array.Reverse(a.Directions); int[] order = a.Directions.Select(d => d.Id).ToArray();
            List<AnswerChange> changes = QuickInput.SetValue(a, 7, 1, false, new[] { 1, 21 });
            Check(changes.Count == 2 && a.Directions.First(d => d.Id == 1).Answers[7] == 1 && a.Directions.First(d => d.Id == 21).Answers[7] == 1, "Scope IDs were treated as array indices.");
            Check(a.Directions.Select(d => d.Id).SequenceEqual(order), "Bulk editing reordered the model array.");
        }
        static void PreserveMetadata()
        {
            Assessment a = Assessment.Create(); a.Organization = "Тест"; a.OrganizationShort = "Т"; a.OrganizationAddress = "Тестовый адрес";
            a.City = "Тестовый город"; a.SystemName = "Тестовая система"; a.Assessor = "Тестовый составитель"; a.AssessorPosition = "Должность";
            a.ApproverName = "Тестовый утверждающий"; a.ApproverPosition = "Должность утверждающего"; a.DocumentNumber = "ТЕСТ-1";
            a.Notes = "Тестовое замечание"; a.Category = 2;
            foreach (DirectionAssessment d in a.Directions) { d.Target = 3 + d.Id % 2; d.Evidence = "Подтверждение " + d.Id; }
            a.Directions[2].Included = false; string before = Metadata(a);
            QuickInput.PreviewSetValue(a, -1, 1, true); List<AnswerChange> changes = QuickInput.SetValue(a, -1, 1, true);
            Check(Metadata(a) == before, "Bulk edit modified metadata, evidence, category, scope or goals.");
            QuickInput.Revert(a, changes); Check(Metadata(a) == before, "Undo modified metadata, evidence, category, scope or goals.");
        }
        static void CanonicalGrade()
        {
            Assessment a = Assessment.Create();
            List<AnswerChange> changes = QuickInput.SetValue(a, 0, .5 + 1e-12, false, new[] { 1 });
            Check(changes.Count == 1 && changes[0].After == .5 && a.Directions[0].Answers[0] == .5, "A near-equal accepted grade was not normalized to its actual option.");
            Check(QuickInput.SetValue(a, 0, .5 + 1e-12, true, new[] { 1 }).Count == 0, "A near-equal grade created a redundant change.");
        }
    }
}
