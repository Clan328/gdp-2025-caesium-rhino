using Rhino;
using Rhino.Commands;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Web;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Text.Json;

namespace CesiumAuthentication
{
    public static class AuthSession
    {
        // Tracks the access token for the session, given by the most recent log-in.
        public static string? CesiumAccessToken { get; private set; }

        // Tracks logged-in status
        public static bool IsLoggedIn => !string.IsNullOrEmpty(CesiumAccessToken);

        private const string CLIENT_ID = "1108"; // ID of OAuth application; TODO: Move this to config file
        
        private static readonly HttpClient client = new HttpClient();
        private static readonly IPEndPoint DefaultLoopbackEndpoint = new IPEndPoint(IPAddress.Loopback, port: 0);

        // Logins user, or returns current api key if they are already logged in
        public static string? LogIn() {
            if (IsLoggedIn) return CesiumAccessToken;

            int port = GetAvailablePort();

            string state = GenerateState();

            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://ion.cesium.com/oauth?response_type=code&client_id={CLIENT_ID}&redirect_uri=http://127.0.0.1:{port}&scope=assets:read assets:list&state={state}",
                UseShellExecute = true
            });

            string code = ListenCode($"http://127.0.0.1:{port}/", state);

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

            CesiumAccessToken = responseValues["access_token"];
            return CesiumAccessToken;
        }

        // Logs out the user, returns true if the user was logged in before
        public static bool LogOut() {
            if (IsLoggedIn) {
                CesiumAccessToken = null;
                return true;
            }
            return false;
        }

        private static string? ListenCode(string prefix, string requiredState)
        {
            // TODO: Error handling for the following cases:
            //       - If user closes auth website, rhino will freeze forever
            //       - Bad requests to server should throw error
            //       - code being null should throw an error
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
            string responseString = File.ReadAllText("index.html");
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);

            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer,0,buffer.Length);

            output.Close();
            listener.Stop();

            return code;
        }

        public static int GetAvailablePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(DefaultLoopbackEndpoint);
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }

        private static string GenerateState() {
            const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            var random = new Random();
            var chars = new char[20];

            for (int i = 0; i < 20; i++)
                chars[i] = characters[random.Next(characters.Length)];

            return new string(chars);
        }
    }    

    // Manually logs the user out of the Cesium account via the command line.
    public class LogOutCommand : Command
    {
        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "LogOut";

        /// <summary>
        /// Handles the user running the command.
        /// </summary>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("Logging out...");

            AuthSession.LogOut();

            RhinoApp.WriteLine("Logged out successfully!");

            return Result.Success;
        }
    }


    public class LogInCommand : Command
    {
        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "LogIn";

        /// <summary>
        /// Handles the user running the command.
        /// </summary>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("Authenticating...");

            string? key = AuthSession.LogIn();

            if (key == null) return Result.Failure;

            RhinoApp.WriteLine("Authentication successful!");

            return Result.Success;
        }
    }
}

// TODO (SKYE) : Implement a way to mark status as 'logged in' or 'logged out', storing access key for session. [x]
// TODO (SKYE) : Embed this process into the GUI [x]