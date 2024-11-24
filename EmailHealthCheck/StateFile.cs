namespace EmailHealthCheck
{
    // Holds a record to store the program state.
    // The reason behind this is:
    // When a new email arrives and we recognize this, all is ok.
    // But the user might move or delete the email.
    // On the next scheduled check, we wouldn't find the email anymore
    // and send a wrong status to the home automation server.
    // To improve this, we remember the latest age we found (per topic) in a file.
    public class StateFile
    {
        public List<State> States { get; set; } = new();
    }

    public class State
    {
        public string MqttTopic { get; set; }
        public double AgeInDays { get; set; }

        public State()
        {
        }

        public State(string mqttTopic, double ageInDays)
        {
            MqttTopic = mqttTopic;
            AgeInDays = ageInDays;
        }
    }
}
