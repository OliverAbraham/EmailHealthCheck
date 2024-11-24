using NLog;
using System.Data;

namespace EmailHealthCheck;

public class Configuration
{
    public int CheckIntervalMinutes         { get; set; }
    public List<MailAccount>? MailAccounts  { get; set; } = null;
    
    public string HomenetServerURL          { get; set; }
    public string HomenetUsername           { get; set; }
    public string HomenetPassword           { get; set; }
    public int    HomenetTimeout            { get; set; }
    
    public string MqttServerURL             { get; set; }
    public string MqttUsername              { get; set; }
    public string MqttPassword              { get; set; }
    public int    MqttTimeout               { get; set; }

    public List<Rating> Ratings { get; set; }

    public void LogOptions(ILogger logger)
    {
        var accounts = (MailAccounts is not null) ? string.Join(',', MailAccounts.Select(x => x.Name)) : "";

        logger.Debug(
            $"CheckIntervalMinutes    : {CheckIntervalMinutes     }\n" +
            $"Accounts                : {accounts}\n" +
            $"Home Automation target  : {HomenetServerURL} / {HomenetUsername} / ***************\n" +
            $"MQTT broker target      : {MqttServerURL} / {MqttUsername} / ***************\n" +
            $"Ratings                 : \n{string.Join("", Ratings)}");
    }
}
                              
public class MailAccount
{
    public string       Name                   { get; set; }
    public string       ImapServer             { get; set; }
    public int          ImapPort               { get; set; } = 993;
    public string       ImapSecurity           { get; set; } = "Ssl";
    public string       Username               { get; set; }
    public string       Password               { get; set; }
    public string       InboxFolderName        { get; set; }
    public string       SenderName             { get; set; }
    public List<string> SenderSubjectWhitelist { get; set; } = new List<string>();
    public string       MqttTopicName          { get; set; }
    public bool         MarkFoundEmailRead     { get; set; }
    public bool         MoveEmailToFolder      { get; set; }
    public string       DestinationFolder      { get; set; }
}

public class Rating
{
    public int AgeDays { get; set; }
    public string Result { get; set; }

    public override string ToString()
    {
        return $"        age <= {AgeDays,9} days --> \"{Result}\"\n";
    }
}
