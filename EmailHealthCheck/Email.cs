using Abraham.Mail;

namespace EmailHealthCheck;

public class Email
                                            {
    public bool             WasFound        { get; set; }
    public double           AgeInDays       { get; set; }
    public DateTimeOffset   Date            { get; set; }
    public Message          EmailMsg        { get; set; }
    public State?           FoundSavedState { get; set; }
}