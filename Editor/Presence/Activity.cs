namespace Zaphkiel.DiscordRichPresence
{
    internal sealed class Activity
    {
        public string Details;
        public string State;
        public long Start;
        public string LargeImage;
        public string LargeText;
        public string SmallImage;
        public string SmallText;

        public string ToJson()
        {
            var json = new JsonWriter().BeginObject()
                .OptionalProperty("details", Details)
                .OptionalProperty("state", State);
            if (Start > 0) json.BeginObject("timestamps").Property("start", Start).EndObject();
            json.BeginObject("assets")
                .OptionalProperty("large_image", LargeImage)
                .OptionalProperty("large_text", LargeText);
            if (!string.IsNullOrEmpty(SmallImage))
                json.Property("small_image", SmallImage).OptionalProperty("small_text", SmallText);
            return json.EndObject().EndObject().ToString();
        }
    }
}
