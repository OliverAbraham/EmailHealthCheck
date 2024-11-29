using Abraham.Mail;
using Abraham.ProgramSettingsManager;
using MailKit;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleToAttribute("EmailAccountManager.Tests")]

namespace EmailHealthCheck;

internal class EmailManager
{
    #region ------------- Properties ----------------------------------------------------------
    public delegate void HomeAutomationConnector (string topic, string value);
    #endregion



    #region ------------- Fields --------------------------------------------------------------
    private string                            _stateFileName    = "state.json";
    private ProgramSettingsManager<StateFile> _stateFileManager = new ProgramSettingsManager<StateFile>();
    private StateFile                         _savedStates      = new();
    private Action<string>                    _errorLogger      = (string message) => { };
    private Action<string>                    _warnLogger       = (string message) => { };
    private Action<string>                    _infoLogger       = (string message) => { };
    private Action<string>                    _debugLogger      = (string message) => { };
    private HomeAutomationConnector           _connector        = (string topic, string value) => { };
    private List<Rating>                      _ratings          = new();
    private IImapClient                       _imapClient;
    #endregion



    #region ------------- Init ----------------------------------------------------------------
    #endregion



    #region ------------- Public Methods ------------------------------------------------------
	public EmailManager UseImapClient(IImapClient imapClient)
	{
        _imapClient = imapClient;
		return this;
	}

	public EmailManager UseErrorLogger(Action<string> errorLogger)
	{
        _errorLogger = errorLogger;
		return this;
	}

	public EmailManager UseInfoLogger(Action<string> infoLogger)
	{
        _infoLogger = infoLogger;
		return this;
	}

	public EmailManager UseWarnLogger(Action<string> _warnLogger)
	{
        _warnLogger = _warnLogger;
		return this;
	}

	public EmailManager UseDebugLogger(Action<string> debugLogger)
	{
        _debugLogger = debugLogger;
		return this;
	}

	public EmailManager UseStateFile(string stateFileName)
	{
        _stateFileName = stateFileName;
		return this;
	}

    public EmailManager UseRatings(List<Rating> ratings)
    {
        _ratings = ratings;
        return this;
    }

	public EmailManager UseHomeAutomationConnector(HomeAutomationConnector connector)
	{
        _connector = connector;
		return this;
	}

    public void ProcessAllEmailAccounts(List<MailAccount> mailAccounts)
    {
        foreach (var account in mailAccounts)
            ReadEmailsFromInboxAndAnalyze(account);
    }
    
	public void ReadStateFile()
    {
        ReadStateFileInternal();
    }

	public void SaveStateFile()
    {
        SaveStateFileInternal();
    }
    
	public void ReadStateFile_ForUnitTestsOnly(string savedStates)
    {
        ReadStateFileInternal_ForUnitTestsOnly(savedStates);
    }

    public string SaveStateFile_ForUnitTestsOnly()
    {
        return SaveStateFileInternal_ForUnitTestsOnly();
    }
    #endregion



    #region ------------- Implementation: Domain logic ----------------------------------------
    private void ReadEmailsFromInboxAndAnalyze(MailAccount account)
    {
        LogAccountName(account);

        (var connected, var inboxFolder, var destinationFolder) = OpenMailAccountAndLocateTwoFolders(account);
        if (AccountCannotBeOpened(connected))
            return;

        try
        {
            if (InboxFolderCannotBeFound(account, inboxFolder))
                return;

            var emails = ReadAllUnreadEmailsFromInbox(inboxFolder, account);

            emails = ApplyFilters(account, emails);

            var emailFromInbox = TryToFindEmailAndComputeAge(ref emails);

            var newerResult = CheckWeHaveANewerResultInSavedState(account, emailFromInbox);
            if (newerResult.WasFound)
                TakeNewerResultFor(ref emailFromInbox, newerResult);

            if (!emailFromInbox.WasFound && !newerResult.WasFound)
                return;

            // OK, we didn't get a newer one before. The current email is the right one. Now go ahead and update the Home automation server.
            SendResultToServer(account, emailFromInbox);

            SaveTheState(emailFromInbox, account.MqttTopicName, newerResult);

            MarkEmailRead(account, inboxFolder, emailFromInbox);

            MoveEmailToDestinationFolder(account, inboxFolder, destinationFolder, emailFromInbox);
        }
        finally
        {
            CloseMailAccount();
        }
    }

    private void TakeNewerResultFor(ref Email? emailFromInbox, Email newerResult)
    {
        if (emailFromInbox is null)
        {
            emailFromInbox = newerResult;
        }
        else
        {
            emailFromInbox.Date = newerResult.Date;
            emailFromInbox.AgeInDays = newerResult.AgeInDays;
        }
    }

    private void LogAccountName(MailAccount account)
    {
        _debugLogger("");
        _debugLogger("");
        _debugLogger($"----------------- Account {account.Name} -------------------------------------");
    }

    private bool AccountCannotBeOpened(bool connected)
    {
        if (!connected)
        {
            _infoLogger($"Cannot open Email account. Please check your settings.");
            return true;
        }
        else
        {
            return false;
        }
    }

    private bool InboxFolderCannotBeFound(MailAccount account, IMailFolder? inboxFolder)
    {
        if (inboxFolder is null)
        {
            _infoLogger($"Cannot find inbox folder named '{account.InboxFolderName}' Please check your settings.");
            return true;
        }
        else
        {
            return false;
        }
    }

    private List<Message> ApplyFilters(MailAccount account, List<Message> emails)
    {
        _debugLogger($"Filtering by sender...");
        emails = emails.Where(x => MailComesFromThePersonMonitoredPerson(x, account)).ToList();
        _debugLogger($"{emails.Count} emails left");


        // take only the emails containing some words
        // (in my case "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday")
        if (account.SenderSubjectWhitelist.Count > 0)
        {
            _debugLogger($"Filtering by subject whitelist: {string.Join(',', account.SenderSubjectWhitelist)}");
            emails = emails.Where(x => SubjectContainsOneWhitelistedWord(x, account)).ToList();
            _debugLogger($"{emails.Count} emails left");
        }

        return emails;
    }

    private Email TryToFindEmailAndComputeAge(ref List<Message> emails)
    {
        var email = new Email();

        if (emails.Any())
        {
            emails = emails.OrderByDescending(x => x.Msg.Date).ToList();

            LogNewestFiveEmails(emails);

            email.EmailMsg = emails.First();
            email.Date = email.EmailMsg.Msg.Date;
            email.AgeInDays = AgeOf(email.Date);
            email.WasFound = true;

            _debugLogger($"Newest Email is {email.AgeInDays:N1} days old");
            return email;
        }
        else
        {
            email.WasFound  = false;
            email.AgeInDays = 999.0;
            email.Date      = DateOf(email.AgeInDays);
            _debugLogger($"No emails found, taking age {email.AgeInDays:N1}");
            return email;
        }
    }

    private void LogNewestFiveEmails(List<Message> emails)
    {
        _debugLogger($"Newest 5 emails:");
        foreach (var e in emails.Take(5))
            _debugLogger($"- {e.Msg.Date.ToString("yyyy-MM-dd hh:MM")}   {e.Msg.Subject}");
    }

    private double AgeOf(DateTimeOffset date)
    {
        return (DateTime.Now - date).TotalDays;
    }

    private DateTimeOffset DateOf(double ageInDays)
    {
        return DateTime.Now.AddDays(-ageInDays);;
    }

    private void SendResultToServer(MailAccount account, Email email)
    {
        var result = Rating.GetRatingForAge(_ratings, email.AgeInDays);

        if (email.WasFound)
            _infoLogger($"Reading from inbox {account.Name} found newest email of age {email.AgeInDays:N1} days, final rating is '{result}'");
        else
            _infoLogger($"Reading from inbox {account.Name} found no emails!      age {email.AgeInDays:N1} days, final rating is '{result}'");

        _connector(account.MqttTopicName, result);
    }

    private bool MailComesFromThePersonMonitoredPerson(Message mail, MailAccount account)
    {
        var sender = mail?.Msg?.From?.First().ToString().ToLower() ?? "";
        return sender.Length > 0 && sender.Contains(account.SenderName);
    }

    private bool SubjectContainsOneWhitelistedWord(Message mail, MailAccount account)
    {
        var subject = mail?.Msg?.Subject ?? "";
            
        foreach(var word in account.SenderSubjectWhitelist)
        {
            if (subject.Length > 0 && subject.Contains(word))
                return true;
        }
        return false;
    }
    #endregion



    #region ------------- IMAP postbox handling -----------------------------------------------
    private (bool, IMailFolder, IMailFolder) OpenMailAccountAndLocateTwoFolders(MailAccount account)
    {
        var connected = false;
        _debugLogger("Opening mail account and locating the folders...");

        var security = account.ImapSecurity switch {
            "Ssl"                   => Security.Ssl,
            "StartTls"              => Security.StartTls,
            "StartTlsWhenAvailable" => Security.StartTlsWhenAvailable,
            _                       => Security.None
        };

        if (_imapClient is null)
            throw new Exception("cannot continue, imap client is not set");

        try
        {
		    _imapClient
                .UseHostname(account.ImapServer)
			    .UseSecurityProtocol(security)
			    .UseAuthentication(account.Username, account.Password)
                .Open();
            connected = true;
        }
        catch (Exception ex)
        {
            return (connected, null, null);
        }


        IMailFolder inboxFolder;
        try
        {
            inboxFolder = _imapClient.GetFolderByName(account.InboxFolderName);
            if (inboxFolder is null)
                throw new Exception();
        }
        catch (Exception ex)
        {
            _errorLogger($"Error getting the folder named '{account.InboxFolderName}' from your imap server. Please check your settings.");
            return (connected, null, null);
        }


        IMailFolder destinationFolder;
        try
        {
            destinationFolder = _imapClient.GetFolderByName(account.DestinationFolder);
            if (destinationFolder is null)
                throw new Exception();
        }
        catch (Exception ex)
        {
            _warnLogger($"The folder named '{account.DestinationFolder}' does not exist.");

            _warnLogger($"The folder named '{account.DestinationFolder}' does not exist. Creating it now.");
            _imapClient.CreateFolder(account.DestinationFolder);

            return (connected, inboxFolder, null);
        }

        return (connected, inboxFolder, destinationFolder);
    }

    private void CloseMailAccount()
    {
        _imapClient.Close();
    }

    private List<Message> ReadAllUnreadEmailsFromInbox(IMailFolder inboxFolder, MailAccount account)
    {
        _debugLogger("Reading the inbox...");
			
        var emails = _imapClient.GetUnreadMessagesFromFolder(inboxFolder).ToList();
            
        _debugLogger($"{emails.Count} unread emails");
        return emails;
    }

    private void MarkEmailRead(MailAccount account, IMailFolder? inboxFolder, Email email)
    {
        // If activated, we mark the email read.
        if (email.WasFound && account.MarkFoundEmailRead)
        {
            _infoLogger($"Marking email as read");
            _imapClient.MarkAsRead(email.EmailMsg, inboxFolder);
        }
    }

    private void MoveEmailToDestinationFolder(MailAccount account, IMailFolder? inboxFolder, IMailFolder? destinationFolder, Email email)
    {
        // If activated, we finally move the email that we've found to another folder (to keep the inbox clean)
        var weShouldMoveEveryEmailWeFind = (account.MoveEmailToFolder && destinationFolder is not null);
        var weCanMoveTheEmail = email.WasFound;

        if (weShouldMoveEveryEmailWeFind && weCanMoveTheEmail)
        {
            _infoLogger($"Moving the email to folder '{account.DestinationFolder}'");
            if (destinationFolder is null)
            {
                _errorLogger($"Cannot find mail folder '{account.DestinationFolder}' in email account!");
            }
            else
            {
                _errorLogger($"Moving the email to folder '{account.DestinationFolder}'");
                _imapClient.MoveEmailToFolder(email.EmailMsg, inboxFolder, destinationFolder);
            }
        }
    }
    #endregion



    #region ------------- State file ----------------------------------------------------------
	private void ReadStateFileInternal()
    {
        try
        {
	        _stateFileManager = new ProgramSettingsManager<StateFile>()
                .UseFilename(Path.GetFileName(_stateFileName));

            _stateFileManager
                .UseFullPathAndFilename(_stateFileName)
                .Load();
            _savedStates = _stateFileManager.Data;
            Console.WriteLine($"Loaded saved state from file '{_stateFileName}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed loading saved state from file '{_stateFileName}'.");
            Console.WriteLine($"Reason: {ex}");
        }
    }

    private void SaveStateFileInternal()
    {
        _stateFileManager.Data = _savedStates;
        //_stateFileManager.Save(_stateFileManager.Data);
        var json = JsonConvert.SerializeObject(_stateFileManager.Data);
        File.WriteAllText(_stateFileName, json);
    }

    private void ReadStateFileInternal_ForUnitTestsOnly(string savedStates)
    {
        _savedStates = JsonConvert.DeserializeObject<StateFile>(savedStates);
    }

    private string SaveStateFileInternal_ForUnitTestsOnly()
    {
        _stateFileManager.Data = _savedStates;
        return JsonConvert.SerializeObject(_stateFileManager.Data);
    }

    private Email CheckWeHaveANewerResultInSavedState(MailAccount account, Email emailFromInbox)
    {
        // Before we update the Homeautomation server,
        // We check if we've got a newer result in our state file.
        // If so, it means that we've got a newer email in the index, found it, and meanwhile the user has deleted or moved the email.
        // But we keep that Date, to avoid saying "found no emails"

        var result = new Email();
        var foundSavedState = _savedStates.States.FirstOrDefault(x => x.MqttTopic == account.MqttTopicName);
            
        bool weveGotANewerResult = (foundSavedState is not null && foundSavedState.Date > emailFromInbox.Date);

        if (weveGotANewerResult)
        {
            result.WasFound  = true;
            result.Date      = foundSavedState.Date;
            result.AgeInDays = AgeOf(foundSavedState.Date);
            _infoLogger($"Reading from inbox gave age {emailFromInbox.AgeInDays:N1} days, but we've already had an email from {result.Date} (age {result.AgeInDays} days). We keep that and don't update the Home automation server.");
        }
        else
        {
            result.WasFound = false;
        }

        result.FoundSavedState = foundSavedState;
        return result;
    }

    private void SaveTheState(Email inboxEmail, string topic, Email savedState)
    {
        // Finally we save this knowledge in our savedState.
        if (WeHaveA(savedState))
            UpdateEntry(inboxEmail, savedState);
        else
            AddANewEntry(inboxEmail, topic);
    }

    private bool WeHaveA(Email savedState)
    {
        return (savedState is not null && savedState.WasFound);
    }

    private void UpdateEntry(Email inboxEmail, Email savedState)
    {
        savedState.Date = inboxEmail.Date;
        savedState.AgeInDays = inboxEmail.AgeInDays;
    }

    private void AddANewEntry(Email inboxEmail, string topic)
    {
        _savedStates.States.Add(new State(topic, inboxEmail.Date));
    }
    #endregion
}
