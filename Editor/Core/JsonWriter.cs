using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Zaphkiel.DiscordRichPresence
{
    internal sealed class JsonWriter
    {
        readonly StringBuilder builder = new StringBuilder();
        readonly Stack<bool> hasMembers = new Stack<bool>();

        public JsonWriter BeginObject()
        {
            builder.Append('{');
            hasMembers.Push(false);
            return this;
        }

        public JsonWriter BeginObject(string name)
        {
            Key(name);
            return BeginObject();
        }

        public JsonWriter EndObject()
        {
            hasMembers.Pop();
            builder.Append('}');
            return this;
        }

        public JsonWriter Property(string name, string value)
        {
            Key(name);
            AppendString(value);
            return this;
        }

        public JsonWriter Property(string name, long value)
        {
            Key(name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public JsonWriter OptionalProperty(string name, string value) =>
            string.IsNullOrEmpty(value) ? this : Property(name, value);

        public JsonWriter RawProperty(string name, string json)
        {
            Key(name);
            builder.Append(json);
            return this;
        }

        public override string ToString() => builder.ToString();

        void Key(string name)
        {
            if (hasMembers.Pop()) builder.Append(',');
            hasMembers.Push(true);
            AppendString(name);
            builder.Append(':');
        }

        void AppendString(string value)
        {
            builder.Append('"');
            foreach (char c in value ?? "")
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    default:
                        if (c < ' ') builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
