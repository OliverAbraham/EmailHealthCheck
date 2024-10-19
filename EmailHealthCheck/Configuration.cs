using NLog;
using System.Data;

namespace ImapSpamfilter;

public class Configuration
{
    public int CheckIntervalMinutes        { get; set; }
    public List<MailAccount>? MailAccounts { get; set; } = null;
    public string HomenetServer            { get; set; }
    public string HomenetUsername          { get; set; }
    public string HomenetPassword          { get; set; }
    public int    HomenetTimeout           { get; set; }

    public void LogOptions(ILogger logger)
    {
        var accounts = (MailAccounts is not null) ? string.Join(',', MailAccounts.Select(x => x.Name)) : "";

        logger.Debug($"CheckIntervalMinutes              : {CheckIntervalMinutes     }");
        logger.Debug($"Accounts                          : {accounts}");
    }
}
                              
public class MailAccount
{
    public string Name                   { get; set; } = "";
    public string ImapServer             { get; set; } = "";
    public int    ImapPort               { get; set; } = 993;
    public string ImapSecurity           { get; set; } = "Ssl";
    public string Username               { get; set; } = "";
    public string Password               { get; set; } = "";
    public string InboxFolderName        { get; set; } = "";
    public string SenderName             { get; set; } = "";
    public List<string> SenderSubjectWhitelist { get; set; } = new List<string>();
}
