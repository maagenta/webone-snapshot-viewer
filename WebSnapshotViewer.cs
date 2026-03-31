using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// Handles all HTTP requests when --web-snapshot-viewer mode is active.
	/// Routes requests to the probe page, screenshot handler, strip server, or click handler.
	/// </summary>
	class WebSnapshotViewer
	{
		private static readonly Dictionary<string, StripSet> Cache = new();
		private static readonly TimeSpan CacheTTL = TimeSpan.FromMinutes(5);

		private readonly HttpRequest ClientRequest;
		private readonly HttpResponse ClientResponse;
		private readonly LogWriter Log;

		public WebSnapshotViewer(HttpRequest request, HttpResponse response, LogWriter log)
		{
			ClientRequest = request;
			ClientResponse = response;
			Log = log;
		}

		public void Handle(Uri requestUrl)
		{
			if (requestUrl == null)
			{
				SendHtml(DimensionProbe.GetPage("about:blank"));
				return;
			}

			if (requestUrl.Host.Equals(DimensionProbe.MagicHost, StringComparison.OrdinalIgnoreCase))
			{
				switch (requestUrl.AbsolutePath.ToLowerInvariant())
				{
					case "/snap":        HandleSnapshot(requestUrl);   break;
					case "/strip":       HandleStrip(requestUrl);       break;
					case "/strip-frame": HandleStripFrame(requestUrl);  break;
					case "/click":       HandleClick(requestUrl);       break;
					case "/scroll-pos":  HandleScrollPos(requestUrl);   break;
					case "/scroll.js":   HandleScrollJs(requestUrl);    break;
					default:
						SendHtml("<HTML><BODY><H1>Unknown endpoint.</H1></BODY></HTML>");
						break;
				}
			}
			else
			{
				Log.WriteLine(" [Snapshot] Probe for: {0}", requestUrl);
				SendHtml(DimensionProbe.GetPage(requestUrl.ToString()));
			}
		}

		private void HandleSnapshot(Uri requestUrl)
		{
			var qs = HttpUtility.ParseQueryString(requestUrl.Query);
			string targetUrl = qs["url"];
			string wStr = qs["w"];
			string hStr = qs["h"];

			if (string.IsNullOrEmpty(targetUrl) || !int.TryParse(wStr, out int width) || !int.TryParse(hStr, out int height))
			{
				SendHtml("<HTML><BODY><H1>Invalid snapshot request.</H1></BODY></HTML>");
				return;
			}

			width  = Math.Clamp(width,  320, 3840);
			height = Math.Clamp(height, 200, 2160);

			string sessionKey = GetSessionKey(targetUrl, width);
			Log.WriteLine(" [Snapshot] {0} @ {1}px wide", targetUrl, width);

			if (Cache.TryGetValue(sessionKey, out var existing) && DateTime.UtcNow - existing.CreatedAt < CacheTTL)
			{
				Log.WriteLine(" [Snapshot] Cache hit.");
				SendHtml(SnapshotPage.GetShellPage(sessionKey, existing));
				return;
			}

			byte[] png;
			try
			{
				png = Task.Run(() => ScreenshotEngine.TakeScreenshot(sessionKey, targetUrl, width, height))
					.GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				Log.WriteLine(" [Snapshot] Error: {0}", ex.Message);
				SendHtml(
					"<HTML><BODY><H1>Screenshot failed</H1>" +
					"<P>" + HttpUtility.HtmlEncode(targetUrl) + "</P>" +
					"<PRE>" + HttpUtility.HtmlEncode(ex.Message) + "</PRE>" +
					"</BODY></HTML>");
				return;
			}

			var strips = StripManager.CreateStrips(png, Program.StripHeight, height);
			Cache[sessionKey] = strips;
			Log.WriteLine(" [Snapshot] {0} strips created ({1}x{2}px).", strips.Strips.Length, strips.ImageWidth, strips.ImageHeight);

			SendHtml(SnapshotPage.GetShellPage(sessionKey, strips));
		}

		private void HandleStripFrame(Uri requestUrl)
		{
			var qs = HttpUtility.ParseQueryString(requestUrl.Query);
			string key = qs["key"];
			string iStr = qs["i"];
			string rev = qs["r"] ?? "0";

			if (string.IsNullOrEmpty(key) || !int.TryParse(iStr, out int index))
			{
				SendHtml("<HTML><BODY><H1>Invalid strip-frame request.</H1></BODY></HTML>");
				return;
			}

			string imgUrl = "http://" + DimensionProbe.MagicHost +
			                "/strip?key=" + key + "&i=" + index + "&r=" + rev;
			SendHtml(SnapshotPage.GetStripFrame(imgUrl));
		}

		private void HandleStrip(Uri requestUrl)
		{
			var qs = HttpUtility.ParseQueryString(requestUrl.Query);
			string key = qs["key"];
			string iStr = qs["i"];

			if (string.IsNullOrEmpty(key) ||
				!int.TryParse(iStr, out int index) ||
				!Cache.TryGetValue(key, out var stripSet) ||
				index < 0 || index >= stripSet.Strips.Length)
			{
				SendHtml("<HTML><BODY><H1>Strip not found.</H1></BODY></HTML>");
				return;
			}

			byte[] jpeg = stripSet.Strips[index].Jpeg;
			ClientResponse.StatusCode = 200;
			ClientResponse.ContentType = "image/jpeg";
			ClientResponse.ContentLength64 = jpeg.Length;
			ClientResponse.SendHeaders();
			ClientResponse.OutputStream.Write(jpeg, 0, jpeg.Length);
			ClientResponse.Close();
		}

		private void HandleClick(Uri requestUrl)
		{
			ClickHandler.Handle(requestUrl, ClientRequest, ClientResponse, Log, Cache);
		}

		private void HandleScrollPos(Uri requestUrl)
		{
			ScrollHandler.HandleScrollPos(requestUrl, ClientResponse, Log, Cache);
		}

		private void HandleScrollJs(Uri requestUrl)
		{
			ScrollHandler.HandleScrollJs(requestUrl, ClientResponse, Cache);
		}

		private void SendHtml(string html)
		{
			byte[] buffer = Encoding.UTF8.GetBytes(html);
			ClientResponse.StatusCode = 200;
			ClientResponse.ContentType = "text/html";
			ClientResponse.ContentLength64 = buffer.Length;
			ClientResponse.SendHeaders();
			ClientResponse.OutputStream.Write(buffer, 0, buffer.Length);
			ClientResponse.Close();
		}

		private static string GetSessionKey(string url, int width)
		{
			string raw = url + ":" + width;
			byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
			return Convert.ToHexString(hash);
		}
	}
}
