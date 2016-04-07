using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace httptest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
        }

        public static readonly DependencyProperty DownloadedSoFarProperty = DependencyProperty.Register(nameof(DownloadedSoFar), typeof(int), typeof(MainWindow));

        public int DownloadedSoFar
        {
            get { return (int)GetValue(DownloadedSoFarProperty); }
            set { SetValue(DownloadedSoFarProperty, value); }
        }

        public static readonly DependencyProperty ContentLengthProperty = DependencyProperty.Register(nameof(ContentLength), typeof(int), typeof(MainWindow));

        public int ContentLength
        {
            get { return (int)GetValue(ContentLengthProperty); }
            set { SetValue(ContentLengthProperty, value); }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var http = new HttpClient();
            var req = new HttpRequestMessage(HttpMethod.Get, "http://mirror.pnl.gov/releases/15.10/ubuntu-15.10-desktop-amd64.iso");
            var tmp = new byte[1024 * 1024];
            var bytesRead = 0;
            var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            var stream = await res.Content.ReadAsStreamAsync();
            try
            {
                var length = res.Content.Headers.ContentLength ?? -1;
                if (res.Content.Headers.ContentLength.HasValue)
                {
                    DownloadedSoFar = 0;
                    ContentLength = 0;
                }
                else
                {
                    DownloadedSoFar = 0;
                    ContentLength = (int)length;
                }
                while (true)
                {
                    try
                    {
                        var ret = await stream.ReadAsync(tmp, 0, tmp.Length);
                        if (ret == 0)
                            break;
                        bytesRead += ret;
                        DownloadedSoFar = bytesRead;
                        Debug.WriteLine(string.Format("{0}/{1}", bytesRead, length));
                    }
                    catch (IOException ex)
                    {
                        var se = ex.InnerException as System.Net.Sockets.SocketException;
                        if (se != null && se.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionAborted)
                        {
                            Debug.WriteLine("Network connection reset; resuming...");
                            await Task.Delay(3000); // sleep
                            req = new HttpRequestMessage(req.Method, req.RequestUri);
                            if (length >= 0)
                            {
                                // try to reuse the downloaded part
                                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue();
                                req.Headers.Range.Ranges.Add(new System.Net.Http.Headers.RangeItemHeaderValue(bytesRead, length));
                            }
                            else
                            {
                                req.Headers.Range = null;
                            }
                            res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                            if (res.StatusCode == System.Net.HttpStatusCode.PartialContent)
                            {
                                // download continue; keep the downloaded part as is
                                Debug.WriteLine("Resuming download...");
                            }
                            else
                            {
                                DownloadedSoFar = bytesRead = 0;
                            }

                            stream = await res.Content.ReadAsStreamAsync();
                            continue;
                        }
                        throw;
                    }
                }
            }
            finally
            {
                if (stream != null)
                    stream.Dispose();
            }
            Debug.WriteLine("Finished.");
        }
    }
}
