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
    /// You can apply another filter by subject, providing a whitelist of words. 
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
    public partial class Program
    {
        #region ------------- Fields --------------------------------------------------------------
	    private static CommandLineOptions                    _commandLineOptions     = new CommandLineOptions();
        private static ProgramSettingsManager<Configuration> _programSettingsManager = new ProgramSettingsManager<Configuration>();
        private static Configuration                         _config                 = new();
        private static NLog.Logger                           _logger                 = NLogBuilder.ConfigureNLog("").GetCurrentClassLogger();
        private static Scheduler?                            _scheduler;
        private static IImapClient                           _imapClient;
        private static EmailManager                          _manager;
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
            InitDomainLogic();
            InitLogger();
            PrintGreeting();
            LogConfiguration();
            HealthChecks();
            StartScheduler();

            Console.ReadKey();

            StopScheduler();
        }

        private static void InitDomainLogic()
        {
            _imapClient = new Abraham.Mail.ImapClient();
            
            _manager = new EmailManager()
                .UseImapClient(_imapClient)
                .UseErrorLogger(_logger.Error)
                .UseWarnLogger (_logger.Warn)
                .UseInfoLogger (_logger.Info)
                .UseDebugLogger(_logger.Debug)
                .UseStateFile(_commandLineOptions.StateFile)
                .UseRatings(_config.Ratings)
                .UseHomeAutomationConnector(HomeAutomationConnector);

            _manager.ReadStateFile();
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
                _manager.ProcessAllEmailAccounts(_config.MailAccounts);
                _manager.SaveStateFile();
            }
            catch (Exception ex) 
            {
                _logger.Error($"Error with the imap server: {ex}");
            }
        }
        #endregion



        #region ------------- Home Automation Server communication --------------------------------
        #region Sending results

        private static void HomeAutomationConnector(string topic, string value)
        {
            SendResultToHomeAutomationServer(topic, value);
        }

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