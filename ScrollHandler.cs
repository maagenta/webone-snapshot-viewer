using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// Keeps the Playwright driver scroll position in sync with the client browser.
	/// </summary>
	static class ScrollHandler
	{
		// 1×1 transparent GIF returned so the browser Image() request completes cleanly.
		private static readonly byte[] TransparentGif = Convert.FromBase64String(
			"R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

		/// <summary>
		/// Returns the scroll-sync JS served at /scroll.js.
		/// ES3-only, Safari 1 compatible.
		/// </summary>
		public static string GetScrollScript(string sessionKey)
		{
			string scrollBase = "http://" + DimensionProbe.MagicHost + "/scroll-pos?key=" + sessionKey + "&y=";
			return
				"window.status='scroll.js loaded';" +
				"function _getScrollY(){" +
				  "if(document.body&&document.body.scrollTop)return document.body.scrollTop;" +
				  "if(document.documentElement&&document.documentElement.scrollTop)return document.documentElement.scrollTop;" +
				  "if(window.pageYOffset!=null)return window.pageYOffset;" +
				  "return 0;" +
				"}" +
				"var _lastY=-1;" +
				"function _sendScroll(){" +
				  "var sy=_getScrollY();" +
				  "if(sy==_lastY)return;" +
				  "_lastY=sy;" +
				  "window.status='scroll y='+sy;" +
				  "var img=new Image();" +
				  "img.src='" + scrollBase + "'+sy+'&t='+(new Date().getTime());" +
				"}" +
				"setInterval('_sendScroll()',50);";
		}

		/// <summary>
		/// Serves the scroll-sync JS at /scroll.js.
		/// </summary>
		public static void HandleScrollJs(
			Uri requestUrl,
			HttpResponse clientResponse,
			Dictionary<string, StripSet> cache)
		{
			var qs = HttpUtility.ParseQueryString(requestUrl.Query);
			string key = qs["key"] ?? "";
			byte[] buffer = Encoding.UTF8.GetBytes(GetScrollScript(key));
			clientResponse.StatusCode = 200;
			clientResponse.ContentType = "text/javascript";
			clientResponse.ContentLength64 = buffer.Length;
			clientResponse.SendHeaders();
			clientResponse.OutputStream.Write(buffer, 0, buffer.Length);
			clientResponse.Close();
		}

		/// <summary>
		/// Scrolls the Playwright page to the given Y position.
		/// Returns a 1×1 transparent GIF so the browser Image() request completes.
		/// </summary>
		public static void HandleScrollPos(
			Uri requestUrl,
			HttpResponse clientResponse,
			LogWriter log,
			Dictionary<string, StripSet> cache)
		{
			var qs = HttpUtility.ParseQueryString(requestUrl.Query);
			string key = qs["key"];
			string yStr = qs["y"];

			if (!string.IsNullOrEmpty(key) && int.TryParse(yStr, out int scrollY))
			{
				log.WriteLine(" [ScrollPos] y={0}", scrollY);
				Console.WriteLine("[ScrollPos] y={0}", scrollY);
				if (cache.TryGetValue(key, out var stripSet))
					stripSet.LastScrollY = scrollY;
				Task.Run(() => ScreenshotEngine.ScrollTo(key, scrollY));
			}

			clientResponse.StatusCode = 200;
			clientResponse.ContentType = "image/gif";
			clientResponse.ContentLength64 = TransparentGif.Length;
			clientResponse.SendHeaders();
			clientResponse.OutputStream.Write(TransparentGif, 0, TransparentGif.Length);
			clientResponse.Close();
		}
	}
}
