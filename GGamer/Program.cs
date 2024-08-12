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


	private const string URL1 = "https://www.chatgpt.com";

	private const string COOKIES_TXT = @"C:\Users\Deci\Downloads\cookies.txt";

	public static async Task Main(string[] args)
	{
		var co = new ChromeOptions()
			{ };
		co.AddArguments("--no-sandbox", "--disable-setuid-sandbox");

		var driver = UndetectedChromeDriver.Create(co, driverExecutablePath:
		                                           await new ChromeDriverInstaller().Auto());

		await driver.Navigate().GoToUrlAsync(URL1);

		foreach (var c in ParseCookies(COOKIES_TXT)) {
			// Debug.WriteLine(c);

			try {
				driver.Manage().Cookies.AddCookie(c);

			}
			catch (Exception e) {
				Console.Error.WriteLine(e.Message);
			}
		}

		await driver.Navigate().GoToUrlAsync(URL1);

		var cts = new CancellationTokenSource();

		var wdw = new WebDriverWait(driver, TimeSpan.FromSeconds(6));
		var em  = wdw.Until(x => x.FindElement(By.Id("prompt-textarea")), cts.Token);

		em.SendKeys("How do I play game\n");

		var wdw2 = new WebDriverWait(driver, TimeSpan.FromSeconds(6))
		{
			PollingInterval = TimeSpan.FromMilliseconds(500)
		};
		wdw2.IgnoreExceptionTypes([typeof(ElementNotInteractableException)]);

		var hs = new HashSet<string>();

		while (!cts.IsCancellationRequested) {
			var msgs = wdw2.Until(x =>
			{
				return x.FindElements(By.XPath("//*[contains(@data-testid,'conversation-turn')]"));
			}, cts.Token);

			/*
			foreach (var msg in msgs) {
				if (!msg.IsStale() && !hs.Contains(msg)) {
					hs.Add(msg);
					Console.WriteLine(msg.Text);
				}

			}
			*/

			wdw2.WaitForElementToStopChanging(By.XPath("//*[contains(@data-testid,'conversation-turn')]"), TimeSpan.FromSeconds(2));

			foreach (IWebElement msg in msgs) {
				if (msg.TryGetText(out string s) && hs.Add(s)) {

					Console.WriteLine(s);

				}
			}

			// await Task.Delay(TimeSpan.FromSeconds(1));
		}

		Console.CancelKeyPress += (sender, eventArgs) =>
		{
			Console.WriteLine($"{sender} -> {eventArgs}");

			cts.Cancel();

		};

		// await Task.Delay(TimeSpan.FromMinutes(6));

		driver.Quit();
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