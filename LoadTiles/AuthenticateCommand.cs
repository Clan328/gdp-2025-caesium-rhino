using Rhino;
using Rhino.Commands;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net;
using System.Web;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Text.Json;

namespace CesiumAuth
{
    public class AuthenticateCommand : Command
    {
        private const string CLIENT_ID = "1108"; // ID of OAuth application; TODO: Move this to config file
        
        private static readonly HttpClient client = new HttpClient();

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "Authenticate";

        private string? ListenCode(string prefix, string requiredState)
        {
            // TODO: Error handling for the following cases:
            //       - If user closes auth website, rhino will freeze forever
            //       - Bad requests to server should throw error
            //       - code being null should throw an error
            //       Improve successful auth website
            if (!HttpListener.IsSupported)
            {
                Console.WriteLine ("HttpListener class is not supported by OS.");
                return null;
            }

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            listener.Start();
            Console.WriteLine("Listening...");
            // The GetContext method freezes Rhino while waiting for a request.
            HttpListenerContext context = listener.GetContext();
            HttpListenerRequest request = context.Request;

            NameValueCollection query = HttpUtility.ParseQueryString(request.Url.Query);

            string code = query.Get("code");
            string? state = query.Get("state");

            if (state == null || state != requiredState) {
                // This means we are being attacked
                return null;
            }

            HttpListenerResponse response = context.Response;
            string responseString = "<HTML><BODY> Authentication successful, you can return to Rhino!</BODY></HTML>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);

            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer,0,buffer.Length);

            output.Close();
            listener.Stop();

            return code;
        }

        private string? Authenticate() {
            int port = 8080; // TODO: Find open port

            string state = GenerateState();

            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://ion.cesium.com/oauth?response_type=code&client_id={CLIENT_ID}&redirect_uri=http://localhost:{port}&scope=assets:read assets:list&state={state}",
                UseShellExecute = true
            });

            string code = ListenCode($"http://*:{port}/", state);

            if (code == null) {
                // TODO: Throw error
                return null;
            }

            Dictionary<string, string> values = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "client_id", CLIENT_ID },
                { "code", code },
                { "redirect_uri",  $"http://localhost:{port}"}
            };

            var content = new FormUrlEncodedContent(values);
            HttpResponseMessage response = Task.Run(() => client.PostAsync("https://api.cesium.com/oauth/token", content)).GetAwaiter().GetResult();
            
            if (!response.IsSuccessStatusCode) {
                // TODO: Throw error based on code
                return null;
            }
            
            string responseString = Task.Run(() => response.Content.ReadAsStringAsync()).GetAwaiter().GetResult();

            var responseValues = JsonSerializer.Deserialize<Dictionary<string, string>>(responseString);
            
            return responseValues["access_token"];
        }

        private string GenerateState() {
            const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            var random = new Random();
            var chars = new char[20];

            for (int i = 0; i < 20; i++)
                chars[i] = characters[random.Next(characters.Length)];

            return new string(chars);
        }

        /// <summary>
        /// Handles the user running the command.
        /// </summary>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("Authenticating...");

            string? key = Authenticate();

            if (key == null) return Result.Failure;

            RhinoApp.WriteLine(key);

            return Result.Success;
        }
    }
}
