using Newtonsoft.Json;

namespace Dictio
{
    public static class Settings
    {
        public class Model
        {
            public string BroadcasterID { get; set; } = "441477997";
            public bool UseTTS { get; set; }
        }

        private static readonly string _sampleJsonFilePath = "./settings.json";

        public static Model? ReadSettings()
        {
            if (!File.Exists(_sampleJsonFilePath))
                return null;

            using StreamReader reader = new(_sampleJsonFilePath);
            string json = reader.ReadToEnd();

            var settings = JsonConvert.DeserializeObject<Model>(json);
            if (settings == null)
                throw new InvalidOperationException("Failed to deserialize settings file.");

            return settings;
        }

        public static void WriteSettings(Model settings)
        {
            string json = JsonConvert.SerializeObject(settings);
            File.WriteAllText(_sampleJsonFilePath, json);
        }
    }
}
