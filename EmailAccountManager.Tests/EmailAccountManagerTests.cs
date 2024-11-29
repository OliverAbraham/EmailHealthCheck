using Abraham.Mail;
using EmailHealthCheck;
using FluentAssertions;
using MailKit;
using MailKit.Search;
using MimeKit;
using System.Collections;

namespace EmailAccountManager.Tests;

[TestClass]
public class EmailAccountManagerTests
{
    #region ------------- Tests ---------------------------------------------------------------
    [TestMethod]
    public void ShouldAcceptNoAccounts()
    {
        // Arrange
        var sut = new EmailManager();
        var accounts = CreateEmptyAccountsList();

        // Act
        sut.ProcessAllEmailAccounts(accounts);

        // Assert
    }

    [TestMethod]
    public void ShouldAcceptEmptyInbox()
    {
        // Arrange
        var imapClientMock = new ImapClientMock();
        var sut = new EmailManager();
        sut.UseImapClient(imapClientMock);
        var connector = new HomeAutomationConnector();
        sut.UseHomeAutomationConnector(connector.SendMessage);
        var accounts = CreateDummyEmailAccount("sender1name", "myTopic");

        // Act
        sut.ProcessAllEmailAccounts(accounts);

        // Assert
        connector.MessagesSentToHomeAutomationServer.Should().BeEmpty();
    }

    [TestMethod]
    public void ShouldRecognizeNewEmail()
    {
        // Arrange
        var imapClientMock = new ImapClientMock();
        imapClientMock.UnreadEmailsInInbox.Add(CreateEmailMessage("subject1", "sender1name"));
        var sut = new EmailManager();
        sut.UseImapClient(imapClientMock);
        var connector = new HomeAutomationConnector();
        sut.UseHomeAutomationConnector(connector.SendMessage);
        var accounts = CreateDummyEmailAccount("sender1name", "myTopic");

        // Act
        sut.ProcessAllEmailAccounts(accounts);

        // Assert
        connector.MessagesSentToHomeAutomationServer.Should().NotBeEmpty();
        connector.MessagesSentToHomeAutomationServer[0].Item1.Should().Be("myTopic");
        connector.MessagesSentToHomeAutomationServer[0].Item2.Should().Be("0"); // ageInDays!
    }

    [TestMethod]
    public void ShouldMarkFoundEmailAsRead()
    {
        // Arrange
        var imapClientMock = new ImapClientMock();
        imapClientMock.UnreadEmailsInInbox.Add(CreateEmailMessage("subject1", "sender1name"));
        var sut = new EmailManager();
        sut.UseImapClient(imapClientMock);
        var connector = new HomeAutomationConnector();
        sut.UseHomeAutomationConnector(connector.SendMessage);
        var accounts = CreateDummyEmailAccount("sender1name", "myTopic", markAsRead:true);

        // Act
        sut.ProcessAllEmailAccounts(accounts);

        // Assert
        imapClientMock.MarkAsReadWasCalled.Should().BeTrue();
    }

    [TestMethod]
    public void ShouldMoveFoundEmailToFolder()
    {
        // Arrange
        var imapClientMock = new ImapClientMock();
        imapClientMock.UnreadEmailsInInbox.Add(CreateEmailMessage("subject1", "sender1name"));
        var sut = new EmailManager();
        sut.UseImapClient(imapClientMock);
        var connector = new HomeAutomationConnector();
        sut.UseHomeAutomationConnector(connector.SendMessage);
        var accounts = CreateDummyEmailAccount("sender1name", "myTopic", markAsRead:false, moveEmailToFolder:true);

        // Act
        sut.ProcessAllEmailAccounts(accounts);

        // Assert
        imapClientMock.MoveEmailToFolderWasCalled.Should().BeTrue();
    }

    [TestMethod]
    public void ShouldRememberSavedStateWhenInboxIsEmpty()
    {
        // Arrange
        var imapClientMock = new ImapClientMock();
        imapClientMock.UnreadEmailsInInbox.Add(CreateEmailMessage("subject1", "sender1name"));
        var sut = new EmailManager();
        sut.UseImapClient(imapClientMock);
        var connector = new HomeAutomationConnector();
        sut.UseHomeAutomationConnector(connector.SendMessage);
        var accounts = CreateDummyEmailAccount("sender1name", "myTopic", markAsRead:false, moveEmailToFolder:true);

        // Act
        sut.ProcessAllEmailAccounts(accounts);
        var savedStateAsJson = sut.SaveStateFile_ForUnitTestsOnly();

        // Assert
        connector.MessagesSentToHomeAutomationServer.Should().NotBeEmpty();

        // Arrange
        imapClientMock = new ImapClientMock(); // inbox is now emptied out
        sut.UseImapClient(imapClientMock);
        connector.MessagesSentToHomeAutomationServer.Clear();

        // Act
        sut.ReadStateFile_ForUnitTestsOnly(savedStateAsJson);
        sut.ProcessAllEmailAccounts(accounts);

        // Assert
        connector.MessagesSentToHomeAutomationServer.Count.Should().Be(1);
        connector.MessagesSentToHomeAutomationServer[0].Item2.Should().Be("0");
    }
    #endregion



    #region ------------- Implementation ------------------------------------------------------
    private static Message CreateEmailMessage(string subject, string fromT)
    {
        var from = new List<MailboxAddress>() { new MailboxAddress("sender1name", "sender1address")};
        var to   = new List<MailboxAddress>() { new MailboxAddress("receiver1name", "receiver1address")};
        var body = new MimePart();

        var msg = new MimeMessage(from, to, subject, body);
        return new Message(new UniqueId(), msg);
    }

    private static List<MailAccount> CreateEmptyAccountsList()
    {
        var accounts = new List<MailAccount>();
        return accounts;
    }

    private static List<MailAccount> CreateDummyEmailAccount(string senderName, string topic, bool markAsRead = false, bool moveEmailToFolder = false)
    {
        var accounts = new List<MailAccount>();
        accounts.Add(new MailAccount()
        {
            Name                   = "",
            ImapServer             = "",
            ImapPort               = 993,
            ImapSecurity           = "Ssl",
            Username               = "",
            Password               = "",
            InboxFolderName        = "inbox",
            SenderName             = senderName,
            SenderSubjectWhitelist = new List<string>(),
            MqttTopicName          = topic,
            MarkFoundEmailRead     = markAsRead,
            MoveEmailToFolder      = moveEmailToFolder,
            DestinationFolder      = "HealthCheck",
        });
        return accounts;
    }
    #endregion
}



#region ------------- Mocks -------------------------------------------------------------------
internal class HomeAutomationConnector
{
    public List<Tuple<string, string>> MessagesSentToHomeAutomationServer { get; set; } = new();

    public void SendMessage(string topic, string value)
    {
        MessagesSentToHomeAutomationServer.Add(new Tuple<string, string>(topic, value));
    }
}

internal class ImapClientMock : IImapClient
{
    public List<Message> UnreadEmailsInInbox { get; set; } = new();
    public bool MarkAsReadWasCalled { get; set; }
    public bool MoveEmailToFolderWasCalled { get; private set; }


    public IImapClient Open() => this;
    public IImapClient Close() => this;
    public List<Message> ReadAllEmailsFromInbox() => null;
    public List<Message> ReadEmailsFromFolder(string folderName, bool unreadOnly = false, bool closeConnectionAfterwards = true) => null;
    public List<Message> ReadUnreadEmailsFromInbox() => null;
    public IImapClient RegisterCodepageProvider() => this;
    public IImapClient UseAuthentication(string username, string password) => this;
    public IImapClient UseHostname(string hostname) => this;
    public IImapClient UseLogger(Action<string> logger) => this;
    public IImapClient UsePort(int port) => this;
    public IImapClient UseSecurityProtocol(Security securityProtocol) => this;
    public void CopyEmailToFolder(Message message, IMailFolder source, IMailFolder destination) { }
    public IMailFolder CreateFolder(string displayName) { return new MyEmailFolder();}
    public void DeleteFolder(string folderName) { }
    public void DeleteFolder(MailKit.Net.Imap.ImapFolder folder) { }
    public IEnumerable<IMailFolder> GetAllFolders() => new List<MyEmailFolder>();
    public IEnumerable<Message> GetAllMessagesFromFolder(IMailFolder folder) => new List<Message>().AsEnumerable();
    public IMailFolder GetFolderByName(string name, bool caseInsensitive = true) => new MyEmailFolder();
    public IMailFolder GetFolderByName(IEnumerable<IMailFolder> folders, string name, bool caseInsensitive = true) => null;
    public IEnumerable<Message> GetMessagesFromFolder(IMailFolder folder, SearchQuery searchQuery) => null;
    public IEnumerable<Message> GetUnreadMessagesFromFolder(IMailFolder folder)
    {
        return UnreadEmailsInInbox.AsEnumerable();
    }

    public void MarkAsRead(Message message, IMailFolder folder)
    {
        MarkAsReadWasCalled = true;
    }

    public void MarkAsUnread(Message message, IMailFolder folder) { }
    public void MoveEmailToFolder(Message message, IMailFolder source, IMailFolder destination)
    {
        MoveEmailToFolderWasCalled = true;
    }
}

internal class MyEmailFolder : IMailFolder
{
    public object SyncRoot => throw new NotImplementedException();

    public IMailFolder ParentFolder => throw new NotImplementedException();

    public FolderAttributes Attributes => throw new NotImplementedException();

    public AnnotationAccess AnnotationAccess => throw new NotImplementedException();

    public AnnotationScope AnnotationScopes => throw new NotImplementedException();

    public uint MaxAnnotationSize => throw new NotImplementedException();

    public MessageFlags PermanentFlags => throw new NotImplementedException();

    public IReadOnlySet<string> PermanentKeywords => throw new NotImplementedException();

    public MessageFlags AcceptedFlags => throw new NotImplementedException();

    public IReadOnlySet<string> AcceptedKeywords => throw new NotImplementedException();

    public char DirectorySeparator => throw new NotImplementedException();

    public FolderAccess Access => throw new NotImplementedException();

    public bool IsNamespace => throw new NotImplementedException();

    public string FullName => "dummy";

    public string Name => "dummy";

    public string Id => throw new NotImplementedException();

    public bool IsSubscribed => throw new NotImplementedException();

    public bool IsOpen => throw new NotImplementedException();

    public bool Exists => throw new NotImplementedException();

    public ulong HighestModSeq => throw new NotImplementedException();

    public uint UidValidity => throw new NotImplementedException();

    public UniqueId? UidNext => throw new NotImplementedException();

    public uint? AppendLimit => throw new NotImplementedException();

    public ulong? Size => throw new NotImplementedException();

    public int FirstUnread => throw new NotImplementedException();

    public int Unread => throw new NotImplementedException();

    public int Recent => throw new NotImplementedException();

    public int Count => throw new NotImplementedException();

    public HashSet<ThreadingAlgorithm> ThreadingAlgorithms => throw new NotImplementedException();

    public event EventHandler<EventArgs> Opened;
    public event EventHandler<EventArgs> Closed;
    public event EventHandler<EventArgs> Deleted;
    public event EventHandler<FolderRenamedEventArgs> Renamed;
    public event EventHandler<EventArgs> Subscribed;
    public event EventHandler<EventArgs> Unsubscribed;
    public event EventHandler<MessageEventArgs> MessageExpunged;
    public event EventHandler<MessagesVanishedEventArgs> MessagesVanished;
    public event EventHandler<MessageFlagsChangedEventArgs> MessageFlagsChanged;
    public event EventHandler<MessageLabelsChangedEventArgs> MessageLabelsChanged;
    public event EventHandler<AnnotationsChangedEventArgs> AnnotationsChanged;
    public event EventHandler<MessageSummaryFetchedEventArgs> MessageSummaryFetched;
    public event EventHandler<MetadataChangedEventArgs> MetadataChanged;
    public event EventHandler<ModSeqChangedEventArgs> ModSeqChanged;
    public event EventHandler<EventArgs> HighestModSeqChanged;
    public event EventHandler<EventArgs> UidNextChanged;
    public event EventHandler<EventArgs> UidValidityChanged;
    public event EventHandler<EventArgs> IdChanged;
    public event EventHandler<EventArgs> SizeChanged;
    public event EventHandler<EventArgs> CountChanged;
    public event EventHandler<EventArgs> RecentChanged;
    public event EventHandler<EventArgs> UnreadChanged;

    public void AddAccessRights(string name, AccessRights rights, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task AddAccessRightsAsync(string name, AccessRights rights, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public UniqueId? Append(IAppendRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public UniqueId? Append(FormatOptions options, IAppendRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<UniqueId> Append(IList<IAppendRequest> requests, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<UniqueId> Append(FormatOptions options, IList<IAppendRequest> requests, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<UniqueId?> AppendAsync(IAppendRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<UniqueId?> AppendAsync(FormatOptions options, IAppendRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<UniqueId>> AppendAsync(IList<IAppendRequest> requests, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<UniqueId>> AppendAsync(FormatOptions options, IList<IAppendRequest> requests, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Check(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CheckAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Close(bool expunge = false, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CloseAsync(bool expunge = false, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public UniqueId? CopyTo(UniqueId uid, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public UniqueIdMap CopyTo(IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void CopyTo(int index, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void CopyTo(IList<int> indexes, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<UniqueId?> CopyToAsync(UniqueId uid, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<UniqueIdMap> CopyToAsync(IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CopyToAsync(int index, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CopyToAsync(IList<int> indexes, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IMailFolder Create(string name, bool isMessageFolder, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IMailFolder Create(string name, IEnumerable<SpecialFolder> specialUses, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IMailFolder Create(string name, SpecialFolder specialUse, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IMailFolder> CreateAsync(string name, bool isMessageFolder, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IMailFolder> CreateAsync(string name, IEnumerable<SpecialFolder> specialUses, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IMailFolder> CreateAsync(string name, SpecialFolder specialUse, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Delete(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Expunge(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Expunge(IList<UniqueId> uids, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task ExpungeAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task ExpungeAsync(IList<UniqueId> uids, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<IMessageSummary> Fetch(IList<UniqueId> uids, IFetchRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<IMessageSummary> Fetch(IList<int> indexes, IFetchRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<IMessageSummary> Fetch(int min, int max, IFetchRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<IMessageSummary>> FetchAsync(IList<UniqueId> uids, IFetchRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<IMessageSummary>> FetchAsync(IList<int> indexes, IFetchRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<IMessageSummary>> FetchAsync(int min, int max, IFetchRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public AccessControlList GetAccessControlList(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<AccessControlList> GetAccessControlListAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public AccessRights GetAccessRights(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<AccessRights> GetAccessRightsAsync(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public MimeEntity GetBodyPart(UniqueId uid, BodyPart part, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public MimeEntity GetBodyPart(int index, BodyPart part, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<MimeEntity> GetBodyPartAsync(UniqueId uid, BodyPart part, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<MimeEntity> GetBodyPartAsync(int index, BodyPart part, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<MimeMessage> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public HeaderList GetHeaders(UniqueId uid, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public HeaderList GetHeaders(UniqueId uid, BodyPart part, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public HeaderList GetHeaders(int index, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public HeaderList GetHeaders(int index, BodyPart part, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<HeaderList> GetHeadersAsync(UniqueId uid, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<HeaderList> GetHeadersAsync(UniqueId uid, BodyPart part, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<HeaderList> GetHeadersAsync(int index, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<HeaderList> GetHeadersAsync(int index, BodyPart part, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public MimeMessage GetMessage(UniqueId uid, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public MimeMessage GetMessage(int index, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<MimeMessage> GetMessageAsync(UniqueId uid, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public string GetMetadata(MetadataTag tag, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public MetadataCollection GetMetadata(IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public MetadataCollection GetMetadata(MetadataOptions options, IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetMetadataAsync(MetadataTag tag, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<MetadataCollection> GetMetadataAsync(IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<MetadataCollection> GetMetadataAsync(MetadataOptions options, IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public AccessRights GetMyAccessRights(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<AccessRights> GetMyAccessRightsAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public FolderQuota GetQuota(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<FolderQuota> GetQuotaAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Stream GetStream(UniqueId uid, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Stream GetStream(int index, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Stream GetStream(UniqueId uid, int offset, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Stream GetStream(int index, int offset, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Stream GetStream(UniqueId uid, BodyPart part, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Stream GetStream(int index, BodyPart part, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Stream GetStream(UniqueId uid, BodyPart part, int offset, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Stream GetStream(int index, BodyPart part, int offset, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Stream GetStream(UniqueId uid, string section, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Stream GetStream(UniqueId uid, string section, int offset, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Stream GetStream(int index, string section, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Stream GetStream(int index, string section, int offset, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(UniqueId uid, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(int index, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(UniqueId uid, int offset, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(int index, int offset, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(UniqueId uid, BodyPart part, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(int index, BodyPart part, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(UniqueId uid, BodyPart part, int offset, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(int index, BodyPart part, int offset, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(UniqueId uid, string section, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(UniqueId uid, string section, int offset, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(int index, string section, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(int index, string section, int offset, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        throw new NotImplementedException();
    }

    public IMailFolder GetSubfolder(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IMailFolder> GetSubfolderAsync(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<IMailFolder> GetSubfolders(StatusItems items, bool subscribedOnly = false, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<IMailFolder> GetSubfolders(bool subscribedOnly = false, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<IMailFolder>> GetSubfoldersAsync(StatusItems items, bool subscribedOnly = false, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<IMailFolder>> GetSubfoldersAsync(bool subscribedOnly = false, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public UniqueId? MoveTo(UniqueId uid, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public UniqueIdMap MoveTo(IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void MoveTo(int index, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void MoveTo(IList<int> indexes, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<UniqueId?> MoveToAsync(UniqueId uid, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<UniqueIdMap> MoveToAsync(IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task MoveToAsync(int index, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task MoveToAsync(IList<int> indexes, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public FolderAccess Open(FolderAccess access, uint uidValidity, ulong highestModSeq, IList<UniqueId> uids, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public FolderAccess Open(FolderAccess access, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<FolderAccess> OpenAsync(FolderAccess access, uint uidValidity, ulong highestModSeq, IList<UniqueId> uids, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<FolderAccess> OpenAsync(FolderAccess access, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void RemoveAccess(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task RemoveAccessAsync(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void RemoveAccessRights(string name, AccessRights rights, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task RemoveAccessRightsAsync(string name, AccessRights rights, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Rename(IMailFolder parent, string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task RenameAsync(IMailFolder parent, string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public UniqueId? Replace(UniqueId uid, IReplaceRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public UniqueId? Replace(FormatOptions options, UniqueId uid, IReplaceRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public UniqueId? Replace(int index, IReplaceRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public UniqueId? Replace(FormatOptions options, int index, IReplaceRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<UniqueId?> ReplaceAsync(UniqueId uid, IReplaceRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<UniqueId?> ReplaceAsync(FormatOptions options, UniqueId uid, IReplaceRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<UniqueId?> ReplaceAsync(int index, IReplaceRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<UniqueId?> ReplaceAsync(FormatOptions options, int index, IReplaceRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<UniqueId> Search(SearchQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<UniqueId> Search(IList<UniqueId> uids, SearchQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public SearchResults Search(SearchOptions options, SearchQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public SearchResults Search(SearchOptions options, IList<UniqueId> uids, SearchQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<UniqueId>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<UniqueId>> SearchAsync(IList<UniqueId> uids, SearchQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<SearchResults> SearchAsync(SearchOptions options, SearchQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<SearchResults> SearchAsync(SearchOptions options, IList<UniqueId> uids, SearchQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void SetAccessRights(string name, AccessRights rights, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SetAccessRightsAsync(string name, AccessRights rights, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void SetMetadata(MetadataCollection metadata, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SetMetadataAsync(MetadataCollection metadata, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public FolderQuota SetQuota(uint? messageLimit, uint? storageLimit, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<FolderQuota> SetQuotaAsync(uint? messageLimit, uint? storageLimit, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<UniqueId> Sort(SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<UniqueId> Sort(IList<UniqueId> uids, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public SearchResults Sort(SearchOptions options, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public SearchResults Sort(SearchOptions options, IList<UniqueId> uids, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<UniqueId>> SortAsync(SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<UniqueId>> SortAsync(IList<UniqueId> uids, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<SearchResults> SortAsync(SearchOptions options, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<SearchResults> SortAsync(SearchOptions options, IList<UniqueId> uids, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Status(StatusItems items, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task StatusAsync(StatusItems items, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public bool Store(UniqueId uid, IStoreFlagsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<UniqueId> Store(IList<UniqueId> uids, IStoreFlagsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public bool Store(int index, IStoreFlagsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<int> Store(IList<int> indexes, IStoreFlagsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public bool Store(UniqueId uid, IStoreLabelsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<UniqueId> Store(IList<UniqueId> uids, IStoreLabelsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public bool Store(int index, IStoreLabelsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<int> Store(IList<int> indexes, IStoreLabelsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Store(UniqueId uid, IList<Annotation> annotations, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Store(IList<UniqueId> uids, IList<Annotation> annotations, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<UniqueId> Store(IList<UniqueId> uids, ulong modseq, IList<Annotation> annotations, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Store(int index, IList<Annotation> annotations, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Store(IList<int> indexes, IList<Annotation> annotations, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<int> Store(IList<int> indexes, ulong modseq, IList<Annotation> annotations, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> StoreAsync(UniqueId uid, IStoreFlagsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<UniqueId>> StoreAsync(IList<UniqueId> uids, IStoreFlagsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> StoreAsync(int index, IStoreFlagsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<int>> StoreAsync(IList<int> indexes, IStoreFlagsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> StoreAsync(UniqueId uid, IStoreLabelsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<UniqueId>> StoreAsync(IList<UniqueId> uids, IStoreLabelsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> StoreAsync(int index, IStoreLabelsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<int>> StoreAsync(IList<int> indexes, IStoreLabelsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task StoreAsync(UniqueId uid, IList<Annotation> annotations, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task StoreAsync(IList<UniqueId> uids, IList<Annotation> annotations, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<UniqueId>> StoreAsync(IList<UniqueId> uids, ulong modseq, IList<Annotation> annotations, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task StoreAsync(int index, IList<Annotation> annotations, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task StoreAsync(IList<int> indexes, IList<Annotation> annotations, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<int>> StoreAsync(IList<int> indexes, ulong modseq, IList<Annotation> annotations, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Subscribe(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SubscribeAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public bool Supports(FolderFeature feature)
    {
        throw new NotImplementedException();
    }

    public IList<MessageThread> Thread(ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IList<MessageThread> Thread(IList<UniqueId> uids, ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<MessageThread>> ThreadAsync(ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<MessageThread>> ThreadAsync(IList<UniqueId> uids, ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Unsubscribe(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task UnsubscribeAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}
#endregion
