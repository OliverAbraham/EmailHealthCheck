using NLog.Web;
using CommandLine;
using Abraham.ProgramSettingsManager;
using Abraham.Scheduler;
using Abraham.Mail;
using Abraham.HomenetBase.Connectors;
using Abraham.HomenetBase.Models;
using Abraham.MQTTClient;
using MailKit;
using Newtonsoft.Json;

namespace EmailHealthCheck
{
    /// <summary>
    /// EMAIL HEALTH CHECK
    /// 
    /// This is a monitor that searches for the newest email from a person or service.
    /// It monitors the age of that email, verifying we get a health signal from that person/service.
    ///
    ///
    /// EXAMPLES
    /// - You want to monitor a backup where you only have emails sent to you. My app can search for the newest email in your inbox, then update your MQTT broker.
    /// - You're getting emails from a person oder service on a regular basis. You want to monitor these and update your MQTT target.
    /// 
    /// 
    /// FUNCTION
    /// The program will connect periodically to your imap mail server and check every new(unread) email.
    /// It will select emails from a certain person (sender).
    /// You can filter emails additionally  by subject, providing a whitelist of words. 
    /// Only those emails containing one of the words in subject will be selected.
    /// Out of these emails, it will pick the newest one and calculate the age in days.
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
    /// https://www.github.com/OliverAbraham/EmailHealthCheck
    /// 
    /// </summary>
    public class Program
    {
        #region ------------- Fields --------------------------------------------------------------
	    private static CommandLineOptions                    _commandLineOptions                = new CommandLineOptions();
        private static ProgramSettingsManager<Configuration> _programSettingsManager            = new ProgramSettingsManager<Configuration>();
        private static ProgramSettingsManager<StateFile>     _stateFileManager                  = new ProgramSettingsManager<StateFile>();
        private static StateFile                             _savedStates                       = new();
        private static Configuration                         _config                            = new();
        private static NLog.Logger                           _logger                            = NLogBuilder.ConfigureNLog("").GetCurrentClassLogger();
        private static Scheduler?                            _scheduler;
        private static DataObjectsConnector                  _homenetClient;
        private static MQTTClient                            _mqttClient;
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


	        [Option('s', "statefile", Default = "state.json", Required = false, HelpText = 
	            """
	            State file (full path and filename).
	            """)]
	        public string StateFile { get; set; } = "";


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
            ReadStateFile();
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
            .Load();
            _config = _programSettingsManager.Data;
            Console.WriteLine($"Loaded configuration file '{_programSettingsManager.ConfigPathAndFilename}'");
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



        #region ------------- State file ---------------------------------------------------------
	    private static void ReadStateFile()
        {
            try
            {
                // ATTENTION: When loading fails, you probably forgot to set the properties of appsettings.hjson to "copy if newer"!
                // ATTENTION: or you have an error in your json file

	            _stateFileManager = new ProgramSettingsManager<StateFile>()
                    .UseFilename(Path.GetFileName(_commandLineOptions.StateFile));

                _stateFileManager
                    .UseFullPathAndFilename(_commandLineOptions.StateFile)
                    .Load();
                _savedStates = _stateFileManager.Data;
                Console.WriteLine($"Loaded saved state from file '{_commandLineOptions.StateFile}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed loading saved state from file '{_commandLineOptions.StateFile}'.");
                Console.WriteLine($"Reason: {ex}");
            }
        }

        private static void SaveStateFile()
        {
            _stateFileManager.Data = _savedStates;
            //_stateFileManager.Save(_stateFileManager.Data);

            var json = JsonConvert.SerializeObject(_stateFileManager.Data);
            File.WriteAllText(_stateFileManager.ConfigPathAndFilename, json);
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
                ReadEmailsFromInboxesAndAnalyze();
            }
            catch (Exception ex) 
            {
                _logger.Error($"Error with the imap server: {ex}");
            }
        }
        #endregion



        #region ------------- Domain logic --------------------------------------------------------
        private static void ReadEmailsFromInboxesAndAnalyze()
        {
            foreach (var account in _config.MailAccounts)
                ReadEmailsFromInboxAndAnalyze(account);

            SaveStateFile();
        }

        private static void ReadEmailsFromInboxAndAnalyze(MailAccount account)
        {
            LogAccountName(account);

            (var client, var inboxFolder, var destinationFolder) = OpenMailAccountAndLocateTwoFolders(account);
            if (AccountCannotBeOpened(client))
                return;

            try
            {
                if (InboxFolderCannotBeFound(account, inboxFolder))
                    return;

                var emails = ReadAllEmailsFromInbox(client, inboxFolder, account);

                emails = ApplyFilters(account, emails);

                (var foundEmail, var ageInDays, var newestEmail) = TryToFindEmailAndComputeAge(ref emails);

                if (WeHaveANewerResultInSavedState(account, ageInDays, out State? savedState))
                    return;

                // OK, we don't have a newer age. Now we go ahead and update the Home automation server.
                SendResultToServer(account, foundEmail, ageInDays);

                SaveTheState(foundEmail, account.MqttTopicName, ageInDays, savedState);

                MarkEmailRead(account, client, inboxFolder, foundEmail, newestEmail);

                MoveEmailToDestinationFolder(account, client, inboxFolder, destinationFolder, foundEmail, newestEmail);
            }
            finally
            {
                client.Close();
            }
        }

        private static void LogAccountName(MailAccount account)
        {
            _logger.Debug("");
            _logger.Debug("");
            _logger.Debug($"----------------- Account {account.Name} -------------------------------------");
        }

        private static bool AccountCannotBeOpened(ImapClient? client)
        {
            if (client is null)
            {
                _logger.Info($"Cannot open Email account. Please check your settings.");
                return true;
            }
            else
            {
                return false;
            }
        }

        private static (ImapClient, IMailFolder, IMailFolder) OpenMailAccountAndLocateTwoFolders(MailAccount account)
        {
            _logger.Debug("Opening mail account and locating the folders...");

            var security = account.ImapSecurity switch {
                "Ssl"                   => Security.Ssl,
                "StartTls"              => Security.StartTls,
                "StartTlsWhenAvailable" => Security.StartTlsWhenAvailable,
                _                       => Security.None
            };

			var client = new Abraham.Mail.ImapClient()
				.UseHostname(account.ImapServer)
				.UseSecurityProtocol(security)
				.UseAuthentication(account.Username, account.Password)
                .Open();

            if (client is null)
                return (null, null, null);


            IMailFolder inboxFolder;
            try
            {
                inboxFolder = client.GetFolderByName(account.InboxFolderName);
                if (inboxFolder is null)
                    throw new Exception();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error getting the folder named '{account.InboxFolderName}' from your imap server. Please check your settings.");
                return (client, null, null);
            }


            IMailFolder destinationFolder;
            try
            {
                destinationFolder = client.GetFolderByName(account.DestinationFolder);
                if (destinationFolder is null)
                    throw new Exception();
            }
            catch (Exception ex)
            {
                _logger.Warn($"The folder named '{account.DestinationFolder}' does not exist.");

                _logger.Warn($"The folder named '{account.DestinationFolder}' does not exist. Creating it now.");
                client.CreateFolder(account.DestinationFolder);

                return (client, inboxFolder, null);
            }

            return (client, inboxFolder, destinationFolder);
        }

        private static bool InboxFolderCannotBeFound(MailAccount account, IMailFolder? inboxFolder)
        {
            if (inboxFolder is null)
            {
                _logger.Info($"Cannot find inbox folder named '{account.InboxFolderName}' Please check your settings.");
                return true;
            }
            else
            {
                return false;
            }
        }

        private static bool WeHaveANewerResultInSavedState(MailAccount account, double ageInDays, out State? savedState)
        {
            // Before we update the Homeautomation server,
            // We check if we've got a newer result in our state file.
            // If so, it means that we've got a newer email in the index, found it, and meanwhile the user has deleted or moved the email.
            // But we keep that age, to avoid saying "found no emails"

            savedState = _savedStates.States.FirstOrDefault(x => x.MqttTopic == account.MqttTopicName);
            bool weveGotANewerResult = (savedState is not null && savedState.AgeInDays < ageInDays);

            if (weveGotANewerResult)
            {
                _logger.Info($"Reading from inbox gave age {ageInDays:N1} days, but we've already got a newer age of {savedState.AgeInDays} days. We keep that and don't update the Home automation server.");
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void SaveTheState(bool foundEmail, string topic, double ageInDays, State? savedState)
        {
            // Finally we save this knowledge in our savedState.
            if (foundEmail)
            {
                if (savedState is not null)
                    savedState.AgeInDays = ageInDays;               // update existing entry
                else
                    _savedStates.States.Add(new State(topic, ageInDays));  // create new entry
            }
        }

        private static void SendResultToServer(MailAccount account, bool foundEmail, double ageInDays)
        {
            var result = MapAgeToDescriptionUsingRatings(ageInDays);

            if (foundEmail)
                _logger.Info($"Reading from inbox {account.Name} found newest email of age {ageInDays:N1} days, final rating is '{result}'");
            else
                _logger.Warn($"Reading from inbox {account.Name} found no emails!      age {ageInDays:N1} days, final rating is '{result}'");
            SendResultToHomeAutomationServer(account.MqttTopicName, result);
        }

        private static void MarkEmailRead(MailAccount account, ImapClient? client, IMailFolder? inboxFolder, bool foundEmail, Message newestEmail)
        {
            // If activated, we mark the email read.
            if (foundEmail && account.MarkFoundEmailRead)
            {
                _logger.Info($"Marking email as read");
                client.MarkAsRead(newestEmail, inboxFolder);
            }
        }

        private static void MoveEmailToDestinationFolder(MailAccount account, ImapClient? client, IMailFolder? inboxFolder, IMailFolder? destinationFolder, bool foundEmail, Message newestEmail)
        {
            // If activated, we finally move the email that we've found to another folder (to keep the inbox clean)
            var weShouldMoveEveryEmailWeFind = (account.MoveEmailToFolder && destinationFolder is not null);
            var weCanMoveTheEmail = (foundEmail && destinationFolder is not null);
            if (weShouldMoveEveryEmailWeFind && weCanMoveTheEmail)
            {
                _logger.Info($"Moving the email to folder '{account.DestinationFolder}'");
                if (destinationFolder is null)
                {
                    _logger.Error($"Cannot find mail folder '{account.DestinationFolder}' in email account!");
                }
                else
                {
                    _logger.Error($"Moving the email to folder '{account.DestinationFolder}'");
                    client.MoveEmailToFolder(newestEmail, inboxFolder, destinationFolder);
                }
            }
        }

        private static List<Message> ApplyFilters(MailAccount account, List<Message> emails)
        {
            _logger.Debug($"Filtering by sender...");
            emails = emails.Where(x => MailComesFromThePersonMonitoredPerson(x, account)).ToList();
            _logger.Debug($"{emails.Count} emails left");


            // take only the emails containing some words
            // (in my case "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday")
            if (account.SenderSubjectWhitelist.Count > 0)
            {
                _logger.Debug($"Filtering by subject whitelist: {string.Join(',', account.SenderSubjectWhitelist)}");
                emails = emails.Where(x => SubjectContainsOneWhitelistedWord(x, account)).ToList();
                _logger.Debug($"{emails.Count} emails left");
            }

            return emails;
        }

        private static (bool, double, Message) TryToFindEmailAndComputeAge(ref List<Message> emails)
        {
            double ageInDays;
            if (emails.Any())
            {
                emails = emails.OrderByDescending(x => x.Msg.Date).ToList();

                _logger.Debug($"Newest 5 emails:");
                foreach (var email in emails.Take(5))
                    _logger.Debug($"- {email.Msg.Date.ToString("yyyy-MM-dd hh:MM")}   {email.Msg.Subject}");

                var newestEmail = emails.First();

                var age = DateTime.Now - newestEmail.Msg.Date;
                ageInDays = age.TotalDays;

                _logger.Debug($"Newest Email is {ageInDays:N1} days old");
                return (true, ageInDays, newestEmail);
            }
            else
            {
                ageInDays = 999999.0;
                _logger.Debug($"No emails found, taking age {ageInDays:N1}");
                return (false, ageInDays, null);
            }
        }

        private static string MapAgeToDescriptionUsingRatings(double ageInDays)
        {
            int age = (int)ageInDays;
            if (_config.Ratings.Any())
                return RateAge(age, _config.Ratings);
            else
                return age.ToString();
        }

        private static string RateAge(int age, List<Rating> ratings)
        {
            foreach(var rating in ratings)
            {
                if ((int)age <= rating.AgeDays)
                    return rating.Result;
            }   
            return "rating error";
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

        private static List<Message> ReadAllEmailsFromInbox(ImapClient client, IMailFolder inboxFolder, MailAccount account)
        {
            _logger.Debug("Reading the inbox...");
			
            var emails = client.GetUnreadMessagesFromFolder(inboxFolder).ToList();
            
            _logger.Debug($"{emails.Count} unread emails");
            return emails;
        }
        #endregion



        #region ------------- Implementation ------------------------------------------------------
        #region Sending results
        private static void SendResultToHomeAutomationServer(string dataObjectName, string value)
        {
            SendOutToHomenet(value, dataObjectName);
            SendOutToMQTT(value, dataObjectName);
        }

        private static void SendOutToHomenet(string value, string dataObjectName)
        {
            try
            {
                if (HomenetServerIsConfigured())
                {
                    _logger.Debug($"Sending out result to Home automation target");
                    if (!ConnectToHomenetServer())
                        _logger.Error("Error connecting to homenet server.");
                    else
                        UpdateDataObject(value, dataObjectName);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"SendOutToHomenet: {ex}");
            }
        }

        private static void SendOutToMQTT(string value, string mqttTopic)
        {
            try
            {
                if (MqttBrokerIsConfigured())
                {
                    _logger.Debug($"Sending out group result to MQTT target");
                    _logger.Debug("Connecting to MQTT broker...");
                    if (!ConnectToMqttBroker())
                        _logger.Error("Error connecting to MQTT broker.");
                    else
                        UpdateTopics(value, mqttTopic);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"SendOutToMQTT: {ex}");
            }
        }
        #endregion

        #region Home automation server target
        private static bool HomenetServerIsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_config.HomenetServerURL) && 
                   !string.IsNullOrWhiteSpace(_config.HomenetUsername) && 
                   !string.IsNullOrWhiteSpace(_config.HomenetPassword);
        }

        private static bool ConnectToHomenetServer()
        {
            _logger.Debug("Connecting to homenet server...");
            try
            {
                _homenetClient = new DataObjectsConnector(_config.HomenetServerURL, _config.HomenetUsername, _config.HomenetPassword, _config.HomenetTimeout);
                _logger.Debug("Connect successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Error connecting to homenet server:\n" + ex.ToString());
                return false;
            }
        }

        private static void UpdateDataObject(string value, string dataObjectName)
        {
            if (_homenetClient is null)
                return;

            bool success = _homenetClient.UpdateValueOnly(new DataObject() { Name = dataObjectName, Value = value});
            if (success)
                _logger.Info($"Homeset server topic {dataObjectName} updated with value {value}");
            else
                _logger.Error($"server update error! {_homenetClient.LastError}");
        }
        #endregion

        #region MQTT target
        private static bool MqttBrokerIsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_config.MqttServerURL) && 
                   !string.IsNullOrWhiteSpace(_config.MqttUsername) && 
                   !string.IsNullOrWhiteSpace(_config.MqttPassword);
        }

        private static bool ConnectToMqttBroker()
        {
            _logger.Debug("Connecting to MQTT broker...");
            try
            {
                _mqttClient = new MQTTClient()
                    .UseUrl(_config.MqttServerURL)
                    .UseUsername(_config.MqttUsername)
                    .UsePassword(_config.MqttPassword)
                    .UseTimeout(_config.MqttTimeout)
                    .UseLogger(delegate(string message) { _logger.Debug(message); })
                    .Build();

                _logger.Debug("Created MQTT client");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Error connecting to MQTT broker:\n" + ex.ToString());
                return false;
            }
        }

        private static void UpdateTopics(string value, string topicName)
        {
            if (_mqttClient is null || value is null)
                return;

            var result = _mqttClient.Publish(topicName, value);
            if (result.IsSuccess)
                _logger.Info($"MQTT topic {topicName} updated with value {value}");
            else
                _logger.Error($"MQTT topic update error! {result.ReasonString}");
        }
        #endregion
        #endregion
    }
}