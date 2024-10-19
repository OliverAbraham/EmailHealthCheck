using Abraham.ProgramSettingsManager;
using Abraham.Scheduler;
using NLog.Web;
using CommandLine;
using Abraham.Mail;
using Abraham.HomenetBase.Connectors;
using Abraham.HomenetBase.Models;

namespace ImapSpamfilter
{
    /// <summary>
    /// IMAP HEALTH CHECK
    /// 
    /// This is a monitor that searches for the newest email from a person.
    /// It monitors the age of that email, verifying we get a health signal from that person.
    /// 
    /// 
    /// FUNCTIONING
    /// 
    /// It will connect periodcally to your imap mail server and check every new(unread) email.
    /// If will select emails from a certain person, optionally having some whitelisted word in subject.
    /// Out of these emails, it will pick the newest one and calculate the age in days.
    /// 
    /// The configuration must be made in the appsettings.hjson file.
    ///     
    /// 
    /// AUTHOR
    /// Written by Oliver Abraham, mail@oliver-abraham.de
    /// 
    /// 
    /// INSTALLATION AND CONFIGURATION
    /// See README.md in the project root folder.
    /// 
    /// 
    /// LICENSE
    /// This project is licensed under Apache license.
    /// 
    /// 
    /// SOURCE CODE
    /// https://www.github.com/OliverAbraham/ImapHealthCheck
    /// 
    /// </summary>
    public class Program
    {
        #region ------------- Fields --------------------------------------------------------------
	    private static CommandLineOptions                    _commandLineOptions                = new CommandLineOptions();
        private static ProgramSettingsManager<Configuration> _programSettingsManager            = new ProgramSettingsManager<Configuration>();
        private static Configuration                         _config                            = new();
        private static NLog.Logger                           _logger                            = NLogBuilder.ConfigureNLog("").GetCurrentClassLogger();
        private static Scheduler?                            _scheduler;
        private static DateTime                              _spamfilterConfigFileLastWriteTime = default(DateTime);
        private static bool                                  _thisIsTheFirstTime                = true;
        #endregion



        #region ------------- Command line options ------------------------------------------------
        class CommandLineOptions
	    {
	        [Option('c', "config", Default = "appsettings.hjson", Required = false, HelpText = 
	            """
	            Configuration file (full path and filename).
	            If you don't specify this option, the program will expect your configuration file 
	            named 'appsettings.hjson' in your program folder.
	            You can specify a different location.
	            You can use Variables for special folders, like %APPDATA%.
	            Please refer to the documentation of my nuget package https://github.com/OliverAbraham/Abraham.ProgramSettingsManager
	            """)]
	        public string ConfigurationFile { get; set; } = "";

	        [Option('n', "nlogconfig", Default = "nlog.config", Required = false, HelpText = 
	            """
	            NLOG Configuration file (full path and filename).
	            If you don't specify this option, the program will expect your configuration file 
	            named 'nlog.config' in your program folder.
	            You can specify a different location.
	            """)]
            public string NlogConfigurationFile { get; set; } = "";
	
	        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
	        public bool Verbose { get; set; }
	    }
	    #endregion



        #region ------------- Init ----------------------------------------------------------------
        public static void Main(string[] args)
        {
	        ParseCommandLineArguments();
    	    ReadConfiguration();
            ValidateConfiguration();
            InitLogger();
            PrintGreeting();
            LogConfiguration();
            HealthChecks();
            StartScheduler();

            Console.ReadKey();

            StopScheduler();
        }
        #endregion



        #region ------------- Health checks -------------------------------------------------------
        private static void HealthChecks()
        {
        }
        #endregion



        #region ------------- Configuration -------------------------------------------------------
	    private static void ParseCommandLineArguments()
	    {
	        string[] args = Environment.GetCommandLineArgs();
	        CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(args)
	            .WithParsed   <CommandLineOptions>(options => { _commandLineOptions = options; })
	            .WithNotParsed<CommandLineOptions>(errors  => { Console.WriteLine(errors.ToString()); });
            
            if (_commandLineOptions is null)
                throw new Exception();
	    }
	
	    private static void ReadConfiguration()
        {
            // ATTENTION: When loading fails, you probably forgot to set the properties of appsettings.hjson to "copy if newer"!
            // ATTENTION: or you have an error in your json file
	        _programSettingsManager = new ProgramSettingsManager<Configuration>()
            .UseFullPathAndFilename(_commandLineOptions.ConfigurationFile)
            //.UsePathRelativeToSpecialFolder(_commandLineOptions.ConfigurationFile)
            .Load();
            _config = _programSettingsManager.Data;
            Console.WriteLine($"Loaded configuration file '{_programSettingsManager.ConfigFilename}'");
        }

        private static void ValidateConfiguration()
        {
            // ATTENTION: When validating fails, you missed to enter a value for a property in your json file
            _programSettingsManager.Validate();
        }

        private static void SaveConfiguration()
        {
            _programSettingsManager.Save(_programSettingsManager.Data);
        }
        #endregion



        #region ------------- Logging -------------------------------------------------------------
        private static void InitLogger()
        {
            try
            {
                _logger = NLogBuilder.ConfigureNLog(_commandLineOptions.NlogConfigurationFile).GetCurrentClassLogger();
                if (_logger is null)
                    throw new Exception();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing our logger with the configuration file {_commandLineOptions.NlogConfigurationFile}. More info: {ex}");
                throw;  // ATTENTION: When you come here, you probably forgot to set the properties of nlog.config to "copy if newer"!
            }
        }

        /// <summary>
        /// To generate text like this, use https://onlineasciitools.com/convert-text-to-ascii-art
        /// </summary>
        private static void PrintGreeting()
        {
            _logger.Debug("");
            _logger.Debug("");
            _logger.Debug("");
            _logger.Debug(@"----------------------------------------------------------------------------------------------");
            _logger.Debug(@"    _____                _ _    _   _            _ _   _        _____ _               _       ");
            _logger.Debug(@"   |  ___|              (_) |  | | | |          | | | | |      /  __ \ |             | |      ");
            _logger.Debug(@"   | |__ _ __ ___   __ _ _| |  | |_| | ___  __ _| | |_| |__    | /  \/ |__   ___  ___| | __   ");
            _logger.Debug(@"   |  __| '_ ` _ \ / _` | | |  |  _  |/ _ \/ _` | | __| '_ \   | |   | '_ \ / _ \/ __| |/ /   ");
            _logger.Debug(@"   | |__| | | | | | (_| | | |  | | | |  __/ (_| | | |_| | | |  | \__/\ | | |  __/ (__|   <    ");
            _logger.Debug(@"   \____/_| |_| |_|\__,_|_|_|  \_| |_/\___|\__,_|_|\__|_| |_|   \____/_| |_|\___|\___|_|\_\   ");
            _logger.Debug(@"                                                                                              ");
            _logger.Info ($"                   Email Health Checker started, Version {AppVersion.Version.VERSION}                             ");
            _logger.Debug(@"----------------------------------------------------------------------------------------------");
        }

        private static void LogConfiguration()
        {
            _logger.Debug($"");
            _logger.Debug($"");
            _logger.Debug($"");
            _logger.Debug($"------------ Configuration -------------------------------------------");
            _logger.Debug($"Loaded from file                  : {_programSettingsManager.ConfigFilename}");
            _programSettingsManager.Data.LogOptions(_logger);
        }
        #endregion



        #region ------------- Periodic actions ----------------------------------------------------
        private static void StartScheduler()
        {
            _scheduler = new Scheduler()
                .UseAction(() => PeriodicJob())
                .UseFirstStartRightNow()
                .UseIntervalMinutes(_config.CheckIntervalMinutes)
                .Start();
        }

        private static void StopScheduler()
        {
            _scheduler?.Stop();
        }

        private static void PeriodicJob()
        {
            try
            {
                ReadEmailsFromInboxAndAnalyze();
            }
            catch (Exception ex) 
            {
                _logger.Error($"Error with the imap server: {ex}");
            }
        }
        #endregion



        #region ------------- Domain logic --------------------------------------------------------
        private static void ReadEmailsFromInboxAndAnalyze()
        {
            var account = _config.MailAccounts[0];

            Console.Write("Reading the inbox...");
            var emails = ReadAllEmailsFromInbox(account);
            Console.WriteLine($"{emails.Count} emails");

            emails = emails.Where(x => MailComesFromThePersonMonitoredPerson(x, account)).ToList();

            // take only the emails containing some words
            // (in my case "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday")
            if (account.SenderSubjectWhitelist.Count > 0) 
                emails = emails.Where(x => SubjectContainsOneWhitelistedWord(x, account)).ToList();

            emails = emails.OrderByDescending(x => x.Msg.Date).ToList();

            Console.WriteLine($"Newest 5 emails:");
            foreach(var email in emails.Take(5))
                Console.WriteLine($"- {email.Msg.Date.ToString("yyyy-MM-dd hh:MM")}   {email.Msg.Subject}");

            var newestEmail = emails.First();

            var age = DateTime.Now - newestEmail.Msg.Date;
            var ageInDays = age.TotalDays;

            Console.WriteLine($"Newest Email is {ageInDays:N1} days old");

            SendResultToHomeAutomationServer("MONITOR_DIETER", ((int)ageInDays).ToString());
        }

        private static bool MailComesFromThePersonMonitoredPerson(Message mail, MailAccount account)
        {
            var sender = mail?.Msg?.From?.First().ToString().ToLower() ?? "";
            return sender.Length > 0 && sender.Contains(account.SenderName);
        }

        private static bool SubjectContainsOneWhitelistedWord(Message mail, MailAccount account)
        {
            var subject = mail?.Msg?.Subject ?? "";
            
            foreach(var word in account.SenderSubjectWhitelist)
            {
                if (subject.Length > 0 && subject.Contains(word))
                    return true;
            }
            return false;
        }

        private static List<Message> ReadAllEmailsFromInbox(MailAccount account)
        {
            var security = account.ImapSecurity switch {
                "Ssl"                   => Security.Ssl,
                "StartTls"              => Security.StartTls,
                "StartTlsWhenAvailable" => Security.StartTlsWhenAvailable,
                _                       => Security.None
            };

			var _client = new Abraham.Mail.ImapClient()
				.UseHostname(account.ImapServer)
				.UseSecurityProtocol(security)
				.UseAuthentication(account.Username, account.Password)
				.Open();

			var emails = _client.ReadUnreadEmailsFromInbox();
            return emails;
        }
        #endregion



        #region ------------- Home automation server ----------------------------------------------
        private static void SendResultToHomeAutomationServer(string dataObjectName, string value)
        {
            try
            {
                Log("Connecting to homenet server...");
                var _homenetClient = new DataObjectsConnector(_config.HomenetServer, _config.HomenetUsername, _config.HomenetPassword, _config.HomenetTimeout);
                Log("Connect successful");

                var existingValue = _homenetClient.TryGet(dataObjectName);
                if (existingValue?.Value == value)
                {
                    Log($"We don't update, because the existing value is the same");
                    return;
			    }

                Log($"Sending new value {value} to the server...");
                var success = _homenetClient.UpdateValueOnly(new DataObject(){ Name=dataObjectName, Value=value});
                Log($"{(success ? "ok" : "send error!")}");
            }
            catch (Exception ex)
            {
                Log("Error connecting to homenet server or sending a value change:\n" + ex.ToString());
            }
        }

        private static void Log(string message) 
        {
            Console.WriteLine(message);
        }
        #endregion
    }
}