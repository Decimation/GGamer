using System.Diagnostics;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using SeleniumUndetectedChromeDriver;
using SCookie = OpenQA.Selenium.DevTools.V127.Network.Cookie;

namespace GGamer;

public static class Program
{

	private static UndetectedChromeDriver _driver;

	private const string URL1 = "https://www.chatgpt.com";

	private const string COOKIES_TXT = @"C:\Users\Deci\Downloads\chatgpt.com_cookies.txt";


	public static async Task Main(string[] args)
	{
		var co = new ChromeOptions()
			{ };
		co.AddArguments("--no-sandbox", "--disable-setuid-sandbox");

		_driver = UndetectedChromeDriver.Create(co, driverExecutablePath:
		                                        await new ChromeDriverInstaller().Auto());

		await _driver.Navigate().GoToUrlAsync(URL1);

		foreach (var c in ParseCookies(COOKIES_TXT)) {
			// Debug.WriteLine(c);

			try {
				_driver.Manage().Cookies.AddCookie(c);

			}
			catch (Exception e) {
				Console.Error.WriteLine(e.Message);
			}
		}

		await _driver.Navigate().GoToUrlAsync(URL1);

		var cts = new CancellationTokenSource();

		var wdw = new WebDriverWait(_driver, TimeSpan.FromSeconds(6));
		var em  = wdw.Until(x => x.FindElement(By.Id("prompt-textarea")), cts.Token);

		em.SendKeys("How do I play game\n");

		var wdw2 = new WebDriverWait(_driver, TimeSpan.FromSeconds(6))
		{
			PollingInterval = TimeSpan.FromMilliseconds(500)
		};
		wdw2.IgnoreExceptionTypes([typeof(ElementNotInteractableException)]);

		var hs = new HashSet<IWebElement>();

		while (!cts.IsCancellationRequested) {
			/*var comp = await _driver.WaitForRequest("r", TimeSpan.FromSeconds(1));

			if (comp) {
				var msgs = wdw2.Until(x =>
				{
					return x.FindElements(By.XPath("//*[contains(@data-testid,'conversation-turn')]"));
				}, cts.Token);

				foreach (IWebElement msg in msgs) {

					if (msg.TryGetText(out string s) && hs.Add(s)) {

						Console.WriteLine(s);

					}

				}

			}*/

			var msgs = wdw2.Until(x =>
			{
				return x.FindElements(By.XPath("//*[contains(@data-testid,'conversation-turn')]"));
			}, cts.Token);

			foreach (IWebElement msg in msgs) {
				/*var r=wdw2.WaitForElementToStopChanging(msg, TimeSpan.FromSeconds(3));

				if (r) {
					Console.WriteLine($"{msg.Text}");
				}*/

				/*if (msg.TryGetText(out var s)) {
					Console.WriteLine(s);
				}*/

				try {
					if (hs.Add(msg)) {
						Console.WriteLine(msg.Text);
					}
				}
				catch (Exception e) {
					// Console.WriteLine(e);
					// throw;
				}
			}

			await Task.Delay(TimeSpan.FromSeconds(1));
			/*
			foreach (var msg in msgs) {
				if (!msg.IsStale() && !hs.Contains(msg)) {
					hs.Add(msg);
					Console.WriteLine(msg.Text);
				}

			}
			*/


			// wdw2.WaitForElementToStopChanging(By.XPath("//*[contains(@data-testid,'conversation-turn')]"), TimeSpan.FromSeconds(2));


			// await Task.Delay(TimeSpan.FromSeconds(1));
		}

		/*Console.CancelKeyPress += (sender, eventArgs) =>
		{
			Console.WriteLine($"{sender} -> {eventArgs}");
			cts.Cancel();
			_driver.Quit();
			Kill();
		};*/

		// await Task.Delay(TimeSpan.FromMinutes(6));

		_driver.Quit();

	}

	private static void Kill()
	{
		var proc = Process.GetProcessesByName("chrome.exe");


		foreach (Process process in proc) {
			process.Kill();
			Console.WriteLine($"Killed {process.Id}");
		}
	}

	private static bool IsStale(this IWebElement e)
	{
		try {
			return !e.Enabled;
		}
		catch {
			return true;
		}
	}

	private static T TryGet<T>(this IWebElement e, Func<IWebElement, T> el)
	{
		try {
			return el(e);
		}
		catch {
			return default;
		}
	}

	private static bool TryGetText(this IWebElement e, out string s)
	{
		try {
			s = e.Text;
			return true;
		}
		catch {
			s = default;
			return false;
		}
	}

	public static void WaitForElementToStopChanging(this WebDriverWait wait, By locator, TimeSpan stableDuration)
	{
		wait.Until(driver =>
		{
			var element          = driver.FindElement(locator);
			var initialText      = element.Text;
			var initialAttribute = element.GetAttribute("value");

			return wait.Until(d =>
			{
				var currentText      = element.Text;
				var currentAttribute = element.GetAttribute("value");

				return initialText == currentText && initialAttribute == currentAttribute;
			});
		});

		// Wait for the stable duration to ensure the element has stopped changing
		System.Threading.Thread.Sleep(stableDuration);
	}

	public static bool WaitForElementToStopChanging(this WebDriverWait wait, IWebElement element,
	                                                TimeSpan stableDuration)
	{
		var res = wait.Until(driver =>
		{
			// var element          = driver.FindElement(locator);
			var initialText      = element.Text;
			var initialAttribute = element.GetAttribute("value");

			return wait.Until(d =>
			{
				var currentText      = element.Text;
				var currentAttribute = element.GetAttribute("value");

				return initialText == currentText && initialAttribute == currentAttribute;
			});
		});

		// Wait for the stable duration to ensure the element has stopped changing
		// System.Threading.Thread.Sleep(stableDuration);

		return res;
	}

	public static async Task<bool> WaitForRequest(this ChromeDriver _chromeDriver, string urlPattern, TimeSpan timeout)
	{
		var devToolsSession  = _chromeDriver.GetDevToolsSession();
		var network          = new OpenQA.Selenium.DevTools.V85.Network.NetworkAdapter(devToolsSession);
		var requestCompleted = new TaskCompletionSource<bool>();

		network.RequestWillBeSent += (sender, e) =>
		{
			if (e.Request.Url.Contains(urlPattern)) {
				requestCompleted.SetResult(true);
			}
		};

		await network.Enable(new OpenQA.Selenium.DevTools.V85.Network.EnableCommandSettings());

		var task = await Task.WhenAny(requestCompleted.Task, Task.Delay(timeout));

		if (task == requestCompleted.Task) {
			// Console.WriteLine("Request completed.");
			return true;
		}
		else {
			// throw new TimeoutException("Request did not complete within the specified timeout.");
			return false;
		}
	}

	public static List<Cookie> ParseCookies(string filePath)
	{
		var cookies = new List<Cookie>();

		foreach (var line in File.ReadLines(filePath)) {
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
				continue;

			var parts = line.Split('\t');

			if (parts.Length != 7)
				continue;

			var    domain            = parts[0];
			var    includeSubdomains = parts[1] == "TRUE";
			var    path              = parts[2];
			string name              = parts[5];
			bool   secure            = parts[3] == "TRUE";
			string value             = parts[6];

			var       seconds = long.Parse(parts[4]);
			DateTime? expiry  = seconds == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(seconds).DateTime;

			var cookie = new Cookie(name, value, domain, path, expiry)
				{ };

			cookies.Add(cookie);
		}

		return cookies;
	}

}