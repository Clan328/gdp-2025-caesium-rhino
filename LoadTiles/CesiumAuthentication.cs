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
using LoadTiles;
using MessageBox = Eto.Forms.MessageBox;

namespace CesiumAuthentication
{
    public static class AuthSession
    {
        // Tracks the access token for the session, given by the most recent log-in.
        public static string CesiumAccessToken
        {
            get { return LoadTilesPlugin.Instance.Settings.GetString("CesiumAccessToken", ""); }
            private set { LoadTilesPlugin.Instance.Settings.SetString("CesiumAccessToken", value); }
        }

        // Tracks logged-in status
        public static bool IsLoggedIn => !string.IsNullOrEmpty(CesiumAccessToken);

        private const string CLIENT_ID = "1108"; // ID of OAuth application
        private const string CLIENT_ID_FETCH = "1143";

        private static readonly HttpClient client = new HttpClient();
        private static readonly IPEndPoint DefaultLoopbackEndpoint = new IPEndPoint(IPAddress.Loopback, port: 0);
        private static string STATE = GenerateState();
        public static int PORT = GetAvailablePort();

        private const string RESPONSE_HTML = @"
            <!doctype html>
            <html>
            <head>
            <meta charset=""utf-8"">
            <title>Rhinoceros</title>
            <style>
                @keyframes fadeIn {
                    0% {opacity: 0.1;}
                    100% {opacity: 1;}
                }

                .fadeIn {
                    animation-duration: 5s;
                        animation-name: fadeIn;
                }
            </style>
            </head>
            <body style=""text-align: center; font-family: Lato, 'Helvetica Neue', Helvetica, Arial, sans-serif;"">
                <div><img src=""https://elisapi.mcneel.com/media/2"" alt=""Rhinoceros"" style=""width: 256px; height: 256px;"" class=""fadeIn""></div>
                <p>Login completed. Please go back to Rhino.</p>
            </body>
            </html>
        ";

        // Logins user, or returns current api key if they are already logged in
        public static void Login(bool fetchRedirect=false) {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://ion.cesium.com/oauth?response_type=code&client_id={(fetchRedirect? CLIENT_ID_FETCH : CLIENT_ID)}&redirect_uri=http://127.0.0.1:{PORT}{(fetchRedirect? "/fetch/" : "")}&scope=assets:read assets:list&state={STATE}",
                UseShellExecute = true
            });
        }

        // Logs out the user, returns true if the user was logged in before
        public static bool Logout() {
            if (IsLoggedIn) {
                CesiumAccessToken = "";
                return true;
            }
            return false;
        }

        public static void ListenCode(string prefix)
        {
            if (!HttpListener.IsSupported)
            {
                Console.WriteLine ("HttpListener class is not supported by OS.");
                return;
            }

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            listener.Start();
            Console.WriteLine("Listening...");
            // The GetContext method freezes Rhino while waiting for a request.
            while (true) {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;

                NameValueCollection query = HttpUtility.ParseQueryString(request.Url.Query);

                string? code = query.Get("code");
                string? state = query.Get("state");
                
                bool fetchRedirect = request.RawUrl.StartsWith("/fetch/");

                if (state == null || state != STATE) {
                    // This means we are being attacked
                    continue;
                }

                HttpListenerResponse listenerResponse = context.Response;
                string listenerResponseString = RESPONSE_HTML;
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(listenerResponseString);

                listenerResponse.ContentLength64 = buffer.Length;
                System.IO.Stream output = listenerResponse.OutputStream;
                output.Write(buffer,0,buffer.Length);
                output.Close();

                if (code == null) {
                    continue;
                }

                Dictionary<string, string> values = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "client_id", (fetchRedirect? CLIENT_ID_FETCH : CLIENT_ID) },
                    { "code", code },
                    { "redirect_uri",  $"http://localhost:{PORT}{(fetchRedirect? "/fetch/" : "")}"}
                };

                var content = new FormUrlEncodedContent(values);
                HttpResponseMessage response = Task.Run(() => client.PostAsync("https://api.cesium.com/oauth/token", content)).GetAwaiter().GetResult();
                
                if (!response.IsSuccessStatusCode) {
                    continue;
                }
                
                string responseString = Task.Run(() => response.Content.ReadAsStringAsync()).GetAwaiter().GetResult();

                var responseValues = JsonSerializer.Deserialize<Dictionary<string, string>>(responseString);
                
                CesiumAccessToken = responseValues["access_token"];

                if (IsLoggedIn) {
                    RhinoApp.InvokeOnUiThread(new Action(() => {
                        MessageBox.Show("Authentication successful!");
                        if (fetchRedirect) {
                            RhinoApp.RunScript(LoadTilesCommand.Instance.EnglishName, false);
                        }
                    }));

                    RhinoApp.WriteLine("Authentication successful!");
                }
            }

            listener.Stop();
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
    public class CesiumLogoutCommand : Command
    {
        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "SealionLogout";

        /// <summary>
        /// Handles the user running the command.
        /// </summary>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("Logging out...");

            AuthSession.Logout();

            MessageBox.Show("Logged out successfully!");
            RhinoApp.WriteLine("Logged out successfully!");

            return Result.Success;
        }
    }


    public class CesiumLoginCommand : Command
    {
        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "SealionLogin";

        /// <summary>
        /// Handles the user running the command.
        /// </summary>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("Authenticating...");

            AuthSession.Login();

            return Result.Success;
        }
    }
}
