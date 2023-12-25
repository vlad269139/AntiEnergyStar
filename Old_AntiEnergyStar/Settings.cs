using AntiEnergyStar.Interop;
using System.Security;
//using System.Text.Json;
//using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;
using System.IO;
using System;
using System.Windows;

namespace AntiEnergyStar
{
    internal class Settings
    {

        public static Settings Instance => _instance ?? (_instance = Load());
    private static Settings _instance;



        public uint Delay { get; set; } = 60;
        public string[] Processes { get; set; } = new string[0];

        private static Settings Load()
        {


            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

            if(!File.Exists(path))
            {
                MessageBox.Show("File 'settings.json' was not found.");
                Environment.Exit(1);
            }

            string json = File.ReadAllText(path);



            var settings = new JsonLoadSettings()
            {
                CommentHandling = CommentHandling.Ignore
            };
            var jSettings = JObject.Parse(json, settings);

            try
            {
                var result = jSettings.ToObject<Settings>();
                return result;
            }
            catch(Newtonsoft.Json.JsonReaderException e)
            {
                MessageBox.Show("Failed to parse settings.json! Please check your settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Unhandled error:\r\n{e}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            return new Settings();



//            var options = new JsonSerializerOptions
//            {
//                ReadCommentHandling = JsonCommentHandling.Skip,
//                // This `options` object overwrites the generated default options, 
//                // so we need to specify `PropertyNamingPolicy` again here.
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//            };
//            try
//            {
//                string json = File.ReadAllText(path);
//                return JsonSerializer.Deserialize(json,
//                    new SettingsJsonContext(options).Settings)!;
//            }
//            catch (Exception ex) when (
//                ex is IOException
//                || ex is SecurityException
//                || ex is UnauthorizedAccessException
//            )
//            {
//                // Show message box to the user since the console is hidden in InvisibleRelease mode.
//                Win32Api.MessageBox(IntPtr.Zero,
//                    "IO Error occurred when reading settings.json!",
//                    "EnergyStar Error", Win32Api.MB_ICONERROR | Win32Api.MB_OK);
//                Environment.Exit(1);
//            }
//            catch (JsonException)
//            {
//                Win32Api.MessageBox(IntPtr.Zero,
//                   "Failed to parse settings.json! Please check your settings.",
//                   "EnergyStar Error", Win32Api.MB_ICONERROR | Win32Api.MB_OK);
//                Environment.Exit(1);
//            }
//            catch (Exception ex)
//            {
//                Win32Api.MessageBox(IntPtr.Zero,
//                   $@"Unknown Error:
//{ex.Message}
//{ex.StackTrace}",
//                   "EnergyStar Error", Win32Api.MB_ICONERROR | Win32Api.MB_OK);
//                Environment.Exit(1);
//            }
//            throw new InvalidOperationException("Unreachable code");
        }
    }

}
