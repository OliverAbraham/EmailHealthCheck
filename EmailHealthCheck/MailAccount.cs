namespace EmailHealthCheck;

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
