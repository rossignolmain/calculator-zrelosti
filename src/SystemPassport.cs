using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace MaturityApp
{
    // Technical facts are deliberately separate from maturity grades and scope decisions.
    // An unknown nullable flag must never be interpreted as confirmed absence.
    public class SystemPassport
    {
        public int Version = 1;
        public string TemplateId = "", ExternalSystem = "", Connection = "unknown", Channel = "unknown", Protection = "", Notes = "";
        public int Workstations = 1, Routers = 1, Switches = 0, Servers = 0;
        public bool? Domain = null, Internet = null, Wireless = null, Mobile = null,
            RemoteAdministration = null, OwnDevelopment = null, ArtificialIntelligence = null;
    }

    public class SystemComponent
    {
        public string Name, Purpose;
        public int Count;
    }

    public static class SystemPassports
    {
        const int MaximumFileLength = 1024 * 1024;
        const string TemplateFormat = "maturity-single-arm";
        static readonly string[] PassportFields = {
            "Version", "TemplateId", "ExternalSystem", "Connection", "Channel", "Protection", "Notes",
            "Workstations", "Routers", "Switches", "Servers", "Domain", "Internet", "Wireless", "Mobile",
            "RemoteAdministration", "OwnDevelopment", "ArtificialIntelligence"
        };
        static readonly string[] CountFields = { "Version", "Workstations", "Routers", "Switches", "Servers" };
        static readonly string[] FlagFields = { "Domain", "Internet", "Wireless", "Mobile", "RemoteAdministration", "OwnDevelopment", "ArtificialIntelligence" };

        public static SystemPassport CreateSingleArm()
        {
            // The user confirmed these four component counts for this base. Technologies remain unknown.
            return new SystemPassport { TemplateId = "single-arm", Workstations = 1, Routers = 1, Switches = 0, Servers = 0 };
        }

        public static void Validate(SystemPassport p)
        {
            if (p == null) return;
            if (p.Version != 1) throw new InvalidDataException("Неподдерживаемая версия паспорта системы.");
            if (p.Workstations < 1 || p.Workstations > 9999) throw new InvalidDataException("Количество АРМ должно быть от 1 до 9999.");
            if (p.Routers < 0 || p.Routers > 9999 || p.Switches < 0 || p.Switches > 9999 || p.Servers < 0 || p.Servers > 9999)
                throw new InvalidDataException("Количество устройств должно быть от 0 до 9999.");
            string connection = String.IsNullOrEmpty(p.Connection) ? "unknown" : p.Connection;
            string channel = String.IsNullOrEmpty(p.Channel) ? "unknown" : p.Channel;
            if (!new[] { "unknown", "browser", "client" }.Contains(connection)) throw new InvalidDataException("Неизвестный способ работы с внешней системой.");
            if (!new[] { "unknown", "internet", "vpn", "isolated" }.Contains(channel)) throw new InvalidDataException("Неизвестный канал подключения.");
            if (channel == "internet" && p.Internet == false) throw new InvalidDataException("Канал «Интернет» несовместим с отсутствием доступа в интернет.");
            foreach (string value in new[] { p.TemplateId, p.ExternalSystem, p.Protection, p.Notes })
                if (value != null && value.Length > 65536) throw new InvalidDataException("Поле паспорта слишком длинное.");
            // Commit normalization only after every field has passed validation.
            p.TemplateId = p.TemplateId ?? ""; p.ExternalSystem = p.ExternalSystem ?? "";
            p.Protection = p.Protection ?? ""; p.Notes = p.Notes ?? "";
            p.Connection = connection; p.Channel = channel;
            if (channel == "internet") p.Internet = true;
        }

        public static SystemPassport Snapshot(SystemPassport p)
        {
            if (p == null) return null;
            SystemPassport copy = new SystemPassport {
                Version = p.Version, TemplateId = p.TemplateId, ExternalSystem = p.ExternalSystem,
                Connection = p.Connection, Channel = p.Channel, Protection = p.Protection, Notes = p.Notes,
                Workstations = p.Workstations, Routers = p.Routers, Switches = p.Switches, Servers = p.Servers,
                Domain = p.Domain, Internet = p.Internet, Wireless = p.Wireless, Mobile = p.Mobile,
                RemoteAdministration = p.RemoteAdministration, OwnDevelopment = p.OwnDevelopment,
                ArtificialIntelligence = p.ArtificialIntelligence
            };
            Validate(copy);
            return copy;
        }

        public static void Apply(Assessment a, SystemPassport p)
        {
            if (a == null) throw new ArgumentNullException("a");
            SystemPassport copy = Snapshot(p);
            a.Passport = copy;
        }

        public static string TemplateName(SystemPassport p)
        {
            if (p == null || String.IsNullOrEmpty(p.TemplateId)) return "Не выбран";
            return p.TemplateId == "single-arm" ? "Один АРМ и роутер" : "Пользовательская конфигурация";
        }
        public static string BooleanText(bool? value) { return !value.HasValue ? "Не уточнено" : value.Value ? "Да" : "Нет"; }
        public static string ConnectionText(string value)
        {
            return value == "browser" ? "Через браузер" : value == "client" ? "Через установленную программу" : "Не уточнено";
        }
        public static string ChannelText(string value)
        {
            return value == "internet" ? "Интернет" : value == "vpn" ? "VPN / защищённый канал" : value == "isolated" ? "Изолированное подключение" : "Не уточнено";
        }
        public static string Description(SystemPassport p)
        {
            if (p == null) return "Паспорт системы не заполнен";
            SystemPassport q = Snapshot(p);
            return String.Join("; ", Components(q).Select(c => c.Name + ": " + c.Count.ToString(CultureInfo.InvariantCulture))) +
                ". Внешняя система: " + (String.IsNullOrWhiteSpace(q.ExternalSystem) ? "не указана" : q.ExternalSystem) +
                ". Способ работы: " + ConnectionText(q.Connection) + ". Канал: " + ChannelText(q.Channel) + ".";
        }
        public static string Topology(SystemPassport p)
        {
            if (p == null) return "Не указана";
            SystemPassport q = Snapshot(p);
            List<string> parts = new List<string>();
            parts.Add("АРМ × " + q.Workstations.ToString(CultureInfo.InvariantCulture));
            if (q.Switches > 0) parts.Add("Коммутатор × " + q.Switches.ToString(CultureInfo.InvariantCulture));
            if (q.Routers > 0) parts.Add("Роутер × " + q.Routers.ToString(CultureInfo.InvariantCulture));
            if (q.Servers > 0) parts.Add("Сервер × " + q.Servers.ToString(CultureInfo.InvariantCulture));
            if (!String.IsNullOrWhiteSpace(q.ExternalSystem)) parts.Add("Внешняя система: " + q.ExternalSystem);
            return String.Join(" → ", parts) + ". Схема по составу; фактические соединения требуют уточнения.";
        }
        public static SystemComponent[] Components(SystemPassport p)
        {
            if (p == null) return new SystemComponent[0];
            SystemPassport q = Snapshot(p);
            return new[] {
                new SystemComponent { Name = "АРМ", Count = q.Workstations, Purpose = "Рабочее место специалиста" },
                new SystemComponent { Name = "Роутеры", Count = q.Routers, Purpose = "Подключение к сети" },
                new SystemComponent { Name = "Коммутаторы", Count = q.Switches, Purpose = "Соединение локальных устройств" },
                new SystemComponent { Name = "Серверы", Count = q.Servers, Purpose = "Серверные функции" }
            };
        }
        public static string[] UnknownFields(SystemPassport p)
        {
            if (p == null) return new string[0];
            SystemPassport q = Snapshot(p);
            List<string> fields = new List<string>();
            if (String.IsNullOrWhiteSpace(q.ExternalSystem)) fields.Add("Внешняя информационная система");
            if (q.Connection == "unknown") fields.Add("Способ работы с внешней системой");
            if (q.Channel == "unknown") fields.Add("Канал подключения");
            if (!q.Domain.HasValue) fields.Add("Домен");
            if (!q.Internet.HasValue) fields.Add("Доступ в интернет");
            if (!q.Wireless.HasValue) fields.Add("Wi-Fi");
            if (!q.Mobile.HasValue) fields.Add("Мобильные устройства");
            if (!q.RemoteAdministration.HasValue) fields.Add("Удалённое администрирование");
            if (!q.OwnDevelopment.HasValue) fields.Add("Собственная разработка ПО");
            if (!q.ArtificialIntelligence.HasValue) fields.Add("Искусственный интеллект");
            if (String.IsNullOrWhiteSpace(q.Protection)) fields.Add("Используемые средства защиты");
            return fields.ToArray();
        }

        public static string SerializeTemplate(SystemPassport p)
        {
            if (p == null) throw new InvalidDataException("Сначала заполните паспорт системы.");
            SystemPassport copy = Snapshot(p);
            return Serializer().Serialize(new Dictionary<string, object> {
                { "Format", TemplateFormat }, { "Version", 1 }, { "Passport", copy }
            });
        }
        public static SystemPassport ParseTemplate(string json)
        {
            if (String.IsNullOrWhiteSpace(json) || json.Length > MaximumFileLength) throw new InvalidDataException("Пустой или слишком большой файл шаблона АРМ.");
            try
            {
                JavaScriptSerializer serializer = Serializer();
                Dictionary<string, object> root = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (root == null || root.Count != 3 || !root.ContainsKey("Format") || !root.ContainsKey("Version") || !root.ContainsKey("Passport") ||
                    !(root["Format"] is string) || (string)root["Format"] != TemplateFormat || !(root["Version"] is int) || (int)root["Version"] != 1)
                    throw new InvalidDataException("Нужен шаблон АРМ версии 1 (.arm.json).");
                Dictionary<string, object> fields = root["Passport"] as Dictionary<string, object>;
                if (fields == null || fields.Keys.Any(k => !PassportFields.Contains(k)))
                    throw new InvalidDataException("Шаблон АРМ может содержать только технический паспорт системы.");
                foreach (KeyValuePair<string, object> field in fields)
                {
                    bool allowed = CountFields.Contains(field.Key) ? field.Value is int :
                        FlagFields.Contains(field.Key) ? field.Value == null || field.Value is bool : field.Value == null || field.Value is string;
                    if (!allowed) throw new InvalidDataException("Некорректный тип поля шаблона: " + field.Key + ".");
                }
                SystemPassport p = serializer.Deserialize<SystemPassport>(serializer.Serialize(fields));
                Validate(p);
                return p;
            }
            catch (InvalidDataException) { throw; }
            catch (Exception ex)
            {
                if (!(ex is ArgumentException) && !(ex is InvalidOperationException)) throw;
                throw new InvalidDataException("Не удалось прочитать шаблон АРМ: некорректный JSON.", ex);
            }
        }
        public static SystemPassport LoadTemplate(string path)
        {
            if (new FileInfo(path).Length > MaximumFileLength) throw new InvalidDataException("Файл шаблона АРМ слишком большой.");
            string json = File.ReadAllText(path, new UTF8Encoding(false, true));
            return ParseTemplate(json);
        }
        public static void SaveTemplate(SystemPassport p, string path)
        {
            // Build and validate before touching the destination; only passport fields are serialized.
            string json = SerializeTemplate(p);
            if (Encoding.UTF8.GetByteCount(json) > MaximumFileLength) throw new InvalidDataException("Файл шаблона АРМ слишком большой.");
            string full = Path.GetFullPath(path), folder = Path.GetDirectoryName(full);
            Directory.CreateDirectory(folder);
            string temp = Path.Combine(folder, "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(full)) File.Replace(temp, full, null); else File.Move(temp, full);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = MaximumFileLength, RecursionLimit = 32 }; }
    }
}
