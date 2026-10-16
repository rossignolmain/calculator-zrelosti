using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace MaturityApp
{
    // Model-only regression checks; never launches a window or accesses user projects.
    public static class SystemPassportVerification
    {
        public static int TestCount { get; private set; }
        public static int AssertionCount { get; private set; }
        public static int Tests { get { return TestCount; } }
        public static int Assertions { get { return AssertionCount; } }
        static List<string> errors;

        public static void Run()
        {
            TestCount = AssertionCount = 0;
            errors = new List<string>();
            Case("legacy version-one projects load without creating a passport", LegacyProject);
            Case("a new single-arm base contains only confirmed composition", SingleArm);
            Case("passport and project roundtrip preserve unknown, absent and present facts", Roundtrip);
            Case("snapshot and template export do not normalize the caller", SnapshotIsolation);
            Case("unknown and absent technologies produce different summaries", UnknownVsAbsent);
            Case("internet channel confirms internet access and rejects contradictory facts atomically", InternetChannel);
            Case("descriptions list known components without assuming a network connection", Descriptions);
            Case("updating and removing passport never change maturity data or calculations", PreserveAssessment);
            Case("invalid passport application is atomic", InvalidApply);
            Case("validation normalizes null strings only after checking the complete passport", AtomicNormalization);
            Case("count boundaries and enum values are validated", Boundaries);
            Case("template contains only technical passport fields", TemplatePrivacy);
            Case("project and organization files cannot masquerade as arm templates", TemplateRejections);
            Case("typed template fields do not silently accept text or fractional counts", TemplateTypes);
            Case("template files roundtrip and invalid saves preserve existing files", TemplateFiles);
            Case("null passports remain optional in the version-one model", OptionalNull);
            if (errors.Count > 0) throw new InvalidOperationException("System passport verification failed: " + String.Join("; ", errors));
        }

        static void Case(string name, Action action)
        {
            TestCount++;
            try { action(); }
            catch (Exception ex) { errors.Add(name + ": " + ex.GetType().Name + ": " + ex.Message); }
        }
        static void Check(bool condition, string message)
        {
            AssertionCount++;
            if (!condition) throw new InvalidOperationException(message);
        }
        static void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, message);
        }
        static string Json(object value) { return new JavaScriptSerializer().Serialize(value); }
        static Assessment FilledAssessment()
        {
            Assessment a = Assessment.Create();
            a.Organization = "ORGANIZATION_PRIVATE_SENTINEL"; a.OrganizationAddress = "ADDRESS_PRIVATE_SENTINEL";
            a.Assessor = "PERSON_PRIVATE_SENTINEL"; a.DocumentNumber = "DOCUMENT_PRIVATE_SENTINEL";
            a.SystemName = "SYSTEM_PRIVATE_SENTINEL"; a.Notes = "ASSESSMENT_PRIVATE_SENTINEL";
            a.Category = 2;
            foreach (DirectionAssessment d in a.Directions)
            {
                d.Target = d.Id % 2 == 0 ? 3 : 2;
                d.Included = d.Id != 21;
                d.Answers = new double?[] { 0, .5, 1, null, .5, .3, 1, 0 };
                d.Evidence = "EVIDENCE_PRIVATE_SENTINEL_" + d.Id;
            }
            return a;
        }
        static string AssessmentWithoutPassport(Assessment a)
        {
            Dictionary<string, object> fields = new JavaScriptSerializer().DeserializeObject(Store.Serialize(a)) as Dictionary<string, object>;
            fields.Remove("Passport");
            return Json(fields);
        }
        static SystemPassport FullPassport()
        {
            SystemPassport p = SystemPassports.CreateSingleArm();
            p.ExternalSystem = "ППО АИСТ ГБД"; p.Connection = "client"; p.Channel = "vpn";
            p.Protection = "Средства защиты уточнены"; p.Notes = "Состав подтверждён";
            p.Switches = 1; p.Domain = false; p.Internet = true; p.Wireless = null;
            p.Mobile = false; p.RemoteAdministration = true; p.OwnDevelopment = false; p.ArtificialIntelligence = false;
            return p;
        }
        static void LegacyProject()
        {
            Assessment original = FilledAssessment();
            string legacy = AssessmentWithoutPassport(original);
            Check(!legacy.Contains("Passport"), "The fixture was not a legacy project.");
            Assessment loaded = Store.Parse(legacy);
            Check(loaded.Version == 1 && loaded.Passport == null, "Legacy JSON was migrated to a guessed passport.");
            Check(AssessmentWithoutPassport(loaded) == legacy, "Legacy assessment facts changed.");
            Check(Json(Calc.Summary(loaded)) == Json(Calc.Summary(original)), "Legacy calculations changed.");
        }
        static void SingleArm()
        {
            SystemPassport p = SystemPassports.CreateSingleArm();
            Check(p.Version == 1 && p.TemplateId == "single-arm", "Wrong single-arm base identity.");
            Check(p.Workstations == 1 && p.Routers == 1 && p.Switches == 0 && p.Servers == 0, "Confirmed component counts were lost.");
            Check(p.Connection == "unknown" && p.Channel == "unknown" && p.ExternalSystem == "" && p.Protection == "", "An unknown connection or means of protection was guessed.");
            Check(!p.Domain.HasValue && !p.Internet.HasValue && !p.Wireless.HasValue && !p.Mobile.HasValue && !p.RemoteAdministration.HasValue && !p.OwnDevelopment.HasValue && !p.ArtificialIntelligence.HasValue, "A technology was assumed absent.");
            Check(SystemPassports.UnknownFields(p).Length == 11, "Missing questions were silently omitted.");
        }
        static void Roundtrip()
        {
            Assessment a = FilledAssessment(); a.Passport = FullPassport();
            Assessment copy = Store.Parse(Store.Serialize(a));
            Check(Store.Serialize(copy) == Store.Serialize(a), "Project passport did not roundtrip.");
            Check(copy.Passport.Domain == false && copy.Passport.Internet == true && copy.Passport.Wireless == null, "Tri-state technology facts were collapsed.");
            Check(Json(SystemPassports.ParseTemplate(SystemPassports.SerializeTemplate(a.Passport))) == Json(a.Passport), "Template passport did not roundtrip.");
        }
        static void SnapshotIsolation()
        {
            SystemPassport p = FullPassport();
            p.ExternalSystem = null; p.Connection = null; p.Channel = null; p.Protection = null; p.Notes = null;
            string before = Json(p);
            SystemPassport copy = SystemPassports.Snapshot(p);
            Check(Json(p) == before, "Snapshot normalized its source.");
            Check(copy.ExternalSystem == "" && copy.Connection == "unknown" && copy.Channel == "unknown", "Snapshot did not normalize the copy.");
            SystemPassports.SerializeTemplate(p); SystemPassports.Description(p); SystemPassports.Topology(p);
            SystemPassports.Components(p); SystemPassports.UnknownFields(p);
            Check(Json(p) == before, "Read-only helpers changed the source.");
            copy.Workstations = 2;
            Check(p.Workstations == 1, "Snapshot shares mutable state with its source.");
        }
        static void UnknownVsAbsent()
        {
            SystemPassport p = FullPassport();
            Check(SystemPassports.UnknownFields(p).SequenceEqual(new[] { "Wi-Fi" }), "Unknown Wi-Fi was not requested for assessment.");
            p.Wireless = false;
            Check(SystemPassports.UnknownFields(p).Length == 0, "Confirmed absence was still unknown.");
            Check(SystemPassports.BooleanText(null) != SystemPassports.BooleanText(false) && SystemPassports.BooleanText(false) != SystemPassports.BooleanText(true), "Distinct tri-state facts have identical labels.");
            p.Channel = "internet"; p.Internet = null;
            Check(!SystemPassports.UnknownFields(p).Contains("Доступ в интернет"), "Internet channel left its required access field unknown.");
            Check(p.Internet == null, "The read-only summary normalized its source.");
        }
        static void InternetChannel()
        {
            SystemPassport p = FullPassport(); p.Channel = "internet"; p.Internet = null;
            SystemPassports.Validate(p);
            Check(p.Internet == true, "Internet channel did not confirm internet access.");
            SystemPassport normalized = SystemPassports.ParseTemplate(SystemPassports.SerializeTemplate(p));
            Check(normalized.Channel == "internet" && normalized.Internet == true, "Internet fact did not roundtrip.");
            p.Internet = false; p.ExternalSystem = null; p.Connection = null;
            string before = Json(p);
            Reject(() => SystemPassports.Validate(p), "Conflicting internet fact accepted.");
            Check(Json(p) == before, "Conflicting internet fact partially normalized input.");
            Assessment a = FilledAssessment(); a.Passport = FullPassport();
            string assessmentBefore = Store.Serialize(a);
            Reject(() => SystemPassports.Apply(a, p), "Apply accepted a contradictory internet fact.");
            Check(Store.Serialize(a) == assessmentBefore && Json(p) == before, "Rejected Apply changed project or source facts.");
            Assessment conflictingProject = FilledAssessment(); conflictingProject.Passport = p;
            string projectJson = Store.Serialize(conflictingProject);
            Reject(() => Store.Parse(projectJson), "Project parse accepted a contradictory internet fact.");
            Check(Store.Serialize(conflictingProject) == projectJson, "Project parse changed source facts.");
            SystemPassport unknownInternet = FullPassport(); unknownInternet.Channel = "internet"; unknownInternet.Internet = null;
            Assessment normalizeProject = FilledAssessment(); normalizeProject.Passport = unknownInternet;
            Check(Store.Parse(Store.Serialize(normalizeProject)).Passport.Internet == true && unknownInternet.Internet == null, "Project parse did not normalize its own internet fact independently.");
            Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(SystemPassports.SerializeTemplate(FullPassport())) as Dictionary<string, object>;
            Dictionary<string, object> fields = root["Passport"] as Dictionary<string, object>;
            fields["Channel"] = "internet"; fields["Internet"] = false;
            string conflictingJson = Json(root);
            Reject(() => SystemPassports.ParseTemplate(conflictingJson), "Template parse accepted a contradictory internet fact.");
            Check(Json(root) == conflictingJson, "Template parse modified the caller's JSON facts.");
            fields["Internet"] = null;
            Check(SystemPassports.ParseTemplate(Json(root)).Internet == true, "Template parse did not confirm internet access implied by channel.");
            string path = Path.Combine(Path.GetTempPath(), "maturity-internet-conflict-" + Guid.NewGuid().ToString("N") + ".arm.json");
            try
            {
                SystemPassports.SaveTemplate(FullPassport(), path);
                string originalFile = File.ReadAllText(path, Encoding.UTF8);
                Reject(() => SystemPassports.SaveTemplate(p, path), "Save accepted a contradictory internet fact.");
                Check(File.ReadAllText(path, Encoding.UTF8) == originalFile && Json(p) == before, "Rejected save changed existing file or source.");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
            SystemPassport laterFailure = FullPassport(); laterFailure.Channel = "internet"; laterFailure.Internet = null;
            laterFailure.Notes = new string('x', 65537);
            string failedBefore = Json(laterFailure);
            Reject(() => SystemPassports.Validate(laterFailure), "Invalid late field accepted.");
            Check(Json(laterFailure) == failedBefore && laterFailure.Internet == null, "Internet access was normalized before the whole passport passed validation.");
        }
        static void Descriptions()
        {
            SystemPassport p = SystemPassports.CreateSingleArm();
            Check(SystemPassports.Components(p).Sum(c => c.Count) == 2, "Component list guessed extra devices.");
            Check(SystemPassports.Description(p).Contains("АРМ: 1") && SystemPassports.Description(p).Contains("Внешняя система: не указана"), "Description hid unknown external system.");
            string topology = SystemPassports.Topology(p);
            Check(topology.Contains("АРМ × 1") && topology.Contains("Роутер × 1") && !topology.Contains("Коммутатор ×") && !topology.Contains("Сервер ×"), "Topology includes absent devices.");
            Check(topology.Contains("фактические соединения требуют уточнения"), "A composition sketch was represented as confirmed wiring.");
            p.TemplateId = "my-base";
            Check(SystemPassports.TemplateName(p) == "Пользовательская конфигурация", "An unknown base was represented as the built-in single-arm base.");
        }
        static void PreserveAssessment()
        {
            Assessment a = FilledAssessment();
            string before = AssessmentWithoutPassport(a), result = Json(Calc.Summary(a));
            SystemPassport p = FullPassport();
            SystemPassports.Apply(a, p);
            Check(AssessmentWithoutPassport(a) == before, "Applying a passport changed assessment facts.");
            Check(Json(Calc.Summary(a)) == result, "Applying a passport changed maturity results.");
            p.ExternalSystem = "Changed after apply";
            Check(a.Passport.ExternalSystem != p.ExternalSystem, "Caller could modify the applied passport through the source reference.");
            SystemPassports.Apply(a, null);
            Check(a.Passport == null && AssessmentWithoutPassport(a) == before, "Removing the passport changed assessment facts.");
        }
        static void InvalidApply()
        {
            Assessment a = FilledAssessment(); a.Passport = FullPassport();
            string before = Store.Serialize(a);
            SystemPassport bad = SystemPassports.CreateSingleArm(); bad.Servers = -1;
            Reject(() => SystemPassports.Apply(a, bad), "Invalid passport was accepted.");
            Check(Store.Serialize(a) == before, "Rejected passport partially changed the project.");
            Check(bad.Servers == -1, "Invalid source was modified.");
        }
        static void AtomicNormalization()
        {
            SystemPassport p = FullPassport(); p.ExternalSystem = null; p.Connection = null; p.Servers = -1;
            string before = Json(p);
            Reject(() => SystemPassports.Validate(p), "Invalid passport was normalized instead of rejected.");
            Check(Json(p) == before, "Failed validation partially normalized input.");
            p.Servers = 0; SystemPassports.Validate(p);
            Check(p.ExternalSystem == "" && p.Connection == "unknown", "Valid null fields were not normalized.");
        }
        static void Boundaries()
        {
            SystemPassport p = FullPassport(); p.Workstations = 9999; p.Routers = 9999; p.Switches = 9999; p.Servers = 9999;
            SystemPassports.Validate(p); Check(p.Workstations == 9999, "Maximum supported count rejected.");
            foreach (Action<SystemPassport> mutate in new Action<SystemPassport>[] {
                q => q.Workstations = 0, q => q.Workstations = 10000, q => q.Routers = -1,
                q => q.Switches = 10000, q => q.Servers = -1, q => q.Version = 2,
                q => q.Connection = "browser-or-client", q => q.Channel = "public", q => q.Notes = new string('x', 65537)
            })
            {
                SystemPassport invalid = FullPassport(); mutate(invalid);
                Reject(() => SystemPassports.Validate(invalid), "Invalid passport boundary accepted.");
            }
        }
        static void TemplatePrivacy()
        {
            Assessment a = FilledAssessment(); a.Passport = FullPassport();
            string json = SystemPassports.SerializeTemplate(a.Passport);
            foreach (string marker in new[] { "ORGANIZATION_PRIVATE_SENTINEL", "ADDRESS_PRIVATE_SENTINEL", "PERSON_PRIVATE_SENTINEL", "DOCUMENT_PRIVATE_SENTINEL", "SYSTEM_PRIVATE_SENTINEL", "ASSESSMENT_PRIVATE_SENTINEL", "EVIDENCE_PRIVATE_SENTINEL" })
                Check(!json.Contains(marker), "Template leaked a private assessment field.");
            Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            Check(root.Count == 3 && root.ContainsKey("Passport") && !root.ContainsKey("Directions"), "Template is not a passport-only envelope.");
            Dictionary<string, object> passport = root["Passport"] as Dictionary<string, object>;
            Check(!passport.ContainsKey("Organization") && !passport.ContainsKey("Answers") && !passport.ContainsKey("Target") && !passport.ContainsKey("Date"), "Template schema contains assessment fields.");
            Assessment other = Assessment.Create(); other.Organization = "Another organization";
            SystemPassports.Apply(other, SystemPassports.ParseTemplate(json));
            Check(other.Organization == "Another organization" && other.Directions.All(d => d.Answers.All(v => !v.HasValue)), "Applying a template copied grades or organization data.");
        }
        static void TemplateRejections()
        {
            Reject(() => SystemPassports.ParseTemplate(Store.Serialize(FilledAssessment())), "A project was accepted as a template.");
            Reject(() => SystemPassports.ParseTemplate("{}"), "An untagged file was accepted.");
            Reject(() => SystemPassports.ParseTemplate("{"), "Broken JSON was accepted.");
            string json = SystemPassports.SerializeTemplate(FullPassport());
            Reject(() => SystemPassports.ParseTemplate(json.Replace("maturity-single-arm", "organization-profile")), "Wrong format accepted.");
            Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            root["Organization"] = "Private";
            Reject(() => SystemPassports.ParseTemplate(Json(root)), "Extra top-level organization field accepted.");
            root.Remove("Organization");
            Dictionary<string, object> fields = root["Passport"] as Dictionary<string, object>;
            fields["Answers"] = new[] { .5, 1.0 };
            Reject(() => SystemPassports.ParseTemplate(Json(root)), "Extra passport answer field accepted.");
            Reject(() => SystemPassports.ParseTemplate(new string('x', 1024 * 1024 + 1)), "Oversized template accepted.");
        }
        static void TemplateTypes()
        {
            foreach (KeyValuePair<string, object> invalid in new[] {
                new KeyValuePair<string, object>("Workstations", "2"), new KeyValuePair<string, object>("Workstations", 1.5),
                new KeyValuePair<string, object>("Wireless", "false"), new KeyValuePair<string, object>("ExternalSystem", 12),
                new KeyValuePair<string, object>("Routers", null), new KeyValuePair<string, object>("Version", true)
            })
            {
                Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(SystemPassports.SerializeTemplate(FullPassport())) as Dictionary<string, object>;
                ((Dictionary<string, object>)root["Passport"])[invalid.Key] = invalid.Value;
                Reject(() => SystemPassports.ParseTemplate(Json(root)), "Untyped value was silently converted.");
            }
        }
        static void TemplateFiles()
        {
            string path = Path.Combine(Path.GetTempPath(), "maturity-passport-" + Guid.NewGuid().ToString("N") + ".arm.json");
            try
            {
                SystemPassport p = FullPassport();
                SystemPassports.SaveTemplate(p, path);
                Check(Json(SystemPassports.LoadTemplate(path)) == Json(p), "Saved template did not roundtrip.");
                string before = File.ReadAllText(path, Encoding.UTF8);
                p.Workstations = 0;
                Reject(() => SystemPassports.SaveTemplate(p, path), "Invalid passport was saved.");
                Check(File.ReadAllText(path, Encoding.UTF8) == before, "Rejected save destroyed an existing template.");
                File.WriteAllText(path, "\ufeff" + before, new UTF8Encoding(false));
                Check(SystemPassports.LoadTemplate(path).ExternalSystem == "ППО АИСТ ГБД", "UTF-8 BOM template was not readable.");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        static void OptionalNull()
        {
            SystemPassports.Validate(null);
            Check(SystemPassports.Snapshot(null) == null, "Snapshot created an unknown passport.");
            Check(SystemPassports.Components(null).Length == 0 && SystemPassports.UnknownFields(null).Length == 0, "Null passport produced guessed composition.");
            Assessment a = Assessment.Create(); Store.Validate(a);
            Check(a.Version == 1 && a.Passport == null && Store.Parse(Store.Serialize(a)).Passport == null, "Optional passport broke project version-one compatibility.");
            Reject(() => SystemPassports.SerializeTemplate(null), "An absent passport was exported as a template.");
        }
    }
}
