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
            Case("projects without a description load without inventing one", LegacyProject);
            Case("1.4-1.6 single-ARM descriptions migrate to lists", Migration);
            Case("free-text protection from old projects is kept as one row", FreeTextProtection);
            Case("lists and unknown/absent/present facts survive saving", Roundtrip);
            Case("snapshot never shares rows with the caller", SnapshotIsolation);
            Case("an Internet channel means Internet access", InternetChannel);
            Case("contradictions and unknown values are rejected", Rejections);
            Case("unknown fields reflect missing lists and facts", UnknownFields);
            Case("descriptions use only what the user entered", Descriptions);
            Case("changing the description never changes maturity data", PreserveAssessment);
            Case("legacy .arm.json files load; foreign files do not", LegacyTemplates);
            Case("object type sets the strictest recommended target and requirements (п. 11)", ObjectTargets);
            Case("plan rows fall back to plan defaults", PlanFallback);
            Case("segments, boundary device, Wi-Fi isolation and personal data details are stored and validated", DetailedFields);
            if (errors.Count > 0) throw new InvalidOperationException("System passport verification failed: " + String.Join("; ", errors));
        }

        static void Case(string name, Action action)
        {
            TestCount++;
            try { action(); }
            catch (Exception ex) { errors.Add(name + ": " + ex.GetType().Name + ": " + ex.Message); }
        }
        static void Check(bool condition, string message) { AssertionCount++; if (!condition) throw new InvalidOperationException(message); }
        static void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, message);
        }
        static JavaScriptSerializer S() { return new JavaScriptSerializer(); }
        static string Answers(Assessment a) { return String.Join("|", a.Directions.Select(d => d.Included + ":" + d.Target + ":" + String.Join(",", d.Answers.Select(x => x.HasValue ? x.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "_")))); }
        static SystemPassport Filled()
        {
            SystemPassport p = SystemPassports.CreateEmpty();
            p.Components = new[] { new PassportComponent { Kind = "ПЛК / контроллер", Count = 40, Note = "цех 2" }, new PassportComponent { Kind = "Сервер SCADA", Count = 2 } };
            p.Connections = new[] { new PassportConnection { System = "Корпоративная сеть", Method = "exchange", Channel = "local", Note = "через межсетевой экран" } };
            p.Tools = new[] { new PassportTool { Class = "fw", Product = "Межсетевой экран (пример)", Note = "сертификат № 0000" } };
            p.Domain = false; p.Wireless = true; p.Internet = null;
            return p;
        }

        static void LegacyProject()
        {
            Assessment a = Assessment.Create();
            Dictionary<string, object> fields = S().DeserializeObject(Store.Serialize(a)) as Dictionary<string, object>;
            fields.Remove("Passport"); fields.Remove("Objects"); fields.Remove("PlanItems");
            Assessment opened = Store.Parse(S().Serialize(fields));
            Check(opened.Passport == null && opened.Objects.Length == 0 && opened.PlanItems.Length == 0, "Opening an old project created a description, object types or plan rows.");
        }
        static void Migration()
        {
            string json = "{\"Version\":1,\"TemplateId\":\"single-arm\",\"ExternalSystem\":\"Внешняя ИС\",\"Connection\":\"client\",\"Channel\":\"isolated\",\"Protection\":\"Криптозащита / сеть: ViPNet; Защита от НСД: не уточнено; Антивирус: Dr.Web; СДЗ: ПАК «Соболь»\",\"Notes\":\"N\",\"Workstations\":1,\"Routers\":1,\"Switches\":0,\"Servers\":0,\"Domain\":null,\"Internet\":false,\"Wireless\":false,\"Mobile\":null,\"RemoteAdministration\":null,\"OwnDevelopment\":null,\"ArtificialIntelligence\":null}";
            SystemPassport p = S().Deserialize<SystemPassport>(json); SystemPassports.Validate(p);
            Check(p.Version == 2 && p.Workstations == 0 && p.ExternalSystem == "" && p.Protection == "", "Legacy fields were not cleared after migration.");
            Check(p.Components.Length == 2 && p.Components[0].Kind == "АРМ пользователя" && p.Components[0].Count == 1 && p.Components[1].Kind == "Маршрутизатор", "Equipment counts did not become component rows.");
            Check(p.Connections.Length == 1 && p.Connections[0].System == "Внешняя ИС" && p.Connections[0].Method == "client" && p.Connections[0].Channel == "dedicated", "The external system did not become a connection row.");
            Check(p.Tools.Length == 3 && p.Tools.Any(t => t.Class == "crypto" && t.Product == "ViPNet") && p.Tools.Any(t => t.Class == "av" && t.Product == "Dr.Web") && p.Tools.Any(t => t.Class == "boot") && !p.Tools.Any(t => t.Class == "nsd"), "Structured protection text did not become tool rows (unknown entries must be skipped).");
            Check(p.Internet == false && p.Wireless == false && !p.Mobile.HasValue && p.Notes == "N", "Facts or notes changed during migration.");
            Assessment a = Assessment.Create(); a.Passport = p; Assessment back = Store.Parse(Store.Serialize(a));
            Check(back.Passport.Version == 2 && back.Passport.Components.Length == 2, "A migrated description did not survive saving.");
        }
        static void FreeTextProtection()
        {
            SystemPassport p = new SystemPassport { Version = 1, Protection = "Антивирус и межсетевой экран, версии уточняются" }; SystemPassports.Validate(p);
            Check(p.Tools.Length == 1 && p.Tools[0].Class == "other" && p.Tools[0].Product.StartsWith("Антивирус и межсетевой"), "Free text protection was lost or split.");
            SystemPassport empty = new SystemPassport { Version = 1 }; SystemPassports.Validate(empty);
            Check(empty.Components.Length == 0 && empty.Connections.Length == 0 && empty.Tools.Length == 0, "An empty legacy description invented rows.");
        }
        static void Roundtrip()
        {
            Assessment a = Assessment.Create(); a.Passport = Filled(); a.Passport.NoConnections = false;
            Assessment back = Store.Parse(Store.Serialize(a));
            SystemPassport p = back.Passport;
            Check(p.Components.Length == 2 && p.Components[0].Note == "цех 2" && p.Connections[0].Note == "через межсетевой экран" && p.Tools[0].Note == "сертификат № 0000", "Rows lost data on saving.");
            Check(p.Domain == false && p.Wireless == true && !p.Internet.HasValue, "Unknown, absent and present facts were not preserved.");
        }
        static void SnapshotIsolation()
        {
            SystemPassport p = Filled(); SystemPassport copy = SystemPassports.Snapshot(p);
            copy.Components[0].Count = 1; copy.Tools = new PassportTool[0];
            Check(p.Components[0].Count == 40 && p.Tools.Length == 1, "Snapshot shares rows with the original.");
        }
        static void InternetChannel()
        {
            SystemPassport p = Filled(); p.Internet = false; p.Connections[0].Channel = "internet"; SystemPassports.Validate(p);
            Check(p.Internet == true, "An Internet channel did not set Internet access.");
        }
        static void Rejections()
        {
            Reject(() => { SystemPassport p = Filled(); p.NoConnections = true; SystemPassports.Validate(p); }, "\"No connections\" with listed connections was accepted.");
            Reject(() => { SystemPassport p = Filled(); p.Connections[0].Method = "telepathy"; SystemPassports.Validate(p); }, "An unknown method was accepted.");
            Reject(() => { SystemPassport p = Filled(); p.Connections[0].Channel = "pigeon"; SystemPassports.Validate(p); }, "An unknown channel was accepted.");
            Reject(() => { SystemPassport p = Filled(); p.Tools[0].Class = "magic"; SystemPassports.Validate(p); }, "An unknown tool class was accepted.");
            Reject(() => { SystemPassport p = Filled(); p.Components[0].Count = -1; SystemPassports.Validate(p); }, "A negative count was accepted.");
            Reject(() => { SystemPassport p = Filled(); p.Version = 3; SystemPassports.Validate(p); }, "An unknown version was accepted.");
            Reject(() => { SystemPassport p = new SystemPassport { Version = 1, Channel = "carrier" }; SystemPassports.Validate(p); }, "An unknown legacy channel was accepted.");
        }
        static void UnknownFields()
        {
            string[] empty = SystemPassports.UnknownFields(SystemPassports.CreateEmpty());
            Check(empty.Contains("Состав объекта") && empty.Any(x => x.StartsWith("Внешние взаимодействия")) && empty.Contains("Средства защиты информации") && empty.Contains("Удалённый доступ"), "Empty description does not list what is missing.");
            SystemPassport p = Filled(); p.Mobile = p.RemoteAdministration = p.OwnDevelopment = p.ArtificialIntelligence = false; p.Internet = false; p.WirelessIsolated = true;
            Check(SystemPassports.UnknownFields(p).Length == 0, "A complete description still reports unknown fields: " + String.Join("; ", SystemPassports.UnknownFields(p)));
            SystemPassport none = SystemPassports.CreateEmpty(); none.NoConnections = true;
            Check(!SystemPassports.UnknownFields(none).Any(x => x.StartsWith("Внешние взаимодействия")), "\"No connections\" is still reported as unknown.");
        }
        static void Descriptions()
        {
            string text = SystemPassports.Description(Filled());
            Check(text.Contains("ПЛК / контроллер — 40") && text.Contains("Корпоративная сеть") && !text.Contains("АРМ") && !text.Contains("Роутер"), "Description added components the user did not enter: " + text);
            SystemPassport none = SystemPassports.CreateEmpty(); none.NoConnections = true;
            Check(SystemPassports.Description(none).Contains("внешних взаимодействий нет"), "Absence of connections is not stated.");
        }
        static void PreserveAssessment()
        {
            Assessment a = Assessment.Create(); a.Category = 2; foreach (DirectionAssessment d in a.Directions) { d.Target = 2; d.Answers = new double?[] { 1, .5, 0, null, 1, .3, 0, 1 }; } a.Directions[3].Included = false;
            string before = Answers(a);
            SystemPassports.Apply(a, Filled()); a.Passport = null; SystemPassports.Apply(a, SystemPassports.CreateEmpty());
            Check(Answers(a) == before && a.Category == 2, "Changing the description changed answers, scope, goals or category.");
        }
        static void LegacyTemplates()
        {
            string json = "{\"Format\":\"maturity-single-arm\",\"Version\":1,\"Passport\":{\"Version\":1,\"Workstations\":3,\"Routers\":0,\"Switches\":1,\"Servers\":1,\"ExternalSystem\":\"\",\"Connection\":\"unknown\",\"Channel\":\"unknown\",\"Protection\":\"\"}}";
            SystemPassport p = SystemPassports.ParseTemplate(json);
            Check(p.Components.Length == 3 && p.Components.Any(c => c.Kind == "Коммутатор") && p.Connections.Length == 0, "A legacy basis file did not migrate.");
            Reject(() => SystemPassports.ParseTemplate(Store.Serialize(Assessment.Create())), "A project file was accepted as a basis.");
            Reject(() => SystemPassports.ParseTemplate("{\"Version\":1,\"Organization\":\"X\"}"), "An organization profile was accepted as a basis.");
            Reject(() => SystemPassports.ParseTemplate("{\"Format\":\"maturity-single-arm\",\"Version\":1,\"Passport\":{\"Version\":2,\"Organization\":\"leak\"}}"), "A basis with foreign fields was accepted.");
        }
        static void ObjectTargets()
        {
            Check(ObjectCatalog.MinimumTarget(new ProtectionObject { Kind = "gis", Level = 3 }) == 1 && ObjectCatalog.MinimumTarget(new ProtectionObject { Kind = "gis", Level = 1 }) == 3, "ГИС class mapping is wrong.");
            Check(ObjectCatalog.MinimumTarget(new ProtectionObject { Kind = "ispdn", Level = 4 }) == 1 && ObjectCatalog.MinimumTarget(new ProtectionObject { Kind = "ispdn", Level = 3 }) == 1 && ObjectCatalog.MinimumTarget(new ProtectionObject { Kind = "ispdn", Level = 2 }) == 2 && ObjectCatalog.MinimumTarget(new ProtectionObject { Kind = "ispdn", Level = 1 }) == 3, "ИСПДн level mapping is wrong.");
            Check(ObjectCatalog.MinimumTarget(new ProtectionObject { Kind = "kii", Level = 2 }) == 2 && ObjectCatalog.MinimumTarget(new ProtectionObject { Kind = "asutp" }) == 1, "КИИ / АСУ ТП mapping is wrong.");
            Assessment a = Assessment.Create(); a.Directions[5].Target = 4;
            a.Objects = new[] { new ProtectionObject { Kind = "gis", Level = 3 }, new ProtectionObject { Kind = "ispdn", Level = 2 } };
            ObjectCatalog.ApplyToAssessment(a);
            Check(a.Category == 2 && a.Directions.All(d => d.Target >= 2) && a.Directions[5].Target == 4, "The strictest object did not set the minimum target, or a raised goal was lowered.");
            Check(a.Requirements.SequenceEqual(new[] { "fstec117", "fstec21" }), "Requirements were not ticked from object types.");
            a.Requirements = new[] { "fstec117" }; a.Objects[0].Level = 2; ObjectCatalog.ApplyToAssessment(a);
            Check(a.Requirements.Contains("fstec21") && a.Category == 2, "Changing an object did not keep the requirements consistent.");
            Assessment unknown = Assessment.Create(); unknown.Objects = new[] { new ProtectionObject { Kind = "kii", Level = 0 } }; ObjectCatalog.ApplyToAssessment(unknown);
            Check(unknown.Category == 0, "An object without class did not leave the category undetermined.");
            Reject(() => { Assessment bad = Assessment.Create(); bad.Objects = new[] { new ProtectionObject { Kind = "gis", Level = 4 } }; Store.Parse(Store.Serialize(bad)); }, "ГИС class 4 was accepted.");
            Reject(() => { Assessment bad = Assessment.Create(); bad.Objects = new[] { new ProtectionObject { Kind = "ufo" } }; Store.Parse(Store.Serialize(bad)); }, "An unknown object type was accepted.");
            Assessment saved = Store.Parse(Store.Serialize(a));
            Check(saved.Objects.Length == 2 && saved.Objects[1].Kind == "ispdn" && saved.Objects[1].Level == 2, "Object types did not survive saving.");
        }
        static void DetailedFields()
        {
            Assessment a = Assessment.Create(); a.Passport = Filled();
            a.Passport.Segments = new[] { new PassportSegment { Name = "Технологический сегмент", Purpose = "SCADA, ПЛК", Note = "VLAN 20" } };
            a.Passport.Connections[0].Via = "Межсетевой экран"; a.Passport.Wireless = true; a.Passport.WirelessIsolated = true;
            a.Objects = new[] { new ProtectionObject { Kind = "ispdn", Level = 2, PdCategories = new[] { "other", "special" }, Subjects = "both", Volume = "gt100k" } };
            Assessment back = Store.Parse(Store.Serialize(a));
            Check(back.Passport.Segments.Length == 1 && back.Passport.Segments[0].Note == "VLAN 20" && back.Passport.Connections[0].Via == "Межсетевой экран" && back.Passport.WirelessIsolated == true, "Segments, boundary device or Wi-Fi isolation were lost.");
            Check(back.Objects[0].PdCategories.SequenceEqual(new[] { "special", "other" }) && back.Objects[0].Subjects == "both" && back.Objects[0].Volume == "gt100k", "Personal data details were lost or not ordered.");
            string text = ObjectCatalog.Text(back.Objects[0]);
            Check(text.Contains("специальные, иные") && text.Contains("100 000"), "Personal data details are missing from the object text: " + text);
            Check(SystemPassports.Description(back.Passport).Contains("через: Межсетевой экран") && SystemPassports.Description(back.Passport).Contains("Технологический сегмент"), "Description misses the boundary device or segments.");
            SystemPassport noWifi = Filled(); noWifi.Wireless = false; noWifi.WirelessIsolated = true; SystemPassports.Validate(noWifi);
            Check(!noWifi.WirelessIsolated.HasValue, "Isolation of absent Wi-Fi was kept as a fact.");
            SystemPassport unknownIsolation = Filled(); unknownIsolation.Wireless = true;
            Check(SystemPassports.UnknownFields(unknownIsolation).Contains("Отделение Wi-Fi от сегментов объекта"), "Unknown Wi-Fi isolation is not reported.");
            Reject(() => { Assessment bad = Assessment.Create(); bad.Objects = new[] { new ProtectionObject { Kind = "gis", Level = 3, Volume = "gt100k" } }; Store.Parse(Store.Serialize(bad)); }, "Personal data details were accepted for a non-ИСПДн object.");
            Reject(() => { Assessment bad = Assessment.Create(); bad.Objects = new[] { new ProtectionObject { Kind = "ispdn", PdCategories = new[] { "secret" } } }; Store.Parse(Store.Serialize(bad)); }, "An unknown personal data category was accepted.");
            Assessment template = Assessment.Create(); AnswerTemplates.Apply(template, AnswerTemplates.Create(back, "Т"));
            Check(template.Objects[0].Volume == "gt100k" && template.Passport.Segments.Length == 1, "A template lost personal data details or segments.");
        }
        static void PlanFallback()
        {
            Assessment a = Assessment.Create(); a.PlanResponsible = "Отдел ИБ"; a.PlanDeadline = "до 01.06.2027";
            a.PlanItems = new[] { new PlanItem { Key = "1:0:assess", Responsible = "Иванов" } };
            PlanItem own = ReportFacts.PlanFor(a, "1:0:assess"), other = ReportFacts.PlanFor(a, "2:1:assess");
            Check(own.Responsible == "Иванов" && own.Deadline == "до 01.06.2027" && other.Responsible == "Отдел ИБ", "Plan rows do not fall back to defaults correctly.");
            Assessment back = Store.Parse(Store.Serialize(a));
            Check(back.PlanItems.Length == 1 && back.PlanItems[0].Responsible == "Иванов", "Plan rows did not survive saving.");
        }
    }
}
