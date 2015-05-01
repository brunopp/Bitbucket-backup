using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using Newtonsoft;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ConsoleGit
{
	class Program
	{
		static string username;
		static string password;

		static void Main(string[] args)
		{
			try
			{
				// ask for username
				Console.Write("Username: ");
				username = Console.ReadLine();
				if (string.IsNullOrEmpty(username))
					throw new Exception("Empty username!");

				// ask for password
				Console.Write("Password: ");
				password = Console.ReadLine();
				if (string.IsNullOrEmpty(password))
					throw new Exception("Empty password!");

				// get team name from config
				var team = System.Configuration.ConfigurationManager.AppSettings["team"];
				if (string.IsNullOrEmpty(team))
				{
					throw new Exception("Team not defined!");
				}

				// get clone destination from config
				var clonedest = System.Configuration.ConfigurationManager.AppSettings["clonedestination"];
				if (string.IsNullOrEmpty(clonedest))
				{
					throw new Exception("Clone destination not defined!");
				}
				if (clonedest[clonedest.Length - 1] != '\\')
					clonedest += "\\";
				if (!System.IO.Directory.Exists(clonedest))
				{
					throw new Exception("Clone destination does not exist!");
				}

				string url = "https://bitbucket.org/api/2.0/repositories/" + team;

				Console.WriteLine("Getting all repos...");

				// recursively get all clone urls
				var cloneRepos = new List<CloneRepository>();
				while (true)
				{
					var t = GetRepositories(url);

					cloneRepos.AddRange(t.Item1);

					if (!string.IsNullOrEmpty(t.Item2))
						url = t.Item2;
					else
						break;
				}

				Console.WriteLine("Done getting repos.");
				Console.WriteLine("Cloning all repos...");

				var now = DateTime.Now;
				var cloneDir = clonedest + now.ToString("yyyy-MM-dd-HH-mm-ss-fff");

				// clone all repos
				foreach (var cloneRepo in cloneRepos)
				{
					var path = cloneDir + @"\" + cloneRepo.Name;
					Console.WriteLine("\nCloning " + cloneRepo.Name);

					// call git.exe to clone
					var psi = new ProcessStartInfo
					{
						UseShellExecute = false,
						FileName = "git.exe",
						Arguments = " clone " + Regex.Replace(cloneRepo.Url, "^(https://[^@]+)", "${1}:" + password, RegexOptions.IgnoreCase) + " --mirror \"" + path + "\""
					};

					var p = new Process();
					p.StartInfo = psi;
					p.Start();
					p.WaitForExit();
					var exitCode = p.ExitCode;

					if (exitCode != 0)
						Console.WriteLine("ERROR: Bad exit code: " + exitCode);
					else
						Console.WriteLine("Done cloning " + cloneRepo.Name);
				}

				Console.WriteLine("All cloning done.");

				Console.ReadLine();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error: " + ex.Message);
				Console.ReadLine();
				return;
			}
		}

		static Tuple<List<CloneRepository>, string> GetRepositories(string url)
		{
			var repos = new List<CloneRepository>();
			var next = "";

			try
			{
				// create a request for the URL. 		
				var request = (HttpWebRequest)WebRequest.Create(url);
				// basic auth
				request.Headers.Add("Authorization", "Basic " + System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password)));
				// create and get response
				HttpWebResponse response = (HttpWebResponse)request.GetResponse();
				var sr = new System.IO.StreamReader(response.GetResponseStream());

				// read as json
				var json = JObject.Parse(sr.ReadToEnd());

				// check if there's a next page
				next = json.Value<string>("next");

				// get repos
				var values = json.SelectTokens("$.values").First();
				foreach (var val in values)
				{
					// get name
					var name = val.Value<string>("name");
					// get clone url
					var cloneUrl = val.SelectToken("$..clone").FirstOrDefault(x => x.Value<string>("name") == "https").Value<string>("href");

					repos.Add(new CloneRepository { Url = cloneUrl, Name = name });
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}

			return new Tuple<List<CloneRepository>, string>(repos, next);
		}

		public class CloneRepository
		{
			public string Name { get; set; }

			public string Url { get; set; }
		}
	}
}
