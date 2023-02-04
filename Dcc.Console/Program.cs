using System.Text;
using YamlDotNet.Serialization;

namespace MyApp;

internal class Program
{
    const string SpecUrl = "https://raw.githubusercontent.com/compose-spec/compose-spec/master/schema/compose-spec.json";

    private static IDictionary<object, object> _main = new Dictionary<object, object>();
    private static IDictionary<object, object> _secrets = new Dictionary<object, object>();
    private static IDictionary<object, object> _services = new Dictionary<object, object>();
    private static IDictionary<object, object> _networks = new Dictionary<object, object>();
    private static IDictionary<object, object> _volumes = new Dictionary<object, object>();


    static Program()
    {
        _main.Add("version", "3.9");
    }

    static async Task Main(string[] args)
    {
        using (HttpClient hc = new())
        {
            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(SpecUrl),
                    Content = new StringContent("some json", Encoding.UTF8),
                };

                var response = await hc.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                Console.Write("Please input DCC project location: ");
                //var projectPath = Console.ReadLine();
                var projectPath = "/Users/joaogabrielmiranda/dev/docker-environment";

                Console.WriteLine($"The DCC project path is: {projectPath}");

                var files = Directory.EnumerateFiles(projectPath!, "*.*", SearchOption.AllDirectories);

                var yamlFiles = files.Where(x => x.EndsWith(".yml") || x.EndsWith(".yaml")).ToList();
                var envFiles = files.Where(x => x.EndsWith(".env")).ToList();

                var mainFile = yamlFiles
                    .SingleOrDefault(x =>
                    x.Split('/').Any(y => y == "main.yaml") ||
                    x.Split('/').Any(y => y == "main.yml") ||
                    x.Split('\\').Any(y => y == "main.yaml") ||
                    x.Split('\\').Any(y => y == "main.yml"));

                if (string.IsNullOrEmpty(mainFile))
                    throw new Exception("No main file was found.");

                yamlFiles.ToList().Remove(mainFile);

                var mainFileData = await File.ReadAllLinesAsync(mainFile);
                var mainFileText = string.Join("\n", mainFileData);
                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties().Build();


                AssignToSection(deserializer.Deserialize<IDictionary<string, object>>(mainFileText));

                IDictionary<string, object> deserializedYamls = new Dictionary<string, object>();
                foreach (var file in yamlFiles)
                {

                    var fileData = await File.ReadAllLinesAsync(file);
                    var textToDeserialize = string.Join("\n", fileData);

                    if (file.Contains("services"))
                        textToDeserialize = $"{mainFileText}\n\n{textToDeserialize}";

                    var deserializedYaml = deserializer.Deserialize<IDictionary<string, object>>(textToDeserialize);

                    if (deserializedYaml is not null)
                        AssignToSection(deserializedYaml);
                }

                var serializer = new SerializerBuilder().Build();

                BuildSerializableData();

                var result = serializer.Serialize(_main);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }

    private static void AssignToSection(IDictionary<string, object> sectionDict)
    {
        foreach(var section in sectionDict)
        {
            var values = (IDictionary<object, object>)section.Value!;

            switch (section.Key)
            {
                case "secrets":
                    foreach(var value in values)
                        _secrets.Add(value.Key, value.Value);
                    break;
                case "services":
                    foreach (var value in values)
                        _services.Add(value.Key, value.Value);
                    break;
                case "networks":
                    foreach (var value in values)
                        _networks.Add(value.Key, value.Value);
                    break;
                case "volumes":
                    foreach (var value in values)
                        _volumes.Add(value.Key, value.Value);
                    break;
                default:
                    if (values is not null)
                    {
                        if (_main.ContainsKey(section.Key))
                        {
                            foreach(var value in values)
                            {
                                var mainValues = (IDictionary<object, object>)_main[section.Key];

                                if (!mainValues.ContainsKey(value.Key))
                                    mainValues.Add(value.Key, value.Value);


                            }
                        }
                        else
                        {
                            _main.Add(section.Key, values);
                        }
                    }
                    break;
            }
        }
    }

    private static void BuildSerializableData()
    {
        _main.Add("secrets", _secrets);
        _main.Add("services", _services);
        _main.Add("networks", _networks);
        _main.Add("volumes", _volumes);
    }
}