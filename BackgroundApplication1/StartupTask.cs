using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.ApplicationModel.Background;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Threading;

namespace BackgroundApplication1
{
    public sealed class StartupTask : IBackgroundTask
    {
        private static BackgroundTaskDeferral _Deferral = null;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _Deferral = taskInstance.GetDeferral();

            var webserver = new MyWebserver();

            await ThreadPool.RunAsync(workItem =>
            {
                webserver.Start();
            });
        }


        internal class MyWebserver
        {
            private const uint BufferSize = 8192;

            public async void Start()
            {
                var listener = new StreamSocketListener();

                await listener.BindServiceNameAsync(localServiceName: "80");

                listener.ConnectionReceived += async (sender, args) =>
                {
                    var request = new StringBuilder();

                    using (var input = args.Socket.InputStream)
                    {
                        var data = new byte[BufferSize];
                        IBuffer buffer = data.AsBuffer();
                        var dataRead = BufferSize;

                        while (dataRead == BufferSize)
                        {
                            await input.ReadAsync(
                                 buffer, BufferSize, InputStreamOptions.Partial);
                            request.Append(Encoding.UTF8.GetString(
                                                          data, 0, data.Length));
                            dataRead = buffer.Length;
                        }
                    }

                    string query = GetQuery(request);

                    using (var output = args.Socket.OutputStream)
                    {
                        using (var response = GetResponse(output))
                        {
                            var html = Encoding.UTF8.GetBytes(
                            $"<html><head><title>Background Message</title></head><body>Hello from the background process!<br/>{query}</body></html>");
                            using (var bodyStream = new MemoryStream(html))
                            {
                                var header = $"HTTP/1.1 200 OK\r\nContent-Length: {bodyStream.Length}\r\nConnection: close\r\n\r\n";
                                var headerArray = Encoding.UTF8.GetBytes(header);
                                await response.WriteAsync(headerArray,
                                                          0, headerArray.Length);
                                await bodyStream.CopyToAsync(response);
                                await response.FlushAsync();
                            }
                        }
                    }
                };
            }

            private static Stream GetResponse(IOutputStream output)
            {
                return output.AsStreamForWrite();
            }

            private static string GetQuery(StringBuilder request)
            {
                var query = "";

                var requestLines = request.ToString().Split(' ');
                var url = requestLines.Length > 1 ? requestLines[1] : string.Empty;
                var requestLeft = requestLines[1].Split('?');
                var req = requestLeft.Length > 1 ? requestLeft[0].Split('/') : requestLines[1].Split('/');


                if (req.Length == 4)
                {
                    if (req[1] == "api" && req[2] == "control" && (req[3] == "reboot" || req[3] == "shutdown"))
                    {
                        goDown(req[3]);
                    }

                }
                else
                {
                    var uri = new Uri("http://localhost" + url);
                    query = uri.Query;
                }
                return query;
            }

            private static void goDown(string kind)
            {
                if (kind == "shutdown")
                {
                    ShutdownManager.BeginShutdown(ShutdownKind.Shutdown, TimeSpan.Zero);
                }
                else
                {
                    ShutdownManager.BeginShutdown(ShutdownKind.Restart, timeout: TimeSpan.Zero);
                }
            }
        }
    }
}
